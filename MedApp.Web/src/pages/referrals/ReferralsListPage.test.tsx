import {screen, waitFor, within} from '@testing-library/react';
import {calls, findAlert, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar, staffUser} from '../../test/render';
import {HalfViewContext} from '../../components/pages/halfView';
import {testReferralSummary} from '../../test/referrals';
import {ReferralsListPage} from './ReferralsListPage';
import type {ReferralsListSearch} from './referralsSearch';

const SEARCH = 'GET /api/referrals/search';

function renderList(search: ReferralsListSearch = {}, {body, status, isNarrow = false}: {body?: unknown; status?: number; isNarrow?: boolean} = {}) {
  const fetch = mockApi({
    [SEARCH]: () =>
      json(
        body ??
          pageOf(
            [
              testReferralSummary({id: 'r1', hasLetter: true}),
              testReferralSummary({id: 'r2', patientName: 'Sam Test', patientNhsNumber: null, specialtyName: 'Urology', requestedPriority: 'Low'}),
            ],
            12,
          ),
        status,
      ),
  });
  const onSearchChange = vi.fn();
  const page = <ReferralsListPage search={search} onSearchChange={onSearchChange} />;
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{page}</HalfViewContext.Provider> : page, {
    user: staffUser,
  });
  return {...result, fetch, onSearchChange};
}

describe('ReferralsListPage', () => {
  it('opens on the inbox: referrals awaiting triage, oldest received first, each opening its referral', async () => {
    const {fetch} = renderList();

    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('href', '/referrals/r1');
    expect(screen.getByRole('heading', {name: 'Referrals', level: 1})).toBeInTheDocument();
    expect(screen.getByText('12 referrals awaiting triage')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`${SEARCH}?Page=1&PageSize=10&Sort=receivedOn&Ascending=true&Statuses=AwaitingTriage`]);
    const table = within(screen.getByRole('table'));
    expect(table.getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    expect(table.getByText('No NHS number')).toBeInTheDocument();
    expect(table.getByText('Urology')).toBeInTheDocument();
    expect(table.getAllByText('12 days ago')).toHaveLength(2);
    expect(table.getAllByText('Synthetic Street Surgery')).toHaveLength(2);
    expect(table.getByText('Yes')).toBeInTheDocument();
    expect(table.getByText('No')).toBeInTheDocument();
    expect(table.getAllByText('Awaiting triage')).toHaveLength(2);
    expect(searchBar()).toHaveTextContent(/Showing: is\s*Awaiting triage/);
    // TODO(useCan): only for holders of RegisterReferrals; for now, everyone signed in.
    expect(screen.getByRole('link', {name: 'Register Referral'})).toHaveAttribute('href', '/referrals/new');
  });

  it('asks the API for the closed ones, or all, sorted and searched as the URL says', async () => {
    const {fetch, unmount} = renderList({view: 'closed', sort: 'Patient', asc: false, page: 2, patient: 'ahmed'});
    await screen.findByText('12 matching referrals');
    expect(calls(fetch)).toEqual([
      `${SEARCH}?Page=2&PageSize=10&Sort=patient&Ascending=false&Statuses=Accepted&Statuses=Rejected&Statuses=RedirectedElsewhere&Statuses=Withdrawn&Statuses=EnteredInError&Search=ahmed`,
    ]);
    unmount();

    const all = renderList({view: 'all'});
    expect(await screen.findByText('12 referrals')).toBeInTheDocument();
    expect(calls(all.fetch)).toEqual([`${SEARCH}?Page=1&PageSize=10&Sort=receivedOn&Ascending=true`]);
  });

  it('switches views from the favourites, keeping the inbox out of the URL', async () => {
    const {user, onSearchChange} = renderList({sort: 'Priority'});
    await screen.findByText('Freya Ahmed');

    await pickFavourite(user, 'Closed');
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: undefined, patient: undefined, view: 'closed'});
    await pickFavourite(user, 'All');
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Priority', asc: undefined, patient: undefined, view: 'all'});
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

    await user.click(screen.getByRole('button', {name: /Specialty/}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, sort: 'Specialty', asc: true});
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
