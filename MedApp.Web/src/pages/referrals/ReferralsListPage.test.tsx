import {screen, waitFor, within} from '@testing-library/react';
import {calls, findAlert, json, mockApi, pageOf, managerUser, pickFavourite, renderWithApp, searchBar, staffUser} from '../../test/render';
import type {CurrentUser} from '../../auth/auth';
import {HalfViewContext} from '../../components/pages/halfView';
import {testReferralSummary} from '../../test/referrals';
import {ReferralsListPage} from './ReferralsListPage';
import type {ReferralsListSearch} from './referralsSearch';

const SEARCH = 'GET /api/referrals/search';
const TEAMS = 'GET /api/teams';
const SKIN_ID = '0000005a-0000-0000-0000-0000000000aa';
/** The list's own searches, leaving out what it asks for its filters' choices (teams). */
const searchCalls = (fetch: ReturnType<typeof mockApi>) => calls(fetch).filter((call) => call.startsWith(SEARCH));

function renderList(search: ReferralsListSearch = {}, {body, status, isNarrow = false, user = staffUser}: {body?: unknown; status?: number; isNarrow?: boolean; user?: CurrentUser} = {}) {
  const fetch = mockApi({
    [SEARCH]: () =>
      json(
        body ??
          pageOf(
            [
              testReferralSummary({id: 'r1', hasLetter: true}),
              testReferralSummary({id: 'r2', patientName: 'Sam Test', patientNhsNumber: null, teamName: 'Urology', requestedPriority: 'Low'}),
            ],
            12,
          ),
        status,
      ),
    [TEAMS]: () => json([{id: SKIN_ID, name: 'Dermatology', isArchived: false}]),
  });
  const onSearchChange = vi.fn();
  const page = <ReferralsListPage search={search} onSearchChange={onSearchChange} />;
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{page}</HalfViewContext.Provider> : page, {
    user,
  });
  return {...result, fetch, onSearchChange};
}

