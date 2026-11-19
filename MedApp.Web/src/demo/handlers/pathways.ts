import type {Schemas} from '../../api/client';
import {PEOPLE, personById, usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {ownerAccess, teamName, templateStages} from '../seed';
import type {DemoNote, DemoNoteDeletionRequest, DemoPathway, DemoStage, DemoStageDefinition} from '../state';
import {db} from '../store';
import {endOfUkDay, nowUtc, today} from '../time';
import {can, contains, flag, list, me, NO_FLAGS, paged, pathwayPatient, patientName, requirePermission, sorted} from './common';
import {canRecordConsent, clinicalGate} from './clinical';
import {canRecordSpecimen, labGate} from './lab';
import {pathwayReferralResponse, referralSummaryClock} from './referrals';
import {canChangePathway, canSeePathway, ensureCanChangePathway, myScope, refuseAcrossWall} from './teamWalls';

/**
 * Pathways, their stages and notes, the stage library, the templates and Insights. The rules are a simplified copy of
 * the API's `PathwayWorkflow`: stages are worked in order (the current stage is the first neither finished nor
 * skipped), the first stage started or finished starts the pathway, and finishing or skipping the last puts it in
 * review. Optional stages can be skipped, a branch point's branch is chosen as it's finished (never by MedApp), and
 * the gates without an override (`StageGates`: the lab's, the letter's and consent's) are checked on every finish.
 * There's no monitoring.
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

/** The stage being worked: the first neither finished nor skipped, while the pathway is open (none when it's on hold or closed). */
export const currentStage = (pathway: DemoPathway) =>
  pathway.status === 'Blocked' || !isOpen(pathway) ? undefined : inOrder(pathway).find(isToDo);

const SKIP_REASONS = ['NotNeeded', 'PatientDeclined', 'Other'];

/** Why a stage can't be skipped now, or null (`PathwayWorkflow.WhySkipNotAllowed`). */
function whySkipNotAllowed(pathway: DemoPathway, stage: DemoStage): string | null {
  if (!stage.isOptional) return `The '${stage.name}' stage isn't optional, so it can't be skipped.`;
  if (!canWorkOnStages(pathway)) return 'Stages can’t be skipped while the pathway is on hold or closed.';
  if (isSkipped(stage)) return `The '${stage.name}' stage has already been skipped.`;
  if (isDone(stage)) return `The '${stage.name}' stage is finished, so it can't be skipped.`;
  const current = currentStage(pathway);
  if (current?.id !== stage.id) return `Only the current stage can be skipped: finish the '${current?.name}' stage first.`;
  return stage.visitRequirement === 'Required' && !can('FinishWithoutVisit')
    ? `The '${stage.name}' stage needs a visit, so only someone who can finish a stage without its visit can skip it.`
    : null;
}

/**
 * A branch point's branches, in the template's order, each with its stages: the stages with a branch name after it, up
 * to the next branch point or the end. None is ever chosen or pre-selected: the clinician chooses.
 */
function branchesOf(pathway: DemoPathway, branchPoint: DemoStage): {name: string; stages: DemoStage[]}[] {
  if (!branchPoint.choosesBranch) return [];
  const branches: {name: string; stages: DemoStage[]}[] = [];
  for (const stage of inOrder(pathway).filter((s) => s.stageOrder > branchPoint.stageOrder)) {
    if (stage.choosesBranch) break;
    if (!stage.branch) continue;
    const branch = branches.find((b) => b.name.toLowerCase() === stage.branch!.toLowerCase());
    if (branch) branch.stages.push(stage);
    else branches.push({name: stage.branch, stages: [stage]});
  }
  return branches;
}

/** Whether the pathway's last stage to do is this one (finishing it leaves none). */
const isLastToDo = (pathway: DemoPathway, stage: DemoStage) => pathway.stages.every((s) => s.id === stage.id || !isToDo(s));

/** Why a stage can't be finished yet, from the gates without an override (`StageGates`), or null. */
function whyGated(pathway: DemoPathway, stage: DemoStage): string | null {
  return labGate(pathway, stage, isLastToDo(pathway, stage)) ?? clinicalGate(pathway, stage);
}

const canWorkOnStages = (pathway: DemoPathway) => ['Unassigned', 'Assigned', 'InProgress', 'InReview'].includes(pathway.status);

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
  const works = can('WorkPathways') && canChangePathway(pathway);
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

/** A branch point's branches as the page shows them. */
const branchResponses = (pathway: DemoPathway, stage: DemoStage): Schemas['PatientPathwayBranchResponse'][] =>
  branchesOf(pathway, stage).map((branch) => ({
    name: branch.name,
    stageNames: branch.stages.map((s) => s.name),
    stageNamesToDo: branch.stages.filter(isToDo).map((s) => s.name),
  }));

/** A pathway as the API answers it, for the signed-in person. */
export function pathwayResponse(pathway: DemoPathway): Schemas['PatientPathwayResponse'] {
  // Only the pathway's own team changes it (the team walls): admins and the lab read it.
  const mine = canChangePathway(pathway);
  const canWork = can('WorkPathways') && mine;
  const current = currentStage(pathway);
  const workable = canWork && canWorkOnStages(pathway);
  const referral = pathway.referralId ? db().referrals.find((r) => r.id === pathway.referralId) : undefined;
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
    currentStageId: current?.id ?? null,
    completedStageCount: pathway.stages.filter(isDone).length,
    skippedStageCount: pathway.stages.filter(isSkipped).length,
    stageCount: pathway.stages.length,
    allowedStatusChanges: canWork ? allowedStatusChanges(pathway) : [],
    canWorkOnStages: workable,
    canEditDetails: canWork && !['Cancelled', 'Archived'].includes(pathway.status),
    canAddNotes: canWork && !['Cancelled', 'Archived'].includes(pathway.status),
    canChangeAssignments: can('AssignPathways') && mine && isOpen(pathway),
    canBeDeleted: can('DeletePathways') && mine && ['Cancelled', 'Archived'].includes(pathway.status),
    readOnlyReason: mine || myScope().seesAllTeams ? null : `This pathway is ${teamName(pathway.teamId) ?? 'another team'}’s: your team can read it, and only its own team changes it.`,
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
      choosesBranch: stage.choosesBranch,
      branch: stage.branch,
      branches: branchResponses(pathway, stage),
      needsBranchChoice: stage.choosesBranch && stage.chosenBranch === null,
      state: isDone(stage) ? 'Completed' : isSkipped(stage) ? 'Skipped' : stage.startedAtUtc ? 'InProgress' : 'NotStarted',
      isCurrent: stage.id === current?.id,
      isLocked: isDone(stage) && !stage.unlocked,
      canEdit: workable && (stage.id === current?.id || (isDone(stage) && stage.unlocked)),
      canUnlock: can('UnlockStages') && mine && isDone(stage) && !stage.unlocked && (isOpen(pathway) || pathway.status === 'Completed'),
      canSkip: canWork && whySkipNotAllowed(pathway, stage) === null,
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
  };
}

