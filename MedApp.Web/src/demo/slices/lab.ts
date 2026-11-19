import {HALLWORTH, LAB, STAFF} from '../accounts';
import {APPOINTMENTS, MORE_APPOINTMENTS, PATHWAYS, USERS, id} from '../ids';
import type {BaseState, DemoNotification} from '../state';
import type {DemoHospitalRequest} from './hospitals';
import {daysFrom, today, ukTime} from '../time';

/**
 * The light demo's state for specimens, specimen types, the lab's test catalogue (MC13) and results, and the rest of referrals (the waiting-time
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
  /** Its SNOMED CT specimen code (FHIR `Specimen.type`; MC13), optional. */
  snomedCode: string | null;
  isArchived: boolean;
}

export type LabTestKind = 'Histology' | 'Cytology' | 'Haematology' | 'ClinicalChemistry' | 'Microbiology' | 'Other';
export type LabStepKind = 'Standard' | 'ExtraTests' | 'SentForSecondOpinion' | 'SecondOpinionReturned';

/** A test in the lab's catalogue (MC13): its time limits in calendar days and its ordered steps. */
export interface DemoLabTest {
  id: string;
  name: string;
  kind: LabTestKind;
  snomedCode: string | null;
  targetDays: number;
  limitDays: number;
  atRiskDays: number;
  steps: {id: string; name: string; kind: LabStepKind; isOptional: boolean}[];
  isArchived: boolean;
}

/** Where a sample came from (`SpecimenSource`, MC14): a clinic's pathway, or the hospital (MC18 registers those). */
export type SpecimenSource = 'Clinic' | 'Hospital';

/**
 * A sample ("specimen" in code), as stored: the step fields filled in as it moves Taken → Sent → Received → Reported,
 * or closes early. MC14: every sample, a clinic's or the hospital's, in one database with one number sequence.
 */
