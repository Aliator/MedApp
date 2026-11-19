import {screen} from '@testing-library/react';
import {json, mockApi, renderWithApp, userWithRoles} from '../../test/render';
import {HOSPITAL_ID, HOSPITALS, REQUEST_ID, testRequestSummary} from '../../test/hospitals';
import {HospitalRequestsPage} from './HospitalRequestsPage';
import type {HospitalRequestsSearch} from './hospitalRequestsSearch';

const labUser = userWithRoles('test-lab', 'Lab');
const SEARCH = 'POST /api/hospital-requests/search';

function renderPage(search: HospitalRequestsSearch = {}, user = labUser) {
  const onSearchChange = vi.fn();
  const items = [
    testRequestSummary(),
    testRequestSummary({id: 'second', labCaseNumber: 'MA26-000202', reportNeededBy: null, isCancerPathway: false, priority: 'Routine'}),
  ];
  const fetch = mockApi({
    [SEARCH]: () => json({items, page: 1, pageSize: 10, totalCount: 2, totalPages: 1}),
    'GET /api/hospitals': () => json(HOSPITALS),
  });
  const result = renderWithApp(<HospitalRequestsPage search={search} onSearchChange={onSearchChange} />, {user});
  return {...result, fetch, onSearchChange};
}

async function searchBodies(fetch: ReturnType<typeof mockApi>) {
  const requests = fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'POST');
  return Promise.all(requests.map(async (r) => ({search: new URL(r.url).search, body: (await r.clone().json()) as Record<string, unknown>})));
}

describe('HospitalRequestsPage', () => {
  it('lists each request’s case, patient, hospital number, priority and report date', async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Hospital Requests', level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: 'MA26-000201'})).toHaveAttribute('href', `/hospital-requests/${REQUEST_ID}`);
    expect(screen.getAllByText('RG-204817').length).toBeGreaterThan(0);
    expect(screen.getAllByText('NHS No. 999 103 1375').length).toBeGreaterThan(0);
    expect(screen.getByText('Urgent')).toBeInTheDocument();
    expect(screen.getByText('Cancer pathway')).toBeInTheDocument();
    expect(screen.getByText('No date given')).toBeInTheDocument();
    expect(screen.getByText('2 requests')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Register Request'})).toBeInTheDocument();
    expect(await searchBodies(fetch)).toEqual([{search: '', body: {page: 1, pageSize: 10}}]);
  });

  it('narrows to the hospital and sort in the address', async () => {
    const {fetch} = renderPage({hospital: HOSPITAL_ID, sort: 'needed', asc: true});

    expect(await screen.findByText('2 requests from Riverside General')).toBeInTheDocument();
    expect((await searchBodies(fetch))[0].body).toEqual({hospitalId: HOSPITAL_ID, page: 1, pageSize: 10, sort: 'needed', ascending: true});
  });

  it('offers registering only to the lab', async () => {
    renderPage({}, userWithRoles('test-nurse', 'Nurse'));
    await screen.findByRole('link', {name: 'MA26-000201'});
    expect(screen.queryByRole('button', {name: 'Register Request'})).not.toBeInTheDocument();
  });
});
