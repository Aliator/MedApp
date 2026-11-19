import type {Schemas} from '../api/client';
import {chainLinks, grownLinks, grownStageOrders} from '../api/pathwayRoutes';
import {dayOfWeek, nextWeekday} from '../utils/calendarSlots';
import {ADMIN, LAB, STAFF, usernameOf} from './accounts';
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
  TEMPLATE_BRANCHES,
  TEMPLATES,
  USERS,
  id,
} from './ids';
import {dermatologyStageDefinitions, dermatologyTemplates} from './dermatology';
import {seedClinical} from './slices/clinical';
import {seedHospitals} from './slices/hospitals';
import {hospitalSample, seedLab} from './slices/lab';
import {seedLabCases} from './slices/labCases';
import {seedSetup} from './slices/setup';
import type {
  BaseState,
  DemoAppointment,
  DemoPathway,
  DemoPathwayAccess,
  DemoPlan,
  DemoPlanStage,
  DemoReferral,
  DemoStage,
  DemoStageDefinition,
  DemoState,
} from './state';
import {daysFrom, thisWeek, today, ukTime} from './time';

/**
 * The light demo's seed: a small TypeScript copy of part of the demo clinic (`tools/MedApp.DevMcp/DemoData`), with
 * fixed IDs (`ids.ts`) and dates relative to the day it's seeded, so the calendar always has this week's events. The C#
 * seed can't be exported for it: it runs the API's commands against SQL Server, and GitHub Pages builds in CI with
 * neither. Everything here is synthetic: no real people, practices or NHS numbers (only the 999 range, never issued).
 */

/** Bump when the seed's shape changes, so a visitor's older copy is seeded again. */
export const SEED_VERSION = 20;

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
 * The stage library's own definition, as the full demo clinic has it: its name, what it involves, whether it needs a
 * visit, and its gates (a letter of a kind sent, a consent record, the results signed off). Dermatology's reviewed
 * templates share it and add the rest (`dermatology.ts`).
 */
const LIBRARY: Record<
  keyof typeof STAGES,
  {name: string; description: string; visit: DemoStage['visitRequirement']; letter?: string; consent?: boolean; results?: boolean}
> = {
  carePlan: {name: 'Agree care plan', description: 'Agree a care plan with the patient: advice, self-care and when to come back.', visit: 'Suggested'},
};

type StageKey = keyof typeof STAGES;

/** The stage library as the state keeps it: the existing definitions, then those Dermatology's reviewed templates add. */
export const stageDefinitionList = (): DemoStageDefinition[] => {
  const library = libraryDefinitions();
  return [...library, ...dermatologyStageDefinitions(library)];
};

const libraryDefinitions = (): DemoStageDefinition[] =>
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

const TEAM_NAMES: [string, string][] = [
  // One clinic and the lab (the owner, 19 Nov).
  [TEAMS.dermatology, 'Dermatology'],
  // The lab (decision 182): a team of its own kind, serving every clinic.
  [TEAMS.lab, 'Lab'],
];

export const teamName = (id: string | null | undefined) => TEAM_NAMES.find(([key]) => key === id)?.[1] ?? null;

/**
 * A template's whole tree as a new pathway keeps it (its plan): each stage with the template's options (optional, a
 * choice node's kind and outcomes) and the definition's gates, copied as the API's `PathwayCreation` does, and its links.
 */
export function planOf(template: Schemas['PathwayTemplateResponse'], definitions: DemoStageDefinition[]): DemoPlan {
  const stages = (template.stages ?? []).map((stage): DemoPlanStage => {
    const definition = definitions.find((d) => d.id === stage.stageDefinitionId);
    return {
      stageDefinitionId: stage.stageDefinitionId ?? '',
      stageOrder: stage.stageOrder ?? 0,
      templateStageOrder: stage.stageOrder ?? 0,
      name: stage.name ?? '',
      description: stage.description ?? null,
      visitRequirement: (stage.visitRequirement as DemoStage['visitRequirement']) ?? 'None',
      needsLetterKind: definition?.needsLetterKind ?? null,
      needsConsent: definition?.needsConsent ?? false,
      waitsForResults: definition?.waitsForResults ?? false,
      isOptional: stage.isOptional ?? false,
      choiceKind: stage.choiceKind ?? null,
      outcomes: (stage.outcomes ?? []).map((o) => ({name: o.name, nextStageOrders: [...(o.nextStageOrders ?? [])]})),
      outcome: null,
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
      ownerTeamId: stage.ownerTeamId ?? null,
      reachedAtUtc: null,
      handedOverFromTeamId: null,
      pickedUpAtUtc: null,
      pickedUpByUserId: null,
    };
  });
  return {stages, links: (template.links ?? chainLinks(stages)).map((l) => ({fromStageOrder: l.fromStageOrder, toStageOrder: l.toStageOrder}))};
}

