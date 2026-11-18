import type {Result, ResultSummary} from '../api/results';

/** Synthetic IDs and a synthetic patient (NHS numbers only from the 999 range, never issued to real people). */
export const RESULT_ID = '00000061-0000-0000-0000-000000000001';
export const RESULT_PATIENT_ID = '00000061-0000-0000-0000-000000000002';
export const RESULT_PATHWAY_ID = '00000061-0000-0000-0000-000000000003';
export const RESULT_SPECIMEN_ID = '00000061-0000-0000-0000-000000000004';
export const RESULT_REPORT_ID = '00000061-0000-0000-0000-000000000005';
export const SENIOR_USER_ID = '00000061-0000-0000-0000-000000000006';
export const CONSULTANT_USER_ID = '00000061-0000-0000-0000-000000000007';
export const CARDIOLOGY_ID = '00000061-0000-0000-0000-000000000008';

export const RESULT_PATIENT = {
  id: RESULT_PATIENT_ID,
  firstName: 'Freya',
  lastName: 'Ahmed',
  dateOfBirth: '1976-12-01',
  nhsNumber: '9990000026',
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
};

/** Holders of SignOffResults, as `GET api/calendar/staff?permission=SignOffResults` lists them. */
export const SIGN_OFF_HOLDERS = [
  {userId: SENIOR_USER_ID, username: 'test-senior'},
  {userId: CONSULTANT_USER_ID, username: 'dr.synthetic'},
];

/**
 * A specimen's report awaiting sign-off by test-senior (`seniorClinicianUser`), which the signed-in user can mark
 * seen, sign off, reassign and enter in error.
 */
export function testResult(fields: Partial<Result> = {}): Result {
  return {
    id: RESULT_ID,
    patientId: RESULT_PATIENT_ID,
    patient: RESULT_PATIENT,
    patientFlags: {isRestricted: false, isDisputed: false, isAnonymised: false},
    pathwayId: RESULT_PATHWAY_ID,
    pathwayTitle: 'Skin lesion review',
    specimenId: RESULT_SPECIMEN_ID,
    specimenNumber: 'MA26-000123',
    specimenTypeName: 'Skin biopsy (punch)',
    source: 'Specimen',
    title: 'Skin biopsy (punch) report',
    reportAttachmentId: RESULT_REPORT_ID,
    reportFileName: 'histology-report.pdf',
    reportHasThumbnail: false,
    reportedAtUtc: '2026-11-04T10:00:00Z',
    reportedByUsername: 'test-lab',
    ageDays: 3,
    responsibleUserId: SENIOR_USER_ID,
    responsibleUsername: 'test-senior',
    needsClinician: false,
    status: 'AwaitingSignOff',
    isAwaitingSignOff: true,
    seenAtUtc: null,
    seenByUsername: null,
    signedOffAtUtc: null,
    signedOffByUsername: null,
    action: null,
    actionDetail: null,
    enteredInErrorAtUtc: null,
    enteredInErrorByUsername: null,
    errorDetail: null,
    version: 2,
    canMarkSeen: true,
    canSignOff: true,
    canReassign: true,
    canMarkEnteredInError: true,
    readOnlyReason: null,
    ...fields,
  };
}

/** A result as a list shows it. */
export function testResultSummary(fields: Partial<ResultSummary> = {}): ResultSummary {
  return {
    id: RESULT_ID,
    patientId: RESULT_PATIENT_ID,
    patientName: 'Freya Ahmed',
    patientNhsNumber: '9990000026',
    patientDateOfBirth: '1976-12-01',
    pathwayId: RESULT_PATHWAY_ID,
    pathwayTitle: 'Skin lesion review',
    source: 'Specimen',
    title: 'Skin biopsy (punch) report',
    specimenNumber: 'MA26-000123',
    reportedAtUtc: '2026-11-04T10:00:00Z',
    ageDays: 3,
    responsibleUsername: 'test-senior',
    needsClinician: false,
    status: 'AwaitingSignOff',
    isAwaitingSignOff: true,
    signedOffAtUtc: null,
    signedOffByUsername: null,
    action: null,
    ...fields,
  };
}
