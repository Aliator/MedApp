import type {Schemas} from '../../api/client';
import {dayOfWeek, nextWeekday} from '../../utils/calendarSlots';
import {CARROW, HALLWORTH, STAFF} from '../accounts';
import {id, MEETINGS, PATHWAYS, TEAMS, USERS} from '../ids';
import type {BaseState, DemoPathway} from '../state';
import {daysFrom, nowUtc, today, ukTime} from '../time';

/**
 * The light demo's state for letters, letter templates, the MDT list, consent forms and consent records, with their
 * seed. Records are kept close to the API's DTOs; what depends on who's looking (`can...` flags) is worked out by the
 * handlers (`handlers/letters.ts`, `mdt.ts`, `consent.ts`). Everything is synthetic.
 */

/** A letter's recipient, as stored. */
export interface DemoLetterRecipient {
  id: string;
  kind: 'GpPractice' | 'Patient' | 'Referrer' | 'Other';
  practiceId: string | null;
  name: string | null;
  addressLines: string | null;
  postcode: string | null;
  email: string | null;
  dispatchStatus: 'NotSent' | 'Sent' | 'Failed';
  method: string | null;
  sentOn: string | null;
  sentAtUtc: string | null;
  sentByUserId: string | null;
}

/** A letter, as stored (`LetterResponse` without what depends on the viewer). */
export interface DemoLetter {
  id: string;
  patientId: string;
  pathwayId: string | null;
  /** Its pathway's team, or its referral's (the team walls, `handlers/teamWalls.ts`). */
  teamId: string;
  referralId: string | null;
  templateId: string;
  templateName: string;
  /** `LetterKind` by name. */
  kind: string;
  letterhead: string;
  body: string;
  closing: string;
  largePrint: boolean;
  status: 'Draft' | 'AwaitingApproval' | 'Approved' | 'Withdrawn';
  authorUserId: string;
  approverUserId: string | null;
  createdAt: string;
  lastUpdated: string;
  submittedAtUtc: string | null;
  approvedAtUtc: string | null;
  withdrawnAtUtc: string | null;
  withdrawnByUserId: string | null;
  withdrawnReason: string | null;
  returnComment: string | null;
  returnedAtUtc: string | null;
  returnedByUserId: string | null;
  replacesLetterId: string | null;
  version: number;
  recipients: DemoLetterRecipient[];
}

/** A letter template, as stored (`LetterTemplateResponse` without what's worked out: usage, team name, fields). */
export type DemoLetterTemplate = Omit<Schemas['LetterTemplateResponse'], 'canBeDeleted' | 'teamName' | 'mergeFields'> & {
  id: string;
  name: string;
  kind: string;
  teamId: string | null;
  letterhead: string;
  bodySkeleton: string;
  closing: string;
  isArchived: boolean;
  version: number;
};

/** An item on the MDT list, as stored. */
export interface DemoMdtItem {
  id: string;
  patientId: string;
  pathwayId: string;
  teamId: string;
  /** The MDT it's on, or null when its meeting was cancelled. */
  meetingId: string | null;
  /** When its meeting starts (kept, so a cancelled one can say when it was). */
  meetingStartUtc: string | null;
  question: string;
  status: 'Listed' | 'Discussed' | 'Deferred' | 'Withdrawn';
  listedByUserId: string;
  listedAtUtc: string;
  outcome: string | null;
  outcomeByUserId: string | null;
  outcomeAtUtc: string | null;
  presentUserIds: string[];
  withdrawnByUserId: string | null;
  withdrawnAtUtc: string | null;
  version: number;
}

/** A consent form (`ConsentFormResponse` without `canBeDeleted`, which is worked out). */
export interface DemoConsentForm {
  id: string;
  name: string;
  formVersion: string;
  isArchived: boolean;
  createdAt: string;
  lastUpdated: string;
}

