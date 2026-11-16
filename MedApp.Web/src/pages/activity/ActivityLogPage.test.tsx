import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, findAlert, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar} from '../../test/render';
import type {ActivityListSearch} from '../listSearch';
import {ActivityLogPage} from './ActivityLogPage';

const SEARCH = '/api/activity/search';
const patientId = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b';
const procedureId = '4f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5c';

const entries = [
  {
    id: 'a1',
    action: 'ProcedureViewed',
    username: 'k.ashford',
    ipAddress: '192.0.2.10',
    atUtc: '2026-11-08T03:31:00Z',
    patientId,
    patientName: 'Oriel Penhallow',
    procedureId,
    details: '{}',
  },
  {
    id: 'a2',
    action: 'RolesChanged',
    username: 'test-admin',
    ipAddress: null,
    atUtc: '2026-11-07T10:00:00Z',
    targetUsername: 'l.northcott',
    details: JSON.stringify({from: ['User'], to: ['Admin', 'User']}),
  },
  {id: 'a3', action: 'SignInFailed', username: '', atUtc: '2026-11-07T09:00:00Z', details: JSON.stringify({reason: 'LockedOut'})},
];

function renderLog(search: ActivityListSearch = {}, body: unknown = pageOf(entries, 23), status = 200) {
  const fetch = mockApi({[`GET ${SEARCH}`]: () => json(body, status)});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<ActivityLogPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

describe('ActivityLogPage', () => {
  it('lists entries newest first: when, who and from where, the action, what about, and the details in words', async () => {
    const {fetch} = renderLog();

    const row = (await screen.findByText('k.ashford')).closest('tr')!;
    expect(within(row).getByText('192.0.2.10')).toBeInTheDocument();
    expect(within(row).getByText('Procedure viewed')).toBeInTheDocument();
    expect(within(row).getByRole('link', {name: 'Oriel Penhallow'})).toHaveAttribute('href', `/patients/${patientId}`);
    expect(within(row).getByRole('link', {name: 'Procedure'})).toHaveAttribute('href', `/procedures/${procedureId}`);
    const roles = screen.getByText('User → Admin, User').closest('tr')!;
    expect(within(roles).getByText('l.northcott')).toBeInTheDocument();
    expect(within(roles).getByText('Roles changed')).toBeInTheDocument();
    const refused = screen.getByText('Account locked').closest('tr')!;
    expect(within(refused).getByText('—')).toBeInTheDocument();
    expect(screen.getByText('23 entries')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PageSize=10&Page=1&Ascending=false`]);
  });

  it("shows one patient's entries, named in the subtitle, with Back to the patient", async () => {
    const {fetch} = renderLog({patientId, page: 2});

    expect(await screen.findByText('23 entries for Oriel Penhallow')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Back'})).toHaveAttribute('href', `/patients/${patientId}`);
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PageSize=10&Page=2&Ascending=false&PatientId=${patientId}`]);
  });

  it('searches by text and actions; a favourite sets its filter, keeping the patient, and back to page 1', async () => {
    const {user, fetch, onSearchChange} = renderLog({patientId, page: 3, q: 'ashford', action: 'SignedIn,SignInFailed'});

    await screen.findByText('k.ashford');
    expect(calls(fetch)[0]).toContain('Search=ashford');
    expect(calls(fetch)[0]).toContain('Actions=SignedIn&Actions=SignInFailed');
    expect(searchBar()).toHaveTextContent(/User or address: contains\s*ashford/);

    await pickFavourite(user, 'Refused sign-ins');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({patientId, action: 'SignInFailed', q: undefined, page: undefined}));
  });

  it('sorts oldest first from the When header, and pages, keeping the first page out of the URL', async () => {
    const {user, onSearchChange} = renderLog({page: 2});

    await screen.findByText('k.ashford');
    await user.click(screen.getByRole('button', {name: /^when/i}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, asc: true});
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined});
    await user.click(screen.getByRole('button', {name: 'Go to page 3'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 3});
  });

  it('says how many a search found, and offers to clear one that finds nothing', async () => {
    const {user, onSearchChange} = renderLog({q: 'nothing', asc: true, patientId}, pageOf([], 0));

    expect((await screen.findAllByText('No entries match your search')).length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({asc: true});
  });

  it('says how many entries a search found', async () => {
    renderLog({q: 'ashford'}, pageOf([entries[0]], 1));

    expect(await screen.findByText('1 entry found')).toBeInTheDocument();
  });

  it('says when nothing has been recorded yet', async () => {
    renderLog({}, pageOf([], 0));

    expect((await screen.findAllByText('Nothing in the activity log yet')).length).toBeGreaterThan(0);
  });

  it('says when the log could not be loaded', async () => {
    renderLog({}, {title: 'Server error'}, 500);

    expect(await findAlert('The activity log could not be loaded. Please try again.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('0 entries')).toBeInTheDocument());
  });

  it('lists file activity, and filters to it from its Files favourite', async () => {
    const files = [
      {id: 'f1', action: 'AttachmentDownloaded', username: 'k.ashford', atUtc: '2026-11-08T10:00:00Z', patientId, patientName: 'Oriel Penhallow', procedureId, details: '{}'},
      {id: 'f2', action: 'AttachmentsViewed', username: 'l.northcott', atUtc: '2026-11-08T09:00:00Z', patientId, patientName: 'Oriel Penhallow', details: '{}'},
    ];
    const {user, onSearchChange} = renderLog({}, pageOf(files, 2));

    const download = (await screen.findByText('File downloaded')).closest('tr')!;
    expect(within(download).getByText('A file on a procedure or stage')).toBeInTheDocument();
    expect(within(download).getByRole('link', {name: 'Procedure'})).toHaveAttribute('href', `/procedures/${procedureId}`);
    const viewed = screen.getByText('Files viewed').closest('tr')!;
    expect(within(viewed).getByText("All the patient's files")).toBeInTheDocument();

    await pickFavourite(user, 'Files');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({action: 'AttachmentsViewed,AttachmentDownloaded', page: undefined}));
  });
});
