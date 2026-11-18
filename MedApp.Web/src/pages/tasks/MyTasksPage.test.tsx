import {screen, within} from '@testing-library/react';
import {calls, findAlert, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar, staffUser} from '../../test/render';
import type {TasksSearch} from '../listSearch';
import {MyTasksPage, taskQuery} from './MyTasksPage';

const SEARCH = 'GET /api/pathways/patient-pathways/search';
const SPECIALTIES = 'GET /api/specialties';
const DEFINITIONS = 'GET /api/pathways/patient-pathway-stage/definitions';
const SPECIALTY_ID = '0000005a-0000-0000-0000-0000000000aa';
const STAGE_ID = '0000000d-0000-0000-0000-000000000001';
/** The list's own searches, leaving out what it asks for its filters' choices (specialties, stages). */
const searchCalls = (fetch: ReturnType<typeof mockApi>) => calls(fetch).filter((call) => call.startsWith(SEARCH));

const summary = (overrides: Record<string, unknown>) => ({
  id: 'proc-1',
  patientId: 'patient-1',
  patientName: 'Test Patient',
  title: 'Colonoscopy',
  templateName: 'Diagnostic procedure',
  dueDate: '2026-12-01',
  dueDateUtc: '2026-12-01T23:59:59Z',
  priority: 'Medium',
  status: 'Assigned',
  isOverdue: false,
  completedStageCount: 0,
  stageCount: 7,
  currentStageName: 'Referral received',
  assignees: ['test-user'],
  lastUpdated: '2026-11-01T10:00:00Z',
  ...overrides,
});

const notStartedPage = pageOf([summary({id: 'a', title: 'Colonoscopy', priority: 'Urgent', isOverdue: true, dueDate: '2026-11-01'})], 1);
const startedPage = pageOf(
  [
    summary({id: 'b', title: 'Knee replacement', status: 'InProgress', completedStageCount: 2, stageCount: 9, currentStageName: 'Consultation'}),
    summary({id: 'c', title: 'Cystoscopy', status: 'Blocked', completedStageCount: 1, currentStageName: 'Consent'}),
  ],
  12,
);

/** Answers by the search's Started flag: not started, started, or (with no flag) both. */
function mockTasks(status = 200) {
  return mockApi({
    [SEARCH]: (request) => {
      const started = new URL(request.url).searchParams.get('Started');
      return json(started === 'false' ? notStartedPage : started === 'true' ? startedPage : pageOf([], 0), status);
    },
    [SPECIALTIES]: () => json([{id: SPECIALTY_ID, name: 'Dermatology', isArchived: false}]),
    [DEFINITIONS]: () => json([{id: STAGE_ID, name: 'Histology (lab)', isArchived: false}]),
  });
}

function renderTasks(search: TasksSearch = {}) {
  const fetch = mockTasks();
  const onSearchChange = vi.fn();
  const result = renderWithApp(<MyTasksPage search={search} onSearchChange={onSearchChange} />, {user: staffUser});
  return {...result, fetch, onSearchChange};
}