/** A consent record, as stored. MedApp records the clinician's statement; it never assesses capacity or validity. */
export interface DemoConsentRecord {
  id: string;
  patientId: string;
  pathwayId: string;
  /** Its pathway's team. */
  teamId: string;
  stageId: string;
  consentFormId: string;
  formName: string;
  formVersion: string;
  signedOn: string;
  /** `ConsentSignedBy` by name. */
  signedBy: 'Patient' | 'PersonWithParentalResponsibility' | 'BestInterestsDecision';
  recordedByUserId: string;
  recordedAtUtc: string;
  status: 'Valid' | 'Withdrawn' | 'EnteredInError';
  withdrawnOn: string | null;
  statusDetail: string | null;
  statusChangedByUserId: string | null;
  statusChangedAtUtc: string | null;
  version: number;
}

export interface ClinicalSlice {
  letterTemplates: DemoLetterTemplate[];
  letters: DemoLetter[];
  mdtItems: DemoMdtItem[];
  consentForms: DemoConsentForm[];
  consentRecords: DemoConsentRecord[];
}

/** Letter templates (kind 0xe6). */
export const LETTER_TEMPLATES = {
  /** A clinic letter, for every team. */
  clinicLetter: id(0xe6, 1),
  /** A discharge summary to the GP, for every team: what the "Discharge letter to GP" stage needs. */
  dischargeSummary: id(0xe6, 2),
  /** A reply to a Dermatology referral. */
  referralResponse: id(0xe6, 3),
} as const;

/** Letters (kind 0xe7; their recipients are 0xe8). */
export const LETTERS = {
  /** Ellis Varden's clinic letter: Nadia Thornbury's draft. */
  draft: id(0xe7, 1),
  /** Priya Holloway's clinic letter, by Tom Hallworth: awaiting Nadia Thornbury's approval. */
  awaitingApproval: id(0xe7, 2),
  /** Margaret Fenwick's clinic letter, approved by the admin: still to send to both recipients. */
  toSend: id(0xe7, 3),
  /** Grace Abernethy's discharge summary: approved and sent to everyone (her pathway is discharged). */
  sent: id(0xe7, 4),
} as const;

/** The MDT added to the base seed's (kind 0xe9; `MEETINGS.skinMdt` is made a Dermatology MDT too). */
export const MDT_MEETINGS = {
  /** A Dermatology MDT that has started (this morning, or the last weekday's), so outcomes can be recorded. */
  today: id(0xe9, 1),
} as const;

/** MDT items (kind 0xea). */
export const MDT_ITEMS = {
  /** Ellis Varden's pathway on today's MDT, still to discuss: the tour records its outcome. */
  toDiscuss: id(0xea, 1),
  /** Ilona Petrova's pathway on today's MDT, discussed: the outcome as written and who was present. */
  discussed: id(0xea, 2),
  /** Priya Holloway's pathway on the skin MDT ahead. */
  priyaListed: id(0xea, 3),
  /** Felix Moreton's pathway on the skin MDT ahead. */
  felixListed: id(0xea, 4),
} as const;

/** Consent forms (kind 0xeb, numbers below 0x100) and consent records (kind 0xeb, from 0x101). */
export const CONSENT = {
  /** The adult procedure form in use now: the one to choose when recording consent. */
  adultForm: id(0xeb, 1),
  /** Its older version: archived, as consent was recorded on it. */
  adultFormOld: id(0xeb, 2),
  /** A best interests form, never used (so it can be deleted). */
  bestInterestsForm: id(0xeb, 3),
  /** Priya Holloway's consent to treatment, taken ahead of the stage: Valid. */
  priyaValid: id(0xeb, 0x101),
  /** Ellis Varden's consent to treatment, withdrawn by the patient. */
  ellisWithdrawn: id(0xeb, 0x102),
} as const;

