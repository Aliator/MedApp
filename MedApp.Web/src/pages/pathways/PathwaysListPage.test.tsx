import {screen, waitFor, within} from '@testing-library/react';
import {calls, findAlert, json, managerUser, mockApi, openAlertDetails, pageOf, pickFavourite, renderWithApp, searchBar, staffUser} from '../../test/render';
import type {CurrentUser} from '../../auth/auth';
import {HalfViewContext} from '../../components/pages/halfView';
import type {PathwaysListSearch} from '../listSearch';
import {PathwaysListPage} from './PathwaysListPage';

const SEARCH = 'GET /api/pathways/patient-pathways/search';
const patientId = 'patient-1';
const STAFF = 'GET /api/calendar/staff';
const TEMPLATES = 'GET /api/pathways/patient-pathway-stage/templates';
const TEMPLATE_ID = '0000000e-0000-0000-0000-000000000001';
/** The list's own searches, leaving out what it asks for its filters' choices (accounts, templates). */
const searchCalls = (fetch: ReturnType<typeof mockApi>) => calls(fetch).filter((call) => call.startsWith(SEARCH));
const STAFF_ID = '0000000a-0000-0000-0000-000000000001';
const SPECIALTIES = 'GET /api/specialties';
const DEFINITIONS = 'GET /api/pathways/patient-pathway-stage/definitions';
const SKIN_ID = '0000005a-0000-0000-0000-0000000000aa';
const FEET_ID = '0000005a-0000-0000-0000-0000000000bb';
const HISTOLOGY_ID = '0000000d-0000-0000-0000-000000000001';
const RETIRED_ID = '0000000d-0000-0000-0000-000000000002';

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

function renderList(
  search: PathwaysListSearch = {},
  options: {patientId?: string; body?: unknown; status?: number; user?: CurrentUser; patientFlags?: object} = {},
) {
  const fetch = mockApi({
    [SEARCH]: () =>
      json(options.body ?? pageOf([summary(), summary({id: 'proc-2', title: 'Biopsy', assignees: [], status: 'Unassigned'})], 23), options.status),
    [`GET /api/patients/${patientId}`]: () => json({id: patientId, firstName: 'Test', lastName: 'Patient', dateOfBirth: '1970-01-01', email: 't@example.com', ...options.patientFlags}),
    [STAFF]: () => json([{userId: STAFF_ID, username: 'k.ashford'}]),
    [TEMPLATES]: () => json([{id: TEMPLATE_ID, name: 'Knee review', isArchived: false, stages: [], stageDefinitionIdsInOrder: []}]),
    [SPECIALTIES]: () =>
      json([
        {id: SKIN_ID, name: 'Dermatology', isArchived: false},
        {id: FEET_ID, name: 'Biomechanics', isArchived: false},
      ]),
    [DEFINITIONS]: () =>
      json([
        {id: HISTOLOGY_ID, name: 'Histology (lab)', isArchived: false},
        {id: RETIRED_ID, name: 'Old lab', isArchived: true},
      ]),
  });
  const onSearchChange = vi.fn();
  const result = renderWithApp(<PathwaysListPage search={search} onSearchChange={onSearchChange} patientId={options.patientId} />, {
    user: options.user ?? staffUser,
  });
  return {...result, fetch, onSearchChange};
}

