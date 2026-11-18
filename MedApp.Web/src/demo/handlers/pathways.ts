import type {Schemas} from '../../api/client';
import {personById, usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {templateStages} from '../seed';
import type {DemoPathway, DemoStage} from '../state';
import {db} from '../store';
import {endOfUkDay, nowUtc, today} from '../time';
import {can, contains, flag, list, me, NO_FLAGS, paged, pathwayPatient, patientName, requirePermission, sorted} from './common';
import {referralSummaryClock} from './referrals';

/**
 * Pathways, their stages and notes, the templates and Insights. The rules are a simplified copy of the API's
 * `PathwayWorkflow`: stages are worked in order, the first stage started or finished starts the pathway, finishing the
 * last puts it in review. There's no audit log or monitoring.
 */

const OPEN: DemoPathway['status'][] = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked'];
const isOpen = (pathway: DemoPathway) => OPEN.includes(pathway.status);
const isDone = (stage: DemoStage) => stage.completedAtUtc !== null;

/** The stage being worked: the first not finished, while the pathway is open (none when it's on hold or closed). */
export const currentStage = (pathway: DemoPathway) =>
  pathway.status === 'Blocked' || !isOpen(pathway) ? undefined : pathway.stages.find((stage) => !isDone(stage));

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
  pathway.stages.some((s) => s.startedAtUtc || isDone(s)) ? 'InProgress' : pathway.assignments.length > 0 ? 'Assigned' : 'Unassigned';

function noteResponse(note: DemoPathway['notes'][number], pathway: DemoPathway): Schemas['PatientPathwayNoteResponse'] {
  const viewer = me();
  const isMine = note.authorUserId === viewer.id;
  const takesNotes = !['Cancelled', 'Archived'].includes(pathway.status);
  return {
    id: note.id,
    authorUserId: note.authorUserId,
    authorUsername: usernameOf(note.authorUserId),
    text: note.text,
    createdAtUtc: note.createdAtUtc,
    lastEditedAtUtc: note.lastEditedAtUtc,
    version: note.version,
    canEdit: isMine && takesNotes,
    canDelete: false,
    canRequestDeletion: false,
    deletionRequestId: null,
  };
}

/** A pathway as the API answers it, for the signed-in person. */
export function pathwayResponse(pathway: DemoPathway): Schemas['PatientPathwayResponse'] {
  const canWork = can('WorkPathways');
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
    specialtyId: pathway.specialtyId,
    referral: referral
      ? {
          id: referral.id,
          receivedOn: referral.receivedOn,
          specialtyName: db().specialties.find((s) => s.id === referral.specialtyId)?.name ?? null,
          version: referral.version,
          clock: referralSummaryClock(referral),
          pauses: [],
          canPauseClock: false,
          canResumeClock: false,
        }
      : undefined,
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
    skippedStageCount: 0,
    stageCount: pathway.stages.length,
    allowedStatusChanges: canWork ? allowedStatusChanges(pathway) : [],
    canWorkOnStages: workable,
    canEditDetails: canWork && !['Cancelled', 'Archived'].includes(pathway.status),
    canAddNotes: canWork && !['Cancelled', 'Archived'].includes(pathway.status),
    canChangeAssignments: can('AssignPathways') && isOpen(pathway),
    canBeDeleted: false,
    readOnlyReason: null,
    patientFlags: NO_FLAGS,
    stages: pathway.stages.map((stage) => ({
      id: stage.id,
      stageDefinitionId: stage.stageDefinitionId,
      stageOrder: stage.stageOrder,
      name: stage.name,
      description: stage.description,
      instructions: null,
      visitRequirement: stage.visitRequirement,
      needsLetterKind: null,
      notes: stage.notes,
      isOptional: false,
      choosesBranch: false,
      branch: null,
      branches: [],
      needsBranchChoice: false,
      state: isDone(stage) ? 'Completed' : stage.startedAtUtc ? 'InProgress' : 'NotStarted',
      isCurrent: stage.id === current?.id,
      isLocked: isDone(stage),
      canEdit: workable && stage.id === current?.id,
      canUnlock: false,
      canSkip: false,
      canReinstate: false,
      canRecordSpecimen: false,
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
    noteList: [...pathway.notes].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)).map((note) => noteResponse(note, pathway)),
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
    skippedStageCount: 0,
    stageCount: pathway.stages.length,
    currentStageName: currentStage(pathway)?.name ?? null,
    assignees: pathway.assignments.map((a) => usernameOf(a.userId) ?? ''),
    lastUpdated: pathway.lastUpdated,
    clock: referral ? referralSummaryClock(referral) : undefined,
  };
}

/** The pathway, or a 404. */
export function findPathway(id: string): DemoPathway {
  const pathway = db().pathways.find((p) => p.id === id);
  if (!pathway) refuse(notFound('The pathway'));
  return pathway;
}

/** A change that can be made: the pathway is one the signed-in person can work, and open. */
function forWork(id: string): DemoPathway {
  requirePermission('WorkPathways');
  const pathway = findPathway(id);
  if (!canWorkOnStages(pathway)) refuse(problem(409, 'This pathway can’t be worked on while it’s on hold or closed.'));
  return pathway;
}

function touch(pathway: DemoPathway) {
  pathway.lastUpdated = nowUtc();
}

/** Starts the pathway when its first stage is started or finished. */
function markStarted(pathway: DemoPathway) {
  if (pathway.status === 'Unassigned' || pathway.status === 'Assigned') pathway.status = 'InProgress';
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
    specialtyId: template.specialtyId!,
    referralId: input.referralId ?? null,
    title: input.title?.trim() || template.name!,
    dueDate: due,
    priority,
    status: input.assigneeUserId ? 'Assigned' : 'Unassigned',
    blockedFromStatus: null,
    createdAt: now,
    lastUpdated: now,
    stages: templateStages(template).map((stage) => ({...stage, id: newId()})),
    assignments: input.assigneeUserId ? [{userId: input.assigneeUserId, assignedByUserId: viewer.id, assignedAtUtc: now}] : [],
    notes: input.notes?.trim()
      ? [{id: newId(), authorUserId: viewer.id, text: input.notes.trim(), createdAtUtc: now, lastEditedAtUtc: null, version: 1}]
      : [],
    statusChanges: [],
  };
  db().pathways.push(pathway);
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
    const specialtyIds = list(query, 'SpecialtyIds');
    const stageDefinitionIds = list(query, 'StageDefinitionIds');
    const includeClosed = flag(query, 'IncludeClosed') === true || statuses.some((s) => ['Completed', 'Cancelled', 'Archived'].includes(s));
    const started = flag(query, 'Started');
    const search = query.get('Search');
    const items = db().pathways.filter(
      (p) =>
        (includeClosed || isOpen(p)) &&
        (statuses.length === 0 || statuses.includes(p.status)) &&
        (priorities.length === 0 || priorities.includes(p.priority)) &&
        (specialtyIds.length === 0 || specialtyIds.includes(p.specialtyId)) &&
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
    return db()
      .pathways.filter((p) => ids.includes(p.id))
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
    return pathwayResponse(createPathway({...body, patientId: body.patientId, templateId: body.templateId}));
  });

  on('patch', '/api/pathways/patient-pathways/{id}', ({params, body}) => {
    requirePermission('WorkPathways');
    const pathway = findPathway(params.id);
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
    if (!stage || stage.id !== currentStage(pathway)?.id) return problem(409, 'Only the current stage can be finished.');
    const attended = db().appointments.some((a) => a.stageId === stage.id && a.status === 'Attended');
    if (stage.visitRequirement !== 'None' && !attended && body.withoutVisit !== true)
      return problem(409, 'This stage involves a visit, and none was attended. Finish it without a visit, giving a reason.');
    const now = nowUtc();
    stage.startedAtUtc ??= now;
    stage.startedByUserId ??= viewer.id;
    stage.completedAtUtc = now;
    stage.completedByUserId = viewer.id;
    markStarted(pathway);
    if (pathway.stages.every(isDone)) pathway.status = 'InReview';
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/status', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    const pathway = findPathway(params.id);
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
    const pathway = findPathway(params.id);
    if (!personById(params.userId)) return notFound('The member of staff');
    if (!pathway.assignments.some((a) => a.userId === params.userId))
      pathway.assignments.push({userId: params.userId, assignedByUserId: viewer.id, assignedAtUtc: nowUtc()});
    if (pathway.status === 'Unassigned') pathway.status = 'Assigned';
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('delete', '/api/pathways/patient-pathways/{id}/assignments/{userId}', ({params}) => {
    requirePermission('AssignPathways');
    const pathway = findPathway(params.id);
    pathway.assignments = pathway.assignments.filter((a) => a.userId !== params.userId);
    if (pathway.status === 'Assigned' && pathway.assignments.length === 0) pathway.status = 'Unassigned';
    touch(pathway);
    return pathwayResponse(pathway);
  });

  on('post', '/api/pathways/patient-pathways/{id}/notes', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    const pathway = findPathway(params.id);
    if (!body.text?.trim()) return invalid({text: ['Write the note.']});
    const note = {id: newId(), authorUserId: viewer.id, text: body.text.trim(), createdAtUtc: nowUtc(), lastEditedAtUtc: null, version: 1};
    pathway.notes.push(note);
    touch(pathway);
    return noteResponse(note, pathway);
  });

  on('put', '/api/pathways/patient-pathways/{id}/notes/{noteId}', ({params, body}) => {
    const viewer = me();
    const pathway = findPathway(params.id);
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

  on('get', '/api/pathways/patient-pathway-stage/templates', ({query}) => {
    me();
    const includeArchived = flag(query, 'includeArchived') === true;
    return db().templates.filter((t) => includeArchived || !t.isArchived);
  });
  on('get', '/api/pathways/patient-pathway-stage/templates/{id}', ({params}) => {
    me();
    return db().templates.find((t) => t.id === params.id) ?? notFound('The template');
  });

  on('get', '/api/pathways/patient-pathway-stage/definitions', () => {
    me();
    const seen = new Map<string, Schemas['PatientPathwayStageDefinitionResponse']>();
    for (const template of db().templates)
      for (const stage of template.stages ?? [])
        if (stage.stageDefinitionId && !seen.has(stage.stageDefinitionId))
          seen.set(stage.stageDefinitionId, {
            id: stage.stageDefinitionId,
            name: stage.name,
            description: stage.description,
            visitRequirement: stage.visitRequirement,
            isArchived: false,
          });
    return [...seen.values()];
  });

  // No audit log in the light demo: the full version keeps one for every change.
  on('get', '/api/pathways/audit/search', ({query}) => paged([], query));

  on('get', '/api/pathways/insights', ({query}) => insights(Number(query.get('Weeks')) || 12, query.get('TemplateId'), query.get('SpecialtyId')));
}

/**
 * Insights: this week's counts come from the demo's pathways; the history (last week, stage times, throughput) is
 * made up, deterministically, so the charts have something to show. The full version reads it from the audit log.
 */
function insights(weeks: number, templateId: string | null, specialtyId: string | null): Schemas['PathwayInsightsResponse'] {
  me();
  const pathways = db().pathways.filter(
    (p) => (!templateId || p.templateId === templateId) && (!specialtyId || p.specialtyId === specialtyId),
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
    specialtyId,
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
      .templates.filter((t) => (!templateId || t.id === templateId) && (!specialtyId || t.specialtyId === specialtyId))
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
