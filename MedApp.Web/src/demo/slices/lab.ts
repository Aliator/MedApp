import {CARROW, LAB, STAFF} from '../accounts';
import {APPOINTMENTS, MORE_APPOINTMENTS, PATHWAYS, USERS, id} from '../ids';
import type {BaseState, DemoNotification} from '../state';
import {daysFrom, today, ukTime} from '../time';

/**
 * The light demo's state for specimens, specimen types, labs and results, and the rest of referrals (the waiting-time
 * clock's pauses, and what a referral keeps beyond the base record). Handlers: `handlers/lab.ts`,
 * `handlers/labResults.ts` and `handlers/referrals.ts`. MedApp tracks specimens and results; it never reads,
 * interprets or flags a report.
 */

export type SpecimenStatus = 'Taken' | 'Sent' | 'Received' | 'Reported' | 'RejectedByLab' | 'Lost' | 'EnteredInError';
export type ResultStatus = 'AwaitingSignOff' | 'Seen' | 'SignedOff' | 'EnteredInError';

export interface DemoSpecimenType {
  id: string;
  name: string;
  expectedTurnaroundDays: number;
  isArchived: boolean;
}

export interface DemoLaboratory {
  id: string;
  name: string;
  usesMedApp: boolean;
  isArchived: boolean;
}

/** A specimen, as stored: the step fields filled in as it moves Taken → Sent → Received → Reported, or closes early. */
export interface DemoSpecimen {
  id: string;
  number: string;
  patientId: string;
  pathwayId: string;
  /** Its pathway's team (the team walls, `handlers/teamWalls.ts`). */
  teamId: string;
  stageId: string;
  specimenTypeId: string;
  laboratoryId: string;
  site: string | null;
  requestNotes: string | null;
  requestingUserId: string;
  status: SpecimenStatus;
  /** When it was recorded (the 24 hours in which whoever recorded it may mark it entered in error run from it). */
  recordedAtUtc: string;
  recordedByUserId: string;
  takenAtUtc: string;
  takenByUserId: string;
  sentAtUtc: string | null;
  sentByUserId: string | null;
  courierReference: string | null;
  receivedAtUtc: string | null;
  receivedByUserId: string | null;
  labReference: string | null;
  reportedAtUtc: string | null;
  reportedByUserId: string | null;
  reportAttachmentId: string | null;
  rejectionReason: string | null;
  closedDetail: string | null;
  closedAtUtc: string | null;
  closedByUserId: string | null;
  version: number;
}

/** A result: a specimen's report, or a report from elsewhere, for a named clinician to sign off. */
export interface DemoResult {
  id: string;
  patientId: string;
  pathwayId: string | null;
  /** Its pathway's team, or for a report without a pathway, the responsible clinician's. */
  teamId: string;
  specimenId: string | null;
  source: 'Specimen' | 'ExternalReport';
  title: string;
  reportAttachmentId: string;
  /** The report file's name, or null once it's deleted (the result entered in error). */
  reportFileName: string | null;
  reportedAtUtc: string;
  reportedByUserId: string;
  responsibleUserId: string;
  status: ResultStatus;
  seenAtUtc: string | null;
  seenByUserId: string | null;
  signedOffAtUtc: string | null;
  signedOffByUserId: string | null;
  action: string | null;
  actionDetail: string | null;
  enteredInErrorAtUtc: string | null;
  enteredInErrorByUserId: string | null;
  errorDetail: string | null;
  version: number;
}

/** A pause of a referral's waiting-time clock (patient-initiated only). */
export interface DemoClockPause {
  id: string;
  referralId: string;
  pausedOn: string;
  pausedAtUtc: string;
  pausedByUserId: string;
  reason: string;
  detail: string | null;
  reviewOn: string;
  resumedOn: string | null;
  resumedByUserId: string | null;
}

/** What a referral holds beyond the base record (`DemoReferral`): the directory's practice, the ODS code, why it was redirected here. */
export interface DemoReferralExtra {
  practiceId: string | null;
  referrerOdsCode: string | null;
  redirectDetail: string | null;
}