describe('MyTasksPage', () => {
  it('starts on the tasks not started, with that filter in the search and their templates', async () => {
    const {fetch} = renderTasks();

    expect(await screen.findByText('Colonoscopy')).toBeInTheDocument();
    expect(screen.getByText('1 pathway not started')).toBeInTheDocument();
    expect(searchBar()).toHaveTextContent(/Progress: is\s*Not started/);
    // The bar's token says it, so the favourite's name isn't repeated beside the star (which still says it).
    expect(screen.getByRole('button', {name: 'Favourites (showing Not started)'})).toBeInTheDocument();
    expect(screen.queryAllByText('Not started').filter((el) => !searchBar().contains(el) && !el.closest('[role="menu"]'))).toEqual([]);
    expect(screen.getByRole('columnheader', {name: 'Template'})).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?PageSize=10&AssignedToMe=true&Started=false&Page=1&Sort=dueDate&Ascending=true`]);
    // A normal list: no search button, and no switch between two lists.
    expect(screen.queryByRole('button', {name: 'Search'})).not.toBeInTheDocument();
  });

  it('filters by specialty and the stage reached, named in the search', async () => {
    const {fetch} = renderTasks({specialty: SPECIALTY_ID, stage: STAGE_ID});

    await vi.waitFor(() => expect(searchBar()).toHaveTextContent(/Stage reached: is\s*Histology \(lab\)/));
    expect(searchBar()).toHaveTextContent(/Specialty: is\s*Dermatology/);
    expect(searchCalls(fetch)).toEqual([
      `${SEARCH}?PageSize=10&AssignedToMe=true&Started=false&Page=1&Sort=dueDate&Ascending=true` +
        `&SpecialtyIds=${SPECIALTY_ID}&StageDefinitionIds=${STAGE_ID}`,
    ]);
  });

  it('shows the started tasks with the stage they are at, when the URL says so', async () => {
    renderTasks({progress: 'started'});

    const row = (await screen.findByText('Knee replacement')).closest('tr')!;
    expect(within(row).getByText('Consultation')).toBeInTheDocument();
    expect(screen.getByText('12 pathways started')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: 'Current stage'})).toBeInTheDocument();
  });

  it('asks for the breached ones by days waited, and shows each task\'s wait', async () => {
    expect(taskQuery({breached: true, sort: 'Wait', asc: false})).toMatchObject({BreachedOnly: true, Sort: 'daysWaited', Ascending: false});
    renderTasks();
    expect(await screen.findByText('Colonoscopy')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: /Wait/})).toBeInTheDocument();
    expect(screen.getByText('No clock')).toBeInTheDocument();
  });

  it('asks the API for both when the progress filter is removed, with the other filters', () => {
    expect(
      taskQuery({progress: 'any', title: 'knee', status: '!Blocked', priority: 'High,Urgent', overdue: true, page: 2, sort: 'Priority', asc: false}),
    ).toEqual({
      AssignedToMe: true,
      Started: undefined,
      Page: 2,
      Sort: 'priority',
      Ascending: false,
      Title: 'knee',
      TemplateName: undefined,
      PatientName: undefined,
      Statuses: ['Unassigned', 'Assigned', 'InProgress', 'InReview'],
      Priorities: ['High', 'Urgent'],
      OverdueOnly: true,
      DueFrom: undefined,
      DueTo: undefined,
    });
  });

  it('switches to the started tasks from the favourites, keeping the sort and back to page 1', async () => {
    const {user, onSearchChange} = renderTasks({sort: 'Priority', page: 2, overdue: true});

    await screen.findByText('Colonoscopy');
    await pickFavourite(user, 'Started');

    expect(onSearchChange).toHaveBeenLastCalledWith(
      expect.objectContaining({sort: 'Priority', page: undefined, progress: 'started', overdue: undefined}),
    );
  });

  it('keeps Not started out of the URL (the default), and removing the progress filter shows both', async () => {
    const {user, onSearchChange} = renderTasks({progress: 'started'});

    await screen.findByText('Knee replacement');
    await pickFavourite(user, 'Not started');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({progress: undefined}));

    await user.click(within(searchBar()).getByRole('button', {name: /clear/i}));
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({progress: 'any'}));
  });

  it('shows no progress filter when the URL asks for both, and says when nothing is assigned', async () => {
    renderTasks({progress: 'any'});

    expect((await screen.findAllByText('Nothing assigned to you')).length).toBeGreaterThan(0);
    expect(screen.getByText('0 pathways to do')).toBeInTheDocument();
    expect(searchBar()).not.toHaveTextContent(/Progress:/);
  });

  it('sorts by a column from its header, back to page 1', async () => {
    const {user, onSearchChange} = renderTasks({page: 2, progress: 'started'});

    await screen.findByText('Knee replacement');
    await user.click(screen.getByRole('button', {name: /^priority/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith({progress: 'started', page: undefined, sort: 'Priority', asc: true});
  });

  it('pages through the tasks, keeping the first page out of the URL', async () => {
    mockApi({[SEARCH]: () => json({...startedPage, totalCount: 25, totalPages: 3, page: 2})});
    const onSearchChange = vi.fn();
    const {user} = renderWithApp(<MyTasksPage search={{progress: 'started', page: 2}} onSearchChange={onSearchChange} />, {user: staffUser});

    await screen.findByText('Knee replacement');
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({progress: 'started', page: undefined});
    await user.click(screen.getByRole('button', {name: 'Go to page 3'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({progress: 'started', page: 3});
  });

  it('offers to clear a search that matches nothing, keeping the sort', async () => {
    mockApi({[SEARCH]: () => json(pageOf([], 0))});
    const onSearchChange = vi.fn();
    const {user} = renderWithApp(<MyTasksPage search={{title: 'nothing', sort: 'Priority', asc: false}} onSearchChange={onSearchChange} />, {
      user: staffUser,
    });

    await user.click(await screen.findByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: false});
  });

  it('says when nothing is waiting to be started', async () => {
    mockApi({[SEARCH]: () => json(pageOf([], 0))});
    renderWithApp(<MyTasksPage search={{}} onSearchChange={() => {}} />, {user: staffUser});

    expect(await screen.findByText('Nothing waiting to be started')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'My Worklist'})).toBeInTheDocument();
  });

  it('says when the tasks cannot be loaded', async () => {
    mockTasks(500);
    renderWithApp(<MyTasksPage search={{}} onSearchChange={() => {}} />, {user: staffUser});

    await findAlert('Your tasks could not be loaded. Please try again.');
  });
});