/** Each recorded outcome on a pathway, by its choice node's template order. */
const recordedOutcomes = (stages: readonly DemoStage[]) =>
  new Map(stages.filter((s) => s.templateStageOrder !== null && s.outcome).map((s) => [s.templateStageOrder!, s.outcome!]));

/** The template stage orders a pathway would have with these outcomes recorded (the owner's model: it grows). */
export const plannedOrders = (plan: DemoPlan, stages: readonly DemoStage[]) =>
  grownStageOrders(plan.stages, plan.links, recordedOutcomes(stages));

/**
 * Grows a pathway from its plan (the owner's model): adds every template stage now reached (up to the next choice node
 * not yet recorded; an outcome not chosen never appears), each with an ID from `idFor`, then numbers its stages again
 * (the template's in their template order, then any added with a branch) and links them as the template does, less the
 * links an outcome didn't take. Returns the stages added.
 */
export function growPathway(pathway: Pick<DemoPathway, 'plan' | 'stages' | 'links'>, idFor: (templateStageOrder: number) => string): DemoStage[] {
  const orders = plannedOrders(pathway.plan, pathway.stages);
  const has = new Set(pathway.stages.map((s) => s.templateStageOrder));
  const added = orders
    .filter((order) => !has.has(order))
    .map((order): DemoStage => {
      const {outcomes: _outcomes, ...stage} = pathway.plan.stages.find((s) => s.templateStageOrder === order)!;
      void _outcomes;
      return {...stage, id: idFor(order)};
    });
  pathway.stages.push(...added);
  const fromTemplate = pathway.stages.filter((s) => s.templateStageOrder !== null).sort((a, b) => a.templateStageOrder! - b.templateStageOrder!);
  const branchStages = pathway.stages.filter((s) => s.templateStageOrder === null).sort((a, b) => a.stageOrder - b.stageOrder);
  [...fromTemplate, ...branchStages].forEach((stage, i) => (stage.stageOrder = i + 1));
  const idAt = new Map(fromTemplate.map((s) => [s.templateStageOrder!, s.id]));
  const branchIds = new Set(branchStages.map((s) => s.id));
  pathway.links = [
    ...grownLinks(pathway.plan.stages, pathway.plan.links, orders, recordedOutcomes(pathway.stages)).map((l) => ({
      fromStageId: idAt.get(l.fromStageOrder ?? 0)!,
      toStageId: idAt.get(l.toStageOrder ?? 0)!,
    })),
    ...pathway.links.filter((l) => branchIds.has(l.fromStageId) || branchIds.has(l.toStageId)),
  ];
  return added;
}

/** The patients: synthetic names, dates of birth, 999-range NHS numbers (each with a valid check digit) and house numbers. */
const PATIENT_SPECS: [string, string, string, string, string, number][] = [
  [PATIENTS.ellisVarden, 'Ellis', 'Varden', '1958-03-14', '9991001379', 10],
  [PATIENTS.priyaHolloway, 'Priya', 'Holloway', '1971-09-02', '9991004114', 11],
  [PATIENTS.samuelOkafor, 'Samuel', 'Okafor', '1989-12-21', '9991006850', 12],
  [PATIENTS.graceAbernethy, 'Grace', 'Abernethy', '1979-04-08', '9991010963', 15],
  [PATIENTS.tobiasLund, 'Tobias', 'Lund', '1994-11-25', '9991012338', 16],
  [PATIENTS.ilonaPetrova, 'Ilona', 'Petrova', '1983-07-12', '9991013709', 17],
  [PATIENTS.oliverMaddox, 'Oliver', 'Maddox', '1961-02-03', '9991015078', 18],
  [PATIENTS.aishaKhoury, 'Aisha', 'Khoury', '1975-10-19', '9991016449', 19],
  [PATIENTS.callumAshby, 'Callum', 'Ashby', '1970-05-05', '9991020551', 21],
  [PATIENTS.harrietQuill, 'Harriet', 'Quill', '1949-01-22', '9991022007', 22],
  [PATIENTS.felixMoreton, 'Felix', 'Moreton', '1968-03-09', '9991023372', 23],
];