export interface LabSlice {
  specimenTypes: DemoSpecimenType[];
  laboratories: DemoLaboratory[];
  specimens: DemoSpecimen[];
  results: DemoResult[];
  /** The last specimen number given (the API's SQL sequence). */
  specimenSequence: number;
  clockPauses: DemoClockPause[];
  /** By referral ID. */
  referralExtras: Record<string, DemoReferralExtra>;
}

export const SPECIMEN_TYPES = {
  skinBiopsy: id(0xe1, 1),
  bloodSample: id(0xe1, 2),
  skinScraping: id(0xe1, 3),
  /** Archived and never used: the setup page can delete it. */
  skinSwab: id(0xe1, 4),
} as const;

export const LABORATORIES = {
  /** Outside the centre, not on MedApp: histology goes there. */
  valley: id(0xe2, 1),
  /** The centre's own lab, on MedApp. */
  centre: id(0xe2, 2),
} as const;

export const SPECIMENS = {
  /** Priya Holloway's punch biopsy, reported two days ago (its result awaits Nadia Thornbury's sign-off). */
  priyaBiopsy: id(0xe3, 1),
  /** Harriet Quill's punch biopsy, reported and signed off. */
  harrietBiopsy: id(0xe3, 2),
  /** Samuel Okafor's skin scraping, sent and now overdue at the lab. */
  samuelScraping: id(0xe3, 3),
  /** Ilona Petrova's first blood sample, rejected by the lab (insufficient sample). */
  ilonaBloodRejected: id(0xe3, 4),
  /** Ilona Petrova's second blood sample, received by the lab. */
  ilonaBlood: id(0xe3, 5),
  /** Ilona Petrova's skin scraping, taken this morning and not yet sent. */
  ilonaScraping: id(0xe3, 6),
  /** Grace Abernethy's blood sample, reported long ago (her pathway is discharged). */
  graceBlood: id(0xe3, 7),
} as const;

export const RESULTS = {
  /** Priya Holloway's biopsy report, awaiting Nadia Thornbury's sign-off. */
  priyaBiopsy: id(0xe4, 1),
  /** Harriet Quill's biopsy report, signed off by Nadia Thornbury. */
  harrietBiopsy: id(0xe4, 2),
  /** Grace Abernethy's blood results, signed off by the admin. */
  graceBlood: id(0xe4, 3),
  /** A report from the GP practice on Daniyal Rees's pathway, awaiting the admin's sign-off (an external result). */
  daniyalExternal: id(0xe4, 4),
} as const;

/** The reports' files (attachments the full version would hold; the light demo has no files). */
const REPORT_FILES = {
  priya: id(0xe5, 1),
  harriet: id(0xe5, 2),
  grace: id(0xe5, 3),
  daniyal: id(0xe5, 4),
} as const;

/** The IDs the Read Me's tour opens. */
export const LAB_IDS = {
  /** The result to sign off as Nadia Thornbury (on Priya Holloway's pathway). */
  priyaResult: RESULTS.priyaBiopsy,
  /** Priya Holloway's reported biopsy specimen. */
  priyaSpecimen: SPECIMENS.priyaBiopsy,
  /** A specimen at the lab now (received): Ilona Petrova's blood sample. */
  specimenAtLab: SPECIMENS.ilonaBlood,
  /** A specimen to send (taken, not sent): Ilona Petrova's skin scraping. */
  specimenToSend: SPECIMENS.ilonaScraping,
  /** A specimen overdue at the lab: Samuel Okafor's skin scraping. */
  specimenOverdue: SPECIMENS.samuelScraping,
  /** The admin's result awaiting sign-off: an external report on Daniyal Rees's pathway. */
  adminResult: RESULTS.daniyalExternal,
} as const;

/** A specimen number as the API makes them: `MA26-000123` (the prefix, the UK year it was recorded, the sequence). */
export const specimenNumber = (sequence: number, onDate: string = today()) =>
  `MA${onDate.slice(2, 4)}-${String(sequence).padStart(6, '0')}`;

const ago = (days: number, hours = 10, minutes = 0) => ukTime(daysFrom(-days), hours, minutes);
const plusMinutes = (isoUtc: string, minutes: number) => new Date(new Date(isoUtc).getTime() + minutes * 60000).toISOString();
const earlierOf = (a: string, b: string) => (Date.parse(a) <= Date.parse(b) ? a : b);

