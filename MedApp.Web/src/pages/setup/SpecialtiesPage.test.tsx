import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, expectAnnounced, findAlert, isDisabled, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import type {SetupListSearch} from '../listSearch';
import {SpecialtiesPage, specialtyUse} from './SpecialtiesPage';

const BASE = '/api/specialties';

const specialties = [
  {id: 's1', name: 'Dermatology', isArchived: false, version: 3, templateCount: 2, pathwayCount: 14, memberCount: 3, canBeDeleted: false},
  {id: 's2', name: 'Urology', isArchived: false, version: 1, templateCount: 0, pathwayCount: 0, memberCount: 0, canBeDeleted: true},
  {id: 's3', name: 'Old clinic', isArchived: true, version: 5, templateCount: 1, pathwayCount: 0, memberCount: 0, canBeDeleted: false},
];

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${BASE}`]: () => json(specialties), ...extra});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<SpecialtiesPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) => fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method);

describe('specialtyUse', () => {
  it('says what uses a specialty, or that nothing does', () => {
    expect(specialtyUse(specialties[0])).toBe('2 templates, 14 pathways, 3 members of staff');
    expect(specialtyUse({templateCount: 1, memberCount: 1})).toBe('1 template, 1 member of staff');
    expect(specialtyUse(specialties[1])).toBe('Not used yet');
  });
});

describe('SpecialtiesPage', () => {
  it('lists the active ones by name, with what uses each, reading archived ones too', async () => {
    const {fetch} = renderPage();

    const dermatology = await rowOf('Dermatology');
    const rows = screen.getAllByRole('row');
    expect(rows.slice(1).map((r) => within(r).getAllByRole('cell')[0].textContent)).toEqual([
      'Dermatology2 templates, 14 pathways, 3 members of staff',
      'UrologyNot used yet',
    ]);
    expect(within(dermatology).getByText('14')).toBeInTheDocument();
    expect(within(dermatology).getByText('Active')).toBeInTheDocument();
    expect(screen.queryByText('Old clinic')).not.toBeInTheDocument();
    expect(screen.getByText('2 active specialties')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${BASE}?includeArchived=true`);
  });

  it('shows archived ones, marked, when asked', async () => {
    renderPage({status: 'archived'});

    expect(within(await rowOf('Old clinic')).getByText('Archived')).toBeInTheDocument();
    expect(screen.getByText('1 archived specialty')).toBeInTheDocument();
  });

  it('adds one', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {[`POST ${BASE}`]: () => json({id: 's9', name: 'Cardiology'}, 201)});

    await user.click(await screen.findByRole('button', {name: 'Add Specialty'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/Each pathway template belongs to a specialty/)).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Add Specialty'}))).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /Name/}), ' Cardiology ');
    await user.click(dialog.getByRole('button', {name: 'Add Specialty'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'POST')[0].json()).toEqual({name: 'Cardiology'});
    await expectAnnounced('Specialty added');
  });

  it("shows the API's reason when a name is taken, and its field errors on the name", async () => {
    const {user} = renderPage(
      {},
      {
        [`POST ${BASE}`]: async (request) =>
          ((await request.json()) as {name: string}).name === 'Urology'
            ? json({title: 'There is already a specialty called Urology.', status: 409}, 409)
            : json({title: 'Validation failed', status: 400, errors: {Name: ['Name must be 100 characters or fewer.']}}, 400),
      },
    );

    await user.click(await screen.findByRole('button', {name: 'Add Specialty'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /Name/}), 'Urology');
    await user.click(dialog.getByRole('button', {name: 'Add Specialty'}));
    expect(await dialog.findByText('There is already a specialty called Urology.')).toBeInTheDocument();

    await user.type(dialog.getByRole('textbox', {name: /Name/}), 'x');
    await user.click(dialog.getByRole('button', {name: 'Add Specialty'}));
    await waitFor(() => expect(dialog.getByRole('textbox', {name: /Name/})).toHaveAccessibleDescription(/100 characters or fewer/));
  });

  it('renames one from the version shown; after a 409 it says why, reads them again, and saves from the new version', async () => {
    withAnnouncements();
    let version = 3;
    let puts = 0;
    const {user, fetch} = renderPage(
      {},
      {
        [`GET ${BASE}`]: () => json(specialties.map((s) => (s.id === 's1' ? {...s, version} : s))),
        [`PUT ${BASE}/s1`]: () => {
          puts += 1;
          if (puts === 1) {
            version = 4;
            return json({title: 'Someone else changed this specialty. Check it, then try again.', status: 409}, 409);
          }
          return json({...specialties[0], name: 'Skin', version: 5});
        },
      },
    );

    await user.click(within(await rowOf('Dermatology')).getByRole('button', {name: 'Rename specialty'}));
    const dialog = within(openDialog()!);
    const name = dialog.getByRole('textbox', {name: /Name/});
    expect(name).toHaveValue('Dermatology');
    await user.clear(name);
    await user.type(name, 'Skin');
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    expect(await dialog.findByText('Someone else changed this specialty. Check it, then try again.')).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    const bodies = await Promise.all(sent(fetch, 'PUT').map((r) => r.json()));
    expect(bodies).toEqual([
      {name: 'Skin', expectedVersion: 3},
      {name: 'Skin', expectedVersion: 4},
    ]);
    await expectAnnounced('Specialty renamed');
  });

  it('archives after a confirmation, and restores an archived one, each from the version shown', async () => {
    const {user, fetch} = renderPage(
      {status: 'any'},
      {[`POST ${BASE}/s1/archive`]: () => json({}), [`POST ${BASE}/s3/restore`]: () => json({})},
    );

    await user.click(within(await rowOf('Dermatology')).getByRole('button', {name: 'Archive specialty'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText('2 templates, 14 pathways, 3 members of staff')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Archive'}));
    await user.click(within(await rowOf('Old clinic')).getByRole('button', {name: 'Restore specialty'}));

    await waitFor(() => expect(calls(fetch)).toEqual(expect.arrayContaining([`POST ${BASE}/s1/archive`, `POST ${BASE}/s3/restore`])));
    const bodies = await Promise.all(sent(fetch, 'POST').map((r) => r.json()));
    expect(bodies).toEqual([{expectedVersion: 3}, {expectedVersion: 5}]);
  });

  it('deletes only ones nothing uses, after a confirmation', async () => {
    const {user, fetch} = renderPage({}, {[`DELETE ${BASE}/s2`]: () => new Response(null, {status: 204})});

    expect(isDisabled(within(await rowOf('Dermatology')).getByRole('button', {name: /can be archived but not deleted/}))).toBe(true);
    await user.click(within(await rowOf('Urology')).getByRole('button', {name: 'Delete specialty'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Specialty'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${BASE}/s2`));
  });

  it("says why an archive failed, in the API's words, with the dialog closed", async () => {
    const {user} = renderPage({}, {[`POST ${BASE}/s1/archive`]: () => json({title: 'Someone else changed this specialty.'}, 409)});

    await user.click(within(await rowOf('Dermatology')).getByRole('button', {name: 'Archive specialty'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));

    expect(await findAlert('Someone else changed this specialty.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('offers to add the first one when there are none', async () => {
    const {user} = renderPage({}, {[`GET ${BASE}`]: () => json([])});

    expect(await screen.findByText('No specialties yet')).toBeInTheDocument();
    expect(screen.getByText(/A specialty in use can only be archived/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Add Your First Specialty'}));
    expect(within(openDialog()!).getByRole('heading', {name: 'Add Specialty'})).toBeInTheDocument();
  });

  it('offers archived ones when none match', async () => {
    const {user, onSearchChange} = renderPage({name: 'zzz'});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));
    expect(onSearchChange).toHaveBeenCalledWith({name: 'zzz', page: undefined, status: 'any'});
  });

  it('says when they could not be loaded', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The specialties could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
