import type {Schemas} from '../../api/client';
import {PEOPLE, personById, usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {
  andList,
  chainLinks,
  CHOICE_KINDS,
  hasRoutes,
  MAX_BRANCH_REASON_LENGTH,
  PARALLEL_ROUTES_LOCKED,
  templateGraphProblems,
} from '../../api/pathwayRoutes';
import {MAX_BRANCH_LENGTH} from '../../api/stageSkips';
import {growPathway, ownerAccess, planOf, plannedOrders, teamName} from '../seed';
import type {DemoNote, DemoNoteDeletionRequest, DemoPathway, DemoStage, DemoStageDefinition} from '../state';
import {db} from '../store';
import {endOfUkDay, nowUtc, today} from '../time';
import {can, contains, flag, list, me, NO_FLAGS, paged, pathwayPatient, patientName, requirePermission, sorted, teamIdsOf} from './common';
import {canRecordConsent, clinicalGate} from './clinical';
import {canRecordSpecimen, labGate} from './lab';
import {caseOfStage, openCaseOfStage, openStageCase} from './labCases';
import {pathwayReferralResponse, referralSummaryClock} from './referrals';
import {
  canChangePathway,
  canSeePathway,
  canWorkStage,
  consultReadOnly,
  ensureCanChangePathway,
  ensureCanWorkStage,
  grantStageOwner,
  insightsTeams,
  isPickedTeam,
  labTeamId,
  mdtReadOnly,
  myScope,
  notYourTeams,
  pickedTeam,
  readsThroughConsult,
  readsThroughMdt,
  refuseAcrossWall,
  stageTeamOf,
  type TeamScope,
} from './teamWalls';

/**
 * Pathways, their stages and notes, the stage library, the templates and Insights. The rules are a simplified copy of
 * the API's `PathwayWorkflow`: stages follow their links (MC8, tree pathways), so a stage is current when it's neither
 * finished nor skipped and nothing before it is still to do; a fork's routes run side by side, and a join waits for
 * each. The first stage started or finished starts the pathway, and finishing or skipping the last on every route puts
 * it in review. Optional stages can be skipped, and the gates without an override (`StageGates`: the lab's, the letter's
 * and consent's) are checked on every finish. There's no monitoring.
 *
 * **Choice nodes** (the owner's model, 19 Nov): a pathway holds only the stages up to its next choice node (an
 * assessment or a lab result). Finishing one records its outcome, chosen by the clinician (never by MedApp, none
 * pre-selected), and adds that outcome's stages from the pathway's plan (`growPathway`); unchosen outcomes never appear.
 * The lab's stage takes its results from its lab case (MC16): authorising the case's report finishes it with them
 * (`finishLabStage`), and the clinic's review (the lab result choice node) shows them. There's no other way to enter them.
 *
 * **The relay** (MC7): a template's stage can be owned by another team (the lab, in Stage 1). Only its owner works it
 * while it's current (and adds notes to the pathway meanwhile); when a finish or skip makes current a stage whose team
 * differs from the finished one's, it's handed over (`reachNext`): the receiving team gets its grant and a
 * notification, and picks it up (Pick Up, or by starting or finishing it).
 */

const OPEN: DemoPathway['status'][] = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked'];
const isOpen = (pathway: DemoPathway) => OPEN.includes(pathway.status);
const isDone = (stage: DemoStage) => stage.completedAtUtc !== null;
const isSkipped = (stage: DemoStage) => stage.skippedAtUtc !== null;
/** A stage still to be worked: neither finished nor skipped. */
export const isToDo = (stage: DemoStage) => !isDone(stage) && !isSkipped(stage);
const inOrder = (pathway: DemoPathway) => [...pathway.stages].sort((a, b) => a.stageOrder - b.stageOrder);
/** Every stage finished and locked (none reopened), or skipped. */
const allFinished = (pathway: DemoPathway) => pathway.stages.every((s) => (isDone(s) && !s.unlocked) || isSkipped(s));

/** The stages straight before a stage (its links' froms). */
const predecessorsOf = (pathway: DemoPathway, stage: DemoStage) =>
  inOrder(pathway).filter((s) => pathway.links.some((l) => l.fromStageId === s.id && l.toStageId === stage.id));

/** Every stage after a stage, on any route (its descendants), by ID. */
function after(pathway: DemoPathway, stage: DemoStage): Set<string> {
  const seen = new Set<string>();
  const queue = [stage.id];
  while (queue.length > 0) {
    const from = queue.shift()!;
    for (const link of pathway.links)
      if (link.fromStageId === from && !seen.has(link.toStageId)) {
        seen.add(link.toStageId);
        queue.push(link.toStageId);
      }
  }
  return seen;
}

/** Whether nothing before a stage, on any route into it, is still to do (`PathwayWorkflow.CurrentStages`). */
const isReached = (pathway: DemoPathway, stage: DemoStage) =>
  pathway.stages.every((other) => !isToDo(other) || !after(pathway, other).has(stage.id));

/**
 * The stages being worked, in order: each neither finished nor skipped, with nothing before it still to do, while the
 * pathway is open (none when it's on hold or closed). A chain has one; routes side by side have one each.
 */
export const currentStages = (pathway: DemoPathway): DemoStage[] =>
  pathway.status === 'Blocked' || !isOpen(pathway) ? [] : inOrder(pathway).filter((s) => isToDo(s) && isReached(pathway, s));

/** The first current stage, for what shows only one (as the API's `CurrentStage`). */
export const currentStage = (pathway: DemoPathway): DemoStage | undefined => currentStages(pathway)[0];

/** Whether a stage is one of the current ones. */
export const isCurrentStage = (pathway: DemoPathway, stage: DemoStage) => currentStages(pathway).some((s) => s.id === stage.id);

const SKIP_REASONS = ['NotNeeded', 'PatientDeclined', 'Other'];

/** Why a stage can't be skipped now, or null (`PathwayWorkflow.WhySkipNotAllowed`). */
function whySkipNotAllowed(pathway: DemoPathway, stage: DemoStage): string | null {
  if (!stage.isOptional) return `The '${stage.name}' stage isn't optional, so it can't be skipped.`;
  if (!canWorkOnStages(pathway)) return 'Stages can’t be skipped while the pathway is on hold or closed.';
  if (isSkipped(stage)) return `The '${stage.name}' stage has already been skipped.`;
  if (isDone(stage)) return `The '${stage.name}' stage is finished, so it can't be skipped.`;
  if (!isCurrentStage(pathway, stage)) return `Only a current stage can be skipped: finish the stages before '${stage.name}' first.`;
  return stage.visitRequirement === 'Required' && !can('FinishWithoutVisit')
    ? `The '${stage.name}' stage needs a visit, so only someone who can finish a stage without its visit can skip it.`
    : null;
}

/**
 * A choice node's outcomes, in the template's order, each with the stages recording it would add (up to the next choice
 * node). None is ever chosen or pre-selected: the clinician records one.
 */
function outcomesOf(pathway: DemoPathway, stage: DemoStage): Schemas['PatientPathwayOutcomeResponse'][] {
  const planned = pathway.plan.stages.find((s) => s.templateStageOrder === stage.templateStageOrder);
  if (!stage.choiceKind || !planned) return [];
  const has = new Set(pathway.stages.map((s) => s.templateStageOrder));
  return planned.outcomes.map((outcome) => {
    const recorded = pathway.stages.map((s) => (s.id === stage.id ? {...s, outcome: outcome.name ?? ''} : s));
    const added = plannedOrders(pathway.plan, recorded).filter((order) => !has.has(order));
    return {name: outcome.name, stageNames: added.map((order) => pathway.plan.stages.find((s) => s.templateStageOrder === order)?.name ?? '')};
  });
}

/** Whether the lab works the stage (MC7's relay): its results come from its lab case (MC16). */
const isLabsStage = (stage: DemoStage) => stage.ownerTeamId !== null && stage.ownerTeamId === labTeamId();

/** `StageGates.WhyLabCaseOpen` (MC16): a lab stage isn't finished by hand while its case is open; authorising does it. */
function whyLabCaseOpen(stage: DemoStage): string | null {
  const open = isLabsStage(stage) ? openCaseOfStage(stage.id) : undefined;
  return open ? `Lab case ${open.number} is still open: authorising its report finishes this stage, with its results.` : null;
}

/** Whether the pathway's last stage to do is this one (finishing it leaves none; a choice node always adds more). */
const isLastToDo = (pathway: DemoPathway, stage: DemoStage) => !stage.choiceKind && pathway.stages.every((s) => s.id === stage.id || !isToDo(s));

/** Why a stage can't be finished yet, from the gates without an override (`StageGates`), or null. */
function whyGated(pathway: DemoPathway, stage: DemoStage): string | null {
  return whyLabCaseOpen(stage) ?? labGate(pathway, stage, isLastToDo(pathway, stage)) ?? clinicalGate(pathway, stage);
}

const canWorkOnStages = (pathway: DemoPathway) => ['Unassigned', 'Assigned', 'InProgress', 'InReview'].includes(pathway.status);

/** Whether the viewer's team works one of the pathway's current stages (the lab, while one of its stages is current). */
const worksCurrentStage = (pathway: DemoPathway, scope: TeamScope = myScope()) =>
  currentStages(pathway).some((s) => canWorkStage(pathway, s, scope));

/** Whether the viewer's team adds and edits notes on the pathway: its own team, or the team working its current stage. */
const canNoteOn = (pathway: DemoPathway) => canChangePathway(pathway) || worksCurrentStage(pathway);

/** A stage handed over and waiting for the receiving team to pick it up. */
const awaitsPickUp = (stage: DemoStage) => stage.handedOverFromTeamId !== null && stage.pickedUpAtUtc === null;

/** Picks up a handed-over stage (Pick Up, or starting or finishing it): once only. */
function pickUp(stage: DemoStage, userId: string) {
  if (!awaitsPickUp(stage)) return;
  stage.pickedUpAtUtc = nowUtc();
  stage.pickedUpByUserId = userId;
}

/**
 * After a stage is finished or skipped (`PathwayChange.HandOver`): each stage now current and not reached before is
 * reached (a fork can reach several, a lab's and a clinic's at once), and when its team differs from `fromTeamId` (the
 * finished stage's) it's handed over: the receiving team gets its grant to see the pathway (`StageOwner`, kept), and
 * its members who work pathways are told, with a fixed title and only who passed it (no patient, no stage).
 */
function reachNext(pathway: DemoPathway, fromTeamId: string, byUserId: string) {
  for (const next of currentStages(pathway).filter((s) => !s.reachedAtUtc)) handOver(pathway, next, fromTeamId, byUserId);
}

function handOver(pathway: DemoPathway, next: DemoStage, fromTeamId: string, byUserId: string) {
  const now = nowUtc();
  next.reachedAtUtc = now;
  // A lab stage reached opens its lab case (MC16), waiting for its sample.
  if (isLabsStage(next)) openStageCase(pathway, next, byUserId);
  const to = stageTeamOf(pathway, next);
  if (to === fromTeamId) return;
  next.handedOverFromTeamId = fromTeamId;
  pathway.handovers.push({stageId: next.id, fromTeamId, toTeamId: to, atUtc: now, byUserId});
  grantStageOwner(pathway, to);
  for (const person of PEOPLE.filter((p) => p.id !== byUserId && p.permissions.includes('WorkPathways') && teamIdsOf(p).includes(to)))
    db().notifications.push({
      id: newId(),
      userId: person.id,
      kind: 'PathwayHandedOver',
      title: 'Pathway handed over',
      message: `${usernameOf(byUserId)} passed a pathway to your team.`,
      pathwayId: pathway.id,
      createdAtUtc: now,
      isRead: false,
    });
}

/** The relay's worklist views (`PathwaySearch.View`), for the viewer's teams. */
const VIEWS: Record<string, (pathway: DemoPathway, scope: TeamScope) => boolean> = {
  /** A current stage owned by my team. */
  OurTurn: (p, scope) => worksCurrentStage(p, scope),
  /** That, handed to us and not picked up yet. */
  NewForUs: (p, scope) => currentStages(p).some((s) => canWorkStage(p, s, scope) && awaitsPickUp(s)),
  /** My team's pathway, with a current stage owned by the lab. */
  AwaitingLab: (p, scope) => {
    const lab = labTeamId();
    return scope.teamIds.includes(p.teamId) && lab !== null && lab !== p.teamId && currentStages(p).some((s) => stageTeamOf(p, s) === lab);
  },
  /** My team's pathway, a current stage reached from a lab stage, not picked up yet. */
  BackFromLab: (p, scope) =>
    scope.teamIds.includes(p.teamId) && currentStages(p).some((s) => s.handedOverFromTeamId === labTeamId() && awaitsPickUp(s)),
};

/** Open and past its target date (pathways on hold are waiting on something else, so never overdue). */
export const isOverdue = (pathway: DemoPathway) => isOpen(pathway) && pathway.status !== 'Blocked' && pathway.dueDate < today();

function allowedStatusChanges(pathway: DemoPathway): string[] {
  switch (pathway.status) {
    case 'Unassigned':
    case 'Assigned':
    case 'InProgress':
      return ['Blocked', 'Cancelled'];
    case 'InReview':
      return ['Completed', 'Blocked', 'Cancelled'];
    case 'Blocked':
      return [pathway.blockedFromStatus ?? statusFromAssignments(pathway), 'Cancelled'];
    case 'Completed':
    case 'Cancelled':
      return ['Archived'];
    default:
      return [];
  }
}

const statusFromAssignments = (pathway: DemoPathway): DemoPathway['status'] =>
  pathway.stages.some((s) => s.startedAtUtc || isDone(s) || isSkipped(s))
    ? 'InProgress'
    : pathway.assignments.length > 0
      ? 'Assigned'
      : 'Unassigned';

function noteResponse(note: DemoNote, pathway: DemoPathway): Schemas['PatientPathwayNoteResponse'] {
  const viewer = me();
  const isMine = note.authorUserId === viewer.id;
  const takesNotes = !['Cancelled', 'Archived'].includes(pathway.status);
  const corrects = can('CorrectRecords');
  const works = can('WorkPathways') && canNoteOn(pathway);
  return {
    id: note.id,
    authorUserId: note.authorUserId,
    authorUsername: usernameOf(note.authorUserId),
    text: note.text,
    createdAtUtc: note.createdAtUtc,
    lastEditedAtUtc: note.lastEditedAtUtc,
    version: note.version,
    canEdit: works && isMine && takesNotes && !note.deleted,
    canDelete: corrects && !note.deleted,
    canRequestDeletion: !corrects && works && !note.deleted && !note.pendingRequestId,
    deletionRequestId: note.pendingRequestId,
  };
}

/** The template's addable branches (MC10), in the template's order. */
const addableBranchesOf = (pathway: DemoPathway) => db().templates.find((t) => t.id === pathway.templateId)?.addableBranches ?? [];

/**
 * Where a template's addable branch can be added now (MC10): after a template stage it may follow (any when it names
 * none), not skipped, while the pathway can be worked and has a stage still to do; nowhere once it's been added, unless
 * it repeats. Its stages run on their own route, which ends by itself (a pathway's routes may end anywhere).
 */
function afterStagesFor(pathway: DemoPathway, branch: Schemas['PathwayTemplateBranchResponse']): DemoStage[] {
  if (!canWorkOnStages(pathway) || !pathway.stages.some(isToDo)) return [];
  if (!branch.isRepeatable && (pathway.branchAdditions ?? []).some((a) => a.templateBranchId === branch.id)) return [];
  const allowed = branch.attachAfterStageOrders ?? [];
  return inOrder(pathway).filter(
    (s) => s.templateStageOrder !== null && !isSkipped(s) && (allowed.length === 0 || allowed.includes(s.templateStageOrder)),
  );
}

/** A pathway's addable branches as its page lists them: in the template's order, never suggested or pre-selected. */
function addableBranchResponses(pathway: DemoPathway, canWork: boolean): Schemas['PatientPathwayAddableBranchResponse'][] {
  return addableBranchesOf(pathway).map((branch) => ({
    templateBranchId: branch.id,
    name: branch.name,
    stageNames: (branch.stages ?? []).map((s) => s.name ?? ''),
    afterStageIds: canWork ? afterStagesFor(pathway, branch).map((s) => s.id) : [],
    isRepeatable: branch.isRepeatable ?? false,
    timesAdded: (pathway.branchAdditions ?? []).filter((a) => a.templateBranchId === branch.id).length,
  }));
}

/** A lab stage's case (MC16), for its page to link to: its open one, else its latest. */
function labCaseFields(stage: DemoStage) {
  const c = isLabsStage(stage) ? caseOfStage(stage.id) : undefined;
  return {labCaseId: c?.id ?? null, labCaseNumber: c?.number ?? null, labCaseStatus: c?.status ?? null};
}

/** A pathway as the API answers it, for the signed-in person. */
export function pathwayResponse(pathway: DemoPathway): Schemas['PatientPathwayResponse'] {
  // Only the pathway's own team changes it (the team walls): admins and the lab read it.
  const mine = canChangePathway(pathway);
  const canWork = can('WorkPathways') && mine;
  const currents = currentStages(pathway);
  const isCurrent = (stage: DemoStage) => currents.some((s) => s.id === stage.id);
  const scope = myScope();
  // The relay: the team working a current stage (the lab, for its stages) works that stage and adds notes too.
  const worksCurrent = worksCurrentStage(pathway, scope);
  const workable = can('WorkPathways') && (mine || worksCurrent) && canWorkOnStages(pathway);
  const notes = can('WorkPathways') && (mine || worksCurrent);
  const referral = pathway.referralId ? db().referrals.find((r) => r.id === pathway.referralId) : undefined;
  const addable = addableBranchResponses(pathway, canWork);
  return {
    id: pathway.id,
    patientId: pathway.patientId,
    patientName: patientName(pathway.patientId),
    patient: pathwayPatient(pathway.patientId),
    templateId: pathway.templateId,
    templateName: pathway.templateName,
    teamId: pathway.teamId,
    referral: referral ? pathwayReferralResponse(referral) : undefined,
    title: pathway.title,
    dueDate: pathway.dueDate,
    dueDateUtc: endOfUkDay(pathway.dueDate),
    priority: pathway.priority,
    status: pathway.status,
    blockedFromStatus: pathway.blockedFromStatus,
    isOverdue: isOverdue(pathway),
    createdAt: pathway.createdAt,
    lastUpdated: pathway.lastUpdated,
    currentStageId: currents[0]?.id ?? null,
    currentStageIds: currents.map((s) => s.id),
    completedStageCount: pathway.stages.filter(isDone).length,
    skippedStageCount: pathway.stages.filter(isSkipped).length,
    stageCount: pathway.stages.length,
    allowedStatusChanges: canWork ? allowedStatusChanges(pathway) : [],
    canWorkOnStages: workable,
    canEditDetails: canWork && !['Cancelled', 'Archived'].includes(pathway.status),
    canAddNotes: notes && !['Cancelled', 'Archived'].includes(pathway.status),
    canChangeAssignments: can('AssignPathways') && mine && isOpen(pathway),
    canBeDeleted: can('DeletePathways') && mine && ['Cancelled', 'Archived'].includes(pathway.status),
    readOnlyReason:
      mine || worksCurrent || scope.seesAllTeams
        ? null
        : readsThroughConsult(pathway, scope)
          ? consultReadOnly(pathway)
          : readsThroughMdt(pathway, scope)
            ? mdtReadOnly(pathway)
            : notYourTeams(pathway),
    patientFlags: NO_FLAGS,
    stages: inOrder(pathway).map((stage) => ({
      id: stage.id,
      stageDefinitionId: stage.stageDefinitionId,
      stageOrder: stage.stageOrder,
      name: stage.name,
      description: stage.description,
      instructions: null,
      visitRequirement: stage.visitRequirement,
      needsLetterKind: stage.needsLetterKind,
      needsConsent: stage.needsConsent,
      canRecordConsent: mine && canRecordConsent(pathway, stage),
      waitsForResults: stage.waitsForResults,
      notes: stage.notes,
      isOptional: stage.isOptional,
      choiceKind: stage.choiceKind,
      outcomes: outcomesOf(pathway, stage),
      outcome: stage.outcome,
      needsOutcome: stage.choiceKind !== null && stage.outcome === null,
      // The lab's results, from its case's authorised report: shown on the pathway (as a note is), never in the audit log.
      result: stage.result
        ? {text: stage.result.text, enteredByUsername: usernameOf(stage.result.enteredByUserId), enteredAtUtc: stage.result.enteredAtUtc}
        : null,
      ...labCaseFields(stage),
      predecessorIds: predecessorsOf(pathway, stage).map((s) => s.id),
      branchAdditionId: stage.branchAdditionId ?? null,
      state: isDone(stage) ? 'Completed' : isSkipped(stage) ? 'Skipped' : stage.startedAtUtc ? 'InProgress' : 'NotStarted',
      isCurrent: isCurrent(stage),
      isLocked: isDone(stage) && !stage.unlocked,
      canEdit: workable && canWorkStage(pathway, stage, scope) && (isCurrent(stage) || (isDone(stage) && stage.unlocked)),
      canUnlock: can('UnlockStages') && mine && isDone(stage) && !stage.unlocked && (isOpen(pathway) || pathway.status === 'Completed'),
      canSkip: can('WorkPathways') && canWorkStage(pathway, stage, scope) && whySkipNotAllowed(pathway, stage) === null,
      canReinstate: can('UnlockStages') && mine && isSkipped(stage) && (isOpen(pathway) || pathway.status === 'Completed'),
      skippedAtUtc: stage.skippedAtUtc,
      skippedByUserId: stage.skippedByUserId,
      skippedByUsername: usernameOf(stage.skippedByUserId),
      skipReason: stage.skipReason,
      skipDetail: stage.skipDetail,
      canRecordSpecimen: canRecordSpecimen(pathway, stage),
      startedAtUtc: stage.startedAtUtc,
      startedByUserId: stage.startedByUserId,
      startedByUsername: usernameOf(stage.startedByUserId),
      completedAtUtc: stage.completedAtUtc,
      completedByUserId: stage.completedByUserId,
      completedByUsername: usernameOf(stage.completedByUserId),
      // Null for the pathway's own team's stages; the owner (the lab) for the rest.
      ownerTeamId: stage.ownerTeamId,
      ownerTeamName: teamName(stage.ownerTeamId),
      isWithAnotherTeam: isCurrent(stage) && stageTeamOf(pathway, stage) !== pathway.teamId && !canWorkStage(pathway, stage, scope),
      reachedAtUtc: stage.reachedAtUtc,
      handedOverFromTeamName: teamName(stage.handedOverFromTeamId),
      pickedUpAtUtc: stage.pickedUpAtUtc,
      pickedUpByUserId: stage.pickedUpByUserId,
      pickedUpByUsername: usernameOf(stage.pickedUpByUserId),
      canPickUp: workable && isCurrent(stage) && canWorkStage(pathway, stage, scope) && awaitsPickUp(stage),
    })),
    assignments: pathway.assignments.map((a) => ({
      userId: a.userId,
      username: usernameOf(a.userId),
      assignedByUserId: a.assignedByUserId,
      assignedByUsername: usernameOf(a.assignedByUserId),
      assignedAtUtc: a.assignedAtUtc,
    })),
    notes: null,
    noteList: pathway.notes
      .filter((note) => !note.deleted)
      .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))
      .map((note) => noteResponse(note, pathway)),
    canAddBranch: addable.some((b) => (b.afterStageIds ?? []).length > 0),
    addableBranches: addable,
    // The reason is the clinician's own words: on the pathway (as a note is), never in the audit log.
    branchAdditions: (pathway.branchAdditions ?? []).map((a) => ({
      id: a.id,
      templateBranchId: a.templateBranchId,
      name: a.name,
      afterStageId: a.afterStageId,
      stageIds: a.stageIds,
      reason: a.reason,
      addedByUsername: usernameOf(a.addedByUserId),
      addedAtUtc: a.addedAtUtc,
    })),
  };
}