type ResultFields = Partial<DemoResult> &
  Pick<DemoResult, 'patientId' | 'teamId' | 'title' | 'reportAttachmentId' | 'reportedAtUtc' | 'reportedByUserId' | 'responsibleUserId'>;

const makeResult = (resultId: string, fields: ResultFields): DemoResult => ({
  id: resultId,
  pathwayId: null,
  specimenId: null,
  source: 'Specimen',
  reportFileName: 'report.pdf',
  status: 'AwaitingSignOff',
  seenAtUtc: null,
  seenByUserId: null,
  signedOffAtUtc: null,
  signedOffByUserId: null,
  action: null,
  actionDetail: null,
  enteredInErrorAtUtc: null,
  enteredInErrorByUserId: null,
  errorDetail: null,
  version: 1,
  ...fields,
});

/**
 * Seeds this slice, after the base seed: specimens in each of the Lab page's views (to send, expected, in the lab,
 * overdue, rejected, reported), a result awaiting each account's sign-off and two signed off. Priya Holloway's result
 * awaits Nadia Thornbury, so her "Results and team review" stage is held until it's signed off; Harriet Quill's
 * biopsy is reported and signed off, so nothing here holds her Treatment stage. Adds the notifications they'd have
 * caused.
 */
export function seedLab(state: BaseState): LabSlice {
  const pathway = (pathwayId: string) => state.pathways.find((p) => p.id === pathwayId)!;
  const stage = (pathwayId: string, order: number) => pathway(pathwayId).stages.find((s) => s.stageOrder === order)!;
  const visit = (appointmentId: string) => state.appointments.find((a) => a.id === appointmentId)?.startUtc ?? ago(14);

  let sequence = 100;
  const specimen = (
    specimenId: string,
    pathwayId: string,
    stageOrder: number,
    typeId: string,
    labId: string,
    takenAtUtc: string,
    takenBy: string,
    steps: Partial<DemoSpecimen>,
  ): DemoSpecimen => {
    sequence++;
    return {
      id: specimenId,
      number: specimenNumber(sequence, takenAtUtc.slice(0, 10)),
      patientId: pathway(pathwayId).patientId,
      pathwayId,
      teamId: pathway(pathwayId).teamId,
      stageId: stage(pathwayId, stageOrder).id,
      specimenTypeId: typeId,
      laboratoryId: labId,
      site: null,
      requestNotes: null,
      requestingUserId: STAFF.id,
      status: 'Taken',
      recordedAtUtc: takenAtUtc,
      recordedByUserId: takenBy,
      takenAtUtc,
      takenByUserId: takenBy,
      sentAtUtc: null,
      sentByUserId: null,
      courierReference: null,
      receivedAtUtc: null,
      receivedByUserId: null,
      labReference: null,
      reportedAtUtc: null,
      reportedByUserId: null,
      reportAttachmentId: null,
      rejectionReason: null,
      closedDetail: null,
      closedAtUtc: null,
      closedByUserId: null,
      version: 1,
      ...steps,
    };
  };

  const priyaTaken = plusMinutes(visit(APPOINTMENTS.priyaBiopsy), 20);
  const harrietTaken = plusMinutes(visit(MORE_APPOINTMENTS.harrietBiopsy), 20);
  const specimens: DemoSpecimen[] = [
    specimen(SPECIMENS.priyaBiopsy, PATHWAYS.histologyToFinish, 2, SPECIMEN_TYPES.skinBiopsy, LABORATORIES.valley, priyaTaken, STAFF.id, {
      site: 'Upper back, left of midline',
      requestNotes: 'Pigmented lesion, 7 mm. Please report the margins.',
      status: 'Reported',
      sentAtUtc: plusMinutes(priyaTaken, 240),
      sentByUserId: STAFF.id,
      courierReference: 'BATCH-0412',
      receivedAtUtc: plusMinutes(priyaTaken, 24 * 60),
      receivedByUserId: LAB.id,
      labReference: 'EVP-55120',
      reportedAtUtc: ago(2, 14, 0),
      reportedByUserId: LAB.id,
      reportAttachmentId: REPORT_FILES.priya,
      version: 4,
    }),
    specimen(SPECIMENS.harrietBiopsy, PATHWAYS.consentToRecord, 2, SPECIMEN_TYPES.skinBiopsy, LABORATORIES.valley, harrietTaken, STAFF.id, {
      site: 'Left temple',
      status: 'Reported',
      sentAtUtc: plusMinutes(harrietTaken, 240),
      sentByUserId: STAFF.id,
      receivedAtUtc: plusMinutes(harrietTaken, 24 * 60),
      receivedByUserId: LAB.id,
      labReference: 'EVP-54871',
      reportedAtUtc: ago(14, 14, 0),
      reportedByUserId: LAB.id,
      reportAttachmentId: REPORT_FILES.harriet,
      version: 4,
    }),
    specimen(SPECIMENS.samuelScraping, PATHWAYS.onHold, 1, SPECIMEN_TYPES.skinScraping, LABORATORIES.valley, ago(13, 10, 30), USERS.hallworth, {
      site: 'Both hands',
      requestingUserId: USERS.hallworth,
      status: 'Sent',
      sentAtUtc: ago(13, 15, 0),
      sentByUserId: USERS.hallworth,
      courierReference: 'BATCH-0398',
      version: 2,
    }),
    specimen(SPECIMENS.ilonaBloodRejected, PATHWAYS.followUpBooked, 2, SPECIMEN_TYPES.bloodSample, LABORATORIES.centre, ago(4, 10, 0), STAFF.id, {
      status: 'RejectedByLab',
      sentAtUtc: ago(4, 12, 0),
      sentByUserId: STAFF.id,
      rejectionReason: 'InsufficientSample',
      closedAtUtc: ago(3, 9, 0),
      closedByUserId: LAB.id,
      version: 3,
    }),
    specimen(SPECIMENS.ilonaBlood, PATHWAYS.followUpBooked, 2, SPECIMEN_TYPES.bloodSample, LABORATORIES.centre, ago(3, 11, 0), STAFF.id, {
      requestNotes: 'Repeat sample: the first was insufficient.',
      status: 'Received',
      sentAtUtc: ago(3, 13, 0),
      sentByUserId: STAFF.id,
      receivedAtUtc: ago(2, 10, 0),
      receivedByUserId: LAB.id,
      labReference: 'CL-20931',
      version: 3,
    }),
    // Taken this morning (or half an hour ago, early in the day), never in the future.
    specimen(SPECIMENS.ilonaScraping, PATHWAYS.followUpBooked, 3, SPECIMEN_TYPES.skinScraping, LABORATORIES.valley, earlierOf(ukTime(today(), 9, 15), plusMinutes(new Date().toISOString(), -30)), STAFF.id, {
      site: 'Right elbow',
    }),
    specimen(SPECIMENS.graceBlood, PATHWAYS.completed, 1, SPECIMEN_TYPES.bloodSample, LABORATORIES.centre, ago(59, 9, 30), CARROW.id, {
      requestingUserId: CARROW.id,
      status: 'Reported',
      sentAtUtc: ago(59, 12, 0),
      sentByUserId: CARROW.id,
      receivedAtUtc: ago(58, 10, 0),
      receivedByUserId: LAB.id,
      labReference: 'CL-18822',
      reportedAtUtc: ago(55, 15, 0),
      reportedByUserId: LAB.id,
      reportAttachmentId: REPORT_FILES.grace,
      version: 4,
    }),
  ];

  const byId = (specimenId: string) => specimens.find((s) => s.id === specimenId)!;
  const fromSpecimen = (s: DemoSpecimen, typeName: string) => ({
    patientId: s.patientId,
    pathwayId: s.pathwayId,
    teamId: s.teamId,
    specimenId: s.id,
    title: `${typeName} ${s.number}`,
    reportAttachmentId: s.reportAttachmentId!,
    reportedAtUtc: s.reportedAtUtc!,
    reportedByUserId: s.reportedByUserId!,
    responsibleUserId: s.requestingUserId,
  });
  const results: DemoResult[] = [
    makeResult(RESULTS.priyaBiopsy, {...fromSpecimen(byId(SPECIMENS.priyaBiopsy), 'Skin biopsy (punch)'), reportFileName: 'histology-report.pdf'}),
    makeResult(RESULTS.harrietBiopsy, {
      ...fromSpecimen(byId(SPECIMENS.harrietBiopsy), 'Skin biopsy (punch)'),
      reportFileName: 'histology-report.pdf',
      status: 'SignedOff',
      seenAtUtc: ago(6, 9, 0),
      seenByUserId: STAFF.id,
      signedOffAtUtc: ago(5, 16, 0),
      signedOffByUserId: STAFF.id,
      action: 'ForMdtDiscussion',
      version: 3,
    }),
    makeResult(RESULTS.graceBlood, {
      ...fromSpecimen(byId(SPECIMENS.graceBlood), 'Blood sample'),
      reportFileName: 'blood-results.pdf',
      status: 'SignedOff',
      signedOffAtUtc: ago(52, 11, 0),
      signedOffByUserId: CARROW.id,
      action: 'PatientInformed',
      version: 2,
    }),
    makeResult(RESULTS.daniyalExternal, {
      patientId: pathway(PATHWAYS.notStarted).patientId,
      pathwayId: PATHWAYS.notStarted,
      teamId: pathway(PATHWAYS.notStarted).teamId,
      source: 'ExternalReport',
      title: 'Blood tests from the GP practice',
      reportAttachmentId: REPORT_FILES.daniyal,
      reportFileName: 'gp-blood-tests.pdf',
      reportedAtUtc: ago(1, 11, 30),
      reportedByUserId: USERS.sandiford,
      responsibleUserId: CARROW.id,
    }),
  ];

  const notifications: DemoNotification[] = [
    {
      id: id(0xe5, 101),
      userId: STAFF.id,
      kind: 'ResultReady',
      title: 'Result ready',
      message: `${LAB.username} reported a Skin biopsy (punch) specimen.`,
      pathwayId: PATHWAYS.histologyToFinish,
      subjectId: RESULTS.priyaBiopsy,
      createdAtUtc: ago(2, 14, 0),
      isRead: false,
    },
    {
      id: id(0xe5, 102),
      userId: CARROW.id,
      kind: 'ResultReady',
      title: 'Result ready',
      message: 'b.sandiford recorded a result from another service.',
      pathwayId: PATHWAYS.notStarted,
      subjectId: RESULTS.daniyalExternal,
      createdAtUtc: ago(1, 11, 30),
      isRead: false,
    },
    {
      id: id(0xe5, 103),
      userId: STAFF.id,
      kind: 'SpecimenRejectedByLab',
      title: 'Specimen rejected',
      message: 'The lab rejected a Blood sample specimen.',
      pathwayId: PATHWAYS.followUpBooked,
      subjectId: SPECIMENS.ilonaBloodRejected,
      createdAtUtc: ago(3, 9, 0),
      isRead: true,
    },
  ];
  state.notifications.push(...notifications);

  return {
    specimenTypes: [
      {id: SPECIMEN_TYPES.skinBiopsy, name: 'Skin biopsy (punch)', expectedTurnaroundDays: 14, isArchived: false},
      {id: SPECIMEN_TYPES.bloodSample, name: 'Blood sample', expectedTurnaroundDays: 5, isArchived: false},
      {id: SPECIMEN_TYPES.skinScraping, name: 'Skin scraping', expectedTurnaroundDays: 10, isArchived: false},
      {id: SPECIMEN_TYPES.skinSwab, name: 'Skin swab', expectedTurnaroundDays: 4, isArchived: true},
    ],
    laboratories: [
      {id: LABORATORIES.valley, name: 'Example Valley Pathology (synthetic)', usesMedApp: false, isArchived: false},
      {id: LABORATORIES.centre, name: 'Centre laboratory (synthetic)', usesMedApp: true, isArchived: false},
    ],
    specimens,
    results,
    specimenSequence: sequence,
    clockPauses: [],
    referralExtras: {},
  };
}