/** The IDs the Read Me's tour can open (all seeded below). */
export const CLINICAL_IDS = {
  /** The Dermatology MDT that has started (open `/mdt?meeting=` with it). */
  mdtToday: MDT_MEETINGS.today,
  /** The MDT ahead (the base seed's skin cancer team meeting, made a Dermatology MDT). */
  mdtAhead: MEETINGS.skinMdt,
  /** Ellis Varden's item on today's MDT, to record an outcome on (n.thornbury is assigned: the admin recording it tells her). */
  mdtItemToDiscuss: MDT_ITEMS.toDiscuss,
  /** The consent form to choose for Harriet Quill's Treatment stage. */
  consentFormToChoose: CONSENT.adultForm,
  /** n.thornbury's letter to approve. */
  letterToApprove: LETTERS.awaitingApproval,
  /** The admin's approved letter, to record as sent. */
  letterToSend: LETTERS.toSend,
  /** n.thornbury's draft. */
  letterDraft: LETTERS.draft,
} as const;

/** A letter's recipient's fixed ID: the letter's number, then its position. */
const recipientId = (letter: number, position: number) => id(0xe8, letter * 0x10 + position);

/** A moment `days` before today at a UK time of day. */
const ago = (days: number, hours = 10, minutes = 0) => ukTime(daysFrom(-days), hours, minutes);

const LETTERHEAD = 'Community Specialist Centre (synthetic)\n1 Example Way\nExampleton\nZZ9 9ZZ';
const CLOSING = 'Yours sincerely,\n\n\n{{author.name}}\n{{author.jobTitle}}';
const RE_LINE = '**Re: {{patient.name}}, born {{patient.dateOfBirth}}, NHS number {{patient.nhsNumber}}**';

const TEMPLATE_SPECS: [string, string, string, string | null, string][] = [
  [LETTER_TEMPLATES.clinicLetter, 'Clinic letter', 'ClinicLetter', null, `Dear colleague,\n\n${RE_LINE}\n\n## Diagnosis\n\n## What we found\n\n## Plan`],
  [
    LETTER_TEMPLATES.dischargeSummary,
    'Discharge summary to GP',
    'DischargeSummary',
    null,
    `Dear colleague,\n\n${RE_LINE}\n\nWe have discharged {{patient.name}} from our care.\n\n## Summary of care\n\n## For the GP`,
  ],
  [
    LETTER_TEMPLATES.referralResponse,
    'Referral response',
    'ReferralResponse',
    TEAMS.dermatology,
    `Dear colleague,\n\n${RE_LINE}\n\nThank you for your referral, received on {{referral.receivedDate}}.\n\n## Our decision`,
  ],
];

/** The GP practice copied onto seeded letters (synthetic; the demo's directory has no practices). */
const GP = {name: 'Example Lane Surgery (synthetic)', addressLines: '1 Example Lane\nExampleton', postcode: 'ZZ1 1ZZ'};

/**
 * The day today's MDT is on (08:00 to 08:45 UK time): today if that has already started (a weekday after 09:00), else
 * the last weekday, so its outcomes can always be recorded.
 */
function mdtTodayDate(): string {
  const on = today();
  return dayOfWeek(on) <= 5 && nowUtc() >= ukTime(on, 9, 0) ? on : nextWeekday(on, -1);
}

