import {screen, within} from '@testing-library/react';
import {calls, findAlert, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar, staffUser} from '../../test/render';
import {HalfViewContext} from '../../components/pages/halfView';
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
    // The bar's token says it, so the favourite's name isn't repeated beside the star (which still says it).
    expect(screen.getByRole('button', {name: 'Favourites (showing Assigned to me)'})).toBeInTheDocument();
    expect(screen.queryAllByText('Assigned to me').filter((el) => !searchBar().contains(el) && !el.closest('[role="menu"]'))).toEqual([]);

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

  it('keeps Assigned to me out of the URL for a favourite without it, when the list starts on everyone', async () => {
    const {user, onSearchChange} = renderList({page: 2});

    await screen.findByText('Colonoscopy');
    await pickFavourite(user, 'Overdue');

    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({overdue: true, mine: undefined, page: undefined}));
  });

  it('pages through the list, keeping the first page out of the URL', async () => {
    const {user, onSearchChange} = renderList({page: 2, sort: 'Status'});

    await screen.findByText('Colonoscopy');
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Status', page: undefined});
    await user.click(screen.getByRole('button', {name: 'Go to page 3'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Status', page: 3});
  });

  it('says how many procedures match a search', async () => {
    renderList({title: 'colon'}, {body: pageOf([summary()], 1)});
    expect(await screen.findByText('1 matching procedure')).toBeInTheDocument();
  });

  it('offers to clear a search that matches nothing, keeping the sort', async () => {
    const {user, onSearchChange} = renderList({title: 'nothing', sort: 'Priority', asc: false, page: 2}, {body: pageOf([], 0)});

    await user.click(await screen.findByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: false});
  });

  it('drops the current stage and the assignees in a half view', async () => {
    mockApi({[SEARCH]: () => json(pageOf([summary()], 1))});
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <ProceduresListPage search={{}} onSearchChange={vi.fn()} />
      </HalfViewContext.Provider>,
      {user: staffUser},
    );

    expect(await screen.findByRole('link', {name: 'Colonoscopy'})).toBeInTheDocument();
    expect(screen.queryByText('2 of 7 stages done')).not.toBeInTheDocument();
    expect(screen.queryByText('a.user')).not.toBeInTheDocument();
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
