import type {Hospital, HospitalRequest, HospitalRequestSummary} from '../api/hospitals';

/** Synthetic hospitals and hospital requests for the web tests (NHS numbers from the 999 range only). */
export const HOSPITAL_ID = '000000c1-0000-4000-8000-000000000001';
export const REQUEST_ID = '000000c2-0000-4000-8000-000000000001';
export const PATIENT_ID = '000000c4-0000-4000-8000-000000000001';
export const BIOPSY_TYPE = '000000e1-0000-4000-8000-000000000001';

export const HOSPITALS: Hospital[] = [
  {
    id: HOSPITAL_ID,
    name: 'Riverside General',
    odsCode: 'ZZR01',
    contact: 'Requests desk',
    reportDeliveryMethod: 'Post',
    defaultDeadlineDays: 10,
    isArchived: false,
    canBeDeleted: false,
  },
  {id: 'old', name: 'Old Infirmary', odsCode: 'ZZO02', contact: null, reportDeliveryMethod: 'Other', defaultDeadlineDays: 1, isArchived: true, canBeDeleted: true},
];

export const testRequestSummary = (more: Partial<HospitalRequestSummary> = {}): HospitalRequestSummary => ({
  id: REQUEST_ID,
  labCaseNumber: 'MA26-000201',
  labCaseStatus: 'Open',
  hospitalName: 'Riverside General',
  patientId: PATIENT_ID,
  patientName: 'Wilfred Ashdown',
  patientNhsNumber: '9991031375',
  hospitalPatientNumber: 'RG-204817',
  priority: 'Urgent',
  labTestName: 'Skin histology',
  source: 'Hospital',
  isCancerPathway: true,
  reportNeededBy: '2026-11-24',
  registeredAtUtc: '2026-11-17T09:30:00Z',
  ...more,
});

export const testRequest = (more: Partial<HospitalRequest> = {}): HospitalRequest => ({
  id: REQUEST_ID,
  hospitalId: HOSPITAL_ID,
  hospitalName: 'Riverside General',
  hospitalOdsCode: 'ZZR01',
  patientId: PATIENT_ID,
  patient: {
    id: PATIENT_ID,
    firstName: 'Wilfred',
    lastName: 'Ashdown',
    dateOfBirth: '1947-11-03',
    nhsNumber: '9991031375',
    isRestricted: false,
    isDisputed: false,
    isAnonymised: false,
  },
  hospitalPatientNumber: 'RG-204817',
  nhsNumber: '9991031375',
  nhsNumbersDiffer: false,
  surgeonName: 'Ms A. Example',
  priority: 'Urgent',
  isCancerPathway: true,
  authoredOn: '2026-11-16T16:00:00Z',
  reportNeededBy: '2026-11-24',
  collectedAtUtc: '2026-11-16T11:15:00Z',
  specimenTypeId: BIOPSY_TYPE,
  specimenTypeName: 'Skin biopsy (punch)',
  specimenTypeSnomedCode: '309066003',
  labTestId: 'test',
  labTestName: 'Skin histology',
  source: 'Hospital',
  labCaseId: 'case',
  labCaseNumber: 'MA26-000201',
  labCaseStatus: 'Open',
  registeredAtUtc: '2026-11-17T09:30:00Z',
  registeredByUsername: 'r.aldous',
  ...more,
});
