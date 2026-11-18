import type {ConsentForm, ConsentRecord} from '../api/consent';
import type {Pathway} from '../api/pathways';

/** Synthetic consent forms and records for the consent tests. */
export const CONSENT_PATHWAY_ID = '00000061-0000-0000-0000-000000000001';
export const CONSENT_STAGE_ID = '00000061-0000-0000-0000-000000000002';
export const CONSENT_RECORD_ID = '00000061-0000-0000-0000-000000000003';
export const CONSENT_FORM_ID = '00000061-0000-0000-0000-000000000004';
export const SCAN_ID = '00000061-0000-0000-0000-000000000005';

export const CONSENT_PATIENT = {
  id: '00000061-0000-0000-0000-000000000006',
  firstName: 'Ivy',
  lastName: 'Example',
  dateOfBirth: '1980-03-04',
  nhsNumber: '9990000018',
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
};

export const CONSENT_FORMS: ConsentForm[] = [
  {id: CONSENT_FORM_ID, name: 'Consent for a skin procedure (synthetic)', formVersion: '3', isArchived: false, canBeDeleted: false},
  {id: '00000061-0000-0000-0000-000000000007', name: 'Old consent form (synthetic)', formVersion: '1', isArchived: true, canBeDeleted: true},
];

export const consentPathway = (canRecordConsent = true): Pathway => ({
  id: CONSENT_PATHWAY_ID,
  title: 'Skin lesion review',
  patientId: CONSENT_PATIENT.id,
  patient: CONSENT_PATIENT,
  stages: [{id: CONSENT_STAGE_ID, name: 'Biopsy', stageOrder: 1, isCurrent: true, needsConsent: true, canRecordConsent}],
});

export const testConsent = (overrides: Partial<ConsentRecord> = {}): ConsentRecord => ({
  id: CONSENT_RECORD_ID,
  patientId: CONSENT_PATIENT.id,
  pathwayId: CONSENT_PATHWAY_ID,
  stageId: CONSENT_STAGE_ID,
  stageName: 'Biopsy',
  consentFormId: CONSENT_FORM_ID,
  formName: 'Consent for a skin procedure (synthetic)',
  formVersion: '3',
  signedOn: '2026-11-02',
  signedBy: 'Patient',
  recordedByUsername: 'test-nurse',
  recordedAtUtc: '2026-11-02T09:00:00Z',
  scanAttachmentId: null,
  status: 'Valid',
  version: 1,
  withdrawnAfterStageFinished: false,
  canWithdraw: true,
  canMarkEnteredInError: true,
  canAttachScan: true,
  ...overrides,
});
