import type {Schemas} from '../api/client';
import {dayOfWeek, nextWeekday} from '../utils/calendarSlots';
import {ADMIN, STAFF} from './accounts';
import {
  ACTIVITY_TYPES,
  APPOINTMENTS,
  MEETINGS,
  MORE_APPOINTMENTS,
  PATHWAYS,
  PATIENTS,
  REFERRALS,
  ROOMS,
  SITES,
  TEAMS,
  STAGES,
  TEMPLATES,
  USERS,
} from './ids';
import {seedClinical} from './slices/clinical';
import {seedLab} from './slices/lab';
import {seedSetup} from './slices/setup';
import type {BaseState, DemoAppointment, DemoPathway, DemoPathwayAccess, DemoReferral, DemoStage, DemoStageDefinition, DemoState} from './state';
import {daysFrom, thisWeek, today, ukTime} from './time';

/**
 * The light demo's seed: a small TypeScript copy of part of the demo clinic (`tools/MedApp.DevMcp/DemoData`), with
 * fixed IDs (`ids.ts`) and dates relative to the day it's seeded, so the calendar always has this week's events. The C#
 * seed can't be exported for it: it runs the API's commands against SQL Server, and GitHub Pages builds in CI with
 * neither. Everything here is synthetic: no real people, practices or NHS numbers (only the 999 range, never issued).
 */

/** Bump when the seed's shape changes, so a visitor's older copy is seeded again. */
export const SEED_VERSION = 5;

/** Activity types for calendar blocks (`ActivityTypeResponse`). */
export const ACTIVITY_TYPE_LIST: Schemas['ActivityTypeResponse'][] = [
  {id: ACTIVITY_TYPES.clinic, name: 'Clinic', colour: 'blue', isArchived: false, canBeDeleted: false},
  {id: ACTIVITY_TYPES.admin, name: 'Admin time', colour: 'gray', isArchived: false, canBeDeleted: false},
  {id: ACTIVITY_TYPES.lunch, name: 'Lunch', colour: 'green', isArchived: false, canBeDeleted: false},
  {id: ACTIVITY_TYPES.triage, name: 'Triage', colour: 'orange', isArchived: false, canBeDeleted: false},
  {id: ACTIVITY_TYPES.minorSurgery, name: 'Minor surgery', colour: 'purple', isArchived: false, canBeDeleted: false},
];

export const SITE = {id: SITES.centre, name: 'Community Specialist Centre (synthetic)'};
export const ROOM_LIST = [
  {id: ROOMS.clinicRoom1, name: 'Clinic room 1'},
  {id: ROOMS.procedureRoom, name: 'Procedure room'},
];

/**
 * The stage library: each definition's name, what it involves, whether it needs a visit, and its gates (a letter of a
 * kind sent, a consent record, the results signed off), as the full demo clinic has them.
 */
const LIBRARY: Record<
  keyof typeof STAGES,
  {name: string; description: string; visit: DemoStage['visitRequirement']; letter?: string; consent?: boolean; results?: boolean}
> = {
  firstClinicVisit: {name: 'First clinic visit', description: 'See the patient in clinic and examine them.', visit: 'Required'},
  biopsyOrSample: {name: 'Biopsy or sample', description: 'Take a biopsy or sample for the lab.', visit: 'Required'},
  histology: {name: 'Histology (lab)', description: 'The lab reports on the sample.', visit: 'None'},
  resultsAndTeamReview: {
    name: 'Results and team review',
    description: 'Review the results, with the team if needed.',
    visit: 'Suggested',
    results: true,
  },
  treatment: {name: 'Treatment', description: 'Treat the lesion: excision or other treatment.', visit: 'Required', consent: true},
  followUp: {name: 'Follow-up', description: 'Check how the patient is doing.', visit: 'Suggested'},
  dischargeLetter: {
    name: 'Discharge letter to GP',
    description: "Write to the patient's GP and discharge.",
    visit: 'None',
    letter: 'DischargeSummary',
  },
  medicinesReview: {name: 'Medicines review', description: 'Review the patient’s medicines with the pharmacist.', visit: 'Suggested'},
  bloodAndUrineTests: {name: 'Blood and urine tests', description: 'Take blood and urine samples.', visit: 'Required'},
  labTests: {name: 'Lab tests (lab)', description: 'The lab runs the tests.', visit: 'None', results: true},
  clinicReview: {name: 'Clinic review', description: 'Review the results with the patient in clinic.', visit: 'Required'},
  excision: {
    name: 'Excision',
    description: 'Remove the lesion in the minor surgery list, without a biopsy first.',
    visit: 'Required',
    consent: true,
  },
};

