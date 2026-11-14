import {screen, within} from '@testing-library/react';
import {calls, findAlert, json, mockApi, openDialog, pageOf, renderWithApp, staffUser} from '../../test/render';
import type {HomeSearch} from '../listSearch';
import {HomePage, TaskBoard} from './HomePage';

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

/** Answers the two lists' searches by their Started flag. */
function mockLists(notStarted: unknown = notStartedPage, started: unknown = startedPage, status = 200) {
  return mockApi({
    [SEARCH]: (request) => json(new URL(request.url).searchParams.get('Started') === 'true' ? started : notStarted, status),
  });
}

function renderBoard(search: HomeSearch = {}, isNarrow = false) {
  const fetch = mockLists();
  const onSearchChange = vi.fn();
  const result = renderWithApp(<TaskBoard search={search} onSearchChange={onSearchChange} isNarrow={isNarrow} />, {user: staffUser});
  return {...result, fetch, onSearchChange};
}

const query = (started: boolean, rest = '') =>
  `${SEARCH}?PageSize=10&AssignedToMe=true&Started=${started}&Page=1&Sort=dueDate&Ascending=true${rest}`;

describe('HomePage', () => {
  it('shows the not started and started lists side by side', async () => {
    const {fetch} = renderBoard();

    expect(await screen.findByText('Knee replacement')).toBeInTheDocument();
    expect(await screen.findByText('1 procedure not started')).toBeInTheDocument();
    expect(screen.getByText('12 procedures started')).toBeInTheDocument();

    const [notStarted, started] = screen.getAllByRole('table');
    expect(within(notStarted).getByText('Colonoscopy')).toBeInTheDocument();
    expect(within(notStarted).getByText('Overdue')).toBeInTheDocument();
    expect(within(notStarted).getByText('Urgent')).toBeInTheDocument();
    expect(within(notStarted).getByText('Diagnostic procedure')).toBeInTheDocument();
    expect(within(started).getByText('2 of 9 stages done')).toBeInTheDocument();
    expect(within(started).getByText('Blocked')).toBeInTheDocument();
    expect(within(started).getAllByRole('link', {name: 'Test Patient'})[0]).toHaveAttribute('href', '/patients/patient-1');
    expect(calls(fetch).sort()).toEqual([query(false), query(true)]);
  });

  it('asks the API for each list with its own page, sort and filters from the URL', async () => {
    const {fetch} = renderBoard({
      startedPage: 2,
      startedSort: 'Priority',
      startedAsc: false,
      startedQ: 'knee',
      startedOverdue: true,
      notStartedPriority: 'High',
    });

    expect(await screen.findByText('12 matching procedures')).toBeInTheDocument();
    expect(calls(fetch)).toContain(
      `${SEARCH}?PageSize=10&AssignedToMe=true&Started=true&Page=2&Sort=priority&Ascending=false&Search=knee&OverdueOnly=true`,
    );
    expect(calls(fetch)).toContain(query(false, '&Priority=High'));
  });

  it('changes one list in the URL and leaves the other alone', async () => {
    const {user, onSearchChange} = renderBoard({notStartedQ: 'colon'});

    await screen.findByText('Knee replacement');
    await user.click(screen.getByRole('button', {name: /go to page 2/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith({notStartedQ: 'colon', startedPage: 2});
  });

  it('searches a list from its search dialog', async () => {
    const {user, onSearchChange} = renderBoard();

    await screen.findByText('Knee replacement');
    await user.click(screen.getAllByRole('button', {name: 'Search'})[0]);
    await user.type(within(openDialog()!).getByRole('textbox', {name: /procedure/i}), 'colon');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({notStartedQ: 'colon'});
  });

  it('shows one list at a time on a narrow window, with a strip that switches to the other', async () => {
    const {user, onSearchChange} = renderBoard({}, true);

    expect(await screen.findByText('Colonoscopy')).toBeInTheDocument();
    expect(screen.getAllByRole('table')).toHaveLength(1);
    expect(screen.queryByText('Knee replacement')).not.toBeInTheDocument();
    const strip = screen.getByRole('button', {name: 'Show started procedures'});
    // The strip shows the other list's count (the card's clickable button is an overlay, so it's beside it).
    expect(await screen.findByText('12')).toBeInTheDocument();

    await user.click(strip);

    expect(onSearchChange).toHaveBeenLastCalledWith({show: 'started'});
  });

  it('shows the started list on a narrow window when the URL says so, with a strip back', async () => {
    const {user, onSearchChange} = renderBoard({show: 'started'}, true);

    expect(await screen.findByText('Knee replacement')).toBeInTheDocument();
    expect(screen.queryByText('Colonoscopy')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Show not started procedures'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({});
  });

  it('says when nothing is assigned', async () => {
    mockLists(pageOf([], 0), pageOf([], 0));
    renderWithApp(<HomePage search={{}} onSearchChange={() => {}} />, {user: staffUser});

    expect(await screen.findByText('Nothing waiting to be started')).toBeInTheDocument();
    expect(screen.getByText('Nothing in progress')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'My tasks'})).toBeInTheDocument();
  });

  it('says when a list cannot be loaded', async () => {
    mockLists({title: 'Server error'}, {title: 'Server error'}, 500);
    renderWithApp(<HomePage search={{}} onSearchChange={() => {}} />, {user: staffUser});

    await findAlert('The started procedures could not be loaded. Please try again.');
  });
});
