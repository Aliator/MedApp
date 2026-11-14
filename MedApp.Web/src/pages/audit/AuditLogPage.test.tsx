import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, json, mockApi, openDialog, pageOf, renderWithApp} from '../../test/render';
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

function renderLog(search: AuditListSearch = {}) {
  const fetch = mockApi({[`GET ${SEARCH}`]: () => json(pageOf(entries, 23))});
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

  it('searches by text and action, keeping the procedure, and back to page 1', async () => {
    const {user, onSearchChange} = renderLog({procedureId, page: 3});

    await user.click(await screen.findByRole('button', {name: 'Search'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /Procedure, stage or user/}), 'ashford');
    await user.click(dialog.getByRole('combobox', {name: /Action/}));
    await user.click(screen.getByRole('option', {name: 'Stage finished'}));
    await user.click(dialog.getByRole('button', {name: 'Search'}));

    expect(onSearchChange).toHaveBeenCalledWith({procedureId, q: 'ashford', action: 'StageFinished', asc: undefined, from: undefined, to: undefined});
  });

  it('sorts oldest first from the When header, and clears a search', async () => {
    const {user, onSearchChange} = renderLog({q: 'knee', asc: false});

    await screen.findByText('k.ashford');
    await user.click(screen.getByRole('button', {name: /^when/i}));
    expect(onSearchChange).toHaveBeenCalledWith({q: 'knee', asc: true, page: undefined});

    await user.click(screen.getAllByRole('button', {name: 'Clear Search'})[0]);
    await waitFor(() => expect(onSearchChange).toHaveBeenLastCalledWith({asc: false}));
  });
});
