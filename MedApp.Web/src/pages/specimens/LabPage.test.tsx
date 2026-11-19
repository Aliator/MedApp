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
  const items = [
    testSummary({isOverdueAtLab: true}),
    testSummary({
      id: 'second',
      number: 'MA26-000124',
      patientNhsNumber: null,
      source: 'Hospital',
      requestingTeamId: null,
      requestingTeamName: null,
      hospitalName: 'Riverside General (demo)',
      pathwayId: null,
      stageId: null,
    }),
  ];
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
  it("opens on To receive: each sample's number, patient, where it came from, test, status and when it was taken", async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Samples', level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('tab', {name: 'To receive', selected: true})).toBeInTheDocument();
    expect(screen.getAllByRole('tab').map((t) => t.getAttribute('aria-label') ?? t.textContent)).toHaveLength(5);
    expect(screen.getByText('Clinic')).toBeInTheDocument();
    expect(screen.getByText('Dermatology')).toBeInTheDocument();
    expect(screen.getByText('Hospital')).toBeInTheDocument();
    expect(screen.getByText('Riverside General (demo)')).toBeInTheDocument();
    expect(screen.getAllByText('Skin histology')).toHaveLength(2);
    expect(await screen.findByRole('link', {name: 'MA26-000123'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}`);
    expect(screen.getAllByText('Freya Ahmed').length).toBeGreaterThan(0);
    expect(screen.getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(screen.getByText('No NHS number')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: /Taken/})).toBeInTheDocument();
    expect(screen.getAllByText('2 Nov 2026, 09:00')).toHaveLength(2);
    expect(screen.getAllByText('7 Nov 2026, 09:00')).toHaveLength(2);
    expect(screen.getByText('Overdue at lab')).toBeInTheDocument();
    expect(screen.getByText('To receive: 2 samples')).toBeInTheDocument();
    expect(await worklistCalls(fetch)).toEqual([{search: '', body: {view: 'ToReceive', page: 1, pageSize: 10}}]);
  });

  it('keeps a view and sort from the address, for clinic staff too', async () => {
    const {fetch} = renderPage({view: 'Reported', sort: 'patient', asc: false}, nurseUser);

    expect(await screen.findByRole('tab', {name: 'Reported', selected: true})).toBeInTheDocument();
    expect(await screen.findByText('Reported: 2 samples')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: /Reported/})).toBeInTheDocument();
    expect((await worklistCalls(fetch))[0].body).toEqual({
      view: 'Reported',
      page: 1,
      pageSize: 10,
      sort: 'patient',
      ascending: false,
    });
  });

  it('sends the filters (source, clinic, test, status, due) to the API', async () => {
    const clinic = 'd1000000-0000-0000-0000-000000000001';
    const {fetch} = renderPage({view: 'All', source: 'Clinic', clinic, test: clinic, status: 'Sent', due: 'Overdue'});

    expect(await screen.findByText('All samples: 2 matching samples')).toBeInTheDocument();
    expect((await worklistCalls(fetch))[0].body).toEqual({
      view: 'All',
      page: 1,
      pageSize: 10,
      source: 'Clinic',
      teamId: clinic,
      labTestId: clinic,
      status: 'Sent',
      due: 'Overdue',
    });
  });

  it('changes the view from its tabs, in the address, with no lab picker (the external labs list was dropped)', async () => {
    const {user, onSearchChange} = renderPage({page: 2});

    await pickFavourite(user, 'In the lab');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({view: 'InLab', sort: undefined, asc: undefined}));
    expect(screen.queryByRole('combobox', {name: /^Lab/})).not.toBeInTheDocument();
  });

  it('narrows the list to a sample number typed or scanned into the search, sent in the body and never the address', async () => {
    const {user, fetch, onSearchChange} = renderPage();
    await screen.findByRole('link', {name: 'MA26-000123'});

    await openSearch(user);
    const input = within(searchBar()).getByRole('combobox');
    await user.type(input, 'MA26-000123{Enter}');

    await waitFor(async () => expect((await worklistCalls(fetch)).at(-1)?.body).toMatchObject({search: 'MA26-000123', view: 'ToReceive'}));
    expect((await worklistCalls(fetch)).every((c) => c.search === '')).toBe(true);
    expect(JSON.stringify(onSearchChange.mock.calls)).not.toContain('MA26');
    expect(screen.getByText('To receive: 2 matching samples')).toBeInTheDocument();
  });

  it('shows every sample on All, and says when there are none', async () => {
    const {user, onSearchChange} = renderPage({view: 'All'}, labUser, {[WORKLIST]: () => pageOf([])});

    expect(await screen.findByText('No samples yet')).toBeInTheDocument();
    expect(screen.getByText('All samples: 0 samples')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: /Taken/})).toBeInTheDocument();
    await pickFavourite(user, 'Rejected and lost');
    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({view: 'RejectedAndLost'}));
  });

  it('sorts by a column, back to the first page', async () => {
    const {user, onSearchChange} = renderPage({view: 'InLab', page: 3});

    await screen.findByRole('link', {name: 'MA26-000123'});
    await user.click(screen.getByRole('button', {name: /^Sample/}));

    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'InLab', page: undefined, sort: 'number', asc: true});
  });

  it('says when a view is empty', async () => {
    renderPage({view: 'Overdue'}, labUser, {[WORKLIST]: () => pageOf([])});
    expect(await screen.findByText('Nothing overdue')).toBeInTheDocument();
    expect(screen.getByText("Not reported by the type's expected days, oldest first.")).toBeInTheDocument();
    expect(screen.getByText('Overdue: 0 samples')).toBeInTheDocument();
  });

  it("says when the samples couldn't be loaded", async () => {
    renderPage({}, labUser, {[WORKLIST]: () => json({}, 500)});
    expect(await screen.findByText('Samples not loaded')).toBeInTheDocument();
  });

  it.each([
    ['ToSend', 'Taken', '2 Nov 2026, 09:00'],
    ['InLab', 'Received', '3 Nov 2026, 10:00'],
    ['Expected', 'Sent', '2 Nov 2026, 15:00'],
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