/** A pathway in a list (`PatientPathwaySummaryResponse`). */
export function pathwaySummary(pathway: DemoPathway): Schemas['PatientPathwaySummaryResponse'] {
  const referral = pathway.referralId ? db().referrals.find((r) => r.id === pathway.referralId) : undefined;
  const currents = currentStages(pathway);
  return {
    id: pathway.id,
    patientId: pathway.patientId,
    patientName: patientName(pathway.patientId),
    title: pathway.title,
    templateName: pathway.templateName,
    dueDate: pathway.dueDate,
    dueDateUtc: endOfUkDay(pathway.dueDate),
    priority: pathway.priority,
    status: pathway.status,
    isOverdue: isOverdue(pathway),
    completedStageCount: pathway.stages.filter(isDone).length,
    skippedStageCount: pathway.stages.filter(isSkipped).length,
    stageCount: pathway.stages.length,
    // Every current stage, so a route side by side isn't missed in a list (H-69); and who has them (the relay).
    currentStageName: andList(currents.map((s) => s.name)) || null,
    currentStageTeamName: andList([...new Set(currents.map((s) => teamName(stageTeamOf(pathway, s)) ?? ''))]) || null,
    awaitingPickUp: currents.some(awaitsPickUp),
    assignees: pathway.assignments.map((a) => usernameOf(a.userId) ?? ''),
    lastUpdated: pathway.lastUpdated,
    clock: referral ? referralSummaryClock(referral) : undefined,
  };
}

