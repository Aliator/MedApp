import type {Schemas} from '../../api/client';
import {ADMIN, personById, usernameOf} from '../accounts';
import {invalid, notFound, on, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoActivityEntry, DemoMonitoringRule} from '../slices/setup';
import {db} from '../store';
import {teamName} from '../seed';
import {nowUtc, ukDateOf} from '../time';
import {contains, list, me, paged, patientName, requirePermission} from './common';

/**
 * The admin's logs and oversight: the activity log (seeded, and added to by sign-ins, patient views and the setup and
 * privacy changes), the pathway audit log (worked out from the pathways as they are now, as the timeline is: never a
 * note's text, only its ID and version), monitoring rules (kept, but nothing fires them in the light demo) and the
 * scheduled jobs (their last runs made up; running one is the server's).
 */

type AuditEntry = Schemas['PatientPathwayAuditEntryResponse'];

/** How long the same view by the same person adds nothing (`Activity:ViewWindowMinutes`). */
const VIEW_WINDOW_MS = 5 * 60_000;
const VIEWS = ['PatientViewed', 'PatientListed', 'PathwayViewed', 'ReferralViewed', 'TeamBoundaryRefused'];

/**
 * Records an entry in the activity log as the signed-in person (field names and IDs only in `details`, never a
 * patient's details or clinical text). A view the same person made of the same thing in the last five minutes adds
 * nothing. A view on a GET is kept until the next change saves the state.
 */
export function logActivity(action: string, more: Partial<Omit<DemoActivityEntry, 'id' | 'atUtc' | 'action'>> = {}) {
  const viewer = personById(more.userId) ?? me();
  const log = db().activityLog;
  const now = nowUtc();
  if (VIEWS.includes(action)) {
    const recent = log.some(
      (e) =>
        e.action === action &&
        e.userId === viewer.id &&
        e.patientId === (more.patientId ?? null) &&
        e.pathwayId === (more.pathwayId ?? null) &&
        e.details === (more.details ?? null) &&
        Date.parse(now) - Date.parse(e.atUtc) < VIEW_WINDOW_MS,
    );
    if (recent) return;
  }
  log.push({
    id: newId(),
    atUtc: now,
    action,
    userId: viewer.id,
    username: viewer.username,
    ipAddress: '192.0.2.10',
    patientId: null,
    pathwayId: null,
    targetUsername: null,
    details: null,
    ...more,
  });
}

/** Every pathway audit entry, worked out from the pathways as they are (newest first). */
export function auditEntries(): AuditEntry[] {
  const entries: AuditEntry[] = [];
  for (const p of db().pathways) {
    const add = (key: string, action: string, atUtc: string | null, userId: string | null | undefined, more: Partial<AuditEntry> = {}) => {
      if (!atUtc) return;
      entries.push({
        id: `${p.id}:${key}`,
        patientPathwayId: p.id,
        pathwayTitle: p.title,
        patientId: p.patientId,
        stageId: null,
        stageName: null,
        action,
        userId: userId ?? undefined,
        username: usernameOf(userId),
        atUtc,
        details: null,
        ...more,
      });
    };
    // Who made it isn't kept: the one who assigned it first, else the admin (as the timeline says).
    add('created', 'Created', p.createdAt, p.assignments[0]?.assignedByUserId ?? ADMIN.id, {
      details: JSON.stringify({template: p.templateName, stageCount: p.stages.length, dueDate: p.dueDate, priority: p.priority}),
    });
    for (const a of p.assignments)
      add(`assigned:${a.userId}`, 'Assigned', a.assignedAtUtc, a.assignedByUserId, {details: JSON.stringify({username: usernameOf(a.userId)})});
    for (const s of p.stages) {
      const stage = {stageId: s.id, stageName: s.name};
      add(`${s.id}:started`, 'StageStarted', s.startedAtUtc, s.startedByUserId, stage);
      add(`${s.id}:finished`, 'StageFinished', s.completedAtUtc, s.completedByUserId, stage);
      add(`${s.id}:skipped`, 'StageSkipped', s.skippedAtUtc, s.skippedByUserId, {
        ...stage,
        // The reason by name; its typed detail stays on the stage.
        details: JSON.stringify({skipReason: s.skipReason}),
      });
    }
    // The relay (MC7): each handover by IDs and team names (`{stageId, fromTeamId, from, toTeamId, to}`), and each
    // pick-up; never free text.
    for (const [i, h] of p.handovers.entries()) {
      const s = p.stages.find((x) => x.id === h.stageId);
      add(`handover:${i}`, 'StageHandedOver', h.atUtc, h.byUserId, {
        stageId: h.stageId,
        stageName: s?.name ?? null,
        details: JSON.stringify({stageId: h.stageId, fromTeamId: h.fromTeamId, from: teamName(h.fromTeamId), toTeamId: h.toTeamId, to: teamName(h.toTeamId)}),
      });
    }
    for (const s of p.stages)
      if (s.handedOverFromTeamId && s.pickedUpAtUtc)
        add(`${s.id}:pickedUp`, 'StagePickedUp', s.pickedUpAtUtc, s.pickedUpByUserId, {stageId: s.id, stageName: s.name});
    // A note by its ID and version only: never its text.
    for (const n of p.notes) {
      add(`${n.id}:added`, 'NoteAdded', n.createdAtUtc, n.authorUserId, {details: JSON.stringify({noteId: n.id, version: 1})});
      if (n.lastEditedAtUtc && n.version > 1)
        add(`${n.id}:edited`, 'NoteEdited', n.lastEditedAtUtc, n.authorUserId, {details: JSON.stringify({noteId: n.id, version: n.version})});
      if (n.deleted)
        add(`${n.id}:deleted`, 'NoteDeleted', n.deleted.atUtc, n.deleted.byUserId, {
          details: JSON.stringify({noteId: n.id, version: n.version, deletionReason: n.deleted.reason}),
        });
    }
    // A branch added on new information (MC10): which, after which stage and the stages it added; never the reason.
    for (const a of p.branchAdditions ?? [])
      add(`branch:${a.id}`, 'BranchAdded', a.addedAtUtc, a.addedByUserId, {
        stageId: a.afterStageId,
        stageName: p.stages.find((s) => s.id === a.afterStageId)?.name ?? null,
        details: JSON.stringify({templateBranchId: a.templateBranchId, branch: a.name, afterStageId: a.afterStageId, stageIds: a.stageIds}),
      });
    for (const [i, c] of p.statusChanges.entries())
      add(`status:${i}`, 'StatusChanged', c.atUtc, c.userId, {details: JSON.stringify({from: c.from, to: c.to})});
  }
  return entries.sort((a, b) => (b.atUtc ?? '').localeCompare(a.atUtc ?? '') || (b.id ?? '').localeCompare(a.id ?? ''));
}