describe('PathwaysListPage', () => {
  it('shows a page of pathways with links to each one and its patient', async () => {
    const {fetch} = renderList();

    expect(await screen.findByRole('link', {name: 'Colonoscopy'})).toHaveAttribute('href', '/pathways/proc-1');
    expect(screen.getByRole('heading', {name: 'Pathways', level: 1})).toBeInTheDocument();
    expect(screen.getByText('23 open pathways')).toBeInTheDocument();
    // A new pathway is for a patient, so it starts from a patient's list, not this one.
    expect(screen.queryByRole('link', {name: 'New Pathway'})).not.toBeInTheDocument();
    expect(within(screen.getByRole('table')).getAllByText('Test Patient').length).toBeGreaterThan(0);
    expect(screen.getAllByRole('link', {name: 'View patient'})[0]).toHaveAttribute('href', `/patients/${patientId}`);
    expect(within(screen.getByRole('table')).getByText('In progress')).toBeInTheDocument();
    expect(screen.getAllByText('2 of 7 stages done')).toHaveLength(2);
    expect(screen.getByText('a.user')).toBeInTheDocument();
    expect(screen.getByText('Nobody')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'View pathway'})[1]).toHaveAttribute('href', '/pathways/proc-2');
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true`]);
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

    expect(await screen.findByText('23 matching pathways')).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([
      `${SEARCH}?PageSize=10&Page=3&Sort=status&Ascending=false&Title=colon&TemplateName=scope&PatientName=freya` +
        '&Statuses=Completed&Statuses=Blocked&Priorities=Medium&Priorities=High&Priorities=Urgent' +
        '&OverdueOnly=true&AssignedToMe=true&IncludeClosed=true&DueFrom=2026-12-01&DueTo=2026-12-31',
    ]);
  });

  it('shows the wait for a pathway from a referral, and asks for the breached ones by days waited', async () => {
    const {fetch} = renderList(
      {breached: true, sort: 'Wait', asc: false},
      {
        body: pageOf([
          summary({clock: {state: 'Running', daysWaited: 130, targetDays: 126, isBreached: true}}),
          summary({id: 'proc-2', title: 'Biopsy'}),
        ]),
      },
    );

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('Day 130 of 126')).toBeInTheDocument();
    expect(table.getByText('Breached')).toBeInTheDocument();
    expect(table.getByText('No clock')).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?PageSize=10&Page=1&Sort=daysWaited&Ascending=false&BreachedOnly=true`]);
    await waitFor(() => expect(searchBar()).toHaveTextContent(/Breached/));
  });

  it('asks the API for the specialties and stage reached in the URL, and names them in the search', async () => {
    const {fetch} = renderList({specialty: `${SKIN_ID},${FEET_ID}`, stage: HISTOLOGY_ID});

    expect(await screen.findByText('23 matching pathways')).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([
      `${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true` +
        `&SpecialtyIds=${SKIN_ID}&SpecialtyIds=${FEET_ID}&StageDefinitionIds=${HISTOLOGY_ID}`,
    ]);
    await vi.waitFor(() => expect(searchBar()).toHaveTextContent(/Stage reached: is\s*Histology \(lab\)/));
    // Several are counted in the token ("2 items"); their names are in its popover.
    expect(searchBar()).toHaveTextContent(/Specialty: is any of\s*2 items/);
  });

  it('names a chosen stage that is not in the list as "Selected stage"', async () => {
    const unknown = '0000000d-0000-0000-0000-0000000000ff';
    renderList({stage: unknown});

    await vi.waitFor(() => expect(searchBar()).toHaveTextContent(/Stage reached: is\s*Selected stage/));
  });

  it('starts on Assigned to me when Account Settings says so, until its filter is removed (mine=false)', async () => {
    localStorage.setItem('medapp.proceduresStartOnMine', 'true');
    const {user, fetch, onSearchChange} = renderList({});

    await screen.findByText('Colonoscopy');
    expect(searchCalls(fetch)[0]).toContain('AssignedToMe=true');
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
    expect(searchCalls(fetch)[0]).toContain('PageSize=30');
  });

  it('shows the search as tokens, and a favourite sets its filter, keeping the sort', async () => {
    const {user, onSearchChange} = renderList({sort: 'Priority', status: '!Blocked', title: 'biopsy'});

    await screen.findByText('Colonoscopy');
    expect(searchBar()).toHaveTextContent(/Pathway: contains\s*biopsy/);
    expect(searchBar()).toHaveTextContent(/Status: is not any of\s*On hold/);

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

  it('says how many pathways match a search', async () => {
    renderList({title: 'colon'}, {body: pageOf([summary()], 1)});
    expect(await screen.findByText('1 matching pathway')).toBeInTheDocument();
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
        <PathwaysListPage search={{}} onSearchChange={vi.fn()} />
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

  it("shows one patient's pathways, with the template instead of the patient", async () => {
    const {fetch} = renderList({}, {patientId});

    expect(await screen.findByRole('heading', {name: 'Pathways for Test Patient', level: 1})).toBeInTheDocument();
    expect(screen.getAllByText('Diagnostic procedure')).toHaveLength(2);
    expect(screen.queryByRole('link', {name: 'View patient'})).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: /Patient/})).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Back'})).toHaveAttribute('href', `/patients/${patientId}`);
    expect(screen.getByRole('link', {name: 'New Pathway'})).toHaveAttribute('href', `/pathways/new?patientId=${patientId}`);
    expect(calls(fetch)).toContain(`${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true&PatientId=${patientId}`);
  });

  it("offers New Pathway only to someone whose roles let them start one (CreatePathways)", async () => {
    renderList({}, {patientId, user: managerUser});

    expect(await screen.findByRole('heading', {name: 'Pathways for Test Patient', level: 1})).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'New Pathway'})).not.toBeInTheDocument();
  });

  it("shows the patient's banner on one patient's pathways, and none on the full list", async () => {
    const {unmount} = renderList({}, {patientId});

    const banner = await screen.findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('No NHS number')).toBeInTheDocument();
    unmount();

    renderList();
    await screen.findByText('Colonoscopy');
    expect(screen.queryByRole('region', {name: /^Patient: /})).not.toBeInTheDocument();
  });

  it("shows the patient's banners over their pathways", async () => {
    const {user} = renderList({}, {patientId, patientFlags: {isDisputed: true, disputeReason: 'Synthetic allergy', isRestricted: true, restrictionReason: 'Synthetic check'}});

    expect(within(await openAlertDetails(user, 'Disputed record')).getByText("What's disputed: Synthetic allergy")).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Restricted record: changes paused, show details'})).toBeInTheDocument();
  });

  it("shows someone's pathways, named from the staff list, which anyone can read", async () => {
    const {fetch} = renderList({overdue: true, assignee: STAFF_ID}, {user: managerUser});

    await screen.findByText('Colonoscopy');
    expect(calls(fetch)).toContain(`${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true&OverdueOnly=true&AssignedToUserId=${STAFF_ID}`);
    await waitFor(() => expect(calls(fetch)).toContain(STAFF));
    await waitFor(() => expect(searchBar()).toHaveTextContent(/Assigned to: is\s*k\.ashford/));
  });

  it('shows the pathways nobody is assigned', async () => {
    const {fetch} = renderList({assignee: 'none'});

    await screen.findByText('Colonoscopy');
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true&NobodyAssigned=true`]);
    expect(searchBar()).toHaveTextContent(/Assigned to: is\s*Nobody/);
  });

  it('shows the pathways made from a template, by its ID, named as the template is now', async () => {
    const {fetch} = renderList({templateId: TEMPLATE_ID});

    await screen.findByText('Colonoscopy');
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?PageSize=10&Page=1&Sort=dueDate&Ascending=true&TemplateId=${TEMPLATE_ID}`]);
    await waitFor(() => expect(searchBar()).toHaveTextContent(/From template: iss*Knee review/));
  });

  it("names a template it can't find as the selected one", async () => {
    renderList({templateId: '0000000e-0000-0000-0000-000000000009'});

    await screen.findByText('Colonoscopy');
    expect(searchBar()).toHaveTextContent(/From template: iss*Selected template/);
  });

  it("names someone it can't find in the staff list (an account since deleted) as the selected person", async () => {
    renderList({assignee: '0000000a-0000-0000-0000-000000000099'});

    await screen.findByText('Colonoscopy');
    expect(searchBar()).toHaveTextContent(/Assigned to: is\s*Selected person/);
  });

  it('says when there are no open pathways', async () => {
    renderList({}, {body: pageOf([], 0)});

    expect(await screen.findByText('No open pathways')).toBeInTheDocument();
  });

  it('says when the pathways cannot be loaded', async () => {
    renderList({}, {body: {title: 'Server error'}, status: 500});

    await findAlert('The pathways could not be loaded. Please try again.');
  });
});