describe('ReferralsListPage', () => {
  it('opens on the inbox: referrals awaiting triage, longest wait first, each opening its referral', async () => {
    const {fetch} = renderList();

    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('href', '/referrals/r1');
    expect(screen.getByRole('heading', {name: 'Referrals', level: 1})).toBeInTheDocument();
    expect(screen.getByText('12 referrals awaiting triage')).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?Page=1&PageSize=10&Sort=daysWaited&Ascending=false&Statuses=AwaitingTriage`]);
    const table = within(screen.getByRole('table'));
    expect(table.getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    expect(table.getByText('No NHS number')).toBeInTheDocument();
    expect(table.getByText('Urology')).toBeInTheDocument();
    expect(table.getAllByText('12 days ago')).toHaveLength(2);
    expect(table.getAllByText('Synthetic Street Surgery')).toHaveLength(2);
    expect(table.getByText('Yes')).toBeInTheDocument();
    expect(table.getByText('No')).toBeInTheDocument();
    expect(table.getAllByText('Awaiting triage')).toHaveLength(2);
    expect(await screen.findByRole('tab', {name: 'Awaiting triage', selected: true})).toBeInTheDocument();
    // Holders of RegisterReferrals (every staff role but Lab and Manager).
    expect(screen.getByRole('link', {name: 'Register Referral'})).toHaveAttribute('href', '/referrals/new');
  });

  it('offers Register Referral only to holders of RegisterReferrals', async () => {
    renderList({}, {user: managerUser});
    await screen.findByText('12 referrals awaiting triage');
    expect(screen.queryByRole('link', {name: 'Register Referral'})).not.toBeInTheDocument();
  });

  it('asks the API for the closed ones, or all, sorted and searched as the URL says', async () => {
    const {fetch, unmount} = renderList({view: 'closed', sort: 'Patient', asc: false, page: 2, patient: 'ahmed'});
    await screen.findByText('12 matching referrals');
    expect(searchCalls(fetch)).toEqual([
      `${SEARCH}?Page=2&PageSize=10&Sort=patient&Ascending=false&Statuses=Accepted&Statuses=Rejected&Statuses=RedirectedElsewhere&Statuses=Withdrawn&Statuses=EnteredInError&Search=ahmed`,
    ]);
    unmount();

    const all = renderList({view: 'all'});
    expect(await screen.findByText('12 referrals')).toBeInTheDocument();
    expect(searchCalls(all.fetch)).toEqual([`${SEARCH}?Page=1&PageSize=10&Sort=receivedOn&Ascending=true`]);
  });

  it('shows the wait where a referral has a clock, in place of how long ago it was received', async () => {
    renderList(
      {},
      {
        body: pageOf([
          testReferralSummary({
            id: 'r1',
            clock: {state: 'Running', daysWaited: 130, targetDays: 126, isBreached: true, targetOn: '2026-11-10', waitStartOn: '2026-07-07'},
          }),
          testReferralSummary({id: 'r2', patientName: 'Sam Test'}),
        ]),
      },
    );
    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('Day 130 of 126')).toBeInTheDocument();
    expect(table.getByText('Breached')).toBeInTheDocument();
    expect(table.getByText('No clock')).toBeInTheDocument();
    // Only the referral without a clock says how long ago it was received.
    expect(table.getAllByText('12 days ago')).toHaveLength(1);
  });

  it('shows the waiting list: accepted, clock not stopped, longest wait first, with the target date', async () => {
    const {fetch} = renderList(
      {view: 'waiting'},
      {
        body: pageOf([
          testReferralSummary({
            id: 'r1',
            status: 'Accepted',
            clock: {state: 'Running', daysWaited: 40, targetDays: 126, targetOn: '2026-12-30', waitStartOn: '2026-10-09'},
          }),
          testReferralSummary({id: 'r2', status: 'Accepted', patientName: 'Sam Test'}),
        ]),
      },
    );
    expect(await screen.findByText('2 referrals on the waiting list')).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?Page=1&PageSize=10&Sort=daysWaited&Ascending=false&Statuses=Accepted&ClockStopped=false`]);
    expect(screen.getByRole('columnheader', {name: /Target date/})).toBeInTheDocument();
    expect(screen.getByText('30 Dec 2026')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(await screen.findByRole('tab', {name: 'Waiting list', selected: true})).toBeInTheDocument();
  });

  it('shows the paused clocks by review date, flagging past ones', async () => {
    const {fetch} = renderList(
      {view: 'paused'},
      {
        body: pageOf([
          testReferralSummary({
            id: 'r1',
            status: 'Accepted',
            clock: {state: 'Paused', daysWaited: 40, targetDays: 126, pausedSinceOn: '2026-11-01', pauseReviewOn: '2026-11-10', isReviewOverdue: true},
          }),
          testReferralSummary({id: 'r2', status: 'Accepted', patientName: 'Sam Test', clock: {state: 'Paused', daysWaited: 3, targetDays: 28}}),
        ]),
      },
    );
    expect(await screen.findByText('2 paused clocks')).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([`${SEARCH}?Page=1&PageSize=10&Sort=reviewOn&Ascending=true&Paused=true`]);
    expect(screen.getByRole('columnheader', {name: /Review date/})).toBeInTheDocument();
    const table = within(screen.getByRole('table'));
    expect(table.getByText('10 Nov 2026')).toBeInTheDocument();
    expect(table.getAllByText('Review overdue').length).toBeGreaterThan(0);
    expect(table.getByText('—')).toBeInTheDocument();
  });

  it('counts one paused clock, and says when none are paused or nobody is waiting', async () => {
    const one = renderList({view: 'paused'}, {body: pageOf([testReferralSummary({id: 'r1'})])});
    expect(await screen.findByText('1 paused clock')).toBeInTheDocument();
    one.unmount();

    const paused = renderList({view: 'paused'}, {body: pageOf([])});
    expect(await screen.findByText('No clocks are paused')).toBeInTheDocument();
    paused.unmount();

    renderList({view: 'waiting'}, {body: pageOf([])});
    expect(await screen.findByText('Nobody is on the waiting list')).toBeInTheDocument();
  });

  it('switches to the waiting list and the paused clocks from the favourites', async () => {
    const {user, onSearchChange} = renderList();
    await screen.findByText('Freya Ahmed');

    await pickFavourite(user, 'Waiting list');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({view: 'waiting'}));
    await pickFavourite(user, 'Paused clocks');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({view: 'paused'}));
  });

  it('filters by team, named in the search, and counts what matches', async () => {
    const {fetch} = renderList({team: SKIN_ID});

    expect(await screen.findByText('12 matching referrals')).toBeInTheDocument();
    expect(searchCalls(fetch)).toEqual([
      `${SEARCH}?Page=1&PageSize=10&Sort=daysWaited&Ascending=false&Statuses=AwaitingTriage&TeamIds=${SKIN_ID}`,
    ]);
    await waitFor(() => expect(searchBar()).toHaveTextContent(/Team: is\s*Dermatology/));
  });

  it('switches views from the favourites, keeping the inbox out of the URL', async () => {
    const {user, onSearchChange} = renderList({sort: 'Priority'});
    await screen.findByText('Freya Ahmed');

    await pickFavourite(user, 'Closed');
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: undefined, patient: undefined, view: 'closed'});
    await pickFavourite(user, 'All');
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: undefined, patient: undefined, view: 'all'});
    await pickFavourite(user, 'Information requested');
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: undefined, patient: undefined, view: 'information'});
  });

  it('shows the inbox again from the favourites', async () => {
    const {user, onSearchChange} = renderList({view: 'closed'});
    await screen.findByText('Freya Ahmed');

    await pickFavourite(user, 'Awaiting triage');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({view: undefined}));
  });

  it('sorts and pages, keeping the first page out of the URL', async () => {
    const {user, onSearchChange} = renderList({page: 2});
    await screen.findByText('Freya Ahmed');

    await user.click(screen.getByRole('button', {name: /Team/}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, sort: 'Team', asc: true});
    await user.click(screen.getByRole('button', {name: /previous page/i}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined});
  });

  it('says when nothing is awaiting triage, nothing is closed, or nothing matches', async () => {
    const {unmount} = renderList({}, {body: pageOf([])});
    expect(await screen.findByText('No referrals are awaiting triage')).toBeInTheDocument();
    unmount();

    const closed = renderList({view: 'closed'}, {body: pageOf([])});
    expect(await screen.findByText('No closed referrals')).toBeInTheDocument();
    closed.unmount();

    const all = renderList({view: 'all'}, {body: pageOf([])});
    expect(await screen.findByText('No referrals yet')).toBeInTheDocument();
    all.unmount();

    const information = renderList({view: 'information'}, {body: pageOf([])});
    expect(await screen.findByText('No referrals are waiting for more information')).toBeInTheDocument();
    expect(screen.getByText('0 referrals waiting for more information')).toBeInTheDocument();
    information.unmount();

    const {user, onSearchChange} = renderList({patient: 'zz'}, {body: pageOf([])});
    expect(await screen.findByText('No referrals match your search')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Clear Search'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, patient: undefined});
  });

  it('says when the referrals could not be loaded', async () => {
    renderList({}, {body: {title: 'Down'}, status: 500});
    expect(await findAlert('The referrals could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('drops the practice and letter in a narrow window or half view', async () => {
    renderList({}, {isNarrow: true});
    await waitFor(() => expect(screen.getAllByText('Freya Ahmed').length).toBeGreaterThan(0));
    expect(screen.queryByText('Synthetic Street Surgery')).not.toBeInTheDocument();
  });
});