/** Seeds this slice, after the base seed: letters, the MDTs and their lists, consent forms and records. */
export function seedClinical(state: BaseState): ClinicalSlice {
  const pathway = (pathwayId: string) => state.pathways.find((p) => p.id === pathwayId)!;
  /** The pathway's stage of this name. */
  const stage = (p: DemoPathway, name: string) => p.stages.find((s) => s.name === name)!;

  const letterTemplates: DemoLetterTemplate[] = TEMPLATE_SPECS.map(([templateId, name, kind, teamId, bodySkeleton]) => ({
    id: templateId,
    name,
    kind,
    teamId,
    letterhead: LETTERHEAD,
    bodySkeleton,
    closing: CLOSING,
    isArchived: false,
    version: 1,
    createdAt: ago(90),
    lastUpdated: ago(90),
  }));
  for (const team of state.teams)
    team.letterTemplateCount = letterTemplates.filter((t) => t.teamId === team.id).length;

  const dispatch = (sent?: {daysAgo: number}) =>
    sent
      ? {dispatchStatus: 'Sent' as const, method: 'Post', sentOn: daysFrom(-sent.daysAgo), sentAtUtc: ago(sent.daysAgo, 16, 0), sentByUserId: USERS.marsh}
      : {dispatchStatus: 'NotSent' as const, method: null, sentOn: null, sentAtUtc: null, sentByUserId: null};
  const patientRecipient = (n: number, position: number, pathwayId: string, sent?: {daysAgo: number}): DemoLetterRecipient => {
    const patient = state.patients.find((p) => p.id === pathway(pathwayId).patientId)!;
    return {
      id: recipientId(n, position),
      kind: 'Patient',
      practiceId: null,
      name: `${patient.firstName} ${patient.lastName}`,
      addressLines: (patient.addressLines ?? []).join('\n'),
      postcode: patient.postcode ?? null,
      email: null,
      ...dispatch(sent),
    };
  };
  const gpRecipient = (n: number, position: number, sent?: {daysAgo: number}): DemoLetterRecipient => ({
    id: recipientId(n, position),
    kind: 'GpPractice',
    practiceId: null,
    ...GP,
    email: null,
    ...dispatch(sent),
  });
  const letter = (
    n: number,
    pathwayId: string,
    templateId: string,
    body: string,
    rest: Partial<DemoLetter> & Pick<DemoLetter, 'status' | 'authorUserId' | 'createdAt'>,
    sent?: {daysAgo: number},
  ): DemoLetter => {
    const template = letterTemplates.find((t) => t.id === templateId)!;
    return {
      id: id(0xe7, n),
      patientId: pathway(pathwayId).patientId,
      pathwayId,
      teamId: pathway(pathwayId).teamId,
      referralId: null,
      templateId,
      templateName: template.name,
      kind: template.kind,
      letterhead: template.letterhead,
      body,
      closing: template.closing,
      largePrint: false,
      approverUserId: null,
      lastUpdated: rest.createdAt,
      submittedAtUtc: null,
      approvedAtUtc: null,
      withdrawnAtUtc: null,
      withdrawnByUserId: null,
      withdrawnReason: null,
      returnComment: null,
      returnedAtUtc: null,
      returnedByUserId: null,
      replacesLetterId: null,
      version: 1,
      recipients: [gpRecipient(n, 0, sent), patientRecipient(n, 1, pathwayId, sent)],
      ...rest,
    };
  };
  const clinicBody = (diagnosis: string, found: string, plan: string) =>
    `Dear colleague,\n\n${RE_LINE}\n\n## Diagnosis\n\n${diagnosis}\n\n## What we found\n\n${found}\n\n## Plan\n\n${plan}`;

  const letters: DemoLetter[] = [
    letter(
      1,
      PATHWAYS.biopsyToBook,
      LETTER_TEMPLATES.clinicLetter,
      clinicBody('Changing pigmented lesion, left forearm.', 'An irregular lesion of about 8 mm.', ''),
      {
        status: 'Draft',
        authorUserId: STAFF.id,
        createdAt: ago(9, 14, 0),
        lastUpdated: ago(9, 14, 20),
        version: 2,
        // The draft's editor chooses a GP practice from the directory, which is empty here: the GP is typed in.
        recipients: [{...gpRecipient(1, 0), kind: 'Other'}, patientRecipient(1, 1, PATHWAYS.biopsyToBook)],
      },
    ),
    letter(
      2,
      PATHWAYS.histologyToFinish,
      LETTER_TEMPLATES.clinicLetter,
      clinicBody('Pigmented lesion, upper back.', 'A biopsy was taken; histology is awaited.', 'We will write again with the result and the plan.'),
      {
        status: 'AwaitingApproval',
        authorUserId: USERS.hallworth,
        approverUserId: STAFF.id,
        createdAt: ago(2, 11, 0),
        lastUpdated: ago(1, 15, 0),
        submittedAtUtc: ago(1, 15, 0),
        version: 3,
      },
    ),
    letter(
      3,
      PATHWAYS.overdue,
      LETTER_TEMPLATES.clinicLetter,
      clinicBody('Post-menopausal bleeding, under investigation.', 'Referred for a first clinic visit.', 'Her first visit is booked.'),
      {
        status: 'Approved',
        authorUserId: HALLWORTH.id,
        approverUserId: HALLWORTH.id,
        createdAt: ago(3, 10, 0),
        lastUpdated: ago(1, 10, 30),
        approvedAtUtc: ago(1, 10, 30),
        version: 3,
      },
    ),
    letter(
      4,
      PATHWAYS.completed,
      LETTER_TEMPLATES.dischargeSummary,
      `Dear colleague,\n\n${RE_LINE}\n\nWe have discharged {{patient.name}} from our care.\n\n## Summary of care\n\nDiabetes reviewed after a change of medicine.\n\n## For the GP\n\nPlease continue the current medicines and review annually.`,
      {
        status: 'Approved',
        authorUserId: CARROW.id,
        approverUserId: CARROW.id,
        createdAt: ago(4, 10, 0),
        lastUpdated: ago(3, 16, 0),
        approvedAtUtc: ago(3, 12, 0),
        version: 5,
      },
      {daysAgo: 3},
    ),
  ];
  state.notifications.push({
    id: id(0xe7, 0x101),
    userId: STAFF.id,
    kind: 'LetterAwaitingApproval',
    title: 'Letter to approve',
    message: 't.hallworth asked you to approve a clinic letter.',
    subjectId: LETTERS.awaitingApproval,
    createdAtUtc: ago(1, 15, 0),
    isRead: false,
  });

  // The MDTs: the base seed's skin cancer team meeting ahead, and one that has started.
  const ahead = state.meetings.find((m) => m.id === MEETINGS.skinMdt);
  if (ahead) Object.assign(ahead, {isMdt: true, teamId: TEAMS.dermatology});
  const todayDate = mdtTodayDate();
  const todayStart = ukTime(todayDate, 8, 0);
  state.meetings.push({
    id: MDT_MEETINGS.today,
    title: 'Skin MDT (morning)',
    notes: 'Dermatology multidisciplinary team meeting.',
    date: todayDate,
    startUtc: todayStart,
    endUtc: ukTime(todayDate, 8, 45),
    organiserUserId: USERS.whitlock,
    organiserUsername: 'j.whitlock',
    attendees: [
      {userId: STAFF.id, username: STAFF.username, reply: 'Accepted', repliedAtUtc: ago(6, 9, 30)},
      {userId: USERS.sandiford, username: 'b.sandiford', reply: 'Accepted', repliedAtUtc: ago(6, 10, 0)},
      {userId: USERS.hallworth, username: 't.hallworth', reply: 'NoReply', repliedAtUtc: null},
    ],
    createdAtUtc: ago(7, 9, 0),
    lastUpdated: ago(6, 10, 0),
    isMdt: true,
    teamId: TEAMS.dermatology,
  });

  const item = (
    itemId: string,
    pathwayId: string,
    meeting: {id: string; startUtc: string},
    question: string,
    listedDaysAgo: number,
    discussed?: {outcome: string; present: string[]; atUtc: string},
  ): DemoMdtItem => ({
    id: itemId,
    patientId: pathway(pathwayId).patientId,
    pathwayId,
    teamId: TEAMS.dermatology,
    meetingId: meeting.id,
    meetingStartUtc: meeting.startUtc,
    question,
    status: discussed ? 'Discussed' : 'Listed',
    listedByUserId: STAFF.id,
    listedAtUtc: ago(listedDaysAgo, 12, 0),
    outcome: discussed?.outcome ?? null,
    outcomeByUserId: discussed ? STAFF.id : null,
    outcomeAtUtc: discussed?.atUtc ?? null,
    presentUserIds: discussed?.present ?? [],
    withdrawnByUserId: null,
    withdrawnAtUtc: null,
    version: discussed ? 2 : 1,
  });
  const todayMeeting = {id: MDT_MEETINGS.today, startUtc: todayStart};
  const aheadMeeting = {id: MEETINGS.skinMdt, startUtc: ahead?.startUtc ?? todayStart};
  const mdtItems: DemoMdtItem[] = [
    item(MDT_ITEMS.toDiscuss, PATHWAYS.biopsyToBook, todayMeeting, 'Changing mole on the forearm: biopsy as planned, or excise straight away?', 5),
    item(MDT_ITEMS.discussed, PATHWAYS.followUpBooked, todayMeeting, 'Psoriasis on the new treatment: carry on, or change it?', 6, {
      outcome: 'Continue the current treatment. Review at the booked follow-up.',
      present: [STAFF.id, USERS.sandiford],
      atUtc: ukTime(todayDate, 8, 20),
    }),
    item(MDT_ITEMS.priyaListed, PATHWAYS.histologyToFinish, aheadMeeting, 'Histology due this week: agree the treatment plan.', 2),
    item(MDT_ITEMS.felixListed, PATHWAYS.branchToChoose, aheadMeeting, 'Suspicious lesion on the shin: biopsy first, or excise directly?', 1),
  ];

  const consentForms: DemoConsentForm[] = [
    {id: CONSENT.adultForm, name: 'Consent to a procedure (adult)', formVersion: '3.2', isArchived: false, createdAt: ago(60), lastUpdated: ago(60)},
    {id: CONSENT.adultFormOld, name: 'Consent to a procedure (adult)', formVersion: '3.1', isArchived: true, createdAt: ago(200), lastUpdated: ago(60)},
    {
      id: CONSENT.bestInterestsForm,
      name: 'Treatment in the patient’s best interests (lacks capacity)',
      formVersion: '1.0',
      isArchived: false,
      createdAt: ago(60),
      lastUpdated: ago(60),
    },
  ];
  const priya = pathway(PATHWAYS.histologyToFinish);
  const ellis = pathway(PATHWAYS.biopsyToBook);
  const consentRecords: DemoConsentRecord[] = [
    {
      id: CONSENT.priyaValid,
      patientId: priya.patientId,
      pathwayId: priya.id,
      teamId: priya.teamId,
      stageId: stage(priya, 'Treatment').id,
      consentFormId: CONSENT.adultForm,
      formName: consentForms[0].name,
      formVersion: consentForms[0].formVersion,
      signedOn: daysFrom(-2),
      signedBy: 'Patient',
      recordedByUserId: STAFF.id,
      recordedAtUtc: ago(2, 11, 30),
      status: 'Valid',
      withdrawnOn: null,
      statusDetail: null,
      statusChangedByUserId: null,
      statusChangedAtUtc: null,
      version: 1,
    },
    {
      id: CONSENT.ellisWithdrawn,
      patientId: ellis.patientId,
      pathwayId: ellis.id,
      teamId: ellis.teamId,
      stageId: stage(ellis, 'Treatment').id,
      consentFormId: CONSENT.adultFormOld,
      formName: consentForms[1].name,
      formVersion: consentForms[1].formVersion,
      signedOn: daysFrom(-10),
      signedBy: 'Patient',
      recordedByUserId: STAFF.id,
      recordedAtUtc: ago(10, 10, 0),
      status: 'Withdrawn',
      withdrawnOn: daysFrom(-4),
      statusDetail: 'The patient would like to talk it over at home first.',
      statusChangedByUserId: STAFF.id,
      statusChangedAtUtc: ago(4, 14, 0),
      version: 2,
    },
  ];

  return {letterTemplates, letters, mdtItems, consentForms, consentRecords};
}