type StageKey = keyof typeof STAGES;

/** The stage library as the state keeps it. */
export const stageDefinitionList = (): DemoStageDefinition[] =>
  (Object.keys(STAGES) as StageKey[]).map((key) => ({
    id: STAGES[key],
    name: LIBRARY[key].name,
    description: LIBRARY[key].description,
    instructions: null,
    snomedCode: null,
    snomedTerm: null,
    visitRequirement: LIBRARY[key].visit,
    needsLetterKind: LIBRARY[key].letter ?? null,
    needsConsent: LIBRARY[key].consent ?? false,
    waitsForResults: LIBRARY[key].results ?? false,
    isArchived: false,
  }));

/** A template's stage: its definition, and whether it's optional, a branch point or in a branch. */
type TemplateStageSpec = StageKey | {key: StageKey; optional?: boolean; choosesBranch?: boolean; branch?: string};

const TEMPLATE_SPECS: {id: string; name: string; teamId: string; stages: TemplateStageSpec[]}[] = [
  {
    id: TEMPLATES.skinLesion,
    name: 'Skin lesion pathway',
    teamId: TEAMS.dermatology,
    stages: ['firstClinicVisit', 'biopsyOrSample', 'histology', 'resultsAndTeamReview', 'treatment', 'followUp', 'dischargeLetter'],
  },
  {
    id: TEMPLATES.inflammatorySkin,
    name: 'Inflammatory skin review',
    teamId: TEAMS.dermatology,
    stages: ['firstClinicVisit', 'medicinesReview', 'followUp', 'dischargeLetter'],
  },
  {
    id: TEMPLATES.postMenopausalBleeding,
    name: 'Post-menopausal bleeding pathway',
    teamId: TEAMS.gynaecology,
    stages: ['firstClinicVisit', 'biopsyOrSample', 'histology', 'resultsAndTeamReview', 'dischargeLetter'],
  },
  {
    id: TEMPLATES.diabetesReview,
    name: 'Diabetes review',
    teamId: TEAMS.diabetes,
    stages: ['bloodAndUrineTests', 'labTests', 'clinicReview', 'medicinesReview', 'followUp', 'dischargeLetter'],
  },
  {
    id: TEMPLATES.suspectedSkinCancer,
    name: 'Suspected skin cancer (biopsy or treat)',
    teamId: TEAMS.dermatology,
    stages: [
      {key: 'firstClinicVisit', choosesBranch: true},
      {key: 'biopsyOrSample', branch: 'Biopsy first'},
      {key: 'histology', branch: 'Biopsy first'},
      {key: 'excision', branch: 'Treat directly'},
      {key: 'followUp', optional: true},
      'dischargeLetter',
    ],
  },
];

const stageSpec = (spec: TemplateStageSpec) => (typeof spec === 'string' ? {key: spec} : spec);

const TEAM_NAMES: [string, string][] = [
  [TEAMS.dermatology, 'Dermatology'],
  [TEAMS.gynaecology, 'Gynaecology'],
  [TEAMS.urology, 'Urology'],
  [TEAMS.diabetes, 'Diabetes'],
  [TEAMS.biomechanics, 'Biomechanics'],
  // The lab (decision 182): a team of its own kind, serving every clinic.
  [TEAMS.lab, 'Lab'],
];

export const teamName = (id: string | null | undefined) => TEAM_NAMES.find(([key]) => key === id)?.[1] ?? null;

/**
 * A template's stages, in order, for a new pathway made from it: the template's options (optional, branches) and the
 * definition's gates, copied onto each stage as the API's `PathwayCreation` does.
 */
export function templateStages(
  template: Schemas['PathwayTemplateResponse'],
  definitions: DemoStageDefinition[],
): Omit<DemoStage, 'id'>[] {
  return (template.stages ?? []).map((stage) => {
    const definition = definitions.find((d) => d.id === stage.stageDefinitionId);
    return {
      stageDefinitionId: stage.stageDefinitionId ?? '',
      stageOrder: stage.stageOrder ?? 0,
      name: stage.name ?? '',
      description: stage.description ?? null,
      visitRequirement: (stage.visitRequirement as DemoStage['visitRequirement']) ?? 'None',
      needsLetterKind: definition?.needsLetterKind ?? null,
      needsConsent: definition?.needsConsent ?? false,
      waitsForResults: definition?.waitsForResults ?? false,
      isOptional: stage.isOptional ?? false,
      choosesBranch: stage.choosesBranch ?? false,
      branch: stage.branch ?? null,
      chosenBranch: null,
      unlocked: false,
      notes: null,
      startedAtUtc: null,
      startedByUserId: null,
      completedAtUtc: null,
      completedByUserId: null,
      skippedAtUtc: null,
      skippedByUserId: null,
      skipReason: null,
      skipDetail: null,
    };
  });
}

