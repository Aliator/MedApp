import {QueryClient} from '@tanstack/react-query';
import {createRouter} from '@tanstack/react-router';
import {screen} from '@testing-library/react';
import {routeTree} from '../routeTree.gen';
import {adminUser, json, mockFetch, pageOf, renderApp} from '../test/render';

/**
 * Every page about one patient shows their banner (the control for the wrong-patient hazards H-01 and H-02). This
 * renders each such route through the real router, and fails if a new route under a patient, a pathway, a referral or a
 * specimen isn't listed here, so a page can't be added without it.
 */

const patientId = '0000000b-0000-4000-8000-000000000001';
const pathwayId = '0000000b-0000-4000-8000-000000000002';
const referralId = '0000000b-0000-4000-8000-000000000003';
const specimenId = '0000000b-0000-4000-8000-000000000005';
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

const referral = {
  id: referralId,
  patientId,
  patient,
  specialtyId: '0000000b-0000-4000-8000-000000000004',
  specialtyName: 'Dermatology',
  receivedOn: '2026-11-02',
  referrerPractice: 'Synthetic Street Surgery',
  requestedPriority: 'Medium',
  reason: 'A synthetic reason.',
  status: 'AwaitingTriage',
  isOpen: true,
  registeredAtUtc: '2026-11-02T10:00:00Z',
  registeredByUsername: 'test-admin',
  version: 1,
  lastUpdated: '2026-11-02T10:00:00Z',
  letterCount: 0,
  canEdit: true,
  canClose: true,
  canAttachFiles: true,
  canTriage: false,
  history: [],
};

const specimen = {
  id: specimenId,
  number: 'MA26-000123',
  patientId,
  patient,
  pathwayId,
  pathwayTitle: 'Knee review',
  specimenTypeName: 'Skin biopsy (punch)',
  laboratoryName: 'Northgate pathology',
  requestingUsername: 'test-admin',
  status: 'Taken',
  isOpen: true,
  takenAtUtc: '2026-11-02T09:00:00Z',
  takenByUsername: 'test-admin',
  expectedByUtc: '2026-11-07T09:00:00Z',
  expectedTurnaroundDays: 5,
  version: 1,
  label: {qrPath: 'M4 4h1v1h-1z', size: 29},
  otherSpecimensFromStage: [],
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
  '/referrals/$id': `/referrals/${referralId}`,
  '/referrals/$id/edit': `/referrals/${referralId}/edit`,
  // Once the patient is chosen (from their page, after adding them, or found on the page itself).
  '/referrals/new': `/referrals/new?patientId=${patientId}`,
  '/specimens/$id': `/specimens/${specimenId}`,
  '/specimens/$id/print': `/specimens/${specimenId}/print?sheet=request`,
};

function answerApi() {
  mockFetch((request) => {
    const {pathname} = new URL(request.url);
    if (pathname === `/api/patients/${patientId}`) return json(patient);
    if (pathname === `/api/patients/${patientId}/export`) return json({exportedAtUtc: '2026-11-16T09:00:00Z', exportedBy: 'test-admin', patient});
    if (pathname === `/api/patients/${patientId}/timeline/overview`) return json({upcoming: [], openCount: 0, pathways: []});
    if (pathname === `/api/patients/${patientId}/timeline`) return json({items: [], nextCursor: null});
    if (pathname === `/api/pathways/patient-pathways/${pathwayId}`) return json(pathway);
    if (pathname === `/api/referrals/${referralId}`) return json(referral);
    if (pathname === `/api/specimens/${specimenId}`) return json(specimen);
    if (pathname === '/api/specialties') return json([]);
    if (pathname === '/api/referrals/awaiting-triage/count') return json({count: 0, inYourSpecialties: false});
    if (pathname === '/api/demographics/status') return json({isAvailable: false, source: null});
    if (pathname === '/api/notifications') return json({items: [], unreadCount: 0});
    if (pathname === '/api/auth/me/settings') return json({settings: null});
    if (/\/(templates|definitions|specialties)$/.test(pathname)) return json([]);
    return json(pageOf([]));
  });
}

describe('the patient banner', {timeout: 30_000}, () => {
  it('is checked on every page about one patient or their pathway', () => {
    const router = createRouter({routeTree, context: {queryClient: new QueryClient()}});
    const aboutOnePatient = Object.keys(router.routesByPath).filter(
      (path) =>
        path.startsWith('/patients/$id') ||
        (path.startsWith('/pathways/') && path !== '/pathways/') ||
        (path.startsWith('/referrals/') && path !== '/referrals/') ||
        path.startsWith('/specimens/'),
    );

    expect(aboutOnePatient.sort()).toEqual(Object.keys(PAGES).sort());
  });

  it.each(Object.entries(PAGES))('shows on %s', async (_, path) => {
    answerApi();
    renderApp({path, user: adminUser});

    expect(await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'}, {timeout: 15_000})).toBeInTheDocument();
  });
});