const inDates = (atUtc: string | null | undefined, from: string | null, to: string | null) =>
  !!atUtc && (!from || ukDateOf(atUtc) >= from) && (!to || ukDateOf(atUtc) <= to);

/** Newest first, unless `Ascending=true`. */
const ordered = <T extends {atUtc?: string; id?: string}>(items: T[], query: URLSearchParams) => {
  const sorted = [...items].sort((a, b) => (b.atUtc ?? '').localeCompare(a.atUtc ?? '') || (b.id ?? '').localeCompare(a.id ?? ''));
  return query.get('Ascending') === 'true' ? sorted.reverse() : sorted;
};

/** The time-based events (`MonitoringEvents.ServiceAlerts`): a job finds them in the full version; nothing runs here. */
const SERVICE_ALERTS = ['ReferralNotTriaged', 'ResultNotSignedOff', 'VisitNotBooked', 'SpecimenOverdue', 'WaitBreached', 'PauseReviewPassed'];

const EVENTS = [
  'PathwayCreated',
  'StageFinished',
  'PathwayCompleted',
  'PathwayBlocked',
  'PathwayCancelled',
  'PathwayAssigned',
  'StageUnlocked',
  'DueDateChanged',
  'StageFinishedWithoutVisit',
  ...SERVICE_ALERTS,
];
const PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'];

/** A monitoring rule as the API answers it, with the names of what it names. */
function ruleResponse(rule: DemoMonitoringRule): Schemas['MonitoringRuleResponse'] {
  const state = db();
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- the owner isn't in the response
  const {ownerUserId, ...fields} = rule;
  return {
    ...fields,
    subjectUsername: usernameOf(rule.subjectUserId),
    templateName: state.templates.find((t) => t.id === rule.templateId)?.name ?? null,
    stageName: state.stageDefinitions.find((d) => d.id === rule.stageDefinitionId)?.name ?? null,
    thresholdDays: rule.thresholdDays ?? null,
    teamId: rule.teamId ?? null,
    teamName: state.teams.find((s) => s.id === rule.teamId)?.name ?? null,
  };
}