/** The patients: synthetic names, dates of birth and 999-range NHS numbers (each with a valid check digit). */
const PATIENT_SPECS: [string, string, string, string][] = [
  [PATIENTS.ellisVarden, 'Ellis', 'Varden', '1958-03-14'],
  [PATIENTS.priyaHolloway, 'Priya', 'Holloway', '1971-09-02'],
  [PATIENTS.samuelOkafor, 'Samuel', 'Okafor', '1989-12-21'],
  [PATIENTS.margaretFenwick, 'Margaret', 'Fenwick', '1952-06-30'],
  [PATIENTS.daniyalRees, 'Daniyal', 'Rees', '1966-01-17'],
  [PATIENTS.graceAbernethy, 'Grace', 'Abernethy', '1979-04-08'],
  [PATIENTS.tobiasLund, 'Tobias', 'Lund', '1994-11-25'],
  [PATIENTS.ilonaPetrova, 'Ilona', 'Petrova', '1983-07-12'],
  [PATIENTS.oliverMaddox, 'Oliver', 'Maddox', '1961-02-03'],
  [PATIENTS.aishaKhoury, 'Aisha', 'Khoury', '1975-10-19'],
  [PATIENTS.ruthPenrose, 'Ruth', 'Penrose', '1956-08-27'],
  [PATIENTS.callumAshby, 'Callum', 'Ashby', '1970-05-05'],
  [PATIENTS.harrietQuill, 'Harriet', 'Quill', '1949-01-22'],
  [PATIENTS.felixMoreton, 'Felix', 'Moreton', '1968-03-09'],
];

/** NHS numbers from the 999 range only (never issued to real people), each with a valid check digit. */
export const NHS_NUMBERS = [
  '9991001379',
  '9991004114',
  '9991006850',
  '9991008225',
  '9991009590',
  '9991010963',
  '9991012338',
  '9991013709',
  '9991015078',
  '9991016449',
  '9991019189',
  '9991020551',
  '9991022007',
  '9991023372',
];

/** A stage's ID within a seeded pathway: fixed, as everything in the seed is. */
const stageId = (pathway: number, order: number) => `000000dc-0000-4000-8000-${(pathway * 100 + order).toString(16).padStart(12, '0')}`;

/** A moment `days` before today at a UK time of day. */
const ago = (days: number, hours = 10, minutes = 0) => ukTime(daysFrom(-days), hours, minutes);

interface PathwaySpec {
  n: number;
  id: string;
  patientId: string;
  templateId: string;
  title: string;
  /** The target date, in days from today (negative is past: overdue). */
  dueIn: number;
  priority: DemoPathway['priority'];
  /** How many stages are finished. */
  done: number;
  /** Whether the next stage has been started. */
  started?: boolean;
  status: DemoPathway['status'];
  blockedFrom?: DemoPathway['status'];
  assignee?: string;
  createdDaysAgo: number;
  referralId?: string;
  notes?: {authorUserId: string; text: string; daysAgo: number}[];
}