/** The pathway, or a 404: also when it's behind the viewer's team wall (`teamWalls.ts`). */
export function findPathway(id: string): DemoPathway {
  const pathway = db().pathways.find((p) => p.id === id);
  if (!pathway) refuse(notFound('The pathway'));
  if (!canSeePathway(pathway)) refuseAcrossWall('Pathway', id, 'The pathway');
  return pathway;
}

/** A pathway to change: visible (else a 404), and its own team's (else a 409: the lab and admins read it). */
export function pathwayToChange(id: string): DemoPathway {
  const pathway = findPathway(id);
  ensureCanChangePathway(pathway);
  return pathway;
}

/** A pathway to add a note to: its own team's, or one whose current stage the viewer's team works (the relay). */
function pathwayToNote(id: string): DemoPathway {
  const pathway = findPathway(id);
  if (!canNoteOn(pathway)) refuse(problem(409, notYourTeams(pathway)));
  return pathway;
}

/**
 * Work on a stage: the pathway visible and open, and the stage the viewer's team's to work (the relay: the lab's stages
 * are the lab's, the rest the pathway's own team's).
 */
function forStageWork(id: string, stageId: string): {pathway: DemoPathway; stage: DemoStage} {
  requirePermission('WorkPathways');
  const pathway = findPathway(id);
  const stage = pathway.stages.find((s) => s.id === stageId);
  if (!stage) refuse(notFound('The stage'));
  if (!canChangePathway(pathway) && !canWorkStage(pathway, stage)) refuse(problem(409, notYourTeams(pathway)));
  ensureCanWorkStage(pathway, stage);
  if (!canWorkOnStages(pathway)) refuse(problem(409, 'This pathway can’t be worked on while it’s on hold or closed.'));
  return {pathway, stage};
}

function touch(pathway: DemoPathway) {
  pathway.lastUpdated = nowUtc();
}

/** Starts the pathway when its first stage is started, finished or skipped. */
function markStarted(pathway: DemoPathway) {
  if (pathway.status === 'Unassigned' || pathway.status === 'Assigned') pathway.status = 'InProgress';
}

/** After a stage is finished or skipped: every stage done puts the pathway in review. */
function afterStageDone(pathway: DemoPathway) {
  markStarted(pathway);
  if (allFinished(pathway) && pathway.status === 'InProgress') pathway.status = 'InReview';
}

/** After a stage is reopened (unlocked or reinstated): a pathway in review or discharged is in progress again. */
function afterStageReopened(pathway: DemoPathway, userId: string) {
  if (pathway.status === 'InReview' || pathway.status === 'Completed') {
    if (pathway.status === 'Completed') pathway.statusChanges.push({atUtc: nowUtc(), userId, from: 'Completed', to: 'InProgress'});
    pathway.status = 'InProgress';
  }
}

