import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, expectAnnounced, findAlert, isDisabled, json, mockApi, nurseUser, openDialog, renderWithApp, userWithRoles, withAnnouncements} from '../../test/render';
import type {SetupListSearch} from '../listSearch';
import {practicesPage, PracticesPage, practiceUse} from './PracticesPage';

const BASE = '/api/practices';

const practices = [
  {
    id: 'p1',
    name: 'Testbrook Surgery',
    odsCode: 'ZZZ123',
    addressLines: ['1 Test Street', 'Testbrook'],
    postcode: 'ZZ1 1ZZ',
    phone: '01632 960001',
    secureEmail: 'testbrook.surgery@nhs.net',
    isArchived: false,
    version: 3,
    patientCount: 12,
    referralCount: 1,
    canBeDeleted: false,
  },
  {id: 'p2', name: 'Avonmere Health Centre', odsCode: null, addressLines: [], postcode: null, phone: null, isArchived: false, version: 1, patientCount: 0, referralCount: 0, canBeDeleted: true},
  {id: 'p3', name: 'Oldfield Practice', odsCode: 'ZZZ999', addressLines: [], isArchived: true, version: 5, patientCount: 2, referralCount: 0, canBeDeleted: false},
];

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}, user = adminUser) {
  const fetch = mockApi({[`GET ${BASE}`]: () => json(practices), ...extra});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<PracticesPage search={search} onSearchChange={onSearchChange} />, {user})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) => fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method);

describe('practicesPage and practiceUse', () => {
  it('finds practices by name or ODS code, then the status filter, by name', () => {
    expect(practicesPage(practices, {}, 10).rows.map((p) => p.id)).toEqual(['p2', 'p1']);
    expect(practicesPage(practices, {name: 'zzz1'}, 10).rows.map((p) => p.id)).toEqual(['p1']);
    expect(practicesPage(practices, {name: 'zzz', status: 'any'}, 10).rows.map((p) => p.id)).toEqual(['p3', 'p1']);
    expect(practicesPage(practices, {name: 'avon'}, 10).total).toBe(1);
  });

  it('says what has a practice, or that nothing does', () => {
    expect(practiceUse(practices[0])).toBe('12 patients, 1 referral');
    expect(practiceUse({patientCount: 1})).toBe('1 patient');
    expect(practiceUse(practices[1])).toBe('Not used yet');
  });
});