function makePathway(spec: PathwaySpec, templates: Schemas['PathwayTemplateResponse'][], definitions: DemoStageDefinition[]): DemoPathway {
  const template = templates.find((t) => t.id === spec.templateId)!;
  const created = ago(spec.createdDaysAgo, 9, 15);
  const doer = spec.assignee ?? ADMIN.id;
  // Finished stages are spread between the pathway's start and a couple of days ago.
  const step = Math.max(1, Math.floor((spec.createdDaysAgo - 2) / Math.max(1, spec.done)));
  const stages = templateStages(template, definitions).map((stage, i): DemoStage => {
    const finishedDaysAgo = spec.createdDaysAgo - step * (i + 1);
    const isDone = i < spec.done;
    const isStarted = isDone || (i === spec.done && spec.started === true);
    return {
      ...stage,
      id: stageId(spec.n, i + 1),
      notes: isDone ? 'Done as planned.' : null,
      // A finished stage was started a day before it finished; the current one just after the one before it finished.
      startedAtUtc: isStarted ? (isDone ? ago(Math.max(1, finishedDaysAgo + 1), 9, 30) : ago(Math.max(1, finishedDaysAgo + step), 15, 30)) : null,
      startedByUserId: isStarted ? doer : null,
      completedAtUtc: isDone ? ago(Math.max(1, finishedDaysAgo), 15, 0) : null,
      completedByUserId: isDone ? doer : null,
    };
  });
  return {
    id: spec.id,
    patientId: spec.patientId,
    templateId: template.id!,
    templateName: template.name!,
    teamId: template.teamId!,
    referralId: spec.referralId ?? null,
    title: spec.title,
    dueDate: daysFrom(spec.dueIn),
    priority: spec.priority,
    status: spec.status,
    blockedFromStatus: spec.blockedFrom ?? null,
    createdAt: created,
    lastUpdated: ago(1, 16, 0),
    stages,
    assignments: spec.assignee ? [{userId: spec.assignee, assignedByUserId: ADMIN.id, assignedAtUtc: ago(spec.createdDaysAgo, 9, 20)}] : [],
    notes: (spec.notes ?? []).map((note, i) => ({
      id: `000000dd-0000-4000-8000-${(spec.n * 100 + i + 1).toString(16).padStart(12, '0')}`,
      authorUserId: note.authorUserId,
      text: note.text,
      createdAtUtc: ago(note.daysAgo, 11, 5),
      lastEditedAtUtc: null,
      version: 1,
      deleted: null,
      pendingRequestId: null,
    })),
    statusChanges:
      spec.status === 'Blocked'
        ? [{atUtc: ago(3, 14, 10), userId: doer, from: spec.blockedFrom ?? 'InProgress', to: 'Blocked'}]
        : spec.status === 'Completed'
          ? [{atUtc: ago(2, 16, 30), userId: doer, from: 'InReview', to: 'Completed'}]
          : [],
  };
}

/** The waiting-time target for a referral's priority, in days. */
export const TARGET_DAYS: Record<DemoReferral['requestedPriority'], number> = {Urgent: 14, High: 28, Medium: 63, Low: 126};

function makeReferral(
  id: string,
  patientId: string,
  teamId: string,
  receivedDaysAgo: number,
  requestedPriority: DemoReferral['requestedPriority'],
  reason: string,
  accepted?: {pathwayId: string; daysAgo: number; templateName: string},
): DemoReferral {
  const registered = ago(receivedDaysAgo - 1 < 0 ? 0 : receivedDaysAgo - 1, 11, 0);
  const history: Schemas['ReferralEventResponse'][] = [
    {
      id: id.replace('000000d6', '000000de'),
      action: 'Registered',
      atUtc: registered,
      username: 'h.marsh',
      toStatus: 'AwaitingTriage',
      teamName: teamName(teamId),
      requestedPriority,
    },
  ];
  if (accepted)
    history.push({
      id: id.replace('000000d6', '000000ef'),
      action: 'Accepted',
      atUtc: ago(accepted.daysAgo, 10, 0),
      username: STAFF.username,
      toStatus: 'Accepted',
      templateName: accepted.templateName,
      pathwayId: accepted.pathwayId,
      priority: requestedPriority,
    });
  return {
    id,
    patientId,
    teamId,
    receivedOn: daysFrom(-receivedDaysAgo),
    referrerPractice: 'Example Lane Surgery (synthetic)',
    referrerClinician: 'Dr A. Sample',
    requestedPriority,
    reason,
    status: accepted ? 'Accepted' : 'AwaitingTriage',
    registeredAtUtc: registered,
    registeredByUserId: USERS.marsh,
    decidedAtUtc: accepted ? ago(accepted.daysAgo, 10, 0) : null,
    decidedByUserId: accepted ? STAFF.id : null,
    rejectionReason: null,
    decisionDetail: null,
    redirectedTo: null,
    pathwayId: accepted?.pathwayId ?? null,
    version: accepted ? 2 : 1,
    lastUpdated: registered,
    history,
  };
}

/** The n-th weekday after today (1 is the next one), so a booking is always ahead. */
const weekdayAhead = (n: number) => {
  let date = today();
  for (let i = 0; i < n; i++) date = nextWeekday(date, 1);
  return date;
};

/** About `n` days ago, moved back to a weekday (clinics don't run at weekends). */
const weekdayBack = (n: number) => {
  let date = daysFrom(-n);
  while (dayOfWeek(date) > 5) date = nextWeekday(date, -1);
  return date;
};

/** A pathway's own team's grant to see it (the team walls), from when it was made. */
export const ownerAccess = (pathway: DemoPathway): DemoPathwayAccess => ({
  pathwayId: pathway.id,
  patientId: pathway.patientId,
  teamId: pathway.teamId,
  reason: 'Owner',
  startedAtUtc: pathway.createdAt,
  endedAtUtc: null,
});