function skipStage(stage: DemoStage, userId: string, reason: string, detail: string | null) {
  stage.skippedAtUtc = nowUtc();
  stage.skippedByUserId = userId;
  stage.skipReason = reason;
  stage.skipDetail = detail;
}

/**
 * Authorising a lab case's report (MC16) finishes its lab stage in the same change, with the results on it, and hands
 * the pathway on (`StageFinished`, then `StageHandedOver` back to the clinic). The gates aren't asked: the report is
 * what the stage waits for. A stage not current (the pathway on hold, or the stage before it unfinished) keeps the
 * results, and the lab finishes it by hand later (its case no longer open).
 */
export function finishLabStage(pathwayId: string, stageId: string, userId: string, text: string, atUtc: string) {
  const pathway = db().pathways.find((p) => p.id === pathwayId);
  const stage = pathway?.stages.find((s) => s.id === stageId);
  if (!pathway || !stage || !isLabsStage(stage) || !isToDo(stage)) return;
  stage.result = {text, enteredByUserId: userId, enteredAtUtc: atUtc};
  touch(pathway);
  if (!isCurrentStage(pathway, stage)) return;
  stage.startedAtUtc ??= atUtc;
  stage.startedByUserId ??= userId;
  stage.completedAtUtc = atUtc;
  stage.completedByUserId = userId;
  pickUp(stage, userId);
  afterStageDone(pathway);
  reachNext(pathway, stageTeamOf(pathway, stage), userId);
}

/** Receiving a lab stage's first sample picks the stage up (MC16), when it's current and waiting to be. */
export function pickUpLabStage(pathwayId: string, stageId: string, userId: string) {
  const pathway = db().pathways.find((p) => p.id === pathwayId);
  const stage = pathway?.stages.find((s) => s.id === stageId);
  if (!pathway || !stage || !isCurrentStage(pathway, stage)) return;
  pickUp(stage, userId);
  touch(pathway);
}

/**
 * An insufficient sample (MC16): the finished stage that took it reopens (as Unlock does), so the clinic takes the
 * repeat there and finishes it again; the lab stage stays with the lab. Returns the stage's name, or null when there's
 * none to reopen (it's still to do, or the pathway is closed).
 */
export function reopenSampleStage(pathwayId: string | null, stageId: string | null): string | null {
  const pathway = db().pathways.find((p) => p.id === pathwayId);
  const stage = pathway?.stages.find((s) => s.id === stageId);
  if (!pathway || !stage || !isDone(stage) || isLabsStage(stage) || !canWorkOnStages(pathway)) return null;
  stage.unlocked = true;
  touch(pathway);
  return stage.name;
}

/** Makes a new pathway for a patient from a template (New Pathway, and accepting a referral). */
export function createPathway(input: {
  patientId: string;
  templateId: string;
  title?: string | null;
  dueDate?: string | null;
  priority?: string | null;
  notes?: string | null;
  assigneeUserId?: string | null;
  referralId?: string | null;
}): DemoPathway {
  const viewer = me();
  const template = db().templates.find((t) => t.id === input.templateId);
  if (!template) refuse(notFound('The pathway template'));
  const now = nowUtc();
  const priority = (['Low', 'Medium', 'High', 'Urgent'].includes(input.priority ?? '') ? input.priority : 'Medium') as DemoPathway['priority'];
  const due = input.dueDate || new Date(Date.now() + 12 * 7 * 86400_000).toISOString().slice(0, 10);
  const pathway: DemoPathway = {
    id: newId(),
    patientId: input.patientId,
    templateId: template.id!,
    templateName: template.name!,
    teamId: template.teamId!,
    referralId: input.referralId ?? null,
    title: input.title?.trim() || template.name!,
    dueDate: due,
    priority,
    status: input.assigneeUserId ? 'Assigned' : 'Unassigned',
    blockedFromStatus: null,
    createdAt: now,
    lastUpdated: now,
    stages: [],
    links: [],
    plan: planOf(template, db().stageDefinitions),
    assignments: input.assigneeUserId ? [{userId: input.assigneeUserId, assignedByUserId: viewer.id, assignedAtUtc: now}] : [],
    handovers: [],
    notes: input.notes?.trim()
      ? [
          {
            id: newId(),
            authorUserId: viewer.id,
            text: input.notes.trim(),
            createdAtUtc: now,
            lastEditedAtUtc: null,
            version: 1,
            deleted: null,
            pendingRequestId: null,
          },
        ]
      : [],
    statusChanges: [],
  };
  // It starts with the stages up to its first choice node (the owner's model): the rest grow as outcomes are recorded.
  growPathway(pathway, () => newId());
  db().pathways.push(pathway);
  db().pathwayAccess.push(ownerAccess(pathway));
  // Its first stage is reached now: handed over at once if another team owns it.
  reachNext(pathway, pathway.teamId, viewer.id);
  template.pathwayCount = (template.pathwayCount ?? 0) + 1;
  if (input.assigneeUserId && input.assigneeUserId !== viewer.id)
    db().notifications.push({
      id: newId(),
      userId: input.assigneeUserId,
      kind: 'PathwayAssigned',
      title: pathway.title,
      message: `${viewer.username} assigned you to this pathway.`,
      pathwayId: pathway.id,
      createdAtUtc: now,
      isRead: false,
    });
  return pathway;
}