/** The fields a rule's create or edit sets, checked (`MonitoringRuleInput`). */
function ruleInput(body: Partial<Schemas['SaveMonitoringRuleRequest']>) {
  const errors: Record<string, string[]> = {};
  if (!body.event || !EVENTS.includes(body.event)) errors.event = ['Choose what to be told about.'];
  if (body.minimumPriority && !PRIORITIES.includes(body.minimumPriority)) errors.minimumPriority = ['Choose a priority.'];
  if (body.subjectUserId && !personById(body.subjectUserId)) errors.subjectUserId = ['Choose someone on the staff, or anyone.'];
  const isAlert = SERVICE_ALERTS.includes(body.event ?? '');
  const days = body.thresholdDays;
  if (isAlert && (days === null || days === undefined || days < 0 || days > 90))
    errors.thresholdDays = ['Give the number of days, from 0 to 90.'];
  if (!isAlert && days !== null && days !== undefined) errors.thresholdDays = ['Days are only for service alerts.'];
  if (isAlert && body.event !== 'ResultNotSignedOff' && body.subjectUserId)
    errors.subjectUserId = ['A service alert is about the service, not one person (except results, for the responsible clinician).'];
  if (body.event === 'ReferralNotTriaged' && body.templateId) errors.templateId = ['A referral awaiting triage has no template yet.'];
  if (body.teamId && !db().teams.some((s) => s.id === body.teamId)) errors.teamId = ['Choose a team.'];
  if (body.event === 'StageUnlocked' && body.subjectUserId && !personById(body.subjectUserId)?.permissions.includes('UnlockStages'))
    errors.subjectUserId = ['Only someone who can unlock stages can unlock one.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  const hasStage = ['StageFinished', 'StageUnlocked', 'StageFinishedWithoutVisit', 'VisitNotBooked', 'SpecimenOverdue'].includes(body.event!);
  return {
    subjectUserId: body.subjectUserId || null,
    event: body.event!,
    minimumPriority: body.minimumPriority || null,
    templateId: body.templateId || null,
    stageDefinitionId: hasStage ? body.stageDefinitionId || null : null,
    thresholdDays: isAlert ? (days ?? null) : null,
    teamId: body.teamId || null,
    isEnabled: body.isEnabled ?? true,
  };
}

/** One of the signed-in admin's own rules (another's is a 404, as in the API). */
function findRule(ruleId: string): DemoMonitoringRule {
  const rule = db().monitoringRules.find((r) => r.id === ruleId && r.ownerUserId === me().id);
  if (!rule) refuse(notFound('The monitoring rule'));
  return rule;
}

export function registerAdminLogs() {
  on('get', '/api/activity/search', ({query}) => {
    requirePermission('ViewAudit');
    const actions = list(query, 'Actions');
    const search = query.get('Search');
    const items = db()
      .activityLog.filter(
        (e) =>
          (!query.get('PatientId') || e.patientId === query.get('PatientId')) &&
          (!query.get('UserId') || e.userId === query.get('UserId')) &&
          (actions.length === 0 || actions.includes(e.action ?? '')) &&
          inDates(e.atUtc, query.get('From'), query.get('To')) &&
          (!search || contains(e.username, search) || contains(e.targetUsername, search) || (!!e.patientId && contains(patientName(e.patientId), search))),
      )
      .map((e) => ({...e, patientName: e.patientId ? patientName(e.patientId) : null}));
    return paged(ordered(items, query), query);
  });

  on('get', '/api/pathways/audit/search', ({query}) => {
    requirePermission('ViewAudit');
    const actions = [...list(query, 'Actions'), ...(query.get('Action') ? [query.get('Action')!] : [])];
    const search = query.get('Search');
    const items = auditEntries().filter(
      (e) =>
        (!query.get('PathwayId') || e.patientPathwayId === query.get('PathwayId')) &&
        (!query.get('PatientId') || e.patientId === query.get('PatientId')) &&
        (!query.get('UserId') || e.userId === query.get('UserId')) &&
        (actions.length === 0 || actions.includes(e.action ?? '')) &&
        inDates(e.atUtc, query.get('From'), query.get('To')) &&
        (!search || contains(e.pathwayTitle, search) || contains(e.username, search) || contains(e.stageName, search)),
    );
    return paged(ordered(items, query), query);
  });

  on('get', '/api/monitoring/rules', () => {
    requirePermission('ManageMonitoring');
    const viewer = me();
    return db()
      .monitoringRules.filter((r) => r.ownerUserId === viewer.id)
      .sort((a, b) => (a.createdAtUtc ?? '').localeCompare(b.createdAtUtc ?? ''))
      .map(ruleResponse);
  });
  on('post', '/api/monitoring/rules', ({body}) => {
    requirePermission('ManageMonitoring');
    const now = nowUtc();
    const rule: DemoMonitoringRule = {id: newId(), ownerUserId: me().id, ...ruleInput(body), createdAtUtc: now, updatedAtUtc: now};
    db().monitoringRules.push(rule);
    return ruleResponse(rule);
  });
  on('put', '/api/monitoring/rules/{id}', ({params, body}) => {
    requirePermission('ManageMonitoring');
    const rule = findRule(params.id);
    Object.assign(rule, ruleInput(body), {updatedAtUtc: nowUtc()});
    return ruleResponse(rule);
  });
  on('put', '/api/monitoring/rules/{id}/enabled', ({params, body}) => {
    requirePermission('ManageMonitoring');
    const rule = findRule(params.id);
    rule.isEnabled = body.isEnabled ?? rule.isEnabled;
    rule.updatedAtUtc = nowUtc();
    return ruleResponse(rule);
  });
  on('delete', '/api/monitoring/rules/{id}', ({params}) => {
    requirePermission('ManageMonitoring');
    findRule(params.id);
    db().monitoringRules = db().monitoringRules.filter((r) => r.id !== params.id);
    return undefined;
  });

  on('get', '/api/jobs', () => {
    requirePermission('ManageSetup');
    return db().jobs;
  });
}