/** NHS numbers from the 999 range only (never issued to real people), each with a valid check digit. */
export const NHS_NUMBERS = PATIENT_SPECS.map((spec) => spec[4]);

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
  /** Which stages are finished, by order, when they aren't the first `done` (a pathway with routes side by side). */
  doneOrders?: number[];
  /** Each choice node's recorded outcome, by its template order or its name (the pathway has grown past it). */
  outcomes?: Record<number | string, string>;
  /** The results the lab entered, by the lab stage's template order or name (else a short synthetic one). */
  results?: Record<number | string, string>;
  /** An addable branch added after the stage of this name, with its first `done` stages finished (MC10). */
  branch?: {templateBranchId: string; after: string; done: number; reason: string; daysAgo: number};
  /** Whether the next stage has been started. */
  started?: boolean;
  status: DemoPathway['status'];
  blockedFrom?: DemoPathway['status'];
  assignee?: string;
  createdDaysAgo: number;
  referralId?: string;
  notes?: {authorUserId: string; text: string; daysAgo: number}[];
}

/**
 * Adds a template's addable branch to a seeded pathway (MC10), as `POST …/branches` does: its stages chained after the
 * stage named, on a route of their own, its first `done` finished (by the lab's member of staff for the lab's stages).
 */
function addBranch(
  spec: PathwaySpec,
  template: Schemas['PathwayTemplateResponse'],
  definitions: DemoStageDefinition[],
  stages: DemoStage[],
  links: DemoPathway['links'],
  doer: string,
): NonNullable<DemoPathway['branchAdditions']>[number] {
  const {templateBranchId, after: afterName, done, reason, daysAgo} = spec.branch!;
  const branch = template.addableBranches!.find((b) => b.id === templateBranchId)!;
  const after = stages.find((s) => s.name === afterName)!;
  const additionId = id(0xfd, spec.n);
  const added = (branch.stages ?? []).map((branchStage, i): DemoStage => {
    const definition = definitions.find((d) => d.id === branchStage.stageDefinitionId)!;
    const ownerTeamId = stages.find((s) => s.stageDefinitionId === definition.id && s.ownerTeamId)?.ownerTeamId ?? null;
    const worker = ownerTeamId === TEAMS.lab ? LAB.id : doer;
    const isDone = i < done;
    return {
      id: stageId(spec.n, 50 + i),
      stageDefinitionId: definition.id,
      stageOrder: stages.length + i + 1,
      name: definition.name ?? '',
      description: definition.description ?? null,
      visitRequirement: (definition.visitRequirement as DemoStage['visitRequirement']) ?? 'None',
      needsLetterKind: definition.needsLetterKind ?? null,
      needsConsent: definition.needsConsent ?? false,
      waitsForResults: definition.waitsForResults ?? false,
      isOptional: false,
      choiceKind: null,
      outcome: null,
      templateStageOrder: null,
      unlocked: false,
      notes: isDone ? 'Done as planned.' : null,
      startedAtUtc: isDone ? ago(daysAgo - 1, 9, 30) : null,
      startedByUserId: isDone ? worker : null,
      completedAtUtc: isDone ? ago(daysAgo - 1, 10, 0) : null,
      completedByUserId: isDone ? worker : null,
      skippedAtUtc: null,
      skippedByUserId: null,
      skipReason: null,
      skipDetail: null,
      branchAdditionId: additionId,
      ownerTeamId,
      reachedAtUtc: null,
      handedOverFromTeamId: null,
      pickedUpAtUtc: null,
      pickedUpByUserId: null,
    };
  });
  stages.push(...added);
  added.forEach((stage, i) => links.push({fromStageId: (i === 0 ? after : added[i - 1]).id, toStageId: stage.id}));
  return {
    id: additionId,
    templateBranchId,
    name: branch.name ?? '',
    afterStageId: after.id,
    stageIds: added.map((s) => s.id),
    reason,
    addedByUserId: doer,
    addedAtUtc: ago(daysAgo, 11, 0),
  };
}