export function registerPathways() {
  on('get', '/api/pathways/patient-pathways/search', ({query}) => {
    const viewer = me();
    const statuses = list(query, 'Statuses');
    const priorities = [...list(query, 'Priorities'), ...list(query, 'Priority')];
    const teamIds = list(query, 'TeamIds');
    const stageDefinitionIds = list(query, 'StageDefinitionIds');
    const includeClosed = flag(query, 'IncludeClosed') === true || statuses.some((s) => ['Completed', 'Cancelled', 'Archived'].includes(s));
    const started = flag(query, 'Started');
    const search = query.get('Search');
    const scope = myScope();
    const picked = pickedTeam(query, scope);
    const view = VIEWS[query.get('View') ?? ''];
    if (query.get('View') && !view) return invalid({View: ['Choose OurTurn, NewForUs, AwaitingLab or BackFromLab.']});
    const items = db().pathways.filter(
      (p) =>
        canSeePathway(p, scope) &&
        isPickedTeam(p.teamId, picked) &&
        (!view || view(p, scope)) &&
        (includeClosed || isOpen(p)) &&
        (statuses.length === 0 || statuses.includes(p.status)) &&
        (priorities.length === 0 || priorities.includes(p.priority)) &&
        (teamIds.length === 0 || teamIds.includes(p.teamId)) &&
        (stageDefinitionIds.length === 0 || currentStages(p).some((s) => stageDefinitionIds.includes(s.stageDefinitionId))) &&
        (!query.get('PatientId') || p.patientId === query.get('PatientId')) &&
        (!query.get('TemplateId') || p.templateId === query.get('TemplateId')) &&
        (flag(query, 'AssignedToMe') !== true || p.assignments.some((a) => a.userId === viewer.id)) &&
        (!query.get('AssignedToUserId') || p.assignments.some((a) => a.userId === query.get('AssignedToUserId'))) &&
        (flag(query, 'NobodyAssigned') !== true || p.assignments.length === 0) &&
        (flag(query, 'OverdueOnly') !== true || isOverdue(p)) &&
        (!query.get('DueFrom') || p.dueDate >= query.get('DueFrom')!) &&
        (!query.get('DueTo') || p.dueDate <= query.get('DueTo')!) &&
        (started === undefined || started === p.stages.some((s) => s.startedAtUtc || isDone(s))) &&
        contains(p.title, query.get('Title')) &&
        contains(p.templateName, query.get('TemplateName')) &&
        contains(patientName(p.patientId), query.get('PatientName')) &&
        (contains(p.title, search) || contains(patientName(p.patientId), search) || contains(p.templateName, search)),
    );
    const order = {Low: 0, Medium: 1, High: 2, Urgent: 3};
    const list_ = sorted(
      items.map(pathwaySummary),
      query,
      {
        title: (p) => (p.title ?? '').toLowerCase(),
        dueDate: (p) => p.dueDate ?? '',
        priority: (p) => order[(p.priority ?? 'Low') as keyof typeof order],
        status: (p) => p.status ?? '',
        patient: (p) => (p.patientName ?? '').toLowerCase(),
        progress: (p) => (p.completedStageCount ?? 0) / Math.max(1, p.stageCount ?? 1),
        lastUpdated: (p) => p.lastUpdated ?? '',
        daysWaited: (p) => p.clock?.daysWaited ?? 0,
      },
      'dueDate',
    );
    return paged(list_, query);
  });

  on('get', '/api/pathways/patient-pathways/labels', ({query}) => {
    me();
    const ids = list(query, 'ids');
    const scope = myScope();
    return db()
      .pathways.filter((p) => ids.includes(p.id) && canSeePathway(p, scope))
      .map((p) => ({id: p.id, title: p.title, patientName: patientName(p.patientId)}));
  });

  on('get', '/api/pathways/patient-pathways/{id}', ({params}) => {
    me();
    return pathwayResponse(findPathway(params.id));
  });

  on('post', '/api/pathways/patient-pathways', ({body}) => {
    requirePermission('CreatePathways');
    if (!body.patientId || !body.templateId)
      return invalid({templateId: ['Choose a template.']});
    // A pathway takes its template's team: staff start pathways in their own team only (the team walls).
    const template = db().templates.find((t) => t.id === body.templateId);
    if (template && !myScope().teamIds.includes(template.teamId ?? ''))
      return invalid({templateId: [`The '${template.name}' template is another team’s. Choose one of your team’s.`]});
    return pathwayResponse(createPathway({...body, patientId: body.patientId, templateId: body.templateId}));
  });

  on('patch', '/api/pathways/patient-pathways/{id}', ({params, body}) => {
    requirePermission('WorkPathways');
    const pathway = pathwayToChange(params.id);
    if (body.title?.trim()) pathway.title = body.title.trim();
    if (body.dueDate) pathway.dueDate = body.dueDate;
    if (body.priority && ['Low', 'Medium', 'High', 'Urgent'].includes(body.priority)) pathway.priority = body.priority as DemoPathway['priority'];
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/start', ({params}) => {
    const viewer = me();
    const {pathway, stage} = forStageWork(params.id, params.stageId);
    if (!isCurrentStage(pathway, stage)) return problem(409, 'Only a current stage can be started.');
    stage.startedAtUtc = nowUtc();
    stage.startedByUserId = viewer.id;
    pickUp(stage, viewer.id);
    markStarted(pathway);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  // Picking up a stage handed to the viewer's team (the relay): it leaves New for us. Again changes nothing.
  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/pick-up', ({params}) => {
    const viewer = me();
    const {pathway, stage} = forStageWork(params.id, params.stageId);
    if (!isCurrentStage(pathway, stage)) return problem(409, 'Only a current stage can be picked up.');
    if (stage.handedOverFromTeamId === null) return problem(409, 'This stage wasn’t handed over, so there’s nothing to pick up.');
    pickUp(stage, viewer.id);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('put', '/api/pathways/patient-pathways/{id}/stages/{stageId}/notes', ({params, body}) => {
    const {pathway, stage} = forStageWork(params.id, params.stageId);
    stage.notes = body.notes ?? null;
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/finish', ({params, body}) => {
    const viewer = me();
    const {pathway, stage} = forStageWork(params.id, params.stageId);
    const reopened = isDone(stage) && stage.unlocked;
    if (!isCurrentStage(pathway, stage) && !reopened) return problem(409, 'Only a current stage can be finished.');
    // A choice node's outcome is recorded as it's first finished, by the clinician: never by MedApp, none pre-selected.
    const outcomes = outcomesOf(pathway, stage);
    const named = body.outcome?.trim() ?? '';
    const chosen = named ? outcomes.find((o) => (o.name ?? '').toLowerCase() === named.toLowerCase()) : undefined;
    if (stage.choiceKind && stage.outcome === null && !chosen)
      return invalid({outcome: [`Record the outcome: ${outcomes.map((o) => o.name).join(', ')}.`]});
    if (named && (!stage.choiceKind || stage.outcome !== null))
      return invalid({outcome: ['The outcome was recorded already: it isn’t changed by finishing the stage again.']});
    if (!reopened) {
      const attended = db().appointments.some((a) => a.stageId === stage.id && a.status === 'Attended');
      if (stage.visitRequirement !== 'None' && !attended && body.withoutVisit !== true)
        return problem(409, 'This stage involves a visit, and none was attended. Finish it without a visit, giving a reason.');
    }
    const gated = whyGated(pathway, stage);
    if (gated) return problem(409, gated);
    const now = nowUtc();
    stage.startedAtUtc ??= now;
    stage.startedByUserId ??= viewer.id;
    stage.completedAtUtc = now;
    stage.completedByUserId = viewer.id;
    stage.unlocked = false;
    if (chosen) {
      stage.outcome = chosen.name ?? named;
      // That outcome's stages join the pathway, up to the next choice node; the others never appear.
      growPathway(pathway, () => newId());
    }
    pickUp(stage, viewer.id);
    afterStageDone(pathway);
    reachNext(pathway, stageTeamOf(pathway, stage), viewer.id);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  // A template's addable branch, added on new information (MC10): only from the template's list, with a reason, never
  // suggested. Its stages run after the stage chosen, side by side with what's left, on a route that ends by itself.
  on('post', '/api/pathways/patient-pathways/{id}/branches', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    // Only the pathway's own team adds a branch (the lab works its stages, but the route is the clinic's choice).
    const pathway = pathwayToChange(params.id);
    if (!canWorkOnStages(pathway)) return problem(409, 'This pathway can’t be worked on while it’s on hold or closed.');
    const reason = body.reason?.trim() ?? '';
    if (!reason) return invalid({reason: ['Say why the branch is being added.']});
    if (reason.length > MAX_BRANCH_REASON_LENGTH) return invalid({reason: [`At most ${MAX_BRANCH_REASON_LENGTH} characters.`]});
    const branch = addableBranchesOf(pathway).find((b) => b.id === body.templateBranchId);
    if (!branch) return invalid({templateBranchId: ['Choose one of the template’s branches.']});
    const after = pathway.stages.find((s) => s.id === body.afterStageId);
    if (!after) return invalid({afterStageId: ['Choose the stage it comes after.']});
    if (!afterStagesFor(pathway, branch).some((s) => s.id === after.id))
      return problem(409, `The '${branch.name}' branch can’t be added after the '${after.name}' stage.`);
    const additionId = newId();
    const last = Math.max(...pathway.stages.map((s) => s.stageOrder));
    const added = (branch.stages ?? []).map((spec, i) => addedStage(pathway, findDefinition(spec.stageDefinitionId ?? ''), last + 1 + i, additionId));
    pathway.stages.push(...added);
    // A chain after the chosen stage, on a route of its own that ends by itself: discharge waits for it (every stage done).
    added.forEach((stage, i) => pathway.links.push({fromStageId: (i === 0 ? after : added[i - 1]).id, toStageId: stage.id}));
    pathway.branchAdditions = [
      ...(pathway.branchAdditions ?? []),
      {
        id: additionId,
        templateBranchId: branch.id ?? '',
        name: branch.name ?? '',
        afterStageId: after.id,
        stageIds: added.map((s) => s.id),
        reason,
        addedByUserId: viewer.id,
        addedAtUtc: nowUtc(),
      },
    ];
    // Added after a finished stage, its first stage is current at once: reached, and handed over if another team's.
    reachNext(pathway, stageTeamOf(pathway, after), viewer.id);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/skip', ({params, body}) => {
    const viewer = me();
    const {pathway, stage} = forStageWork(params.id, params.stageId);
    const why = whySkipNotAllowed(pathway, stage);
    if (why) return problem(409, why);
    if (!SKIP_REASONS.includes(body.reason ?? '')) return invalid({reason: ['Choose why it’s being skipped.']});
    const detail = body.detail?.trim() || null;
    if (!detail && (body.reason === 'Other' || stage.visitRequirement === 'Required'))
      return invalid({detail: ['Say why it’s being skipped.']});
    skipStage(stage, viewer.id, body.reason!, detail);
    db().appointments.forEach((a) => {
      if (a.stageId === stage.id && a.status === 'Requested') a.status = 'Cancelled';
    });
    afterStageDone(pathway);
    reachNext(pathway, stageTeamOf(pathway, stage), viewer.id);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/reinstate', ({params, body}) => {
    const viewer = me();
    requirePermission('UnlockStages');
    const pathway = pathwayToChange(params.id);
    const stage = pathway.stages.find((s) => s.id === params.stageId);
    if (!stage) return notFound('The stage');
    if (!isSkipped(stage) || !(isOpen(pathway) || pathway.status === 'Completed'))
      return problem(409, 'Only a skipped stage of an open or discharged pathway can be reinstated.');
    if (!body.reason?.trim()) return invalid({reason: ['Say why it’s being reinstated.']});
    stage.skippedAtUtc = null;
    stage.skippedByUserId = null;
    stage.skipReason = null;
    stage.skipDetail = null;
    afterStageReopened(pathway, viewer.id);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/unlock', ({params}) => {
    const viewer = me();
    requirePermission('UnlockStages');
    const pathway = pathwayToChange(params.id);
    const stage = pathway.stages.find((s) => s.id === params.stageId);
    if (!stage) return notFound('The stage');
    if (!isDone(stage) || stage.unlocked || !(isOpen(pathway) || pathway.status === 'Completed'))
      return problem(409, 'Only a finished, locked stage of an open or discharged pathway can be unlocked.');
    stage.unlocked = true;
    afterStageReopened(pathway, viewer.id);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('delete', '/api/pathways/patient-pathways/{id}', ({params}) => {
    requirePermission('DeletePathways');
    const pathway = pathwayToChange(params.id);
    if (!['Cancelled', 'Archived'].includes(pathway.status))
      return problem(409, 'Only a cancelled or archived pathway can be deleted.');
    const state = db();
    state.pathways = state.pathways.filter((p) => p.id !== pathway.id);
    state.appointments = state.appointments.filter((a) => a.pathwayId !== pathway.id);
    const template = state.templates.find((t) => t.id === pathway.templateId);
    if (template) template.pathwayCount = Math.max(0, (template.pathwayCount ?? 1) - 1);
    return undefined;
  });

  on('post', '/api/pathways/patient-pathways/{id}/status', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    const pathway = pathwayToChange(params.id);
    const to = body.status as DemoPathway['status'];
    if (!allowedStatusChanges(pathway).includes(to)) return problem(409, 'That change of status isn’t allowed from here.');
    if ((to === 'Blocked' || to === 'Cancelled') && !body.reason?.trim())
      return invalid({reason: ['Give a reason.']});
    pathway.statusChanges.push({atUtc: nowUtc(), userId: viewer.id, from: pathway.status, to});
    pathway.blockedFromStatus = to === 'Blocked' ? pathway.status : null;
    pathway.status = to;
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/assignments/{userId}', ({params}) => {
    const viewer = me();
    requirePermission('AssignPathways');
    const pathway = pathwayToChange(params.id);
    if (!personById(params.userId)) return notFound('The member of staff');
    if (!pathway.assignments.some((a) => a.userId === params.userId))
      pathway.assignments.push({userId: params.userId, assignedByUserId: viewer.id, assignedAtUtc: nowUtc()});
    if (pathway.status === 'Unassigned') pathway.status = 'Assigned';
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('delete', '/api/pathways/patient-pathways/{id}/assignments/{userId}', ({params}) => {
    requirePermission('AssignPathways');
    const pathway = pathwayToChange(params.id);
    pathway.assignments = pathway.assignments.filter((a) => a.userId !== params.userId);
    if (pathway.status === 'Assigned' && pathway.assignments.length === 0) pathway.status = 'Unassigned';
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/notes', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    const pathway = pathwayToNote(params.id);
    if (!body.text?.trim()) return invalid({text: ['Write the note.']});
    const note: DemoNote = {
      id: newId(),
      authorUserId: viewer.id,
      text: body.text.trim(),
      createdAtUtc: nowUtc(),
      lastEditedAtUtc: null,
      version: 1,
      deleted: null,
      pendingRequestId: null,
    };
    pathway.notes.push(note);
    touch(pathway);
    return noteResponse(note, pathway);
  });

  on('put', '/api/pathways/patient-pathways/{id}/notes/{noteId}', ({params, body}) => {
    const viewer = me();
    const pathway = pathwayToNote(params.id);
    const note = pathway.notes.find((n) => n.id === params.noteId);
    if (!note) return notFound('The note');
    if (note.authorUserId !== viewer.id) return problem(403, 'Only a note’s author edits it.');
    if (body.expectedVersion !== undefined && body.expectedVersion !== note.version)
      return problem(409, 'Someone saved this note first. Reload to see their changes.');
    note.text = body.text?.trim() ?? note.text;
    note.lastEditedAtUtc = nowUtc();
    note.version++;
    return noteResponse(note, pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/notes/{noteId}/delete', ({params, body}) => {
    const viewer = me();
    requirePermission('CorrectRecords');
    const pathway = findPathway(params.id);
    const note = pathway.notes.find((n) => n.id === params.noteId);
    if (!note) return notFound('The note');
    if (note.deleted) return problem(409, 'This note has been deleted already.');
    if (body.expectedVersion !== undefined && body.expectedVersion !== note.version)
      return problem(409, 'This note has been edited since you opened it. Reload to see the change.');
    const why = whyNoteDeletionInvalid(body.reason, body.detail);
    if (why) return why;
    deleteNote(pathway, note, viewer.id, body.reason!, body.detail?.trim() || null);
    return undefined;
  });

  on('post', '/api/pathways/patient-pathways/{id}/notes/{noteId}/deletion-requests', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    const pathway = pathwayToChange(params.id);
    const note = pathway.notes.find((n) => n.id === params.noteId);
    if (!note || note.deleted) return notFound('The note');
    if (note.pendingRequestId) return problem(409, 'Someone has asked for this note to be deleted already.');
    const why = whyNoteDeletionInvalid(body.reason, body.detail);
    if (why) return why;
    const request = {
      id: newId(),
      pathwayId: pathway.id,
      noteId: note.id,
      status: 'Pending' as const,
      reason: body.reason!,
      detail: body.detail?.trim() || null,
      requestedByUserId: viewer.id,
      requestedAtUtc: nowUtc(),
      decidedByUserId: null,
      decidedAtUtc: null,
    };
    db().noteDeletionRequests.push(request);
    note.pendingRequestId = request.id;
    // Everyone who can correct records is told (a fixed title; the message names nobody's text).
    for (const person of PEOPLE.filter((p) => p.permissions.includes('CorrectRecords') && p.id !== viewer.id))
      db().notifications.push({
        id: newId(),
        userId: person.id,
        kind: 'NoteDeletionRequested',
        title: 'Note deletion requested',
        message: `${viewer.username} asked for a note to be deleted.`,
        pathwayId: pathway.id,
        noteDeletionRequestId: request.id,
        createdAtUtc: request.requestedAtUtc,
        isRead: false,
      });
    return new DemoReply(201, noteResponse(note, pathway));
  });

  on('get', '/api/pathways/note-deletion-requests/{id}', ({params}) => {
    requirePermission('CorrectRecords');
    return noteDeletionRequestResponse(findNoteDeletionRequest(params.id));
  });

  on('post', '/api/pathways/note-deletion-requests/{id}/decision', ({params, body}) => {
    const viewer = me();
    requirePermission('CorrectRecords');
    const request = findNoteDeletionRequest(params.id);
    const pathway = findPathway(request.pathwayId);
    const note = pathway.notes.find((n) => n.id === request.noteId);
    if (request.status !== 'Pending' || !note || note.deleted) return problem(409, 'This request has been decided already.');
    request.decidedByUserId = viewer.id;
    request.decidedAtUtc = nowUtc();
    note.pendingRequestId = null;
    if (body.deleteNote === true) {
      if (body.expectedVersion != null && body.expectedVersion !== note.version)
        return problem(409, 'This note has been edited since you opened it. Reload to see the change.');
      request.status = 'Approved';
      deleteNote(pathway, note, viewer.id, body.reason || request.reason, body.detail?.trim() || request.detail);
    } else request.status = 'Declined';
    return noteDeletionRequestResponse(request);
  });

  registerSetupOfPathways();

  on('get', '/api/pathways/insights', ({query}) => insights(Number(query.get('Weeks')) || 12, query.get('TemplateId'), pickedTeam(query)));
}

/**
 * Insights: this week's counts come from the demo's pathways; the history (last week, stage times, throughput) is
 * made up, deterministically, so the charts have something to show. The full version reads it from the audit log.
 */
function insights(weeks: number, templateId: string | null, teamId: string | null): Schemas['PathwayInsightsResponse'] {
  me();
  // Only the viewer's teams (an admin's: every team, or the one picked), as the API's `InsightsScope.TeamIds`.
  const counted = insightsTeams(teamId);
  const pathways = db().pathways.filter((p) => (!templateId || p.templateId === templateId) && counted(p.teamId));
  const open = pathways.filter(isOpen);
  const inProgress = pathways.filter((p) => p.status === 'InProgress');
  const overdue = pathways.filter(isOverdue);
  const weekAgo = new Date(Date.now() - 7 * 86400_000).toISOString();
  const completed = pathways.filter((p) => p.statusChanges.some((c) => c.to === 'Completed' && c.atUtc >= weekAgo));
  const byAssignee = new Map<string, number>();
  for (const p of overdue) for (const a of p.assignments) byAssignee.set(a.userId, (byAssignee.get(a.userId) ?? 0) + 1);
  const monday = new Date(`${today()}T00:00:00Z`);
  monday.setUTCDate(monday.getUTCDate() - ((monday.getUTCDay() + 6) % 7));
  return {
    weeks,
    templateId,
    teamId,
    from: new Date(monday.getTime() - (weeks - 1) * 7 * 86400_000).toISOString().slice(0, 10),
    to: today(),
    open: {current: open.length, lastWeek: open.length + 1},
    inProgress: {current: inProgress.length, lastWeek: Math.max(0, inProgress.length - 1)},
    overdue: {current: overdue.length, lastWeek: overdue.length + 1},
    completedThisWeek: {current: completed.length, lastWeek: 2},
    overdueByAssignee: [...byAssignee].map(([userId, count]) => ({userId, username: usernameOf(userId), count})),
    overdueByPriority: (['Urgent', 'High', 'Medium', 'Low'] as const)
      .map((priority) => ({priority, count: overdue.filter((p) => p.priority === priority).length}))
      .filter((row) => row.count > 0),
    stageTimes: db()
      .templates.filter((t) => (!templateId || t.id === templateId) && !!t.teamId && counted(t.teamId))
      .map((template, t) => ({
        templateId: template.id,
        templateName: template.name,
        stages: (template.stages ?? []).map((stage, i) => ({
          stageOrder: stage.stageOrder,
          stageName: stage.name,
          finishedCount: 4 + ((t * 7 + i * 3) % 9),
          averageWaitingHours: 24 * (2 + ((t * 5 + i * 7) % 11)),
          averageWorkingHours: stage.visitRequirement === 'None' ? 24 * (3 + ((i + t) % 6)) : 1 + ((i * 2 + t) % 4),
        })),
      })),
    throughput: Array.from({length: weeks}, (_, i) => ({
      weekStart: new Date(monday.getTime() - (weeks - 1 - i) * 7 * 86400_000).toISOString().slice(0, 10),
      created: 3 + ((i * 7) % 5),
      completed: 2 + ((i * 5 + 3) % 5),
    })),
  };
}

const NOTE_DELETION_REASONS = ['WrongPatient', 'WrittenInError', 'Duplicate', 'Other'];

/** A note deletion's reason and detail, refused as the API's validator does, or null when they'll do. */
function whyNoteDeletionInvalid(reason: string | null | undefined, detail: string | null | undefined): DemoReply | null {
  if (!NOTE_DELETION_REASONS.includes(reason ?? '')) return invalid({reason: ['Choose why the note should be deleted.']});
  if (reason === 'Other' && !detail?.trim()) return invalid({detail: ['Say why the note should be deleted.']});
  return null;
}

/** Deletes a note (soft: hidden from the pathway, kept), and tells its author (`AuthorsNoteDeleted`). */
function deleteNote(pathway: DemoPathway, note: DemoNote, userId: string, reason: string, detail: string | null) {
  note.deleted = {atUtc: nowUtc(), byUserId: userId, reason, detail};
  note.pendingRequestId = null;
  if (note.authorUserId !== userId)
    db().notifications.push({
      id: newId(),
      userId: note.authorUserId,
      kind: 'AuthorsNoteDeleted',
      title: 'Your note was deleted',
      message: `${usernameOf(userId)} deleted a note you wrote.`,
      pathwayId: pathway.id,
      createdAtUtc: note.deleted.atUtc,
      isRead: false,
    });
}

function findNoteDeletionRequest(id: string) {
  const request = db().noteDeletionRequests.find((r) => r.id === id);
  if (!request) refuse(notFound('The request'));
  return request;
}

function noteDeletionRequestResponse(request: DemoNoteDeletionRequest): Schemas['NoteDeletionRequestResponse'] {
  const pathway = db().pathways.find((p) => p.id === request.pathwayId);
  const note = pathway?.notes.find((n) => n.id === request.noteId);
  return {
    id: request.id,
    status: request.status,
    reason: request.reason,
    detail: request.detail,
    requestedByUsername: usernameOf(request.requestedByUserId),
    requestedAtUtc: request.requestedAtUtc,
    decidedByUsername: usernameOf(request.decidedByUserId),
    decidedAtUtc: request.decidedAtUtc,
    pathwayId: request.pathwayId,
    pathwayTitle: pathway?.title ?? null,
    patient: pathway ? pathwayPatient(pathway.patientId) : undefined,
    note: note
      ? {
          id: note.id,
          text: note.text,
          authorUsername: usernameOf(note.authorUserId),
          createdAtUtc: note.createdAtUtc,
          lastEditedAtUtc: note.lastEditedAtUtc,
          version: note.version,
          deletedAtUtc: note.deleted?.atUtc ?? null,
          deletedByUsername: usernameOf(note.deleted?.byUserId),
          deletionReason: note.deleted?.reason ?? null,
          deletionDetail: note.deleted?.detail ?? null,
        }
      : undefined,
    canDecide: request.status === 'Pending' && note !== undefined && !note.deleted,
    whyCantDecide: null,
  };
}

const VISITS = ['None', 'Suggested', 'Required'];
const LETTER_KINDS = ['ClinicLetter', 'DischargeSummary', 'ReferralResponse', 'InformationRequest', 'AppointmentLetter', 'Other', 'OnwardReferral'];

/** A stage definition as the stage library shows it, with how many templates use it. */
function definitionResponse(definition: DemoStageDefinition): Schemas['PatientPathwayStageDefinitionResponse'] {
  const templateCount = db().templates.filter((t) => (t.stageDefinitionIdsInOrder ?? []).includes(definition.id)).length;
  return {...definition, templateCount, canBeDeleted: templateCount === 0};
}

/**
 * A stage added with a branch (MC10): plain, with its definition's gates, as a template's stage is copied. Its owner
 * (the relay, MC7) is the one the template gives the same definition (the lab for a lab stage), else the pathway's team.
 */
function addedStage(pathway: DemoPathway, definition: DemoStageDefinition, stageOrder: number, branchAdditionId: string): DemoStage {
  return {
    id: newId(),
    stageDefinitionId: definition.id,
    stageOrder,
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
    notes: null,
    startedAtUtc: null,
    startedByUserId: null,
    completedAtUtc: null,
    completedByUserId: null,
    skippedAtUtc: null,
    skippedByUserId: null,
    skipReason: null,
    skipDetail: null,
    branchAdditionId,
    ownerTeamId: pathway.plan.stages.find((s) => s.stageDefinitionId === definition.id && s.ownerTeamId)?.ownerTeamId ?? null,
    reachedAtUtc: null,
    handedOverFromTeamId: null,
    pickedUpAtUtc: null,
    pickedUpByUserId: null,
  };
}

function findDefinition(id: string): DemoStageDefinition {
  const definition = db().stageDefinitions.find((d) => d.id === id);
  if (!definition) refuse(notFound('The stage'));
  return definition;
}

/**
 * A template's stages from its definitions in order, with each one's options (optional, a choice node's kind and
 * outcomes). A stage still at its place keeps its description (the seed's reviewed templates write their own notes there).
 */
function templateStageList(
  ids: string[],
  options: Schemas['PathwayTemplateStageOptionsRequest'][] | null | undefined,
  clockStopStageOrder: number | null | undefined,
  kept: readonly Schemas['PathwayTemplateStageResponse'][] = [],
): Schemas['PathwayTemplateStageResponse'][] {
  return ids.map((id, i) => {
    const definition = findDefinition(id);
    const same = kept.find((s) => s.stageOrder === i + 1 && s.stageDefinitionId === id);
    return {
      stageOrder: i + 1,
      stageDefinitionId: id,
      name: definition.name,
      description: same ? same.description : definition.description,
      snomedCode: definition.snomedCode,
      snomedTerm: definition.snomedTerm,
      isArchived: definition.isArchived,
      visitRequirement: definition.visitRequirement,
      isOptional: options?.[i]?.isOptional ?? false,
      choiceKind: options?.[i]?.choiceKind || null,
      outcomes: options?.[i]?.choiceKind
        ? (options[i].outcomes ?? []).map((o) => ({name: o.name?.trim() ?? '', nextStageOrders: [...new Set(o.nextStageOrders ?? [])].sort((a, b) => a - b)}))
        : null,
      stopsClock: clockStopStageOrder === i + 1,
      ownerTeamId: options?.[i]?.ownerTeamId || null,
      ownerTeamName: teamName(options?.[i]?.ownerTeamId),
    };
  });
}

/** A list's rules (a template without routes): at least one stage, the last not optional, and no choice node (those need routes). */
function whyTemplateInvalid(stages: Schemas['PathwayTemplateStageResponse'][]): DemoReply | null {
  if (stages.length === 0) return invalid({stageDefinitionIdsInOrder: ['Add at least one stage.']});
  if (stages[stages.length - 1].isOptional) return invalid({stageOptions: ['The last stage can’t be optional.']});
  if (stages.some((s) => s.choiceKind)) return invalid({links: ['A choice node’s outcomes are routes: send the template’s links.']});
  return null;
}

/**
 * A template's addable branches as saved (MC10), or a 400: each named (unique, ignoring case, at most
 * `MAX_BRANCH_LENGTH`), with at least one stage from the library, following only stages before the last. A branch
 * keeps its ID when it's sent back with it.
 */
function addableBranchList(
  sent: Schemas['PathwayTemplateBranchRequest'][],
  template: Schemas['PathwayTemplateResponse'],
  stageCount: number,
): Schemas['PathwayTemplateBranchResponse'][] | DemoReply {
  const names = new Set<string>();
  const result: Schemas['PathwayTemplateBranchResponse'][] = [];
  for (const branch of sent) {
    const name = branch.name?.trim() ?? '';
    if (!name) return invalid({addableBranches: ['Give each branch a name.']});
    if (name.length > MAX_BRANCH_LENGTH) return invalid({addableBranches: [`A branch name must not exceed ${MAX_BRANCH_LENGTH} characters.`]});
    if (names.has(name.toLowerCase())) return invalid({addableBranches: ['Each branch needs a different name.']});
    names.add(name.toLowerCase());
    const ids = branch.stageDefinitionIds ?? [];
    if (ids.length === 0) return invalid({addableBranches: [`Add at least one stage to the '${name}' branch.`]});
    const definitions = ids.map((id) => db().stageDefinitions.find((d) => d.id === id));
    if (definitions.some((d) => !d)) return invalid({addableBranches: [`A stage in the '${name}' branch isn’t in the stage library.`]});
    const after = [...new Set(branch.attachAfterStageOrders ?? [])].sort((a, b) => a - b);
    if (after.some((o) => o < 1 || o >= stageCount))
      return invalid({addableBranches: [`The '${name}' branch can follow only a stage before the last.`]});
    const kept = template.addableBranches?.find((b) => b.id === branch.id);
    result.push({
      id: kept?.id ?? newId(),
      name,
      stages: definitions.map((d) => ({stageDefinitionId: d!.id, name: d!.name})),
      attachAfterStageOrders: after,
      isRepeatable: branch.isRepeatable ?? false,
    });
  }
  return result;
}

function findTemplate(id: string) {
  const template = db().templates.find((t) => t.id === id);
  if (!template) refuse(notFound('The template'));
  return template;
}

/** The stage library and the pathway templates (`ManageSetup` to change them). */
function registerSetupOfPathways() {
  on('get', '/api/pathways/patient-pathway-stage/templates', ({query}) => {
    me();
    const includeArchived = flag(query, 'includeArchived') === true;
    return db().templates.filter((t) => includeArchived || !t.isArchived);
  });
  on('get', '/api/pathways/patient-pathway-stage/templates/{id}', ({params}) => {
    me();
    return findTemplate(params.id);
  });

  const saveTemplate = (
    template: Schemas['PathwayTemplateResponse'],
    body: Partial<Schemas['CreatePathwayTemplateRequest']> | Partial<Schemas['UpdatePathwayTemplateRequest']>,
  ): DemoReply | null => {
    const name = body.name?.trim() ?? template.name ?? '';
    if (!name) return invalid({name: ['Give the template a name.']});
    if (db().templates.some((t) => t.id !== template.id && t.name?.toLowerCase() === name.toLowerCase()))
      return problem(409, 'A template with this name already exists.');
    const teamId = body.teamId || template.teamId;
    if (!teamId || !db().teams.some((s) => s.id === teamId)) return invalid({teamId: ['Choose a team.']});
    const ids = body.stageDefinitionIdsInOrder ?? template.stageDefinitionIdsInOrder ?? [];
    const sameList = JSON.stringify(ids) === JSON.stringify(template.stageDefinitionIdsInOrder ?? []);
    // Options left out with the same stage list keep the stored ones; with a new list, every stage is plain.
    const options =
      body.stageOptions ??
      (sameList
        ? (template.stages ?? []).map((s) => ({isOptional: s.isOptional, choiceKind: s.choiceKind, outcomes: s.outcomes, ownerTeamId: s.ownerTeamId}))
        : null);
    // The relay (MC7): a stage's owner is the template's own team (none given) or, in Stage 1, the lab; nothing else.
    const lab = labTeamId();
    if ((options ?? []).some((o) => o.ownerTeamId && o.ownerTeamId !== lab && o.ownerTeamId !== teamId))
      return invalid({stageOptions: ['A stage can be owned only by the template’s own team or the lab.']});
    if ((options ?? []).some((o) => o.choiceKind && !CHOICE_KINDS.some((k) => k.value === o.choiceKind)))
      return invalid({stageOptions: ['A choice node is an assessment or a lab result.']});
    const clockStop = body.clockStopStageOrder === undefined ? template.clockStopStageOrder : body.clockStopStageOrder;
    const stages = templateStageList(
      ids,
      options?.map((o) => ({...o, ownerTeamId: o.ownerTeamId === teamId ? null : o.ownerTeamId})),
      clockStop,
      template.stages ?? [],
    );
    // Routes drawn in the route editor (MC9) come as links, checked by the graph rules (`TemplateGraphRules`); a list
    // has its branch rules. A template with routes side by side changes its stages only with its links.
    const links = body.links?.map((l) => ({fromStageOrder: l.fromStageOrder, toStageOrder: l.toStageOrder})) ?? null;
    const parallel = hasRoutes(template);
    if (links) {
      if (stages.length === 0) return invalid({stageDefinitionIdsInOrder: ['Add at least one stage.']});
      const [first] = [...templateGraphProblems(stages, links).entries()].sort(([a], [b]) => a - b);
      if (first) return invalid({links: [`Stage ${first[0] + 1}: ${first[1]}`]});
    } else if (parallel) {
      // Its routes are kept: only what doesn't change them (an owner, say) changes without its links.
      const optionsOf = (list: Schemas['PathwayTemplateStageResponse'][]) =>
        JSON.stringify(list.map((s) => [s.stageDefinitionId, s.isOptional, s.choiceKind ?? null, s.outcomes ?? null]));
      if (optionsOf(stages) !== optionsOf(template.stages ?? [])) return invalid({stageDefinitionIdsInOrder: [PARALLEL_ROUTES_LOCKED]});
    } else {
      const why = whyTemplateInvalid(stages);
      if (why) return why;
    }
    const branches = body.addableBranches ? addableBranchList(body.addableBranches, template, stages.length) : (template.addableBranches ?? []);
    if (branches instanceof DemoReply) return branches;
    Object.assign(template, {
      name,
      teamId,
      teamName: teamName(teamId) ?? db().teams.find((s) => s.id === teamId)?.name ?? null,
      retentionClass: body.retentionClass || template.retentionClass || 'Adult',
      clockStopStageOrder: clockStop ?? null,
      stageDefinitionIdsInOrder: ids,
      stages,
      links: links ?? (parallel ? template.links : chainLinks(stages)),
      addableBranches: branches,
    });
    return null;
  };

  on('post', '/api/pathways/patient-pathway-stage/templates', ({body}) => {
    requirePermission('ManageSetup');
    const template: Schemas['PathwayTemplateResponse'] = {id: newId(), isArchived: false, pathwayCount: 0, stageDefinitionIdsInOrder: []};
    const why = saveTemplate(template, body);
    if (why) return why;
    db().templates.push(template);
    return new DemoReply(201, template);
  });

  on('patch', '/api/pathways/patient-pathway-stage/templates/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const template = findTemplate(params.id);
    const copy = structuredClone(template);
    const why = saveTemplate(copy, body);
    if (why) return why;
    Object.assign(template, copy);
    return template;
  });

  on('delete', '/api/pathways/patient-pathway-stage/templates/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const template = findTemplate(params.id);
    if (db().pathways.some((p) => p.templateId === template.id))
      return problem(409, 'This template has pathways made from it, so it can’t be deleted. Archive it instead.');
    db().templates = db().templates.filter((t) => t.id !== template.id);
    return undefined;
  });

  for (const [action, isArchived] of [
    ['archive', true],
    ['restore', false],
  ] as const) {
    on('post', `/api/pathways/patient-pathway-stage/templates/{id}/${action}`, ({params}) => {
      requirePermission('ManageSetup');
      const template = findTemplate(params.id);
      template.isArchived = isArchived;
      return template;
    });
    on('post', `/api/pathways/patient-pathway-stage/definitions/{id}/${action}`, ({params}) => {
      requirePermission('ManageSetup');
      const definition = findDefinition(params.id);
      definition.isArchived = isArchived;
      for (const template of db().templates)
        for (const stage of template.stages ?? []) if (stage.stageDefinitionId === definition.id) stage.isArchived = isArchived;
      return definitionResponse(definition);
    });
  }

  on('get', '/api/pathways/patient-pathway-stage/definitions', ({query}) => {
    me();
    const includeArchived = flag(query, 'includeArchived') === true;
    return db()
      .stageDefinitions.filter((d) => includeArchived || !d.isArchived)
      .map(definitionResponse);
  });

  on('get', '/api/pathways/patient-pathway-stage/definitions/{id}', ({params}) => {
    me();
    return definitionResponse(findDefinition(params.id));
  });

  /** Applies a definition's details; refuses as the API's validator does. */
  const saveDefinition = (
    definition: DemoStageDefinition,
    body: Partial<Schemas['CreatePatientPathwayStageDefinitionRequest']> | Partial<Schemas['UpdatePatientPathwayStageDefinitionRequest']>,
  ): DemoReply | null => {
    const name = body.name?.trim() ?? definition.name ?? '';
    if (!name) return invalid({name: ['Give the stage a name.']});
    if (db().stageDefinitions.some((d) => d.id !== definition.id && d.name?.toLowerCase() === name.toLowerCase()))
      return problem(409, 'A stage with this name is in the library already.');
    if (body.visitRequirement && !VISITS.includes(body.visitRequirement)) return invalid({visitRequirement: ['Choose a visit requirement.']});
    if (body.needsLetterKind && !LETTER_KINDS.includes(body.needsLetterKind)) return invalid({needsLetterKind: ['Choose a letter kind.']});
    Object.assign(definition, {
      name,
      description: body.description === undefined ? definition.description : body.description?.trim() || null,
      instructions: body.instructions === undefined ? definition.instructions : body.instructions?.trim() || null,
      snomedCode: body.snomedCode === undefined ? definition.snomedCode : body.snomedCode || null,
      snomedTerm: body.snomedTerm === undefined ? definition.snomedTerm : body.snomedTerm || null,
      visitRequirement: body.visitRequirement || definition.visitRequirement || 'None',
      needsLetterKind: body.needsLetterKind === undefined ? definition.needsLetterKind : body.needsLetterKind || null,
      needsConsent: body.needsConsent ?? definition.needsConsent ?? false,
      waitsForResults: body.waitsForResults ?? definition.waitsForResults ?? false,
    });
    // Templates show their stages from the library; pathways keep the copies they were made with.
    for (const template of db().templates)
      for (const stage of template.stages ?? [])
        if (stage.stageDefinitionId === definition.id)
          Object.assign(stage, {name, description: definition.description, visitRequirement: definition.visitRequirement});
    return null;
  };

  on('post', '/api/pathways/patient-pathway-stage/definitions', ({body}) => {
    requirePermission('ManageSetup');
    const definition: DemoStageDefinition = {id: newId(), name: '', isArchived: false, needsConsent: false, waitsForResults: false};
    const why = saveDefinition(definition, body);
    if (why) return why;
    db().stageDefinitions.push(definition);
    return new DemoReply(201, definitionResponse(definition));
  });

  on('patch', '/api/pathways/patient-pathway-stage/definitions/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const definition = findDefinition(params.id);
    const copy = {...definition};
    const why = saveDefinition(copy, body);
    if (why) {
      // Put the templates' copies back as they were.
      saveDefinition(definition, {});
      return why;
    }
    Object.assign(definition, copy);
    return definitionResponse(definition);
  });

  on('delete', '/api/pathways/patient-pathway-stage/definitions/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const definition = findDefinition(params.id);
    if (!definitionResponse(definition).canBeDeleted)
      return problem(409, 'This stage is used by a template, so it can’t be deleted. Archive it instead.');
    db().stageDefinitions = db().stageDefinitions.filter((d) => d.id !== definition.id);
    return undefined;
  });
}
