import {QueryClient} from '@tanstack/react-query';
import {createRouter} from '@tanstack/react-router';
import {screen} from '@testing-library/react';
import {routeTree} from '../routeTree.gen';
import {adminUser, json, mockFetch, pageOf, renderApp} from '../test/render';

/**
 * Every page about one patient shows their banner (the control for the wrong-patient hazards H-01 and H-02). This
 * renders each such route through the real router, and fails if a new route under a patient or a pathway isn't
 * listed here, so a page can't be added without it.
 */

const patientId = '0000000b-0000-4000-8000-000000000001';
const pathwayId = '0000000b-0000-4000-8000-000000000002';
// A synthetic patient and test NHS number (the 999 range is never issued to real patients).
const patient = {
  id: patientId,
  firstName: 'Freya',
  lastName: 'Ahmed',
  dateOfBirth: '1976-12-01',
  email: 'freya.ahmed18@example.com',
  nhsNumber: '9990000018',
};
const pathway = {
  id: pathwayId,
  patientId,
  patientName: 'Freya Ahmed',
  patient,
  title: 'Knee review',
  noteList: [],
  dueDate: '2099-12-01',
  dueDateUtc: '2099-12-01T23:59:59Z',
  priority: 'Medium',
  status: 'InProgress',
  isOverdue: false,
  createdAt: '2026-11-01T10:00:00Z',
  lastUpdated: '2026-11-01T10:00:00Z',
  completedStageCount: 0,
  stageCount: 0,
  allowedStatusChanges: [],
  canWorkOnStages: false,
  canEditDetails: true,
  canChangeAssignments: false,
  canBeDeleted: false,
  assignments: [],
  stages: [],
};

/** The pages about one patient, as the router names them, and an address for each. */
const PAGES: Record<string, string> = {
  '/patients/$id': `/patients/${patientId}`,
  '/patients/$id/edit': `/patients/${patientId}/edit`,
  '/patients/$id/export': `/patients/${patientId}/export`,
  '/patients/$id/pathways': `/patients/${patientId}/pathways`,
  '/pathways/$id': `/pathways/${pathwayId}`,
  '/pathways/$id/edit': `/pathways/${pathwayId}/edit`,
  '/pathways/new': `/pathways/new?patientId=${patientId}`,
};

function answerApi() {
  mockFetch((request) => {
    const {pathname} = new URL(request.url);
    if (pathname === `/api/patients/${patientId}`) return json(patient);
    if (pathname === `/api/patients/${patientId}/export`) return json({exportedAtUtc: '2026-11-16T09:00:00Z', exportedBy: 'test-admin', patient});
    if (pathname === `/api/patients/${patientId}/timeline/overview`) return json({upcoming: [], openCount: 0, pathways: []});
    if (pathname === `/api/patients/${patientId}/timeline`) return json({items: [], nextCursor: null});
    if (pathname === `/api/pathways/patient-pathways/${pathwayId}`) return json(pathway);
    if (pathname === '/api/demographics/status') return json({isAvailable: false, source: null});
    if (pathname === '/api/notifications') return json({items: [], unreadCount: 0});
    if (pathname === '/api/auth/me/settings') return json({settings: null});
    if (/\/(templates|definitions)$/.test(pathname)) return json([]);
    return json(pageOf([]));
  });
}

describe('the patient banner', {timeout: 30_000}, () => {
  it('is checked on every page about one patient or their pathway', () => {
    const router = createRouter({routeTree, context: {queryClient: new QueryClient()}});
    const aboutOnePatient = Object.keys(router.routesByPath).filter(
      (path) => path.startsWith('/patients/$id') || (path.startsWith('/pathways/') && path !== '/pathways/'),
    );

    expect(aboutOnePatient.sort()).toEqual(Object.keys(PAGES).sort());
  });

  it.each(Object.entries(PAGES))('shows on %s', async (_, path) => {
    answerApi();
    renderApp({path, user: adminUser});

    expect(await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'}, {timeout: 15_000})).toBeInTheDocument();
  });
});