function makePathway(spec: PathwaySpec, templates: Schemas['PathwayTemplateResponse'][], definitions: DemoStageDefinition[]): DemoPathway {
  const template = templates.find((t) => t.id === spec.templateId)!;
  const created = ago(spec.createdDaysAgo, 9, 15);
  const doer = spec.assignee ?? ADMIN.id;
  // Finished stages are spread between the pathway's start and a couple of days ago.
  const step = Math.max(1, Math.floor((spec.createdDaysAgo - 2) / Math.max(1, spec.done)));
  const handovers: DemoPathway['handovers'] = [];
  const plan = planOf(template, definitions);
  // A stage of the plan by its template order, or by its name (the first of that name).
  const planOrder = (key: string) => (/^\d+$/.test(key) ? Number(key) : plan.stages.find((s) => s.name === key)!.templateStageOrder!);
  const results = new Map(Object.entries(spec.results ?? {}).map(([key, text]) => [planOrder(key), text]));
  // The pathway has grown up to its next choice node not recorded: the stages of the outcomes recorded, no others.
  const grown: Pick<DemoPathway, 'plan' | 'stages' | 'links'> = {plan, stages: [], links: []};
  for (const [key, outcome] of Object.entries(spec.outcomes ?? {}))
    plan.stages.find((s) => s.templateStageOrder === planOrder(key))!.outcome = outcome;
  // Each round adds the stages the outcomes recorded so far reach (their copies carry the next outcomes).
  while (growPathway(grown, (order) => `${order}`).length > 0);
  const ordered = [...grown.stages].sort((a, b) => a.stageOrder - b.stageOrder);
  const stages = ordered.map((stage, i): DemoStage => {
    const finishedDaysAgo = spec.createdDaysAgo - step * (i + 1);
    const isDone = spec.doneOrders ? spec.doneOrders.includes(i + 1) : i < spec.done;
    const isStarted = isDone || (i === spec.done && spec.started === true);
    // The lab's stages are worked by the lab's member of staff (the relay), who enters the result as it's finished.
    const isLabs = stage.ownerTeamId === TEAMS.lab;
    const worker = isLabs ? LAB.id : doer;
    const completedAtUtc = isDone ? ago(Math.max(1, finishedDaysAgo), 15, 0) : null;
    return {
      ...stage,
      id: stageId(spec.n, i + 1),
      notes: isDone ? 'Done as planned.' : null,
      // A finished stage was started a day before it finished; the current one just after the one before it finished.
      startedAtUtc: isStarted ? (isDone ? ago(Math.max(1, finishedDaysAgo + 1), 9, 30) : ago(Math.max(1, finishedDaysAgo + step), 15, 30)) : null,
      startedByUserId: isStarted ? worker : null,
      completedAtUtc,
      completedByUserId: isDone ? worker : null,
      result:
        isLabs && completedAtUtc
          ? {
              text: results.get(stage.templateStageOrder ?? 0) ?? 'Reported (synthetic): see the full report on the result.',
              enteredByUserId: worker,
              enteredAtUtc: completedAtUtc,
            }
          : null,
    };
  });
  // The plan's copies hold no outcome: a pathway's own stages do.
  for (const stage of plan.stages) stage.outcome = null;
  const idOf = new Map(ordered.map((s, i) => [s.id, stages[i].id]));
  const links = grown.links.map((l) => ({fromStageId: idOf.get(l.fromStageId)!, toStageId: idOf.get(l.toStageId)!}));
  const branchAdditions = spec.branch ? [addBranch(spec, template, definitions, stages, links, doer)] : undefined;
  // Each stage reached (every stage before it on its routes finished) was reached when the last of them finished, or
  // when the pathway was made; one whose team differs from that stage's was handed over then (the relay), and picked
  // up when it was started.
  for (const stage of stages) {
    const before = stages
      .filter((s) => links.some((l) => l.fromStageId === s.id && l.toStageId === stage.id))
      .sort((a, b) => (a.completedAtUtc ?? '').localeCompare(b.completedAtUtc ?? ''));
    if (before.some((s) => !s.completedAtUtc)) continue;
    const last = before.at(-1);
    stage.reachedAtUtc = last ? last.completedAtUtc : created;
    const from = last ? (last.ownerTeamId ?? template.teamId!) : template.teamId!;
    const to = stage.ownerTeamId ?? template.teamId!;
    if (from === to || !stage.reachedAtUtc) continue;
    stage.handedOverFromTeamId = from;
    stage.pickedUpAtUtc = stage.startedAtUtc;
    stage.pickedUpByUserId = stage.startedByUserId;
    handovers.push({stageId: stage.id, fromTeamId: from, toTeamId: to, atUtc: stage.reachedAtUtc, byUserId: last?.completedByUserId ?? doer});
  }
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
    handovers,
    links,
    plan,
    ...(branchAdditions ? {branchAdditions} : {}),
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
  const templates: Schemas['PathwayTemplateResponse'][] = dermatologyTemplates(stageDefinitions);
  for (const template of templates) template.links ??= chainLinks(template.stages ?? []);

  const pathways = (
    [
      {
        n: 1, id: PATHWAYS.biopsyToBook, patientId: PATIENTS.ellisVarden, templateId: TEMPLATES.suspectedSkinCancer,
        title: 'Changing mole, left forearm', dueIn: 30, priority: 'High', done: 2, outcomes: {'Dermoscopy assessment': 'Uncertain: biopsy'}, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 14, referralId: REFERRALS.ellisAccepted,
        notes: [{authorUserId: STAFF.id, text: 'Seen in clinic. Plan a punch biopsy within two weeks.', daysAgo: 10}],
      },
      {
        n: 2, id: PATHWAYS.histologyToFinish, patientId: PATIENTS.priyaHolloway, templateId: TEMPLATES.suspectedSkinCancer,
        title: 'Pigmented lesion, upper back', dueIn: 20, priority: 'High', done: 3, outcomes: {'Dermoscopy assessment': 'Uncertain: biopsy'}, started: true, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 28,
      },
      {
        n: 3, id: PATHWAYS.onHold, patientId: PATIENTS.samuelOkafor, templateId: TEMPLATES.skinInfection,
        title: 'Scaly rash on both hands', dueIn: 40, priority: 'Medium', done: 2, outcomes: {'Infection assessment': 'Sample needed'},
        started: true, status: 'Blocked',
        blockedFrom: 'InProgress', assignee: USERS.hallworth, createdDaysAgo: 35,
        notes: [{authorUserId: USERS.hallworth, text: 'On hold while the patient is away for a month.', daysAgo: 3}],
      },
      {
        n: 7, id: PATHWAYS.unassigned, patientId: PATIENTS.tobiasLund, templateId: TEMPLATES.changingMole,
        title: 'New lesion, right cheek', dueIn: 60, priority: 'Medium', done: 0, status: 'Unassigned', createdDaysAgo: 2,
      },
      {
        n: 8, id: PATHWAYS.followUpBooked, patientId: PATIENTS.ilonaPetrova, templateId: TEMPLATES.psoriasis,
        title: 'Psoriasis, new care plan', dueIn: 25, priority: 'Medium', done: 3, outcomes: {'Psoriasis assessment': 'Self-care is enough'},
        status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 45,
      },
      {
        n: 9, id: PATHWAYS.referredToHospital, patientId: PATIENTS.harrietQuill, templateId: TEMPLATES.suspectedSkinCancer,
        title: 'Basal cell carcinoma, left temple', dueIn: -3, priority: 'High', done: 5, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 40, outcomes: {'Dermoscopy assessment': 'Uncertain: biopsy', 'Histology result': 'Low-risk BCC'},
        results: {Histology: 'Basal cell carcinoma, nodular type (synthetic report).'},
        notes: [{authorUserId: STAFF.id, text: 'Team review: photograph, then refer to the hospital for excision.', daysAgo: 6}],
      },
      {
        n: 10, id: PATHWAYS.branchToChoose, patientId: PATIENTS.felixMoreton, templateId: TEMPLATES.suspectedSkinCancer,
        title: 'Suspicious lesion, right shin', dueIn: 35, priority: 'High', done: 1, started: true, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 6,
      },
      // Routes side by side (MC8, MC10): a second lesion, so Biopsy another lesion was added after the dermoscopy. Both
      // biopsies are taken, so the two Histology stages are current side by side, both the lab's.
      {
        n: 13, id: PATHWAYS.forkedTests, patientId: PATIENTS.aishaKhoury, templateId: TEMPLATES.suspectedSkinCancer,
        title: 'Two lesions, left shin', dueIn: 30, priority: 'Medium', done: 3, outcomes: {'Dermoscopy assessment': 'Uncertain: biopsy'},
        status: 'InProgress', assignee: STAFF.id, createdDaysAgo: 20,
        branch: {templateBranchId: TEMPLATE_BRANCHES.biopsyAnother, after: 'Dermoscopy assessment', done: 1, reason: 'A second lesion at the skin check.', daysAgo: 8},
      },
      // Back from the lab (the relay): the lab finished Histology, so Histology result (a choice node) is Dermatology's
      // again, not picked up yet.
      {
        n: 14, id: PATHWAYS.backFromLab, patientId: PATIENTS.callumAshby, templateId: TEMPLATES.suspectedSkinCancer,
        title: 'Scaly patch, back of the right hand', dueIn: 24, priority: 'Medium', done: 4, outcomes: {'Dermoscopy assessment': 'Uncertain: biopsy'}, status: 'InProgress',
        assignee: STAFF.id, createdDaysAgo: 21,
        results: {Histology: 'Seborrhoeic keratosis, no dysplasia (synthetic report).'},
      },
    ] satisfies PathwaySpec[]
  ).map((spec) => makePathway(spec as PathwaySpec, templates, stageDefinitions));
  for (const template of templates) template.pathwayCount = pathways.filter((p) => p.templateId === template.id).length;

  const referrals = [
    makeReferral(REFERRALS.dermatologyToTriage, PATIENTS.oliverMaddox, TEAMS.dermatology, 3, 'Urgent',
      'Rapidly growing lesion on the scalp, bleeds on contact.'),
    makeReferral(REFERRALS.aishaDermatology, PATIENTS.aishaKhoury, TEAMS.dermatology, 6, 'Medium',
      'Persistent rash on both hands despite emollients.'),
    makeReferral(REFERRALS.ellisAccepted, PATIENTS.ellisVarden, TEAMS.dermatology, 16, 'High',
      'Mole on the left forearm has changed shape and colour.',
      {pathwayId: PATHWAYS.biopsyToBook, daysAgo: 14, templateName: 'Suspected skin cancer'}),
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
    appointment(APPOINTMENTS.ilonaFollowUp, PATHWAYS.followUpBooked, 3, STAFF, weekdayAhead(1), 14, 0, 20, 'Booked'),
    appointment(APPOINTMENTS.ellisFirstVisit, PATHWAYS.biopsyToBook, 1, STAFF, weekdayBack(10), 9, 30, 30, 'Attended'),
    appointment(APPOINTMENTS.priyaFirstVisit, PATHWAYS.histologyToFinish, 1, STAFF, weekdayBack(20), 9, 0, 30, 'Attended'),
    appointment(APPOINTMENTS.priyaBiopsy, PATHWAYS.histologyToFinish, 2, STAFF, weekdayBack(12), 11, 0, 30, 'Attended'),
    appointment(MORE_APPOINTMENTS.harrietFirstVisit, PATHWAYS.referredToHospital, 1, STAFF, weekdayBack(36), 10, 0, 30, 'Attended'),
    appointment(MORE_APPOINTMENTS.harrietBiopsy, PATHWAYS.referredToHospital, 2, STAFF, weekdayBack(28), 11, 30, 30, 'Attended', ROOMS.procedureRoom),
    appointment(MORE_APPOINTMENTS.harrietResults, PATHWAYS.referredToHospital, 4, STAFF, weekdayBack(0), 9, 0, 20, 'Attended'),
    appointment(MORE_APPOINTMENTS.felixFirstVisit, PATHWAYS.branchToChoose, 1, STAFF, weekdayBack(1), 14, 0, 30, 'Attended'),
  ];
  // Pathways whose stage visits were attended had those stages started by the visit.
  pathway(PATHWAYS.followUpBooked).stages[3].startedAtUtc = ago(2, 9, 0);
  pathway(PATHWAYS.followUpBooked).stages[3].startedByUserId = STAFF.id;

  // This week's and next week's blocks on Nadia Thornbury's calendar.
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
      block(STAFF.id, date, ACTIVITY_TYPES.lunch, [12, 30], [13, 15], null);
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

  const patients: Schemas['PatientResponse'][] = PATIENT_SPECS.map(([id, firstName, lastName, dateOfBirth, nhsNumber, house]) => ({
    id,
    firstName,
    lastName,
    dateOfBirth,
    email: `${firstName}.${lastName}@example.com`.toLowerCase(),
    nhsNumber,
    addressLines: [`${house} Sample Road`, 'Exampleton'],
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
      // The relay's handovers: a fixed title, and only who passed it (no patient, no stage).
      ...[
        {n: 4, userId: STAFF.id, pathwayId: PATHWAYS.backFromLab},
        {n: 5, userId: LAB.id, pathwayId: PATHWAYS.forkedTests},
      ].map(({n, userId, pathwayId}) => {
        const handover = pathways.find((p) => p.id === pathwayId)!.handovers.at(-1)!;
        return {
          id: `000000e0-0000-4000-8000-00000000000${n}`,
          userId,
          kind: 'PathwayHandedOver',
          title: 'Pathway handed over',
          message: `${usernameOf(handover.byUserId)} passed a pathway to your team.`,
          pathwayId,
          createdAtUtc: handover.atUtc,
          isRead: false,
        };
      }),
      // A service alert (MC19) from the admin's lab clock rule: a fixed title and a count, never the patient.
      {
        id: '000000e0-0000-4000-8000-000000000006',
        userId: ADMIN.id,
        kind: 'MonitoredLabCaseOverdue',
        title: 'Lab cases overdue',
        message: '1 lab case is past its due date.',
        createdAtUtc: ago(1, 7, 0),
        isRead: false,
      },
    ],
    notepads: {},
    settings: {},
  };
  // Each area's slice seeds after the base, reading (or adding to) its records.
  const lab = seedLab(base);
  // The hospital's cases take numbers from the lab's one sequence (MC18), after the clinics' specimens.
  const hospitals = seedHospitals(base, lab);
  // Each hospital request's sample, in the one samples database (MC14).
  lab.specimens.push(...hospitals.hospitalRequests.map((r, i) => hospitalSample(r, id(0xc6, i + 1))));
  const state: DemoState = {...base, ...lab, ...seedClinical(base), ...seedSetup(base), ...hospitals, labCases: []};
  // The lab's cases (MC15), last: they read the samples, the hospital's requests and the results.
  state.labCases = seedLabCases(state).labCases;
  // The team walls' grants: each pathway's own team, and the lab on every pathway with a sample passed to it or a
  // stage of its reached (the relay), from whichever came first.
  const labSince = (p: DemoPathway) =>
    [
      ...state.specimens.filter((s) => s.pathwayId === p.id).map((s) => s.takenAtUtc),
      ...p.stages.filter((s) => s.ownerTeamId === TEAMS.lab && s.reachedAtUtc).map((s) => s.reachedAtUtc!),
    ].sort()[0];
  state.pathwayAccess = [
    ...pathways.map((p) => ownerAccess(p)),
    ...pathways
      .filter((p) => p.teamId !== TEAMS.lab && labSince(p))
      .map((p) => ({pathwayId: p.id, patientId: p.patientId, teamId: TEAMS.lab, reason: 'StageOwner' as const, startedAtUtc: labSince(p), endedAtUtc: null})),
  ];
  return state;
}