// The add and edit tests fill a long form field by field, which can pass Vitest's 5 s default on a busy CI runner.
describe('PracticesPage', {timeout: 20_000}, () => {
  it('lists the active ones by name, with their code, postcode, phone and use, reading archived ones too', async () => {
    const {fetch} = renderPage();

    const testbrook = await rowOf('Testbrook Surgery');
    expect(within(testbrook).getByText('1 Test Street, Testbrook')).toBeInTheDocument();
    expect(within(testbrook).getByText('ZZZ123')).toBeInTheDocument();
    expect(within(testbrook).getByText('ZZ1 1ZZ')).toBeInTheDocument();
    expect(within(testbrook).getByText('01632 960001')).toBeInTheDocument();
    expect(within(testbrook).getByText('12')).toBeInTheDocument();
    expect(within(testbrook).getByText('Active')).toBeInTheDocument();
    expect(screen.queryByText('Oldfield Practice')).not.toBeInTheDocument();
    expect(screen.getByText('2 active practices')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${BASE}?includeArchived=true`);
  });

  it('shows archived ones, marked, when asked', async () => {
    renderPage({status: 'archived'});

    expect(within(await rowOf('Oldfield Practice')).getByText('Archived')).toBeInTheDocument();
    expect(screen.getByText('1 archived practice')).toBeInTheDocument();
  });

  it('lets someone who can view patients but not edit them only read it', async () => {
    renderPage({}, {}, userWithRoles('test-lab', 'Lab'));

    expect(await rowOf('Testbrook Surgery')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add Practice'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Edit Testbrook Surgery'})).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: 'Actions'})).not.toBeInTheDocument();
  });

  it('adds one with every detail, blank ones as none', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {[`POST ${BASE}`]: () => json({id: 'p9', name: 'Newcombe Surgery'}, 201)});

    await user.click(await screen.findByRole('button', {name: 'Add Practice'}));
    const dialog = within(openDialog()!);
    expect(isDisabled(dialog.getByRole('button', {name: 'Add Practice'}))).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /^Name/}), ' Newcombe Surgery ');
    await user.type(dialog.getByRole('textbox', {name: /^ODS code/}), 'zzz456');
    await user.type(dialog.getByRole('textbox', {name: /^Address line 1/}), '5 Synthetic Road');
    await user.type(dialog.getByRole('textbox', {name: /^Address line 3/}), 'Newcombe');
    await user.type(dialog.getByRole('textbox', {name: /^Postcode/}), 'zz4 4zz');
    await user.type(dialog.getByRole('textbox', {name: /^Secure email/}), 'newcombe@nhs.net');
    await user.click(dialog.getByRole('button', {name: 'Add Practice'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'POST')[0].json()).toEqual({
      name: 'Newcombe Surgery',
      odsCode: 'zzz456',
      addressLines: ['5 Synthetic Road', 'Newcombe'],
      postcode: 'zz4 4zz',
      phone: null,
      secureEmail: 'newcombe@nhs.net',
    });
    await expectAnnounced('Practice added');
  });

  it("shows the API's field errors on their fields, and a 409 in its words", async () => {
    const {user} = renderPage(
      {},
      {
        [`POST ${BASE}`]: async (request) =>
          ((await request.json()) as {odsCode: string | null}).odsCode === 'ZZZ123'
            ? json({title: 'Another practice, Testbrook Surgery, has the ODS code ZZZ123.', status: 409}, 409)
            : json({title: 'Validation failed', status: 400, errors: {SecureEmail: ['The secure email must be at an allowed domain.']}}, 400),
      },
    );

    await user.click(await screen.findByRole('button', {name: 'Add Practice'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /^Name/}), 'Copy Surgery');
    await user.type(dialog.getByRole('textbox', {name: /^ODS code/}), 'ZZZ123');
    await user.click(dialog.getByRole('button', {name: 'Add Practice'}));
    expect(await dialog.findByText('Another practice, Testbrook Surgery, has the ODS code ZZZ123.')).toBeInTheDocument();

    await user.clear(dialog.getByRole('textbox', {name: /^ODS code/}));
    await user.type(dialog.getByRole('textbox', {name: /^Secure email/}), 'someone@example.com');
    await user.click(dialog.getByRole('button', {name: 'Add Practice'}));
    await waitFor(() => expect(dialog.getByRole('textbox', {name: /^Secure email/})).toHaveAccessibleDescription(/allowed domain/));
  });

  it('edits one from the version shown; after a 409 it reads them again and saves from the new version', async () => {
    withAnnouncements();
    let version = 3;
    let puts = 0;
    const {user, fetch} = renderPage(
      {},
      {
        [`GET ${BASE}`]: () => json(practices.map((p) => (p.id === 'p1' ? {...p, version} : p))),
        [`PUT ${BASE}/p1`]: () => {
          puts += 1;
          if (puts === 1) {
            version = 4;
            return json({title: 'Someone else changed this practice. Check it, then try again.', status: 409}, 409);
          }
          return json({...practices[0], phone: '01632 960002', version: 5});
        },
      },
    );

    await user.click(within(await rowOf('Testbrook Surgery')).getByRole('button', {name: 'Edit Testbrook Surgery'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('textbox', {name: /^Address line 2/})).toHaveValue('Testbrook');
    const phone = dialog.getByRole('textbox', {name: /^Phone/});
    expect(phone).toHaveValue('01632 960001');
    await user.clear(phone);
    await user.type(phone, '01632 960002');
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    expect(await dialog.findByText('Someone else changed this practice. Check it, then try again.')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    const bodies = (await Promise.all(sent(fetch, 'PUT').map((r) => r.json()))) as {expectedVersion: number; phone: string}[];
    expect(bodies.map((b) => b.expectedVersion)).toEqual([3, 4]);
    expect(bodies[0]).toMatchObject({name: 'Testbrook Surgery', odsCode: 'ZZZ123', addressLines: ['1 Test Street', 'Testbrook'], phone: '01632 960002'});
    await expectAnnounced('Practice saved');
  });

  it('archives after a confirmation, and restores an archived one, each from the version shown', async () => {
    const {user, fetch} = renderPage({status: 'any'}, {[`POST ${BASE}/p1/archive`]: () => json({}), [`POST ${BASE}/p3/restore`]: () => json({})});

    await user.click(within(await rowOf('Testbrook Surgery')).getByRole('button', {name: 'Archive Testbrook Surgery'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText('12 patients, 1 referral')).toBeInTheDocument();
    expect(dialog.getByText('1 Test Street, Testbrook, ZZ1 1ZZ')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Archive'}));
    await user.click(within(await rowOf('Oldfield Practice')).getByRole('button', {name: 'Restore Oldfield Practice'}));

    await waitFor(() => expect(calls(fetch)).toEqual(expect.arrayContaining([`POST ${BASE}/p1/archive`, `POST ${BASE}/p3/restore`])));
    const bodies = await Promise.all(sent(fetch, 'POST').map((r) => r.json()));
    expect(bodies).toEqual([{expectedVersion: 3}, {expectedVersion: 5}]);
  });

  it('deletes only ones nothing has, after a confirmation', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {[`DELETE ${BASE}/p2`]: () => new Response(null, {status: 204})});

    expect(isDisabled(within(await rowOf('Testbrook Surgery')).getByRole('button', {name: /can be archived but not deleted/}))).toBe(true);
    await user.click(within(await rowOf('Avonmere Health Centre')).getByRole('button', {name: 'Delete Avonmere Health Centre'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Practice'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${BASE}/p2`));
    await expectAnnounced('Practice deleted');
  });

  it("says why an archive failed, in the API's words, with the dialog closed", async () => {
    const {user} = renderPage({}, {[`POST ${BASE}/p1/archive`]: () => json({title: 'Someone else changed this practice.'}, 409)});

    await user.click(within(await rowOf('Testbrook Surgery')).getByRole('button', {name: 'Archive Testbrook Surgery'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));

    expect(await findAlert('Someone else changed this practice.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('says when there are none yet, and offers archived ones when none match', async () => {
    const {unmount} = renderPage({}, {[`GET ${BASE}`]: () => json([])}, nurseUser);
    expect(await screen.findByText('No practices yet')).toBeInTheDocument();
    unmount();

    const {user, onSearchChange} = renderPage({name: 'nothing like it'});
    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));
    expect(onSearchChange).toHaveBeenCalledWith({name: 'nothing like it', page: undefined, status: 'any'});
  });

  it('says when they could not be loaded', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The practices could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
