import {screen, within} from '@testing-library/react';
import {calls, findAlert, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar, staffUser} from '../../test/render';
import type {ProceduresListSearch} from '../listSearch';
import {ProceduresListPage} from './ProceduresListPage';

const SEARCH = 'GET /api/procedures/patient-procedures/search';
const patientId = 'patient-1';

const summary = (overrides: Record<string, unknown> = {}) => ({
  id: 'proc-1',
  patientId,
  patientName: 'Test Patient',
  title: 'Colonoscopy',
  templateName: 'Diagnostic procedure',
  dueDate: '2026-12-01',
  dueDateUtc: '2026-12-01T23:59:59Z',
  priority: 'High',
  status: 'InProgress',
  isOverdue: false,
  completedStageCount: 2,
  stageCount: 7,
  currentStageName: 'Consent',
  assignees: ['a.user'],
  lastUpdated: '2026-11-01T10:00:00Z',
  ...overrides,
});

function renderList(search: ProceduresListSearch = {}, options: {patientId?: string; body?: unknown; status?: number} = {}) {
  const fetch = mockApi({
    [SEARCH]: () =>
      json(options.body ?? pageOf([summary(), summary({id: 'proc-2', title: 'Biopsy', assignees: [], status: 'Unassigned'})], 23), options.status),
    [`GET /api/patients/${patientId}`]: () => json({id: patientId, firstName: 'Test', lastName: 'Patient', dateOfBirth: '1970-01-01', email: 't@example.com'}),
  });
  const onSearchChange = vi.fn();
  const result = renderWithApp(<ProceduresListPage search={search} onSearchChange={onSearchChange} patientId={options.patientId} />, {
    user: staffUser,
  });
  return {...result, fetch, onSearchChange};
}

describe('ProceduresListPage', () => {
  it('shows a page of procedures with links to each one and its patient', async () => {
    const {fetch} = renderList();

    expect(await screen.findByRole('link', {name: 'Colonoscopy'})).toHaveAttribute('href', '/procedures/proc-1');
    expect(screen.getByRole('heading', {name: 'Procedures', level: 1})).toBeInTheDocument();
    expect(screen.getByText('23 open procedures')).toBeInTheDocument();
    // A new procedure is for a patient, so it starts from a patient's list, not this one.
    expect(screen.queryByRole('link', {name: 'New Procedure'})).not.toBeInTheDocument();
    expect(within(screen.getByRole('table')).getAllByText('Test Patient').length).toBeGreaterThan(0);
    expect(screen.getAllByRole('link', {name: 'View patient'})[0]).toHaveAttribute('href', `/patients/${patientId}`);
    expect(within(screen.getByRole('table')).getByText('In progress')).toBeInTheDocument();
    expect(screen.getAllByText('2 of 7 stages done')).toHaveLength(2);
    expect(screen.getByText('a.user')).toBeInTheDocument();
    expect(screen.getByText('Nobody')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'View procedure'})[1]).toHaveAttribute('href', '/procedures/proc-2');
    expect(calls(fetch)).toEqual([`${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true`]);
  });

  it('asks the API for the page, sort and filters in the URL', async () => {
    const {fetch} = renderList({
      page: 3,
      sort: 'Status',
      asc: false,
      title: 'colon',
      template: 'scope',
      patient: 'freya',
      status: 'Completed,Blocked',
      priority: '!Low',
      overdue: true,
      mine: true,
      closed: true,
      dueFrom: '2026-12-01',
      dueTo: '2026-12-31',
    });

    expect(await screen.findByText('23 matching procedures')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([
      `${SEARCH}?PageSize=10&Page=3&Sort=status&Ascending=false&Title=colon&TemplateName=scope&PatientName=freya` +
        '&Statuses=Completed&Statuses=Blocked&Priorities=Medium&Priorities=High&Priorities=Urgent' +
        '&OverdueOnly=true&AssignedToMe=true&IncludeClosed=true&DueFrom=2026-12-01&DueTo=2026-12-31',
    ]);
  });

  it('starts on Assigned to me when Account Settings says so, until its filter is removed (mine=false)', async () => {
    localStorage.setItem('medapp.proceduresStartOnMine', 'true');
    const {user, fetch, onSearchChange} = renderList({});

    await screen.findByText('Colonoscopy');
    expect(calls(fetch)[0]).toContain('AssignedToMe=true');
    expect(searchBar()).toHaveTextContent(/Assigned to me: only/);

    await user.click(within(searchBar()).getByRole('button', {name: /clear/i}));
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({mine: false}));
  });

  it('asks the API for the rows per page chosen in Account Settings', async () => {
    localStorage.setItem('medapp.pageSize', '30');
    const {fetch} = renderList({});

    await screen.findByText('Colonoscopy');
    expect(calls(fetch)[0]).toContain('PageSize=30');
  });

  it('shows the search as tokens, and a favourite sets its filter, keeping the sort', async () => {
    const {user, onSearchChange} = renderList({sort: 'Priority', status: '!Blocked', title: 'biopsy'});

    await screen.findByText('Colonoscopy');
    expect(searchBar()).toHaveTextContent(/Procedure: contains\s*biopsy/);
    expect(searchBar()).toHaveTextContent(/Status: is not any of\s*Blocked/);

    await pickFavourite(user, 'Assigned to me');
    expect(onSearchChange).toHaveBeenLastCalledWith(
      expect.objectContaining({sort: 'Priority', page: undefined, mine: true, status: undefined, title: undefined}),
    );
  });

  it('sorts by a column from its header', async () => {
    const {user, onSearchChange} = renderList({page: 2});

    await screen.findByText('Colonoscopy');
    await user.click(screen.getByRole('button', {name: /^priority/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: true});
  });

  it("shows one patient's procedures, with the template instead of the patient", async () => {
    const {fetch} = renderList({}, {patientId});

    expect(await screen.findByRole('heading', {name: 'Procedures for Test Patient', level: 1})).toBeInTheDocument();
    expect(screen.getAllByText('Diagnostic procedure')).toHaveLength(2);
    expect(screen.queryByRole('link', {name: 'View patient'})).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: /Patient/})).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Back'})).toHaveAttribute('href', `/patients/${patientId}`);
    expect(screen.getByRole('link', {name: 'New Procedure'})).toHaveAttribute('href', `/procedures/new?patientId=${patientId}`);
    expect(calls(fetch)).toContain(`${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true&PatientId=${patientId}`);
  });

  it('says when there are no open procedures', async () => {
    renderList({}, {body: pageOf([], 0)});

    expect(await screen.findByText('No open procedures')).toBeInTheDocument();
  });

  it('says when the procedures cannot be loaded', async () => {
    renderList({}, {body: {title: 'Server error'}, status: 500});

    await findAlert('The procedures could not be loaded. Please try again.');
  });
});
