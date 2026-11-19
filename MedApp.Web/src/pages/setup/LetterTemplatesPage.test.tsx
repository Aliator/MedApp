import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, expectAnnounced, findAlert, json, mockApi, openDialog, pickFavourite, renderWithApp, withAnnouncements} from '../../test/render';
import type {SetupListSearch} from '../listSearch';
import {LetterTemplatesPage, letterTemplateSummary} from './LetterTemplatesPage';

const BASE = '/api/letter-templates';

const templates = [
  {id: 'l1', name: 'Skin clinic letter', kind: 'ClinicLetter', teamId: 'sp1', teamName: 'Dermatology', isArchived: false, version: 3},
  {id: 'l2', name: 'Discharge', kind: 'DischargeSummary', teamId: null, teamName: null, isArchived: false, version: 1},
  {id: 'l3', name: 'Acne follow-up', kind: 'AppointmentLetter', teamId: 'sp1', teamName: 'Dermatology', isArchived: false, version: 2},
  {id: 'l4', name: 'Old referral reply', kind: 'ReferralResponse', teamId: 'sp2', teamName: 'Urology', isArchived: true, version: 5},
];

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${BASE}`]: () => json(templates), ...extra});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<LetterTemplatesPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) => fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method);

describe('letterTemplateSummary', () => {
  it("gives a template's kind and team, or every team", () => {
    expect(letterTemplateSummary(templates[0])).toBe('Clinic letter · Dermatology');
    expect(letterTemplateSummary(templates[1])).toBe('Discharge summary · All teams');
  });
});

describe('LetterTemplatesPage', () => {
  it("lists the active ones, every team's first, then by team and name, reading archived ones too", async () => {
    const {fetch} = renderPage();

    const discharge = await rowOf('Discharge');
    const names = screen
      .getAllByRole('row')
      .slice(1)
      .map((r) => within(r).getAllByRole('cell')[0].textContent);
    expect(names).toEqual([
      'DischargeDischarge summary · All teams',
      'Acne follow-upAppointment letter · Dermatology',
      'Skin clinic letterClinic letter · Dermatology',
    ]);
    expect(within(discharge).getByText('All teams')).toBeInTheDocument();
    expect(within(discharge).getByText('Active')).toBeInTheDocument();
    expect(within(discharge).getByRole('link', {name: 'Edit letter template'})).toHaveAttribute('href', '/admin/letter-templates/l2');
    expect(screen.queryByText('Old referral reply')).not.toBeInTheDocument();
    expect(screen.getByText('3 active letter templates')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Add Letter Template'})).toHaveAttribute('href', '/admin/letter-templates/new');
    expect(calls(fetch)).toContain(`GET ${BASE}?includeArchived=true`);
  });

  it('shows archived ones, marked, when asked', async () => {
    renderPage({status: 'archived'});

    const old = await rowOf('Old referral reply');
    expect(within(old).getByText('Archived')).toBeInTheDocument();
    expect(within(old).getByText('Referral response')).toBeInTheDocument();
    expect(screen.getByText('1 archived letter template')).toBeInTheDocument();
  });

  it('archives after a confirmation, and restores an archived one, each from the version shown', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage(
      {status: 'any'},
      {[`POST ${BASE}/l1/archive`]: () => json({...templates[0], isArchived: true}), [`POST ${BASE}/l4/restore`]: () => json(templates[3])},
    );

    await user.click(within(await rowOf('Skin clinic letter')).getByRole('button', {name: 'Archive letter template'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/won't be offered for new letters/)).toBeInTheDocument();
    expect(dialog.getByText('Clinic letter · Dermatology')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Archive'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await expectAnnounced('Skin clinic letter archived');

    await user.click(within(await rowOf('Old referral reply')).getByRole('button', {name: 'Restore letter template'}));
    await expectAnnounced('Old referral reply restored');

    expect(calls(fetch)).toEqual(expect.arrayContaining([`POST ${BASE}/l1/archive`, `POST ${BASE}/l4/restore`]));
    const bodies = await Promise.all(sent(fetch, 'POST').map((r) => r.json()));
    expect(bodies).toEqual([{expectedVersion: 3}, {expectedVersion: 5}]);
  });

  it("says why an archive or restore failed, in the API's words, with the dialog closed", async () => {
    const {user} = renderPage(
      {status: 'any'},
      {
        [`POST ${BASE}/l1/archive`]: () => json({title: 'Someone else changed this letter template.'}, 409),
        [`POST ${BASE}/l4/restore`]: () => json({title: 'Server error'}, 500),
      },
    );

    await user.click(within(await rowOf('Skin clinic letter')).getByRole('button', {name: 'Archive letter template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));
    expect(await findAlert('Someone else changed this letter template.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();

    await user.click(within(await rowOf('Old referral reply')).getByRole('button', {name: 'Restore letter template'}));
    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it('deletes one no letter has used, after asking, in place of archiving it', async () => {
    withAnnouncements();
    const unused = {...templates[1], canBeDeleted: true};
    const unusedArchived = {...templates[3], canBeDeleted: true};
    const {user, fetch} = renderPage(
      {status: 'any'},
      {
        [`GET ${BASE}`]: () => json([templates[0], unused, templates[2], unusedArchived]),
        [`DELETE ${BASE}/l2`]: () => new Response(null, {status: 204}),
        [`DELETE ${BASE}/l4`]: () => json({title: 'A letter has been written from it since: archive it instead.'}, 409),
      },
    );

    const row = within(await rowOf('Discharge'));
    expect(row.queryByRole('button', {name: 'Archive letter template'})).not.toBeInTheDocument();
    // An archived one no letter has used can be restored or deleted.
    expect(within(await rowOf('Old referral reply')).getByRole('button', {name: 'Restore letter template'})).toBeInTheDocument();

    await user.click(row.getByRole('button', {name: 'Delete letter template'}));
    let dialog = within(openDialog()!);
    expect(dialog.getByText('Discharge summary · All teams')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(row.getByRole('button', {name: 'Delete letter template'}));
    dialog = within(openDialog()!);
    await user.click(dialog.getByRole('button', {name: 'Delete Letter Template'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await expectAnnounced('Discharge deleted');
    expect(calls(fetch)).toContain(`DELETE ${BASE}/l2`);

    await user.click(within(await rowOf('Old referral reply')).getByRole('button', {name: 'Delete letter template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Letter Template'}));
    expect(await findAlert('A letter has been written from it since: archive it instead.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('closes the archive dialog when cancelled', async () => {
    const {user, fetch} = renderPage();

    await user.click(within(await rowOf('Skin clinic letter')).getByRole('button', {name: 'Archive letter template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(sent(fetch, 'POST')).toEqual([]);
  });

  it('offers to add the first one when there are none', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json([])});

    expect(await screen.findByText('No letter templates yet')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Add Your First Letter Template'})).toHaveAttribute('href', '/admin/letter-templates/new');
  });

  it('offers archived ones when none match', async () => {
    const {user, onSearchChange} = renderPage({name: 'zzz'});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));
    expect(onSearchChange).toHaveBeenCalledWith({name: 'zzz', page: undefined, status: 'any'});
  });

  it('keeps the search in the URL', async () => {
    const many = Array.from({length: 12}, (_, i) => ({...templates[1], id: `m${i}`, name: `Letter ${String(i).padStart(2, '0')}`}));
    const {user, onSearchChange} = renderPage({}, {[`GET ${BASE}`]: () => json(many)});

    await screen.findByText('Letter 00');
    await user.click(screen.getByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenCalledWith({page: 2});
  });

  it('goes back to page 1 without a page in the URL, and shows all of them from the favourites', async () => {
    const many = Array.from({length: 12}, (_, i) => ({...templates[1], id: `m${i}`, name: `Letter ${String(i).padStart(2, '0')}`}));
    const {user, onSearchChange} = renderPage({page: 2}, {[`GET ${BASE}`]: () => json(many)});

    await screen.findByText('Letter 10');
    await user.click(screen.getByRole('button', {name: 'Go to previous page'}));
    expect(onSearchChange).toHaveBeenCalledWith({page: undefined});

    await pickFavourite(user, 'All');
    expect(onSearchChange).toHaveBeenCalledWith({name: undefined, status: 'any', page: undefined});
  });

  it('says when they could not be loaded', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The letter templates could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