export interface DemoSpecimen {
  id: string;
  number: string;
  patientId: string;
  source: SpecimenSource;
  /** The clinic that asked for it (a clinic sample's pathway team); null for the hospital's. */
  requestingTeamId: string | null;
  /** The hospital that sent it, and the request it came with (MC18); null for a clinic's. */
  hospitalId: string | null;
  hospitalRequestId: string | null;
  /** The test asked for, from the lab's catalogue (MC13), if one was chosen. */
  labTestId: string | null;
  /** Its lab case (MC15): the case it's worked in, or none (not received yet, or reported before lab cases). */
  labCaseId: string | null;
  /** Its pathway and stage: a clinic sample's; the hospital's have none. */
  pathwayId: string | null;
  /** Its pathway's team, or the lab's for a hospital sample (the team walls, `handlers/teamWalls.ts`). */
  teamId: string;
  stageId: string | null;
  specimenTypeId: string;
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
  /** The lab case it reports (MC15), and the results the lab entered there (the light demo's report, in place of a file). */
  labCaseId?: string | null;
  reportText?: string | null;
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
  labTests: DemoLabTest[];
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

export const LAB_TESTS = {
  skinHistology: id(0xed, 1),
  fullBloodCount: id(0xed, 2),
  fungalScraping: id(0xed, 3),
  /** Archived and never used: the setup page can delete it. */
  liverFunction: id(0xed, 4),
} as const;

export const SPECIMENS = {
  /** Priya Holloway's punch biopsy, received by the lab: its case is to report (MC16). */
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
  /** Callum Ashby's punch biopsy, reported three days ago (its result awaits Nadia Thornbury's sign-off). */
  callumBiopsy: id(0xe3, 8),
  /** Aisha Khoury's second lesion's biopsy: too little to test, so its case asked for a repeat (MC22; `seedLabCases`). */
  aishaBiopsy: id(0xe3, 9),
} as const;

export const RESULTS = {
  /** Callum Ashby's biopsy report, awaiting Nadia Thornbury's sign-off (his review stage waits for it). */
  callumBiopsy: id(0xe4, 1),
  /** Harriet Quill's biopsy report, signed off by Nadia Thornbury. */
  harrietBiopsy: id(0xe4, 2),
  /** A report from the GP practice on Tobias Lund's pathway, awaiting Tom Hallworth's sign-off (an external result). */
  gpReport: id(0xe4, 4),
} as const;

/** The reports' files (attachments the full version would hold; the light demo has no files). */
const REPORT_FILES = {
  callum: id(0xe5, 1),
  harriet: id(0xe5, 2),
  gpReport: id(0xe5, 4),
} as const;

/** The IDs the Read Me's tour opens. */
export const LAB_IDS = {
  /** The result to sign off as Nadia Thornbury (on Callum Ashby's pathway, back from the lab). */
  resultToSignOff: RESULTS.callumBiopsy,
  /** Priya Holloway's biopsy specimen, in the lab. */
  priyaSpecimen: SPECIMENS.priyaBiopsy,
  /** A specimen at the lab now (received): Ilona Petrova's blood sample. */
  specimenAtLab: SPECIMENS.ilonaBlood,
  /** A specimen to send (taken, not sent): Ilona Petrova's skin scraping. */
  specimenToSend: SPECIMENS.ilonaScraping,
  /** A specimen overdue at the lab: Samuel Okafor's skin scraping. */
  specimenOverdue: SPECIMENS.samuelScraping,
  /** Tom Hallworth's result awaiting sign-off: an external report on Tobias Lund's pathway. */
  gpReport: RESULTS.gpReport,
} as const;

/** A specimen number as the API makes them: `MA26-000123` (the prefix, the UK year it was recorded, the sequence). */
export const specimenNumber = (sequence: number, onDate: string = today()) =>
  `MA${onDate.slice(2, 4)}-${String(sequence).padStart(6, '0')}`;

/** The test each seeded type was sent for (the lab's catalogue, MC13). */
const TEST_OF_TYPE: Record<string, string> = {
  [SPECIMEN_TYPES.skinBiopsy]: LAB_TESTS.skinHistology,
  [SPECIMEN_TYPES.bloodSample]: LAB_TESTS.fullBloodCount,
  [SPECIMEN_TYPES.skinScraping]: LAB_TESTS.fungalScraping,
};

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
 * overdue, rejected, reported), results awaiting Nadia Thornbury's and Tom Hallworth's sign-off and one signed off. Callum Ashby's result
 * awaits Nadia Thornbury, so his "Histology result" stage is held until it's signed off; Harriet Quill's
 * biopsy is reported and signed off, so nothing here holds her Refer to hospital stage. Adds the notifications they'd have
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
    takenAtUtc: string,
    takenBy: string,
    steps: Partial<DemoSpecimen>,
  ): DemoSpecimen => {
    sequence++;
    return {
      id: specimenId,
      number: specimenNumber(sequence, takenAtUtc.slice(0, 10)),
      patientId: pathway(pathwayId).patientId,
      source: 'Clinic',
      requestingTeamId: pathway(pathwayId).teamId,
      hospitalId: null,
      hospitalRequestId: null,
      labTestId: TEST_OF_TYPE[typeId] ?? null,
      labCaseId: null,
      pathwayId,
      teamId: pathway(pathwayId).teamId,
      stageId: stage(pathwayId, stageOrder).id,
      specimenTypeId: typeId,
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
  const callumTaken = plusMinutes(stage(PATHWAYS.backFromLab, 3).completedAtUtc!, -120);
  // Aisha Khoury's added route's Take biopsy (MC10): its sample, received; its case rejects it as insufficient (MC22).
  const aishaTake = pathway(PATHWAYS.forkedTests).stages.find((s) => s.branchAdditionId && s.name === 'Take biopsy')!;
  const aishaTaken = plusMinutes(aishaTake.completedAtUtc!, -20);
  const specimens: DemoSpecimen[] = [
    specimen(SPECIMENS.priyaBiopsy, PATHWAYS.histologyToFinish, 3, SPECIMEN_TYPES.skinBiopsy, priyaTaken, STAFF.id, {
      site: 'Upper back, left of midline',
      requestNotes: 'Pigmented lesion, 7 mm. Please report the margins.',
      status: 'Received',
      sentAtUtc: plusMinutes(priyaTaken, 240),
      sentByUserId: STAFF.id,
      courierReference: 'BATCH-0412',
      receivedAtUtc: plusMinutes(priyaTaken, 24 * 60),
      receivedByUserId: LAB.id,
      labReference: 'EVP-55120',
      version: 3,
    }),
    specimen(SPECIMENS.harrietBiopsy, PATHWAYS.referredToHospital, 3, SPECIMEN_TYPES.skinBiopsy, harrietTaken, STAFF.id, {
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
    specimen(SPECIMENS.samuelScraping, PATHWAYS.onHold, 3, SPECIMEN_TYPES.skinScraping, ago(13, 10, 30), USERS.hallworth, {
      site: 'Both hands',
      requestingUserId: USERS.hallworth,
      status: 'Sent',
      sentAtUtc: ago(13, 15, 0),
      sentByUserId: USERS.hallworth,
      courierReference: 'BATCH-0398',
      version: 2,
    }),
    specimen(SPECIMENS.ilonaBloodRejected, PATHWAYS.followUpBooked, 3, SPECIMEN_TYPES.bloodSample, ago(4, 10, 0), STAFF.id, {
      status: 'RejectedByLab',
      sentAtUtc: ago(4, 12, 0),
      sentByUserId: STAFF.id,
      rejectionReason: 'InsufficientSample',
      closedAtUtc: ago(3, 9, 0),
      closedByUserId: LAB.id,
      version: 3,
    }),
    specimen(SPECIMENS.ilonaBlood, PATHWAYS.followUpBooked, 3, SPECIMEN_TYPES.bloodSample, ago(3, 11, 0), STAFF.id, {
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
    specimen(SPECIMENS.ilonaScraping, PATHWAYS.followUpBooked, 4, SPECIMEN_TYPES.skinScraping, earlierOf(ukTime(today(), 9, 15), plusMinutes(new Date().toISOString(), -30)), STAFF.id, {
      site: 'Right elbow',
    }),
    // Reported when its case's report was authorised, which finished his Histology (MC16).
    specimen(SPECIMENS.callumBiopsy, PATHWAYS.backFromLab, 3, SPECIMEN_TYPES.skinBiopsy, callumTaken, STAFF.id, {
      site: 'Back of the right hand',
      status: 'Reported',
      sentAtUtc: plusMinutes(callumTaken, 240),
      sentByUserId: STAFF.id,
      receivedAtUtc: plusMinutes(callumTaken, 24 * 60),
      receivedByUserId: LAB.id,
      labReference: 'EVP-55187',
      reportedAtUtc: stage(PATHWAYS.backFromLab, 4).completedAtUtc!,
      reportedByUserId: LAB.id,
      reportAttachmentId: REPORT_FILES.callum,
      version: 4,
    }),
    specimen(SPECIMENS.aishaBiopsy, PATHWAYS.forkedTests, aishaTake.stageOrder, SPECIMEN_TYPES.skinBiopsy, aishaTaken, STAFF.id, {
      site: 'Left shin, lower lesion',
      status: 'Received',
      sentAtUtc: plusMinutes(aishaTaken, 240),
      sentByUserId: STAFF.id,
      receivedAtUtc: plusMinutes(aishaTaken, 24 * 60),
      receivedByUserId: LAB.id,
      version: 3,
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
    makeResult(RESULTS.callumBiopsy, {...fromSpecimen(byId(SPECIMENS.callumBiopsy), 'Skin biopsy (punch)'), reportFileName: 'histology-report.pdf'}),
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
    makeResult(RESULTS.gpReport, {
      patientId: pathway(PATHWAYS.unassigned).patientId,
      pathwayId: PATHWAYS.unassigned,
      teamId: pathway(PATHWAYS.unassigned).teamId,
      source: 'ExternalReport',
      title: 'Lesion photographs from the GP practice',
      reportAttachmentId: REPORT_FILES.gpReport,
      reportFileName: 'gp-report.pdf',
      reportedAtUtc: ago(1, 11, 30),
      reportedByUserId: USERS.sandiford,
      responsibleUserId: HALLWORTH.id,
    }),
  ];

  const notifications: DemoNotification[] = [
    {
      id: id(0xe5, 101),
      userId: STAFF.id,
      kind: 'ResultReady',
      title: 'Result ready',
      message: `${LAB.username} reported a Skin biopsy (punch) specimen.`,
      pathwayId: PATHWAYS.backFromLab,
      subjectId: RESULTS.callumBiopsy,
      createdAtUtc: stage(PATHWAYS.backFromLab, 4).completedAtUtc!,
      isRead: false,
    },
    {
      id: id(0xe5, 102),
      userId: HALLWORTH.id,
      kind: 'ResultReady',
      title: 'Result ready',
      message: 'b.sandiford recorded a result from another service.',
      pathwayId: PATHWAYS.unassigned,
      subjectId: RESULTS.gpReport,
      createdAtUtc: ago(1, 11, 30),
      isRead: false,
    },
    {
      id: id(0xe5, 103),
      userId: STAFF.id,
      kind: 'SpecimenRejectedByLab',
      title: 'Specimen rejected',
      message: 'The lab rejected a sample (Blood sample).',
      pathwayId: PATHWAYS.followUpBooked,
      subjectId: SPECIMENS.ilonaBloodRejected,
      createdAtUtc: ago(3, 9, 0),
      isRead: true,
    },
  ];
  state.notifications.push(...notifications);

  return {
    specimenTypes: [
      {id: SPECIMEN_TYPES.skinBiopsy, name: 'Skin biopsy (punch)', expectedTurnaroundDays: 14, snomedCode: '309066003', isArchived: false},
      {id: SPECIMEN_TYPES.bloodSample, name: 'Blood sample', expectedTurnaroundDays: 5, snomedCode: '119297000', isArchived: false},
      {id: SPECIMEN_TYPES.skinScraping, name: 'Skin scraping', expectedTurnaroundDays: 10, snomedCode: null, isArchived: false},
      {id: SPECIMEN_TYPES.skinSwab, name: 'Skin swab', expectedTurnaroundDays: 4, snomedCode: null, isArchived: true},
    ],
    labTests: [
      {
        id: LAB_TESTS.skinHistology,
        name: 'Skin histology',
        kind: 'Histology',
        snomedCode: '252416005',
        targetDays: 7,
        limitDays: 10,
        atRiskDays: 2,
        steps: [
          {id: id(0xed, 101), name: 'Cut-up', kind: 'Standard', isOptional: false},
          {id: id(0xed, 102), name: 'Processing and embedding', kind: 'Standard', isOptional: false},
          {id: id(0xed, 103), name: 'Sectioning and staining', kind: 'Standard', isOptional: false},
          {id: id(0xed, 104), name: 'Extra stains', kind: 'ExtraTests', isOptional: true},
          {id: id(0xed, 105), name: 'Sent for a second opinion', kind: 'SentForSecondOpinion', isOptional: true},
          {id: id(0xed, 106), name: 'Second opinion back', kind: 'SecondOpinionReturned', isOptional: true},
          {id: id(0xed, 107), name: 'Microscopy and report', kind: 'Standard', isOptional: false},
        ],
        isArchived: false,
      },
      {
        id: LAB_TESTS.fullBloodCount,
        name: 'Full blood count',
        kind: 'Haematology',
        snomedCode: '26604007',
        targetDays: 2,
        limitDays: 3,
        atRiskDays: 1,
        steps: [{id: id(0xed, 111), name: 'Analyser run', kind: 'Standard', isOptional: false}, {id: id(0xed, 112), name: 'Blood film review', kind: 'ExtraTests', isOptional: true}],
        isArchived: false,
      },
      {
        id: LAB_TESTS.fungalScraping,
        name: 'Skin scraping for fungi',
        kind: 'Microbiology',
        snomedCode: null,
        targetDays: 14,
        limitDays: 21,
        atRiskDays: 3,
        steps: [{id: id(0xed, 121), name: 'Microscopy', kind: 'Standard', isOptional: false}, {id: id(0xed, 122), name: 'Culture', kind: 'Standard', isOptional: false}, {id: id(0xed, 123), name: 'Report', kind: 'Standard', isOptional: false}],
        isArchived: false,
      },
      {
        id: LAB_TESTS.liverFunction,
        name: 'Liver function tests',
        kind: 'ClinicalChemistry',
        snomedCode: null,
        targetDays: 2,
        limitDays: 3,
        atRiskDays: 1,
        steps: [{id: id(0xed, 131), name: 'Analyser run', kind: 'Standard', isOptional: false}],
        isArchived: true,
      },
    ],
    specimens,
    results,
    specimenSequence: sequence,
    clockPauses: [],
    referralExtras: {},
  };
}

/**
 * The sample a hospital request brings (MC14 with MC18): the lab's (its team), with no pathway or stage, numbered as
 * its case (one sequence), received by the lab when the request was registered, and taken when the hospital collected
 * it, in the request's lab case. The hospital's report goes back to the hospital (MC20), so it makes no result here.
 */
export function hospitalSample(
  request: Pick<
    DemoHospitalRequest,
    'id' | 'hospitalId' | 'patientId' | 'teamId' | 'specimenTypeId' | 'labTestId' | 'labCaseId' | 'labCaseNumber' | 'collectedAtUtc' | 'registeredAtUtc' | 'registeredByUserId'
  >,
  sampleId: string,
): DemoSpecimen {
  return {
    id: sampleId,
    number: request.labCaseNumber,
    patientId: request.patientId,
    source: 'Hospital',
    requestingTeamId: null,
    hospitalId: request.hospitalId,
    hospitalRequestId: request.id,
    labTestId: request.labTestId,
    // The request's lab case (MC15), opened with it.
    labCaseId: request.labCaseId,
    pathwayId: null,
    teamId: request.teamId,
    stageId: null,
    specimenTypeId: request.specimenTypeId,
    site: null,
    requestNotes: null,
    requestingUserId: request.registeredByUserId,
    status: 'Received',
    recordedAtUtc: request.registeredAtUtc,
    recordedByUserId: request.registeredByUserId,
    takenAtUtc: request.collectedAtUtc ?? request.registeredAtUtc,
    takenByUserId: request.registeredByUserId,
    sentAtUtc: null,
    sentByUserId: null,
    courierReference: null,
    receivedAtUtc: request.registeredAtUtc,
    receivedByUserId: request.registeredByUserId,
    labReference: null,
    reportedAtUtc: null,
    reportedByUserId: null,
    reportAttachmentId: null,
    rejectionReason: null,
    closedDetail: null,
    closedAtUtc: null,
    closedByUserId: null,
    version: 1,
  };
}
