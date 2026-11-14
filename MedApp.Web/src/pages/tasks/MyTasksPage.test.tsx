import {screen, within} from '@testing-library/react';
import {calls, findAlert, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar, staffUser} from '../../test/render';
import type {TasksSearch} from '../listSearch';
import {MyTasksPage, taskQuery} from './MyTasksPage';

const SEARCH = 'GET /api/procedures/patient-procedures/search';

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
    expect(screen.getByText('1 procedure not started')).toBeInTheDocument();
    expect(searchBar()).toHaveTextContent(/Progress: is\s*Not started/);
    expect(screen.getByRole('columnheader', {name: 'Template'})).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`${SEARCH}?PageSize=10&AssignedToMe=true&Started=false&Page=1&Sort=dueDate&Ascending=true`]);
    // A normal list: no search button, and no switch between two lists.
    expect(screen.queryByRole('button', {name: 'Search'})).not.toBeInTheDocument();
  });

  it('shows the started tasks with the stage they are at, when the URL says so', async () => {
    renderTasks({progress: 'started'});

    const row = (await screen.findByText('Knee replacement')).closest('tr')!;
    expect(within(row).getByText('Consultation')).toBeInTheDocument();
    expect(screen.getByText('12 procedures started')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: 'Current stage'})).toBeInTheDocument();
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

  it('says when nothing is waiting to be started', async () => {
    mockApi({[SEARCH]: () => json(pageOf([], 0))});
    renderWithApp(<MyTasksPage search={{}} onSearchChange={() => {}} />, {user: staffUser});

    expect(await screen.findByText('Nothing waiting to be started')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'My Tasks'})).toBeInTheDocument();
  });

  it('says when the tasks cannot be loaded', async () => {
    mockTasks(500);
    renderWithApp(<MyTasksPage search={{}} onSearchChange={() => {}} />, {user: staffUser});

    await findAlert('Your tasks could not be loaded. Please try again.');
  });
});
