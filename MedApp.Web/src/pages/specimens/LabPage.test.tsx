import {screen, waitFor, within} from '@testing-library/react';
import {json, mockApi, nurseUser, openSearch, pickFavourite, renderWithApp, searchBar, userWithRoles} from '../../test/render';
import {SPECIMEN_ID, testSummary} from '../../test/specimens';
import {HalfViewContext} from '../../components/pages/halfView';
import type {LabSearch} from './labSearch';
import {LabPage} from './LabPage';

const labUser = userWithRoles('test-lab', 'Lab');
const WORKLIST = 'POST /api/specimens/search';

const pageOf = (items: unknown[], totalCount = items.length) => json({items, page: 1, pageSize: 10, totalCount, totalPages: 1});

function renderPage(search: LabSearch = {}, user = labUser, handlers: Record<string, (request: Request) => Response> = {}) {
  const onSearchChange = vi.fn();
  const items = [testSummary({isOverdueAtLab: true}), testSummary({id: 'second', number: 'MA26-000124', patientNhsNumber: null})];
  const fetch = mockApi({
    [WORKLIST]: () => pageOf(items),
    ...handlers,
  });
  const result = renderWithApp(<LabPage search={search} onSearchChange={onSearchChange} />, {user});
  return {...result, fetch, onSearchChange};
}

/** The worklist requests made: each one's address and body. */
async function worklistCalls(fetch: ReturnType<typeof mockApi>) {
  const requests = fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'POST' && new URL(r.url).pathname === '/api/specimens/search');
  return Promise.all(requests.map(async (r) => ({search: new URL(r.url).search, body: (await r.clone().json()) as Record<string, unknown>})));
}

describe('LabPage', () => {
  it("opens on Expected for the lab's staff: each specimen's number, patient, status and when it was sent", async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Samples', level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('tab', {name: 'Expected', selected: true})).toBeInTheDocument();
    expect(screen.getAllByRole('tab').map((t) => t.getAttribute('aria-label') ?? t.textContent)).toHaveLength(6);
    expect(await screen.findByRole('link', {name: 'MA26-000123'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}`);
    expect(screen.getAllByText('Freya Ahmed').length).toBeGreaterThan(0);
    expect(screen.getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(screen.getByText('No NHS number')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: /Sent/})).toBeInTheDocument();
    expect(screen.getAllByText('2 Nov 2026, 15:00')).toHaveLength(2);
    expect(screen.getByText('Overdue at lab')).toBeInTheDocument();
    expect(screen.getByText('Expected: 2 specimens')).toBeInTheDocument();
    expect(await worklistCalls(fetch)).toEqual([{search: '', body: {view: 'Expected', page: 1, pageSize: 10}}]);
  });

  it('opens on To send for those who send them, and keeps a view and sort from the address', async () => {
    const {fetch} = renderPage({view: 'Reported', sort: 'patient', asc: false}, nurseUser);

    expect(await screen.findByRole('tab', {name: 'Reported', selected: true})).toBeInTheDocument();
    expect(await screen.findByText('Reported: 2 specimens')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: /Reported/})).toBeInTheDocument();
    expect((await worklistCalls(fetch))[0].body).toEqual({
      view: 'Reported',
      page: 1,
      pageSize: 10,
      sort: 'patient',
      ascending: false,
    });
  });

  it('changes the view from its tabs, in the address, with no lab picker (the external labs list was dropped)', async () => {
    const {user, onSearchChange} = renderPage({page: 2});

    await pickFavourite(user, 'In the lab');
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'InLab', sort: undefined, asc: undefined});
    expect(screen.queryByRole('combobox', {name: /^Lab/})).not.toBeInTheDocument();
  });

  it('narrows the list to a specimen number typed or scanned into the search, sent in the body and never the address', async () => {
    const {user, fetch, onSearchChange} = renderPage();
    await screen.findByRole('link', {name: 'MA26-000123'});

    await openSearch(user);
    const input = within(searchBar()).getByRole('combobox');
    await user.type(input, 'MA26-000123{Enter}');

    await waitFor(async () => expect((await worklistCalls(fetch)).at(-1)?.body).toMatchObject({search: 'MA26-000123', view: 'Expected'}));
    expect((await worklistCalls(fetch)).every((c) => c.search === '')).toBe(true);
    expect(JSON.stringify(onSearchChange.mock.calls)).not.toContain('MA26');
    expect(screen.getByText('Expected: 2 specimens')).toBeInTheDocument();
  });

  it('shows every specimen once the view is removed, and says when nothing matches the search', async () => {
    const {user, onSearchChange} = renderPage({view: 'All'}, labUser, {[WORKLIST]: () => pageOf([])});

    expect(await screen.findByText('No specimens yet')).toBeInTheDocument();
    expect(screen.getByText('All specimens: 0 specimens')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: /Taken/})).toBeInTheDocument();
    // No tab shows every specimen, so the search shows; going back to the tabs takes the first.
    expect(searchBar()).not.toBeNull();
    await pickFavourite(user, 'Overdue');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({view: 'Overdue'}));
  });

  it('sorts by a column, back to the first page', async () => {
    const {user, onSearchChange} = renderPage({view: 'InLab', page: 3});

    await screen.findByRole('link', {name: 'MA26-000123'});
    await user.click(screen.getByRole('button', {name: /^Specimen/}));

    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'InLab', page: undefined, sort: 'number', asc: true});
  });

  it('says when a view is empty', async () => {
    renderPage({view: 'Overdue'}, labUser, {[WORKLIST]: () => pageOf([])});
    expect(await screen.findByText('Nothing overdue')).toBeInTheDocument();
    expect(screen.getByText("Not reported by the type's expected days, oldest first.")).toBeInTheDocument();
    expect(screen.getByText('Overdue: 0 specimens')).toBeInTheDocument();
  });

  it("says when the specimens couldn't be loaded", async () => {
    renderPage({}, labUser, {[WORKLIST]: () => json({}, 500)});
    expect(await screen.findByText('The specimens could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it.each([
    ['ToSend', 'Taken', '2 Nov 2026, 09:00'],
    ['InLab', 'Received', '3 Nov 2026, 10:00'],
    ['Overdue', 'Expected by', '7 Nov 2026, 09:00'],
    ['RejectedAndLost', 'Closed', '4 Nov 2026, 10:00'],
  ] as const)('shows the time the %s view is about', async (view, header, time) => {
    const row = testSummary({receivedAtUtc: '2026-11-03T10:00:00Z', closedAtUtc: '2026-11-04T10:00:00Z'});
    renderPage({view}, labUser, {[WORKLIST]: () => pageOf([row])});

    expect(await screen.findByRole('columnheader', {name: new RegExp(header)})).toBeInTheDocument();
    expect(await screen.findByText(time)).toBeInTheDocument();
  });

  it('has no lab column, in a narrow window too', async () => {
    mockApi({[WORKLIST]: () => pageOf([testSummary()])});
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <LabPage search={{}} onSearchChange={vi.fn()} />
      </HalfViewContext.Provider>,
      {user: labUser},
    );

    expect(await screen.findByRole('link', {name: 'MA26-000123'})).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: /^Lab/})).not.toBeInTheDocument();
  });

  it('pages through a view', async () => {
    const items = Array.from({length: 10}, (_, i) => testSummary({id: `s${i}`, number: `MA26-0000${10 + i}`}));
    const {user, onSearchChange} = renderPage({view: 'InLab'}, labUser, {[WORKLIST]: () => pageOf(items, 25)});
    await user.click(await screen.findByRole('button', {name: 'Go to page 2'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'InLab', page: 2});
  });
});
