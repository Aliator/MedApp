import type {Referral, ReferralSummary} from '../api/referrals';

/** Synthetic IDs and a synthetic patient (NHS numbers only from the 999 range, never issued to real people). */
export const REFERRAL_ID = '00000042-0000-0000-0000-000000000001';
export const REFERRAL_PATIENT_ID = '00000042-0000-0000-0000-000000000002';
export const DERMATOLOGY_ID = '00000042-0000-0000-0000-000000000003';
export const UROLOGY_ID = '00000042-0000-0000-0000-000000000004';

export const REFERRAL_PATIENT = {
  id: REFERRAL_PATIENT_ID,
  firstName: 'Freya',
  lastName: 'Ahmed',
  dateOfBirth: '1976-12-01',
  nhsNumber: '9990000018',
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
};

export const SPECIALTIES = [
  {id: DERMATOLOGY_ID, name: 'Dermatology', isArchived: false, version: 1},
  {id: UROLOGY_ID, name: 'Urology', isArchived: false, version: 1},
];

/** A referral awaiting triage, which the signed-in user can edit, close and attach its letter to. */
export function testReferral(fields: Partial<Referral> = {}): Referral {
  return {
    id: REFERRAL_ID,
    patientId: REFERRAL_PATIENT_ID,
    patient: REFERRAL_PATIENT,
    patientFlags: {isRestricted: false, isDisputed: false, isAnonymised: false},
    specialtyId: DERMATOLOGY_ID,
    specialtyName: 'Dermatology',
    receivedOn: '2026-11-02',
    referrerPractice: 'Synthetic Street Surgery',
    referrerClinician: 'Dr Test',
    referrerOdsCode: 'A12345',
    requestedPriority: 'High',
    reason: 'A synthetic reason for referral.',
    status: 'AwaitingTriage',
    isOpen: true,
    registeredAtUtc: '2026-11-02T10:00:00Z',
    registeredByUsername: 'k.ashford',
    decidedAtUtc: null,
    decidedByUsername: null,
    version: 2,
    lastUpdated: '2026-11-02T10:00:00Z',
    letterCount: 0,
    canEdit: true,
    canClose: true,
    canAttachFiles: true,
    readOnlyReason: null,
    canTriage: false,
    whyCantTriage: null,
    history: [
      {id: 'e1', action: 'Registered', atUtc: '2026-11-02T10:00:00Z', username: 'k.ashford', specialtyName: 'Dermatology', requestedPriority: 'High', changedFields: []},
    ],
    ...fields,
  };
}

let next = 0;

/** A referral in a list. */
export function testReferralSummary(fields: Partial<ReferralSummary> = {}): ReferralSummary {
  return {
    id: `00000042-0000-0000-0001-${String(++next).padStart(12, '0')}`,
    patientId: REFERRAL_PATIENT_ID,
    patientName: 'Freya Ahmed',
    patientNhsNumber: '9990000018',
    patientDateOfBirth: '1976-12-01',
    specialtyId: DERMATOLOGY_ID,
    specialtyName: 'Dermatology',
    receivedOn: '2026-11-02',
    daysSinceReceived: 12,
    requestedPriority: 'High',
    referrerPractice: 'Synthetic Street Surgery',
    status: 'AwaitingTriage',
    hasLetter: false,
    registeredAtUtc: '2026-11-02T10:00:00Z',
    decidedAtUtc: null,
    ...fields,
  };
}
