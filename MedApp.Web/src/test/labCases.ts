import type {LabCase, LabCaseSummary} from '../api/labCases';
import {SPECIMEN_PATIENT, SPECIMEN_PATIENT_ID, testSummary} from './specimens';

/** A clock (MC19): the hospital's date sooner than the lab's target, so it applies, and the case at risk. */
export const testClock = (more: Partial<NonNullable<LabCase['clock']>> = {}): NonNullable<LabCase['clock']> => ({
  labDueOn: '2026-11-10',
  labLimitOn: '2026-11-13',
  hospitalDueOn: '2026-11-07',
  hospitalDueBasis: 'ReportNeededBy',
  dueOn: '2026-11-07',
  setBy: 'Hospital',
  dueAtUtc: '2026-11-07T23:59:59.999Z',
  atRiskOn: '2026-11-05',
  state: 'AtRisk',
  daysLeft: 1,
  ...more,
});

/** A synthetic lab case (MC15), its patient from the specimens' fixtures (NHS number from the 999 range). */
export const CASE_ID = '00000052-0000-0000-0000-000000000001';

export function testCase(more: Partial<LabCase> = {}): LabCase {
  return {
    id: CASE_ID,
    number: 'MA26-000300',
    status: 'Open',
    source: 'Clinic',
    labTestId: '00000051-0000-0000-0000-000000000007',
    labTestName: 'Skin histology',
    labTestKind: 'Histology',
    patientId: SPECIMEN_PATIENT_ID,
    patient: SPECIMEN_PATIENT,
    patientFlags: {isRestricted: false, isDisputed: false, isAnonymised: false},
    requestingTeamId: '00000052-0000-0000-0000-0000000000d1',
    requestingTeamName: 'Dermatology',
    hospitalId: null,
    hospitalName: null,
    hospitalRequestId: null,
    pathwayId: '00000051-0000-0000-0000-000000000003',
    pathwayTitle: 'Skin lesion review',
    stageId: '00000051-0000-0000-0000-000000000004',
    stageName: 'Biopsy',
    openedAtUtc: '2026-11-03T10:00:00Z',
    openedByUsername: 'test-lab',
    receivedAtUtc: '2026-11-03T10:00:00Z',
    steps: [
      {id: 'case-step-1', kind: 'Received', name: 'Received', atUtc: '2026-11-03T10:00:00Z', byUsername: 'test-lab', isEnteredInError: false, canMarkEnteredInError: false},
      {id: 'case-step-2', kind: 'Accessioned', name: 'Accessioned', atUtc: '2026-11-03T11:00:00Z', byUsername: 'test-lab', isEnteredInError: false, canMarkEnteredInError: false},
      {
        id: 'case-step-3',
        kind: 'Outcome',
        name: 'Sample adequate?',
        choice: 'SampleAdequate',
        outcome: 'Adequate',
        atUtc: '2026-11-03T12:00:00Z',
        byUsername: 'test-lab',
        isEnteredInError: false,
        canMarkEnteredInError: true,
      },
    ],
    next: {kind: 'Choice', name: 'Needs more work?', choice: {choice: 'NeedsMoreWork', question: 'Needs more work?', outcomes: ['NoMoreWork', 'ExtraTests', 'SecondOpinion']}},
    samples: [testSummary({status: 'Received'})],
    reportText: null,
    reportedAtUtc: null,
    reportedByUsername: null,
    resultId: null,
    repeatDetail: null,
    repeatRequestedAtUtc: null,
    repeatRequestedByUsername: null,
    errorDetail: null,
    enteredInErrorAtUtc: null,
    enteredInErrorByUsername: null,
    version: 3,
    canRecord: true,
    canAuthorise: false,
    canMarkEnteredInError: false,
    readOnlyReason: null,
    ...more,
  };
}

export function testCaseSummary(more: Partial<LabCaseSummary> = {}): LabCaseSummary {
  return {
    id: CASE_ID,
    number: 'MA26-000300',
    status: 'Open',
    source: 'Clinic',
    sourceName: 'Dermatology',
    patientId: SPECIMEN_PATIENT_ID,
    patientName: 'Ahmed, Freya',
    patientNhsNumber: '9990000026',
    labTestName: 'Skin histology',
    nextName: 'Needs more work?',
    nextKind: 'Choice',
    openedAtUtc: '2026-11-03T10:00:00Z',
    reportedAtUtc: null,
    ...more,
  };
}
