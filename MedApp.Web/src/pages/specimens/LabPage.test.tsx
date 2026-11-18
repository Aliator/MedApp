import {screen, waitFor} from '@testing-library/react';
import {calls, json, mockApi, nurseUser, renderWithApp, userWithRoles} from '../../test/render';
import {LABORATORIES, NORTHGATE_LAB_ID, SPECIMEN_ID, testSpecimen, testSummary} from '../../test/specimens';
import {HalfViewContext} from '../../components/pages/halfView';
import type {LabSearch} from './labSearch';
import {LabPage} from './LabPage';

const labUser = userWithRoles('test-lab', 'Lab');

function renderPage(search: LabSearch = {}, user = labUser, handlers: Record<string, (request: Request) => Response> = {}) {
  const onSearchChange = vi.fn();
  const items = [testSummary({isOverdueAtLab: true}), testSummary({id: 'second', number: 'MA26-000124', patientNhsNumber: null})];
  const fetch = mockApi({
    'GET /api/specimens/search': () => json({items, page: 1, pageSize: 10, totalCount: 2, totalPages: 1}),
    'GET /api/laboratories': () => json(LABORATORIES),
    ...handlers,
  });
  const result = renderWithApp(<LabPage search={search} onSearchChange={onSearchChange} />, {user});
  return {...result, fetch, onSearchChange};
}

describe('LabPage', () => {
  it("opens on Expected for the lab's staff: each specimen's number, patient, lab, status and when it was sent", async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Lab', level: 1})).toBeInTheDocument();
    expect(screen.getByRole('tab', {name: 'Expected'})).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('Sent and not yet received by the lab, oldest first.')).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: 'MA26-000123'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}`);
    expect(screen.getAllByText('Freya Ahmed').length).toBeGreaterThan(0);
    expect(screen.getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(screen.getByText('No NHS number')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: 'Sent'})).toBeInTheDocument();
    expect(screen.getAllByText('2 Nov 2026, 15:00')).toHaveLength(2);
    expect(screen.getByText('Overdue at lab')).toBeInTheDocument();
    expect(screen.getByText('2 specimens')).toBeInTheDocument();
    expect(calls(fetch)).toContain('GET /api/specimens/search?View=Expected&Page=1&PageSize=10');
  });

  it('opens on To send for those who send them, and keeps a view and lab from the address', async () => {
    const {fetch} = renderPage({view: 'Reported', lab: NORTHGATE_LAB_ID}, nurseUser);

    expect(await screen.findByRole('tab', {name: 'Reported'})).toHaveAttribute('aria-selected', 'true');
    expect(await screen.findByText('2 specimens for Northgate pathology')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: 'Reported'})).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET /api/specimens/search?View=Reported&LaboratoryId=${NORTHGATE_LAB_ID}&Page=1&PageSize=10`);
  });

  it('changes the view and the lab in the address', async () => {
    const {user, onSearchChange} = renderPage({lab: NORTHGATE_LAB_ID});

    await user.click(await screen.findByRole('tab', {name: 'In the lab'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'InLab', lab: NORTHGATE_LAB_ID});

    await user.click(screen.getByRole('combobox', {name: /^Lab/}));
    await user.click(await screen.findByRole('option', {name: 'All labs'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: undefined, lab: undefined});
  });

  it('finds a specimen by its number, typed or scanned with Enter, in the body, and opens it', async () => {
    const {user, fetch, router} = {
      ...renderPage({}, labUser, {'POST /api/specimens/find': () => json(testSpecimen())}),
    };

    await user.type(await screen.findByRole('textbox', {name: /Find by specimen number/}), 'MA26-000123{Enter}');

    await waitFor(() => expect(router.state.location.pathname).toBe(`/specimens/${SPECIMEN_ID}`));
    const find = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(new URL(find.url).search).toBe('');
    expect(await find.json()).toEqual({number: 'MA26-000123'});
  });

  it('says when no specimen has the number', async () => {
    const {user} = renderPage({}, labUser, {'POST /api/specimens/find': () => json({title: 'Not found'}, 404)});

    await user.type(await screen.findByRole('textbox', {name: /Find by specimen number/}), 'MA26-999999');
    await user.click(screen.getByRole('button', {name: 'Find'}));

    expect(await screen.findByText('No specimen has the number MA26-999999.')).toBeInTheDocument();
  });

  it('says when a view is empty, or the specimens could not be loaded', async () => {
    renderPage({view: 'Overdue'}, labUser, {'GET /api/specimens/search': () => json({items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0})});
    expect(await screen.findByText('Nothing overdue')).toBeInTheDocument();
    expect(screen.getByText('0 specimens')).toBeInTheDocument();
  });

  it("says when the specimens couldn't be loaded", async () => {
    renderPage({}, labUser, {'GET /api/specimens/search': () => json({}, 500)});
    expect(await screen.findByText('The specimens could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it.each([
    ['ToSend', 'Taken', '2 Nov 2026, 09:00'],
    ['InLab', 'Received', '3 Nov 2026, 10:00'],
    ['Overdue', 'Expected by', '7 Nov 2026, 09:00'],
    ['RejectedAndLost', 'Closed', '4 Nov 2026, 10:00'],
  ] as const)('shows the time the %s view is about', async (view, header, time) => {
    const row = testSummary({receivedAtUtc: '2026-11-03T10:00:00Z', closedAtUtc: '2026-11-04T10:00:00Z'});
    renderPage({view}, labUser, {'GET /api/specimens/search': () => json({items: [row], page: 1, pageSize: 10, totalCount: 1, totalPages: 1})});

    expect(await screen.findByRole('columnheader', {name: header})).toBeInTheDocument();
    expect(await screen.findByText(time)).toBeInTheDocument();
  });

  it('leaves out the lab column in a narrow window, and finds nothing for an empty number', async () => {
    const onSearchChange = vi.fn();
    const fetch = mockApi({
      'GET /api/specimens/search': () => json({items: [testSummary()], page: 1, pageSize: 10, totalCount: 1, totalPages: 1}),
      'GET /api/laboratories': () => json(LABORATORIES),
    });
    const {user} = renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <LabPage search={{}} onSearchChange={onSearchChange} />
      </HalfViewContext.Provider>,
      {user: labUser},
    );

    expect(await screen.findByRole('link', {name: 'MA26-000123'})).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: 'Lab'})).not.toBeInTheDocument();
    await user.type(screen.getByRole('textbox', {name: /Find by specimen number/}), '   {Enter}');
    expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(false);
  });

  it('pages through a view', async () => {
    const items = Array.from({length: 10}, (_, i) => testSummary({id: `s${i}`, number: `MA26-0000${10 + i}`}));
    const {user, onSearchChange} = renderPage({view: 'InLab'}, labUser, {
      'GET /api/specimens/search': () => json({items, page: 1, pageSize: 10, totalCount: 25, totalPages: 3}),
    });
    await user.click(await screen.findByRole('button', {name: 'Go to page 2'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'InLab', page: 2});
  });
});
