import {screen} from '@testing-library/react';
import {calls, json, mockApi, nurseUser, pickFavourite, renderWithApp, searchBar, seniorClinicianUser} from '../../test/render';
import {CARDIOLOGY_ID, RESULT_ID, RESULT_PATHWAY_ID, testResultSummary} from '../../test/results';
import type {ResultsSearch} from './resultsSearch';
import {ResultsPage} from './ResultsPage';

const TEAMS = [
  {id: CARDIOLOGY_ID, name: 'Cardiology', isArchived: false},
  {id: '00000061-0000-0000-0000-000000000009', name: 'Old clinic', isArchived: true},
];

function renderPage(search: ResultsSearch = {}, user = seniorClinicianUser, items = [testResultSummary(), testResultSummary({id: 'second', patientNhsNumber: null, pathwayId: null, pathwayTitle: null, needsClinician: true, ageDays: 1})]) {
  const onSearchChange = vi.fn();
  const fetch = mockApi({
    'GET /api/results/search': () => json({items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1}),
    'GET /api/teams': () => json(TEAMS),
  });
  const result = renderWithApp(<ResultsPage search={search} onSearchChange={onSearchChange} />, {user});
  return {...result, fetch, onSearchChange};
}

describe('ResultsPage', () => {
  it('opens on Awaiting my sign-off for those who sign off: the patient, result, pathway, reported and age, clinician and status', async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Results', level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('tab', {name: 'Awaiting my sign-off', selected: true})).toBeInTheDocument();
    expect((await screen.findAllByRole('link', {name: 'Skin biopsy (punch) report'}))[0]).toHaveAttribute('href', `/results/${RESULT_ID}`);
    expect(screen.getAllByRole('link', {name: 'Freya Ahmed'}).length).toBe(2);
    expect(screen.getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(screen.getByText('No NHS number')).toBeInTheDocument();
    expect(screen.getAllByText('Born 1 Dec 1976')).toHaveLength(2);
    expect(screen.getByRole('link', {name: 'Skin lesion review'})).toHaveAttribute('href', `/pathways/${RESULT_PATHWAY_ID}`);
    expect(screen.getAllByText('4 Nov 2026, 10:00')).toHaveLength(2);
    expect(screen.getByText('3 days')).toBeInTheDocument();
    expect(screen.getByText('1 day')).toBeInTheDocument();
    expect(screen.getAllByText('test-senior')).toHaveLength(2);
    expect(screen.getAllByText('Needs a clinician').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Awaiting sign-off')).toHaveLength(2);
    expect(screen.getByText('Awaiting my sign-off: 2 results')).toBeInTheDocument();
    expect(calls(fetch)).toContain('GET /api/results/search?View=AwaitingMine&Page=1&PageSize=10');
  });

  it("opens on everyone's for those who don't sign off, without a tab of their own, and keeps a view and team from the address", async () => {
    const {fetch} = renderPage({team: CARDIOLOGY_ID}, nurseUser);

    expect(await screen.findByRole('tab', {name: 'Awaiting sign-off (everyone)'})).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('tab', {name: 'Awaiting my sign-off'})).not.toBeInTheDocument();
    expect(await screen.findByText('Awaiting sign-off (everyone): 2 results in Cardiology')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET /api/results/search?View=Awaiting&TeamId=${CARDIOLOGY_ID}&Page=1&PageSize=10`);
  });

  it('shows who signed off, when and what they did in Signed off', async () => {
    renderPage({view: 'SignedOff'}, seniorClinicianUser, [
      testResultSummary({status: 'SignedOff', isAwaitingSignOff: false, signedOffAtUtc: '2026-11-06T09:30:00Z', signedOffByUsername: 'dr.synthetic', action: 'ForMdtDiscussion'}),
    ]);

    expect(await screen.findByRole('columnheader', {name: 'Signed off'})).toBeInTheDocument();
    expect(await screen.findByText('To be discussed at MDT')).toBeInTheDocument();
    expect(screen.getByText('dr.synthetic, 6 Nov 2026, 09:30')).toBeInTheDocument();
  });

  it('changes the view from its tabs, and the team from the picker beside the title, in the address', async () => {
    const {user, onSearchChange} = renderPage({team: CARDIOLOGY_ID, page: 2});

    await pickFavourite(user, 'Needs a clinician');
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'NeedsClinician', team: CARDIOLOGY_ID, patient: undefined, sort: undefined, asc: undefined});

    await user.click(screen.getByRole('combobox', {name: /^Team/}));
    expect(screen.queryByRole('option', {name: 'Old clinic'})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('option', {name: 'All teams'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, team: undefined});
  });

  it('asks for the patient searched for, every result once the view is removed, and the column sorted by', async () => {
    const {fetch} = renderPage({view: 'All', patient: 'Freya', sort: 'title', asc: false});

    await screen.findAllByRole('link', {name: 'Freya Ahmed'});
    expect(calls(fetch)).toContain('GET /api/results/search?View=All&Search=Freya&Sort=title&Ascending=false&Page=1&PageSize=10');
    // No tab shows a search by name, so the search shows it.
    expect(searchBar()).toHaveTextContent(/Patient: contains\s*Freya/);
    expect(screen.getByText('All results: 2 results')).toBeInTheDocument();
  });

  it('sorts by a column, back to the first page', async () => {
    const {user, onSearchChange} = renderPage({view: 'SignedOff', page: 2});

    await screen.findAllByRole('link', {name: 'Freya Ahmed'});
    await user.click(screen.getByRole('button', {name: /^Patient/}));

    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'SignedOff', page: undefined, sort: 'patient', asc: true});
  });

  it('says when a view is empty, or nothing matches the search', async () => {
    renderPage({view: 'NeedsClinician'}, seniorClinicianUser, []);
    expect(await screen.findByText('Every result awaiting sign-off has a clinician')).toBeInTheDocument();
  });

  it('offers to clear a search that matches nothing', async () => {
    const {user, onSearchChange} = renderPage({patient: 'Nobody'}, seniorClinicianUser, []);

    await user.click(await screen.findByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, patient: undefined});
  });
});

describe('ResultsPage, paging', () => {
  it('puts the next page in the address, and the first page as none', {timeout: 20_000}, async () => {
    const items = [testResultSummary()];
    const onSearchChange = vi.fn();
    mockApi({
      'GET /api/results/search': () => json({items, page: 2, pageSize: 10, totalCount: 25, totalPages: 3}),
      'GET /api/teams': () => json(TEAMS),
    });
    const {user} = renderWithApp(<ResultsPage search={{page: 2}} onSearchChange={onSearchChange} />, {user: seniorClinicianUser});

    await user.click(await screen.findByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 3});
    await user.click(screen.getByRole('button', {name: 'Go to previous page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined});
  });
});
