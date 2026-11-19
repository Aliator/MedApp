import type {Letter, LetterRecipient, LetterSummary} from '../api/letters';

/** Synthetic IDs and a synthetic patient (NHS numbers only from the 999 range, never issued to real people). */
export const LETTER_ID = '00000061-0000-0000-0000-000000000001';
export const LETTER_PATIENT_ID = '00000061-0000-0000-0000-000000000002';
export const LETTER_PATHWAY_ID = '00000061-0000-0000-0000-000000000003';
export const LETTER_AUTHOR_ID = '00000061-0000-0000-0000-000000000004';
export const LETTER_APPROVER_ID = '00000061-0000-0000-0000-000000000005';
export const LETTER_PRACTICE_ID = '00000061-0000-0000-0000-000000000006';
export const LETTER_PDF_ID = '00000061-0000-0000-0000-000000000007';

export const LETTER_PATIENT = {
  id: LETTER_PATIENT_ID,
  firstName: 'Freya',
  lastName: 'Ahmed',
  dateOfBirth: '1976-12-01',
  nhsNumber: '9990000018',
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
};

/** The patient's practice, as a GP practice recipient. */
export function practiceRecipient(fields: Partial<LetterRecipient> = {}): LetterRecipient {
  return {
    id: '00000061-0000-0000-0001-000000000001',
    kind: 'GpPractice',
    practiceId: LETTER_PRACTICE_ID,
    name: 'Testbrook Surgery',
    addressLines: '1 Synthetic Street\nTestbrook',
    postcode: 'ZZ1 1ZZ',
    email: 'testbrook.surgery@nhs.net',
    dispatchStatus: 'NotSent',
    method: null,
    sentOn: null,
    sentAtUtc: null,
    sentByUsername: null,
    canSendSecureEmail: false,
    whyNoSecureEmail: null,
    canRecordSent: false,
    ...fields,
  };
}

/** The patient, as a recipient. */
export function patientRecipient(fields: Partial<LetterRecipient> = {}): LetterRecipient {
  return {
    id: '00000061-0000-0000-0001-000000000002',
    kind: 'Patient',
    practiceId: null,
    name: 'Freya Ahmed',
    addressLines: '2 Example Road\nTestbrook',
    postcode: 'ZZ2 2ZZ',
    email: null,
    dispatchStatus: 'NotSent',
    method: null,
    sentOn: null,
    sentAtUtc: null,
    sentByUsername: null,
    canSendSecureEmail: false,
    whyNoSecureEmail: null,
    canRecordSent: false,
    ...fields,
  };
}

/** A draft its author (the signed-in user) can edit, about a pathway, to the patient's practice and the patient. */
export function testLetter(fields: Partial<Letter> = {}): Letter {
  return {
    id: LETTER_ID,
    patientId: LETTER_PATIENT_ID,
    patientName: 'Freya Ahmed',
    pathwayId: LETTER_PATHWAY_ID,
    pathwayTitle: 'Skin check',
    referralId: null,
    kind: 'DischargeSummary',
    templateName: 'Dermatology end of care',
    status: 'Draft',
    isReturned: false,
    authorUserId: LETTER_AUTHOR_ID,
    authorUsername: 'k.ashford',
    approverUserId: null,
    approverUsername: null,
    createdAt: '2026-11-16T10:00:00Z',
    lastUpdated: '2026-11-16T10:30:00Z',
    submittedAtUtc: null,
    approvedAtUtc: null,
    withdrawnAtUtc: null,
    recipientCount: 2,
    unsentCount: 2,
    patient: LETTER_PATIENT,
    patientFlags: {isRestricted: false, isDisputed: false, isAnonymised: false},
    letterhead: 'Synthetic Skin Centre\n1 Example Way',
    body: '# Diagnosis\n\nA synthetic diagnosis.\n\n# Plan\n\nReview in a month.',
    closing: 'Yours sincerely,\n\nk.ashford',
    largePrint: false,
    returnComment: null,
    returnedAtUtc: null,
    returnedByUsername: null,
    withdrawnByUsername: null,
    withdrawnReason: null,
    pdfAttachmentId: null,
    replacesLetterId: null,
    replacedByLetterId: null,
    missingMergeFields: [],
    version: 3,
    recipients: [practiceRecipient(), patientRecipient()],
    canEdit: true,
    canSubmit: true,
    canApprove: false,
    canReturn: false,
    canDiscard: true,
    canWithdraw: false,
    canWriteCorrection: false,
    canPreview: true,
    readOnlyReason: null,
    secureEmailAvailable: true,
    ...fields,
  };
}

/** An approved letter, read only, still to send to both recipients. */
export function approvedLetter(fields: Partial<Letter> = {}): Letter {
  return testLetter({
    status: 'Approved',
    approverUserId: LETTER_APPROVER_ID,
    approverUsername: 'd.clinician',
    submittedAtUtc: '2026-11-17T09:00:00Z',
    approvedAtUtc: '2026-11-17T11:00:00Z',
    pdfAttachmentId: LETTER_PDF_ID,
    canEdit: false,
    canSubmit: false,
    canDiscard: false,
    canPreview: false,
    canWithdraw: true,
    recipients: [
      practiceRecipient({canSendSecureEmail: true, canRecordSent: true}),
      patientRecipient({canRecordSent: true, whyNoSecureEmail: 'Letters go to patients by post or by hand, never by email.'}),
    ],
    ...fields,
  });
}

let next = 0;

/** A letter in a list. */
export function testLetterSummary(fields: Partial<LetterSummary> = {}): LetterSummary {
  return {
    id: `00000061-0000-0000-0002-${String(++next).padStart(12, '0')}`,
    patientId: LETTER_PATIENT_ID,
    patientName: 'Freya Ahmed',
    pathwayId: LETTER_PATHWAY_ID,
    pathwayTitle: 'Skin check',
    referralId: null,
    kind: 'ClinicLetter',
    templateName: 'Clinic follow-up',
    status: 'Draft',
    isReturned: false,
    authorUserId: LETTER_AUTHOR_ID,
    authorUsername: 'k.ashford',
    approverUserId: null,
    approverUsername: null,
    createdAt: '2026-11-16T10:00:00Z',
    lastUpdated: '2026-11-16T10:30:00Z',
    submittedAtUtc: null,
    approvedAtUtc: null,
    withdrawnAtUtc: null,
    recipientCount: 2,
    unsentCount: 2,
    ...fields,
  };
}

/** A page of letters, as the search returns it. */
export const letterPage = (items: LetterSummary[], totalCount = items.length) => ({items, page: 1, pageSize: 10, totalCount, totalPages: Math.ceil(totalCount / 10)});
