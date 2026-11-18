import type {DataRequest, DataRequestReviewItem, DataRequestSummary} from '../api/dataRequests';

/** Synthetic data requests for the data request tests. */
export const DATA_REQUEST_ID = '000000d1-0000-0000-0000-000000000001';
export const DATA_REQUEST_PATIENT_ID = '000000d1-0000-0000-0000-000000000002';
export const NOTE_ID = '000000d1-0000-0000-0000-000000000003';
export const FILE_ID = '000000d1-0000-0000-0000-000000000004';

/** The patient, as `GET api/patients/{id}` gives them (an NHS number from the 999 range only). */
export const DATA_REQUEST_PATIENT = {
  id: DATA_REQUEST_PATIENT_ID,
  firstName: 'Freya',
  lastName: 'Ahmed',
  dateOfBirth: '1984-06-02',
  nhsNumber: '9990000026',
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
};

/** An open request the patient made, not yet checked or reviewed, which a holder of ManagePrivacy can manage. */
export const testDataRequest = (overrides: Partial<DataRequest> = {}): DataRequest => ({
  id: DATA_REQUEST_ID,
  patientId: DATA_REQUEST_PATIENT_ID,
  patientName: 'Freya Ahmed',
  receivedOn: '2026-11-02',
  requestedByPatient: true,
  requesterName: null,
  requesterRelationship: null,
  identityCheckedOn: null,
  identityCheckedByUsername: null,
  identityCheckMethod: null,
  identityCheckDetail: null,
  firstDueOn: '2026-12-02',
  dueOn: '2026-12-02',
  extensionMonths: 0,
  extensionReason: null,
  status: 'Open',
  refusalReason: null,
  sentOn: null,
  sendMethod: null,
  closedAtUtc: null,
  closedByUsername: null,
  reviewedAtUtc: null,
  reviewedByUsername: null,
  withheld: [],
  createdAtUtc: '2026-11-02T09:30:00Z',
  createdByUsername: 'test-admin',
  lastUpdatedUtc: '2026-11-02T09:30:00Z',
  version: 3,
  isOverdue: false,
  canManage: true,
  canReview: false,
  canMakePack: false,
  packBlockedReason: 'Record the identity check first.',
  ...overrides,
});

/** The same request, checked and reviewed, so its pack can be made. */
export const readyDataRequest = (overrides: Partial<DataRequest> = {}): DataRequest =>
  testDataRequest({
    identityCheckedOn: '2026-11-03',
    identityCheckedByUsername: 'test-admin',
    identityCheckMethod: 'Documents',
    identityCheckDetail: 'Passport seen',
    reviewedAtUtc: '2026-11-05T14:00:00Z',
    reviewedByUsername: 'd.clinician',
    withheld: [{kind: 'Note', itemId: NOTE_ID, reason: 'Likely to cause serious harm'}],
    canMakePack: true,
    packBlockedReason: null,
    ...overrides,
  });

export const testDataRequestSummary = (overrides: Partial<DataRequestSummary> = {}): DataRequestSummary => ({
  id: DATA_REQUEST_ID,
  patientId: DATA_REQUEST_PATIENT_ID,
  patientName: 'Freya Ahmed',
  receivedOn: '2026-11-02',
  dueOn: '2026-12-02',
  status: 'Open',
  requestedByPatient: true,
  isIdentityChecked: false,
  isReviewed: false,
  isOverdue: false,
  ...overrides,
});

/** Items for the review: a note that starts withheld (about another patient), and a file that doesn't. */
export const testReviewItems = (): DataRequestReviewItem[] => [
  {
    kind: 'Note',
    itemId: NOTE_ID,
    atUtc: '2026-10-20T10:00:00Z',
    title: 'Skin check',
    text: 'A synthetic note that mentions another patient.',
    by: 'k.ashford',
    isDeleted: false,
    isWithheld: true,
    withheldReason: 'About another patient',
  },
  {
    kind: 'File',
    itemId: FILE_ID,
    atUtc: '2026-10-21T11:00:00Z',
    title: 'scan.pdf',
    text: '',
    by: 'k.ashford',
    isDeleted: true,
    isWithheld: false,
    withheldReason: null,
  },
];
