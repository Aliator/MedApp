import {screen} from '@testing-library/react';
import {REQUEST_ID, testRequestSummary} from '../../test/hospitals';
import {json, mockApi, renderWithApp, userWithRoles} from '../../test/render';
import {SPECIMEN_ID, testSummary} from '../../test/specimens';
import {LabHomePage} from './LabHomePage';

const labUser = userWithRoles('test-lab', 'Lab');
const PATHWAY_ID = '00000051-0000-0000-0000-0000000000a1';

const pageOf = (items: unknown[], totalCount = items.length) => json({items, page: 1, pageSize: 5, totalCount, totalPages: 1});

const handedOver = {
  id: PATHWAY_ID,
  patientName: 'Freya Ahmed',
  title: 'Skin lesion review',
  currentStageName: 'Histology',
  dueDate: '2026-11-30',
  awaitingPickUp: true,
};

/** The page's requests: the relay's team views by `View`, the samples views by the body's `view`. */
function renderPage(samples: Record<string, unknown[]> = {}, pathways: Record<string, unknown[]> = {}) {
  const fetch = mockApi({
    'GET /api/pathways/patient-pathways/search': (request) => {
      const view = new URL(request.url).searchParams.get('View') ?? '';
      return pageOf(pathways[view] ?? []);
    },
    'POST /api/specimens/search': async (request) => {
      const {view} = (await request.clone().json()) as {view: string};
      const items = samples[view] ?? [];
      return pageOf(items, view === 'InLab' ? 12 : items.length);
    },
    'POST /api/hospital-requests/search': () => pageOf([testRequestSummary()]),
  });
  return {...renderWithApp(<LabHomePage />, {user: labUser}), fetch};
}

describe('LabHomePage', () => {
  it("shows what's waiting for the lab: pathways handed over and samples in each state, each linking to its list", async () => {
    const {fetch} = renderPage(
      {Expected: [testSummary()], InLab: [testSummary({id: 'in-lab', number: 'MA26-000200', receivedAtUtc: '2026-11-03T10:00:00Z'})]},
      {NewForUs: [handedOver]},
    );

    expect(await screen.findByRole('heading', {name: 'Lab', level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: 'Skin lesion review'})).toHaveAttribute('href', `/pathways/${PATHWAY_ID}`);
    expect(screen.getByText(/^Freya Ahmed · Histology · Target /)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'MA26-000123'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}`);
    expect(screen.getByText('Skin biopsy (punch) · Freya Ahmed · Sent 2 Nov 2026, 15:00')).toBeInTheDocument();
    // A list longer than it shows says how many there are, and opens them all.
    expect(screen.getByRole('link', {name: 'See all 12'})).toHaveAttribute('href', '/lab/samples?view=InLab');
    expect(screen.getAllByRole('link', {name: 'Open the list'}).map((l) => l.getAttribute('href'))).toEqual([
      '/tasks?view=newForUs',
      '/lab/samples?view=Expected',
      '/hospital-requests',
    ]);
    // The hospital's requests (MC18), each opening its lab case.
    expect(screen.getByRole('link', {name: 'MA26-000201'})).toHaveAttribute('href', `/hospital-requests/${REQUEST_ID}`);
    // Empty lists say what they'd hold.
    expect(screen.getByText("Nothing overdue. Not reported by the type's expected days.")).toBeInTheDocument();
    expect(screen.getByText('No pathway is with the lab. Pathways at a stage the lab works.')).toBeInTheDocument();

    const requests = fetch.mock.calls.map(([r]) => r as Request);
    const views = requests.filter((r) => r.method === 'GET').map((r) => new URL(r.url).searchParams.get('View'));
    expect(views.sort()).toEqual(['NewForUs', 'OurTurn']);
    const bodies = await Promise.all(
      requests.filter((r) => r.method === 'POST' && new URL(r.url).pathname === '/api/specimens/search').map((r) => r.clone().json()),
    );
    expect(bodies.map((b: {view: string}) => b.view).sort()).toEqual(['Expected', 'InLab', 'Overdue', 'Reported']);
    expect(bodies[0]).toMatchObject({page: 1, pageSize: 5});
  });

  it("names each list's headings in order, and says when one couldn't be loaded", async () => {
    mockApi({
      'GET /api/pathways/patient-pathways/search': () => json({}, 500),
      'POST /api/specimens/search': () => pageOf([]),
      'POST /api/hospital-requests/search': () => pageOf([]),
    });
    renderWithApp(<LabHomePage />, {user: labUser});

    expect(await screen.findByText('New for the lab could not be loaded. Please try again.')).toBeInTheDocument();
    expect(screen.getAllByRole('heading', {level: 2}).map((h) => h.textContent)).toEqual([
      'New for the lab',
      'Our turn',
      'To receive',
      'In progress',
      'Overdue',
      'Back to the clinic',
      'From the hospital',
    ]);
    expect(await screen.findByText(/^Nothing reported in the last 7 days/)).toBeInTheDocument();
  });
});
