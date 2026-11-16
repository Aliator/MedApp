import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, findAlert, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar} from '../../test/render';
import type {AuditListSearch} from '../listSearch';
import {AuditLogPage} from './AuditLogPage';

const SEARCH = '/api/procedures/audit/search';
const procedureId = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b';

const entries = [
  {
    id: 'a1',
    patientProcedureId: procedureId,
    procedureTitle: 'Knee review',
    stageName: null,
    action: 'StatusChanged',
    username: 'k.ashford',
    atUtc: '2026-11-08T03:31:00Z',
    details: JSON.stringify({from: 'InProgress', to: 'Blocked', automatic: false, reason: 'Awaiting results'}),
  },
  {
    id: 'a2',
    patientProcedureId: procedureId,
    procedureTitle: 'Knee review',
    stageName: 'Consent',
    action: 'StageFinished',
    username: 'l.northcott',
    atUtc: '2026-11-07T10:00:00Z',
    details: '{}',
  },
];

function renderLog(search: AuditListSearch = {}, body: unknown = pageOf(entries, 23), status = 200) {
  const fetch = mockApi({[`GET ${SEARCH}`]: () => json(body, status)});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<AuditLogPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

describe('AuditLogPage', () => {
  it('lists entries newest first: when, the procedure and stage, the action, who, and the details in words', async () => {
    const {fetch} = renderLog();

    const row = (await screen.findByText('In progress → blocked: Awaiting results')).closest('tr')!;
    expect(within(row).getByRole('link', {name: 'Knee review'})).toHaveAttribute('href', `/procedures/${procedureId}`);
    expect(within(row).getByText('Status changed')).toBeInTheDocument();
    expect(within(row).getByText('k.ashford')).toBeInTheDocument();
    const stageRow = within(screen.getByRole('table')).getByText('Stage finished').closest('tr')!;
    expect(within(stageRow).getByText('Consent')).toBeInTheDocument();
    expect(screen.getByText('23 entries')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PageSize=10&Page=1&Ascending=false`]);
  });

  it("shows one procedure's entries, named in the subtitle, with Back to the procedure", async () => {
    const {fetch} = renderLog({procedureId, page: 2});

    expect(await screen.findByText('23 entries for Knee review')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Back'})).toHaveAttribute('href', `/procedures/${procedureId}`);
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PageSize=10&Page=2&Ascending=false&ProcedureId=${procedureId}`]);
  });

  it('searches by text and actions; a favourite sets its filter, keeping the procedure, and back to page 1', async () => {
    const {user, fetch, onSearchChange} = renderLog({procedureId, page: 3, q: 'ashford', action: 'StageFinished,Created'});

    await screen.findByText('k.ashford');
    expect(calls(fetch)[0]).toContain('Search=ashford');
    expect(calls(fetch)[0]).toContain('Actions=StageFinished&Actions=Created');
    expect(searchBar()).toHaveTextContent(/Procedure, stage or user: contains\s*ashford/);
    expect(searchBar()).toHaveTextContent(/Action: is any of/);

    await pickFavourite(user, 'Status changes');
    expect(onSearchChange).toHaveBeenLastCalledWith(
      expect.objectContaining({procedureId, action: 'StatusChanged', q: undefined, page: undefined}),
    );
  });

  it('sorts oldest first from the When header, and clears a search', async () => {
    const {user, onSearchChange} = renderLog({q: 'knee', asc: false});

    await screen.findByText('k.ashford');
    await user.click(screen.getByRole('button', {name: /^when/i}));
    expect(onSearchChange).toHaveBeenCalledWith({q: 'knee', asc: true, page: undefined});

    await user.click(within(searchBar()).getByRole('button', {name: /clear/i}));
    await waitFor(() => expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({asc: false, q: undefined, page: undefined})));
  });

  it('keeps newest first (the default) out of the URL', async () => {
    const {user, onSearchChange} = renderLog({asc: true});

    await screen.findByText('k.ashford');
    await user.click(screen.getByRole('button', {name: /^when/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith({asc: undefined, page: undefined});
  });

  it('pages through the log, keeping the first page out of the URL', async () => {
    const {user, onSearchChange} = renderLog({page: 2, q: 'knee'});

    await screen.findByText('k.ashford');
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({q: 'knee', page: undefined});
    await user.click(screen.getByRole('button', {name: 'Go to page 3'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({q: 'knee', page: 3});
  });

  it('says how many entries a search found', async () => {
    renderLog({q: 'knee'}, pageOf([entries[0]], 1));

    expect(await screen.findByText('1 entry found')).toBeInTheDocument();
  });

  it('offers to clear a search that finds nothing, keeping the order', async () => {
    const {user, onSearchChange} = renderLog({procedureId, q: 'nothing', asc: true}, pageOf([], 0));

    expect((await screen.findAllByText('No entries match your search')).length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({asc: true});
  });

  it('says when nothing has been recorded yet', async () => {
    renderLog({}, pageOf([], 0));

    expect((await screen.findAllByText('Nothing in the audit log yet')).length).toBeGreaterThan(0);
  });

  it('says when the audit log could not be loaded', async () => {
    renderLog({}, {title: 'Server error'}, 500);

    expect(await findAlert('The audit log could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it("lists file entries: a patient's own file as a Patient file, linked to the patient, and a procedure's as usual", async () => {
    const patientId = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5c';
    const files = [
      {
        id: 'f1',
        patientProcedureId: '00000000-0000-0000-0000-000000000000',
        procedureTitle: '',
        patientId,
        stageName: null,
        action: 'AttachmentDownloaded',
        username: 'k.ashford',
        atUtc: '2026-11-08T10:00:00Z',
        details: JSON.stringify({attachmentId: 'x', fileName: 'letter.docx', contentType: 'text/plain', size: 2048, attachedTo: 'Patient'}),
      },
      {
        id: 'f2',
        patientProcedureId: procedureId,
        procedureTitle: 'Knee review',
        patientId,
        stageName: 'Consent',
        action: 'AttachmentAdded',
        username: 'l.northcott',
        atUtc: '2026-11-08T09:00:00Z',
        details: JSON.stringify({attachmentId: 'y', fileName: 'consent.pdf', size: 1258291, attachedTo: 'Stage'}),
      },
      {...entries[1], id: 'f3', action: 'AttachmentDeleted', details: JSON.stringify({fileName: 'old.pdf', attachedTo: 'Procedure'})},
    ];
    renderLog({}, pageOf(files, 3));

    const patientRow = within((await screen.findByText('letter.docx (2 KB), on the patient')).closest('tr')!);
    expect(patientRow.getByRole('link', {name: 'Patient file'})).toHaveAttribute('href', `/patients/${patientId}`);
    expect(patientRow.getByText('File downloaded')).toBeInTheDocument();
    const stageRow = within(screen.getByText('consent.pdf (1.2 MB), on the stage').closest('tr')!);
    expect(stageRow.getByRole('link', {name: 'Knee review'})).toHaveAttribute('href', `/procedures/${procedureId}`);
    expect(stageRow.getByText('Consent')).toBeInTheDocument();
    expect(stageRow.getByText('File attached')).toBeInTheDocument();
    expect(screen.getByText('File deleted')).toBeInTheDocument();
  });

  it('filters to file entries from its Files favourite', async () => {
    const {user, onSearchChange} = renderLog();

    await screen.findByText('k.ashford');
    await pickFavourite(user, 'Files');

    expect(onSearchChange).toHaveBeenLastCalledWith(
      expect.objectContaining({action: 'AttachmentAdded,AttachmentDeleted,AttachmentDownloaded', page: undefined}),
    );
  });
});
