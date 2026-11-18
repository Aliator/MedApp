import type {Laboratory, Specimen, SpecimenSummary, SpecimenType} from '../api/specimens';

/** Synthetic IDs and a synthetic patient (NHS numbers only from the 999 range, never issued to real people). */
export const SPECIMEN_ID = '00000051-0000-0000-0000-000000000001';
export const SPECIMEN_PATIENT_ID = '00000051-0000-0000-0000-000000000002';
export const SPECIMEN_PATHWAY_ID = '00000051-0000-0000-0000-000000000003';
export const SPECIMEN_STAGE_ID = '00000051-0000-0000-0000-000000000004';
export const BIOPSY_TYPE_ID = '00000051-0000-0000-0000-000000000005';
export const SWAB_TYPE_ID = '00000051-0000-0000-0000-000000000006';
export const NORTHGATE_LAB_ID = '00000051-0000-0000-0000-000000000007';
export const RIVERSIDE_LAB_ID = '00000051-0000-0000-0000-000000000008';
export const REPORT_ID = '00000051-0000-0000-0000-000000000009';
export const NURSE_USER_ID = '00000051-0000-0000-0000-00000000000a';
export const DOCTOR_USER_ID = '00000051-0000-0000-0000-00000000000b';

export const SPECIMEN_PATIENT = {
  id: SPECIMEN_PATIENT_ID,
  firstName: 'Freya',
  lastName: 'Ahmed',
  dateOfBirth: '1976-12-01',
  nhsNumber: '9990000026',
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
};

export const SPECIMEN_TYPES: SpecimenType[] = [
  {id: BIOPSY_TYPE_ID, name: 'Skin biopsy (punch)', expectedTurnaroundDays: 5, isArchived: false, canBeDeleted: false},
  {id: SWAB_TYPE_ID, name: 'Wound swab', expectedTurnaroundDays: 1, isArchived: false, canBeDeleted: true},
];

export const LABORATORIES: Laboratory[] = [
  {id: NORTHGATE_LAB_ID, name: 'Northgate pathology', usesMedApp: true, isArchived: false, canBeDeleted: false},
  {id: RIVERSIDE_LAB_ID, name: 'Riverside lab', usesMedApp: false, isArchived: false, canBeDeleted: true},
];

/** Holders of WorkPathways, as `GET api/calendar/staff?permission=WorkPathways` lists them. */
export const REQUESTERS = [
  {userId: NURSE_USER_ID, username: 'test-nurse'},
  {userId: DOCTOR_USER_ID, username: 'dr.synthetic'},
];

/** A specimen taken and not yet sent, which the signed-in user can edit, send, mark lost and enter in error. */
export function testSpecimen(fields: Partial<Specimen> = {}): Specimen {
  return {
    id: SPECIMEN_ID,
    number: 'MA26-000123',
    patientId: SPECIMEN_PATIENT_ID,
    patient: SPECIMEN_PATIENT,
    patientFlags: {isRestricted: false, isDisputed: false, isAnonymised: false},
    pathwayId: SPECIMEN_PATHWAY_ID,
    pathwayTitle: 'Skin lesion review',
    stageId: SPECIMEN_STAGE_ID,
    stageName: 'Biopsy',
    specimenTypeId: BIOPSY_TYPE_ID,
    specimenTypeName: 'Skin biopsy (punch)',
    expectedTurnaroundDays: 5,
    laboratoryId: NORTHGATE_LAB_ID,
    laboratoryName: 'Northgate pathology',
    site: 'Left forearm',
    requestNotes: 'Synthetic notes for the lab.',
    requestingUserId: DOCTOR_USER_ID,
    requestingUsername: 'dr.synthetic',
    status: 'Taken',
    isOpen: true,
    takenAtUtc: '2026-11-02T09:00:00Z',
    takenByUsername: 'test-nurse',
    sentAtUtc: null,
    sentByUsername: null,
    courierReference: null,
    receivedAtUtc: null,
    receivedByUsername: null,
    labReference: null,
    reportedAtUtc: null,
    reportedByUsername: null,
    reportAttachmentId: null,
    rejectionReason: null,
    closedDetail: null,
    closedAtUtc: null,
    closedByUsername: null,
    expectedByUtc: '2026-11-07T09:00:00Z',
    isOverdueAtLab: false,
    version: 3,
    canEdit: true,
    canMarkSent: true,
    canMarkReceived: false,
    canReject: false,
    canReport: false,
    canMarkLost: true,
    canMarkEnteredInError: true,
    readOnlyReason: null,
    label: {qrPath: 'M4 4h1v1h-1z', size: 29},
    otherSpecimensFromStage: [],
    ...fields,
  };
}

/** A specimen as a list shows it. */
export function testSummary(fields: Partial<SpecimenSummary> = {}): SpecimenSummary {
  return {
    id: SPECIMEN_ID,
    number: 'MA26-000123',
    patientId: SPECIMEN_PATIENT_ID,
    patientName: 'Freya Ahmed',
    patientNhsNumber: '9990000026',
    patientDateOfBirth: '1976-12-01',
    pathwayId: SPECIMEN_PATHWAY_ID,
    stageId: SPECIMEN_STAGE_ID,
    specimenTypeName: 'Skin biopsy (punch)',
    laboratoryName: 'Northgate pathology',
    status: 'Sent',
    isOpen: true,
    takenAtUtc: '2026-11-02T09:00:00Z',
    sentAtUtc: '2026-11-02T15:00:00Z',
    receivedAtUtc: null,
    reportedAtUtc: null,
    closedAtUtc: null,
    expectedByUtc: '2026-11-07T09:00:00Z',
    isOverdueAtLab: false,
    ...fields,
  };
}
