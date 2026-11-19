import {screen} from '@testing-library/react';
import type {CurrentUser} from '../../auth/auth';
import {json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {TeamHomePage} from './TeamHomePage';

const DERMATOLOGY = {id: 'derm', name: 'Dermatology', kind: 'Clinic' as const, role: 'Member' as const};
const clinician: CurrentUser = {
  ...staffUser,
  username: 'test-derm',
  permissions: [...staffUser.permissions, 'TriageReferrals', 'SignOffResults', 'BookAppointments'],
  teams: [DERMATOLOGY],
};
const PATHWAY_ID = '00000051-0000-0000-0000-0000000000a1';

const pageOf = (items: unknown[], totalCount = items.length) => json({items, page: 1, pageSize: 5, totalCount, totalPages: 1});

const referral = {id: 'ref-1', patientName: 'Freya Ahmed', teamName: 'Dermatology', receivedOn: '2026-11-02'};
const result = {id: 'res-1', title: 'Skin histology', patientName: 'Freya Ahmed', reportedAtUtc: '2026-11-03T10:00:00Z'};
const toBook = {id: 'appt-1', patientName: 'Ellis Varden', pathwayId: PATHWAY_ID, pathwayTitle: 'Suspected skin cancer', stageName: 'Take biopsy', pathwayDueDate: '2026-11-30'};
const handedBack = {id: PATHWAY_ID, patientName: 'Freya Ahmed', title: 'Skin lesion review', currentStageName: 'Results review', dueDate: '2026-11-30'};

function mockLists() {
  return mockApi({
    'GET /api/referrals/search': () => pageOf([referral], 7),
    'GET /api/pathways/patient-pathways/search': (request) => {
      const params = new URL(request.url).searchParams;
      return pageOf(params.get('View') === 'BackFromLab' ? [handedBack] : []);
    },
    'GET /api/results/search': () => pageOf([result]),
    'GET /api/appointments/search': () => pageOf([toBook]),
    'GET /api/consults/incoming': () => json([]),
  });
}

describe('TeamHomePage', () => {
  it("shows the team's lists, each linking to its full list, from the existing searches", async () => {
    const fetch = mockLists();
    renderWithApp(<TeamHomePage />, {user: clinician});

    expect(await screen.findByRole('heading', {name: 'Dermatology', level: 1})).toBeInTheDocument();
    expect(screen.getAllByRole('heading', {level: 2}).map((h) => h.textContent)).toEqual([
      'Triage',
      'My pathways',
      'Back from the lab',
      'Results to sign off',
      'Appointments to book',
      'Consults for your team',
    ]);
    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('href', '/referrals/ref-1');
    expect(screen.getByText(/^Dermatology · Received 2 Nov/)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'See all 7'})).toHaveAttribute('href', '/referrals');
    expect(await screen.findByRole('link', {name: 'Skin lesion review'})).toHaveAttribute('href', `/pathways/${PATHWAY_ID}`);
    expect(screen.getByRole('link', {name: 'Skin histology'})).toHaveAttribute('href', '/results/res-1');
    expect(screen.getByRole('link', {name: 'Ellis Varden'})).toHaveAttribute('href', `/pathways/${PATHWAY_ID}`);
    expect(screen.getByText(/^Take biopsy · Suspected skin cancer · Target /)).toBeInTheDocument();
    expect(screen.getByText('Nothing assigned to you. Unfinished pathways assigned to you.')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'Open the list'}).map((l) => l.getAttribute('href'))).toEqual([
      '/tasks?view=backFromLab',
      '/results',
      '/calendar/requires-appointment',
    ]);

    const urls = fetch.mock.calls.map(([r]) => new URL((r as Request).url));
    const search = (path: string) => urls.filter((u) => u.pathname === path).map((u) => Object.fromEntries(u.searchParams));
    expect(search('/api/referrals/search')[0]).toMatchObject({Statuses: 'AwaitingTriage', PageSize: '5'});
    expect(search('/api/results/search')[0]).toMatchObject({View: 'AwaitingMine', PageSize: '5'});
    expect(search('/api/appointments/search')[0]).toMatchObject({Statuses: 'Requested', PageSize: '5'});
    expect(search('/api/pathways/patient-pathways/search').map((q) => q.AssignedToMe ?? q.View).sort()).toEqual(['BackFromLab', 'true']);
  });

  it('shows only the lists the person works, and "Team" for someone in several clinics', async () => {
    const fetch = mockLists();
    const reader: CurrentUser = {
      ...clinician,
      permissions: ['UseApp', 'ViewPatients', 'WorkPathways'],
      teams: [DERMATOLOGY, {...DERMATOLOGY, id: 'resp', name: 'Respiratory'}],
    };
    renderWithApp(<TeamHomePage />, {user: reader});

    expect(await screen.findByRole('heading', {name: 'Team', level: 1})).toBeInTheDocument();
    expect(screen.getAllByRole('heading', {level: 2}).map((h) => h.textContent)).toEqual(['My pathways', 'Back from the lab', 'Consults for your team']);
    await screen.findByRole('link', {name: 'Skin lesion review'});
    const paths = fetch.mock.calls.map(([r]) => new URL((r as Request).url).pathname);
    expect(paths).not.toContain('/api/referrals/search');
    expect(paths).not.toContain('/api/results/search');
    expect(paths).not.toContain('/api/appointments/search');
  });
});