/** Builds the light demo's data as of today. Everyone starts signed out. */
export function seed(): DemoState {
  const on = today();
  const stageDefinitions = stageDefinitionList();
  const templates: Schemas['PathwayTemplateResponse'][] = TEMPLATE_SPECS.map((spec) => ({
    id: spec.id,
    name: spec.name,
    isArchived: false,
    teamId: spec.teamId,
    teamName: teamName(spec.teamId),
    retentionClass: 'Adult',
    pathwayCount: 0,
    clockStopStageOrder: 1,
    stageDefinitionIdsInOrder: spec.stages.map((s) => STAGES[stageSpec(s).key]),
    stages: spec.stages.map((s, i) => {
      const {key, optional, choosesBranch, branch} = stageSpec(s);
      return {
        stageOrder: i + 1,
        stageDefinitionId: STAGES[key],
        name: LIBRARY[key].name,
        description: LIBRARY[key].description,
        isArchived: false,
        visitRequirement: LIBRARY[key].visit,
        isOptional: optional ?? false,
        choosesBranch: choosesBranch ?? false,
        branch: branch ?? null,
        stopsClock: i === 0,
      };
    }),
  }));

  const pathways = (
    [
      {
        n: 1, id: PATHWAYS.biopsyToBook, patientId: PATIENTS.ellisVarden, templateId: TEMPLATES.skinLesion,
        title: 'Changing mole, left forearm', dueIn: 30, priority: 'High', done: 1, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 14, referralId: REFERRALS.ellisAccepted,
        notes: [{authorUserId: STAFF.id, text: 'Seen in clinic. Plan a punch biopsy within two weeks.', daysAgo: 10}],
      },
      {
        n: 2, id: PATHWAYS.histologyToFinish, patientId: PATIENTS.priyaHolloway, templateId: TEMPLATES.skinLesion,
        title: 'Pigmented lesion, upper back', dueIn: 20, priority: 'High', done: 2, started: true, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 28,
      },
      {
        n: 3, id: PATHWAYS.onHold, patientId: PATIENTS.samuelOkafor, templateId: TEMPLATES.inflammatorySkin,
        title: 'Eczema flare, not settling', dueIn: 40, priority: 'Medium', done: 1, status: 'Blocked',
        blockedFrom: 'InProgress', assignee: USERS.hallworth, createdDaysAgo: 35,
        notes: [{authorUserId: USERS.hallworth, text: 'On hold while the patient is away for a month.', daysAgo: 3}],
      },
      {
        n: 4, id: PATHWAYS.overdue, patientId: PATIENTS.margaretFenwick, templateId: TEMPLATES.postMenopausalBleeding,
        title: 'Post-menopausal bleeding', dueIn: -4, priority: 'Urgent', done: 0, status: 'Assigned',
        assignee: ADMIN.id, createdDaysAgo: 30,
      },
      {
        n: 5, id: PATHWAYS.notStarted, patientId: PATIENTS.daniyalRees, templateId: TEMPLATES.diabetesReview,
        title: 'Annual diabetes review', dueIn: 50, priority: 'Low', done: 0, status: 'Assigned',
        assignee: ADMIN.id, createdDaysAgo: 5,
      },
      {
        n: 6, id: PATHWAYS.completed, patientId: PATIENTS.graceAbernethy, templateId: TEMPLATES.diabetesReview,
        title: 'Diabetes review after new medicine', dueIn: -10, priority: 'Medium', done: 6, status: 'Completed',
        assignee: ADMIN.id, createdDaysAgo: 70,
      },
      {
        n: 7, id: PATHWAYS.unassigned, patientId: PATIENTS.tobiasLund, templateId: TEMPLATES.skinLesion,
        title: 'New lesion, right cheek', dueIn: 60, priority: 'Medium', done: 0, status: 'Unassigned', createdDaysAgo: 2,
      },
      {
        n: 8, id: PATHWAYS.followUpBooked, patientId: PATIENTS.ilonaPetrova, templateId: TEMPLATES.inflammatorySkin,
        title: 'Psoriasis, new treatment', dueIn: 25, priority: 'Medium', done: 2, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 45,
      },
      {
        n: 9, id: PATHWAYS.consentToRecord, patientId: PATIENTS.harrietQuill, templateId: TEMPLATES.skinLesion,
        title: 'Basal cell carcinoma, left temple', dueIn: 12, priority: 'High', done: 4, started: true, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 40,
        notes: [{authorUserId: STAFF.id, text: 'Team review: excise with a 4 mm margin.', daysAgo: 6}],
      },
      {
        n: 10, id: PATHWAYS.branchToChoose, patientId: PATIENTS.felixMoreton, templateId: TEMPLATES.suspectedSkinCancer,
        title: 'Suspicious lesion, right shin', dueIn: 35, priority: 'High', done: 0, started: true, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 6,
      },
      // Pathways in other clinics beside a Dermatology one, so the team walls show: Dermatology's staff see each only
      // as a summary line ("a pathway in Gynaecology, in progress since ..."), and opening one is a 404.
      {
        n: 11, id: PATHWAYS.priyaGynaecology, patientId: PATIENTS.priyaHolloway, templateId: TEMPLATES.postMenopausalBleeding,
        title: 'Post-menopausal bleeding', dueIn: 18, priority: 'High', done: 1, status: 'InProgress', createdDaysAgo: 12,
      },
      {
        n: 12, id: PATHWAYS.ellisDiabetes, patientId: PATIENTS.ellisVarden, templateId: TEMPLATES.diabetesReview,
        title: 'Diabetes review', dueIn: -20, priority: 'Low', done: 6, status: 'Completed', createdDaysAgo: 90,
      },
    ] satisfies PathwaySpec[]
  ).map((spec) => makePathway(spec as PathwaySpec, templates, stageDefinitions));
  for (const template of templates) template.pathwayCount = pathways.filter((p) => p.templateId === template.id).length;

  const referrals = [
    makeReferral(REFERRALS.dermatologyToTriage, PATIENTS.oliverMaddox, TEAMS.dermatology, 3, 'Urgent',
      'Rapidly growing lesion on the scalp, bleeds on contact.'),
    makeReferral(REFERRALS.aishaDermatology, PATIENTS.aishaKhoury, TEAMS.dermatology, 6, 'Medium',
      'Persistent rash on both hands despite emollients.'),
    makeReferral(REFERRALS.ruthGynaecology, PATIENTS.ruthPenrose, TEAMS.gynaecology, 2, 'Urgent',
      'Post-menopausal bleeding, first episode.'),
    makeReferral(REFERRALS.callumDiabetes, PATIENTS.callumAshby, TEAMS.diabetes, 9, 'Low',
      'Type 2 diabetes, review of control and medicines.'),
    makeReferral(REFERRALS.ellisAccepted, PATIENTS.ellisVarden, TEAMS.dermatology, 16, 'High',
      'Mole on the left forearm has changed shape and colour.',
      {pathwayId: PATHWAYS.biopsyToBook, daysAgo: 14, templateName: 'Skin lesion pathway'}),
  ];

  const pathway = (id: string) => pathways.find((p) => p.id === id)!;
  const patientName = (id: string) => {
    const spec = PATIENT_SPECS.find(([key]) => key === id)!;
    return `${spec[1]} ${spec[2]}`;
  };
  const appointment = (
    id: string,
    pathwayId: string,
    stageIndex: number,
    staff: {id: string; username: string},
    date: string | null,
    hour: number,
    minute: number,
    minutes: number,
    status: 'Requested' | 'Booked' | 'Attended',
    roomId: string = ROOMS.clinicRoom1,
  ): DemoAppointment => {
    const p = pathway(pathwayId);
    const stage = p.stages[stageIndex];
    const startUtc = date ? ukTime(date, hour, minute) : null;
    return {
      id,
      teamId: p.teamId,
      patientId: p.patientId,
      patientName: patientName(p.patientId),
      pathwayId: p.id,
      pathwayTitle: p.title,
      pathwayDueDate: p.dueDate,
      stageId: stage.id,
      stageName: stage.name,
      visitRequirement: stage.visitRequirement,
      status,
      staffUserId: date ? staff.id : null,
      staffUsername: date ? staff.username : null,
      startUtc,
      endUtc: startUtc ? new Date(new Date(startUtc).getTime() + minutes * 60000).toISOString() : null,
      durationMinutes: minutes,
      roomId: date ? roomId : null,
      notes: null,
      isAutomatic: false,
      createdAtUtc: ago(10, 12, 0),
      createdByUsername: 'h.marsh',
      isRecordedAfterEvent: false,
      bookedAtUtc: date ? ago(9, 12, 0) : null,
      bookedByUsername: date ? 'h.marsh' : null,
      outcomeAtUtc: status === 'Attended' && startUtc ? new Date(new Date(startUtc).getTime() + minutes * 60000).toISOString() : null,
      outcomeByUsername: status === 'Attended' ? staff.username : null,
      lastUpdated: ago(1, 9, 0),
    };
  };
  const appointments = [
    appointment(APPOINTMENTS.margaretFirstVisit, PATHWAYS.overdue, 0, ADMIN, weekdayAhead(1), 10, 0, 45, 'Booked'),
    appointment(APPOINTMENTS.ilonaFollowUp, PATHWAYS.followUpBooked, 2, STAFF, weekdayAhead(1), 14, 0, 20, 'Booked'),
    appointment(APPOINTMENTS.ellisFirstVisit, PATHWAYS.biopsyToBook, 0, STAFF, weekdayBack(10), 9, 30, 30, 'Attended'),
    appointment(APPOINTMENTS.priyaFirstVisit, PATHWAYS.histologyToFinish, 0, STAFF, weekdayBack(20), 9, 0, 30, 'Attended'),
    appointment(APPOINTMENTS.priyaBiopsy, PATHWAYS.histologyToFinish, 1, STAFF, weekdayBack(12), 11, 0, 30, 'Attended'),
    appointment(APPOINTMENTS.daniyalBloods, PATHWAYS.notStarted, 0, ADMIN, null, 0, 0, 15, 'Requested'),
    appointment(MORE_APPOINTMENTS.harrietFirstVisit, PATHWAYS.consentToRecord, 0, STAFF, weekdayBack(36), 10, 0, 30, 'Attended'),
    appointment(MORE_APPOINTMENTS.harrietBiopsy, PATHWAYS.consentToRecord, 1, STAFF, weekdayBack(28), 11, 30, 30, 'Attended', ROOMS.procedureRoom),
    appointment(MORE_APPOINTMENTS.harrietExcision, PATHWAYS.consentToRecord, 4, STAFF, weekdayBack(0), 9, 0, 45, 'Attended', ROOMS.procedureRoom),
    appointment(MORE_APPOINTMENTS.felixFirstVisit, PATHWAYS.branchToChoose, 0, STAFF, weekdayBack(1), 14, 0, 30, 'Attended'),
  ];
  // Pathways whose stage visits were attended had those stages started by the visit.
  pathway(PATHWAYS.followUpBooked).stages[2].startedAtUtc = ago(2, 9, 0);
  pathway(PATHWAYS.followUpBooked).stages[2].startedByUserId = STAFF.id;

  // This week's and next week's blocks on both accounts' calendars.
  const blocks: Schemas['CalendarBlockResponse'][] = [];
  let blockNumber = 0;
  const block = (staffUserId: string, date: string, activity: string, from: [number, number], to: [number, number], note: string | null) => {
    const type = ACTIVITY_TYPE_LIST.find((t) => t.id === activity)!;
    blockNumber++;
    blocks.push({
      blockId: `000000df-0000-4000-8000-${blockNumber.toString(16).padStart(12, '0')}`,
      templateBlockId: null,
      isFromTemplate: false,
      staffUserId,
      date,
      startUtc: ukTime(date, ...from),
      endUtc: ukTime(date, ...to),
      activityTypeId: type.id,
      activityName: type.name,
      colour: type.colour,
      note,
      canEdit: true,
      overlapsAppointment: false,
    });
  };
  for (const week of [0, 7]) {
    for (let day = 0; day < 5; day++) {
      const date = thisWeek(week + day, on);
      for (const person of [ADMIN.id, STAFF.id]) block(person, date, ACTIVITY_TYPES.lunch, [12, 30], [13, 15], null);
      block(ADMIN.id, date, ACTIVITY_TYPES.admin, [17, 0], [18, 0], 'Letters and results');
      if (day === 0 || day === 3) block(ADMIN.id, date, ACTIVITY_TYPES.clinic, [9, 0], [12, 30], 'Diabetes clinic');
      if (day === 2) block(STAFF.id, date, ACTIVITY_TYPES.triage, [9, 0], [10, 30], 'Referral triage');
      if (day === 1 || day === 3) block(STAFF.id, date, ACTIVITY_TYPES.minorSurgery, [9, 0], [12, 0], 'Minor surgery list');
      if (day === 0 || day === 4) block(STAFF.id, date, ACTIVITY_TYPES.clinic, [13, 30], [17, 0], 'Skin clinic');
    }
  }

  const mdtDate = weekdayAhead(2);
  const meetings: Schemas['MeetingResponse'][] = [
    {
      id: MEETINGS.skinMdt,
      title: 'Skin cancer team meeting',
      notes: 'Weekly review of this week’s histology results.',
      date: mdtDate,
      startUtc: ukTime(mdtDate, 15, 30),
      endUtc: ukTime(mdtDate, 16, 30),
      organiserUserId: ADMIN.id,
      organiserUsername: ADMIN.username,
      attendees: [
        {userId: STAFF.id, username: STAFF.username, reply: 'Accepted', repliedAtUtc: ago(2, 10, 0)},
        {userId: USERS.sandiford, username: 'b.sandiford', reply: 'NoReply', repliedAtUtc: null},
      ],
      createdAtUtc: ago(3, 9, 0),
      lastUpdated: ago(2, 10, 0),
    },
  ];

  const patients: Schemas['PatientResponse'][] = PATIENT_SPECS.map(([id, firstName, lastName, dateOfBirth], i) => ({
    id,
    firstName,
    lastName,
    dateOfBirth,
    email: `${firstName}.${lastName}@example.com`.toLowerCase(),
    nhsNumber: NHS_NUMBERS[i],
    addressLines: [`${10 + i} Sample Road`, 'Exampleton'],
    postcode: null,
    mobilePhone: null,
    reminderChannel: 'None',
    practiceId: null,
    isRestricted: false,
    isDisputed: false,
    isAnonymised: false,
  }));

  const teams: Schemas['TeamResponse'][] = TEAM_NAMES.map(([id, name]) => ({
    id,
    name,
    isArchived: false,
    kind: id === TEAMS.lab ? 'Lab' : 'Clinic',
    version: 1,
    templateCount: templates.filter((t) => t.teamId === id).length,
    pathwayCount: pathways.filter((p) => p.teamId === id).length,
    memberCount: id === TEAMS.dermatology ? 3 : 1,
    referralCount: referrals.filter((r) => r.teamId === id).length,
    letterTemplateCount: 0,
    triagerCount: id === TEAMS.dermatology ? 3 : 1,
    canBeDeleted: false,
    targetDaysUrgent: TARGET_DAYS.Urgent,
    targetDaysHigh: TARGET_DAYS.High,
    targetDaysMedium: TARGET_DAYS.Medium,
    targetDaysLow: TARGET_DAYS.Low,
    createdAt: ago(120),
    lastUpdated: ago(120),
  }));

  const base: BaseState = {
    version: SEED_VERSION,
    seededOn: on,
    user: null,
    patients,
    teams,
    stageDefinitions,
    templates,
    pathways,
    noteDeletionRequests: [],
    referrals,
    appointments,
    pathwayAccess: [],
    blocks,
    meetings,
    notifications: [
      {
        id: '000000e0-0000-4000-8000-000000000001',
        userId: STAFF.id,
        kind: 'MeetingInvited',
        title: 'Meeting invitation',
        message: `${ADMIN.username} invited you to 'Skin cancer team meeting'.`,
        calendarDate: mdtDate,
        meetingId: MEETINGS.skinMdt,
        createdAtUtc: ago(3, 9, 0),
        isRead: false,
      },
      {
        id: '000000e0-0000-4000-8000-000000000002',
        userId: STAFF.id,
        kind: 'PathwayAssigned',
        title: 'Pigmented lesion, upper back',
        message: `${ADMIN.username} assigned you to this pathway.`,
        pathwayId: PATHWAYS.histologyToFinish,
        createdAtUtc: ago(28, 9, 20),
        isRead: true,
      },
      {
        id: '000000e0-0000-4000-8000-000000000003',
        userId: ADMIN.id,
        kind: 'AppointmentBooked',
        title: 'Appointment booked',
        message: 'h.marsh booked an appointment with you.',
        appointmentId: APPOINTMENTS.margaretFirstVisit,
        pathwayId: PATHWAYS.overdue,
        createdAtUtc: ago(9, 12, 0),
        isRead: false,
      },
    ],
    notepads: {},
    settings: {},
  };
  // Each area's slice seeds after the base, reading (or adding to) its records.
  const state: DemoState = {...base, ...seedLab(base), ...seedClinical(base), ...seedSetup(base)};
  // The team walls' grants: each pathway's own team, and the lab on every pathway with a sample passed to it.
  state.pathwayAccess = [
    ...pathways.map((p) => ownerAccess(p)),
    ...[...new Set(state.specimens.map((s) => s.pathwayId))].map((pathwayId) => {
      const p = pathways.find((x) => x.id === pathwayId)!;
      const first = state.specimens.filter((s) => s.pathwayId === pathwayId).sort((a, b) => a.takenAtUtc.localeCompare(b.takenAtUtc))[0];
      return {pathwayId, patientId: p.patientId, teamId: TEAMS.lab, reason: 'StageOwner' as const, startedAtUtc: first.takenAtUtc, endedAtUtc: null};
    }),
  ];
  return state;
}