/** A pathway in a list (`PatientPathwaySummaryResponse`). */
export function pathwaySummary(pathway: DemoPathway): Schemas['PatientPathwaySummaryResponse'] {
  const referral = pathway.referralId ? db().referrals.find((r) => r.id === pathway.referralId) : undefined;
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
    currentStageName: currentStage(pathway)?.name ?? null,
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

/** A change that can be made: the pathway is one the signed-in person can work, and open. */
function forWork(id: string): DemoPathway {
  requirePermission('WorkPathways');
  const pathway = pathwayToChange(id);
  if (!canWorkOnStages(pathway)) refuse(problem(409, 'This pathway can’t be worked on while it’s on hold or closed.'));
  return pathway;
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
    stages: templateStages(template, db().stageDefinitions).map((stage) => ({...stage, id: newId()})),
    assignments: input.assigneeUserId ? [{userId: input.assigneeUserId, assignedByUserId: viewer.id, assignedAtUtc: now}] : [],
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
  db().pathways.push(pathway);
  db().pathwayAccess.push(ownerAccess(pathway));
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
    const items = db().pathways.filter(
      (p) =>
        canSeePathway(p, scope) &&
        (includeClosed || isOpen(p)) &&
        (statuses.length === 0 || statuses.includes(p.status)) &&
        (priorities.length === 0 || priorities.includes(p.priority)) &&
        (teamIds.length === 0 || teamIds.includes(p.teamId)) &&
        (stageDefinitionIds.length === 0 || stageDefinitionIds.includes(currentStage(p)?.stageDefinitionId ?? '')) &&
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
    const pathway = forWork(params.id);
    const stage = pathway.stages.find((s) => s.id === params.stageId);
    if (!stage || stage.id !== currentStage(pathway)?.id) return problem(409, 'Only the current stage can be started.');
    stage.startedAtUtc = nowUtc();
    stage.startedByUserId = viewer.id;
    markStarted(pathway);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('put', '/api/pathways/patient-pathways/{id}/stages/{stageId}/notes', ({params, body}) => {
    const pathway = forWork(params.id);
    const stage = pathway.stages.find((s) => s.id === params.stageId);
    if (!stage) return notFound('The stage');
    stage.notes = body.notes ?? null;
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/finish', ({params, body}) => {
    const viewer = me();
    const pathway = forWork(params.id);
    const stage = pathway.stages.find((s) => s.id === params.stageId);
    const reopened = stage !== undefined && isDone(stage) && stage.unlocked;
    if (!stage || (stage.id !== currentStage(pathway)?.id && !reopened)) return problem(409, 'Only the current stage can be finished.');
    // A branch point's branch is chosen as it's first finished, by the clinician: never by MedApp.
    const branches = branchesOf(pathway, stage);
    const chosen = body.branch?.trim() ? branches.find((b) => b.name.toLowerCase() === body.branch!.trim().toLowerCase()) : undefined;
    if (stage.choosesBranch && stage.chosenBranch === null && !chosen)
      return invalid({branch: [`Choose which branch comes next: ${branches.map((b) => b.name).join(' or ')}.`]});
    if (body.branch?.trim() && (!stage.choosesBranch || stage.chosenBranch !== null))
      return invalid({branch: ['The branch was chosen already. It changes only by reinstating a stage it skipped.']});
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
      stage.chosenBranch = chosen.name;
      for (const other of branches.filter((b) => b !== chosen))
        for (const skipped of other.stages.filter(isToDo)) skipStage(skipped, viewer.id, 'BranchNotChosen', null);
    }
    afterStageDone(pathway);
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/stages/{stageId}/skip', ({params, body}) => {
    const viewer = me();
    const pathway = forWork(params.id);
    const stage = pathway.stages.find((s) => s.id === params.stageId);
    if (!stage) return notFound('The stage');
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
    const pathway = pathwayToChange(params.id);
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
    const pathway = pathwayToChange(params.id);
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

  on('get', '/api/pathways/insights', ({query}) => insights(Number(query.get('Weeks')) || 12, query.get('TemplateId'), query.get('TeamId')));
}

/**
 * Insights: this week's counts come from the demo's pathways; the history (last week, stage times, throughput) is
 * made up, deterministically, so the charts have something to show. The full version reads it from the audit log.
 */
function insights(weeks: number, templateId: string | null, teamId: string | null): Schemas['PathwayInsightsResponse'] {
  me();
  const pathways = db().pathways.filter(
    (p) => (!templateId || p.templateId === templateId) && (!teamId || p.teamId === teamId),
  );
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
      .templates.filter((t) => (!templateId || t.id === templateId) && (!teamId || t.teamId === teamId))
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
const LETTER_KINDS = ['ClinicLetter', 'DischargeSummary', 'ReferralResponse', 'InformationRequest', 'AppointmentLetter', 'Other'];

/** A stage definition as the stage library shows it, with how many templates use it. */
function definitionResponse(definition: DemoStageDefinition): Schemas['PatientPathwayStageDefinitionResponse'] {
  const templateCount = db().templates.filter((t) => (t.stageDefinitionIdsInOrder ?? []).includes(definition.id)).length;
  return {...definition, templateCount, canBeDeleted: templateCount === 0};
}

function findDefinition(id: string): DemoStageDefinition {
  const definition = db().stageDefinitions.find((d) => d.id === id);
  if (!definition) refuse(notFound('The stage'));
  return definition;
}

/** A template's stages from its definitions in order, with each one's options (optional, a branch point, a branch). */
function templateStageList(
  ids: string[],
  options: Schemas['PathwayTemplateStageOptionsRequest'][] | null | undefined,
  clockStopStageOrder: number | null | undefined,
): Schemas['PathwayTemplateStageResponse'][] {
  return ids.map((id, i) => {
    const definition = findDefinition(id);
    return {
      stageOrder: i + 1,
      stageDefinitionId: id,
      name: definition.name,
      description: definition.description,
      snomedCode: definition.snomedCode,
      snomedTerm: definition.snomedTerm,
      isArchived: definition.isArchived,
      visitRequirement: definition.visitRequirement,
      isOptional: options?.[i]?.isOptional ?? false,
      choosesBranch: options?.[i]?.choosesBranch ?? false,
      branch: options?.[i]?.branch?.trim() || null,
      stopsClock: clockStopStageOrder === i + 1,
    };
  });
}

/** The template's branch rules (`TemplateBranchRules`), as 400s, or null when they hold. */
function whyTemplateInvalid(stages: Schemas['PathwayTemplateStageResponse'][]): DemoReply | null {
  if (stages.length === 0) return invalid({stageDefinitionIdsInOrder: ['Add at least one stage.']});
  const last = stages[stages.length - 1];
  if (last.isOptional || last.choosesBranch || last.branch)
    return invalid({stageOptions: ['The last stage can’t be optional, a branch point or in a branch.']});
  let open: {names: Set<string>} | null = null;
  for (const stage of stages) {
    if (stage.choosesBranch) {
      if (open && open.names.size < 2) return invalid({stageOptions: ['A branch point needs two branches after it.']});
      if (stage.isOptional || stage.branch) return invalid({stageOptions: ['A branch point can’t be optional or in a branch.']});
      open = {names: new Set()};
    } else if (stage.branch) {
      if (!open) return invalid({stageOptions: ['A stage can be in a branch only after a branch point.']});
      open.names.add(stage.branch.toLowerCase());
    }
  }
  if (open && open.names.size < 2) return invalid({stageOptions: ['A branch point needs two branches after it.']});
  return null;
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
      (sameList ? (template.stages ?? []).map((s) => ({isOptional: s.isOptional, choosesBranch: s.choosesBranch, branch: s.branch})) : null);
    const clockStop = body.clockStopStageOrder === undefined ? template.clockStopStageOrder : body.clockStopStageOrder;
    const stages = templateStageList(ids, options, clockStop);
    const why = whyTemplateInvalid(stages);
    if (why) return why;
    Object.assign(template, {
      name,
      teamId,
      teamName: teamName(teamId) ?? db().teams.find((s) => s.id === teamId)?.name ?? null,
      retentionClass: body.retentionClass || template.retentionClass || 'Adult',
      clockStopStageOrder: clockStop ?? null,
      stageDefinitionIdsInOrder: ids,
      stages,
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
