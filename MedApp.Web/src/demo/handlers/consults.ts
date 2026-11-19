import type {Schemas} from '../../api/client';
import {PEOPLE, usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {consultAccess, type DemoConsult} from '../slices/consults';
import type {DemoPathway} from '../state';
import {db} from '../store';
import {nowUtc} from '../time';
import {logActivity} from './adminLogs';
import {can, me, pathwayPatient, requirePermission, teamIdsOf} from './common';
import {findPathway} from './pathways';
import {canChangePathway, canSeePathway, myScope, refuseAcrossWall, type TeamScope} from './teamWalls';

/**
 * Consults (`api/consults`, MC25 of `docs/multi-clinic-plan.md`), simplified from the API's `ConsultChange`: a pathway's
 * own team asks another team (a clinic or the lab) a question; the asked team reads the pathway while it's open (a
 * `Consult` grant, written with the consult and ended with it), and replies; only the asking team closes it after the
 * reply, or withdraws it before one. Every change carries `expectedVersion`. **Nothing is suggested:** the team and the
 * words are the clinicians'. The audit log and notifications name teams and people only, never the question, the reply
 * or the patient. A restricted or anonymised record can't be asked about or replied to; closing and withdrawing stay
 * allowed (they protect the patient by ending the other team's reading).
 */

type AuditEntry = Schemas['PatientPathwayAuditEntryResponse'];

const CHANGED_MEANWHILE = 'Someone else changed this consult since you opened it. Reload it, then try again.';
const MAX_QUESTION = 2000;
const MAX_REPLY = 4000;
const CLOSED_PATHWAY: DemoPathway['status'][] = ['Completed', 'Cancelled', 'Archived'];

const teamName = (teamId: string | null | undefined) => db().teams.find((t) => t.id === teamId)?.name ?? 'another team';

const isOpen = (consult: DemoConsult) => consult.status === 'Open' || consult.status === 'Replied';

const isRecordLocked = (patientId: string) => {
  const patient = db().patients.find((p) => p.id === patientId);
  return Boolean(patient?.isRestricted || patient?.isAnonymised);
};

function ensureRecordCanChange(patientId: string) {
  if (isRecordLocked(patientId)) refuse(problem(409, 'This patient’s record is restricted, so it can’t be changed.'));
}

/** Whether the viewer may ask about a pathway: its own team, with WorkPathways, while it's open and the record can change. */
const canRequestOn = (pathway: DemoPathway, scope: TeamScope) =>
  can('WorkPathways') && canChangePathway(pathway, scope) && !CLOSED_PATHWAY.includes(pathway.status) && !isRecordLocked(pathway.patientId);

/** A consult as the API answers it, for the signed-in person (`ConsultMapper.ToResponse`). */
function consultResponse(consult: DemoConsult, scope: TeamScope = myScope()): Schemas['ConsultResponse'] {
  const pathway = db().pathways.find((p) => p.id === consult.pathwayId);
  const works = can('WorkPathways');
  const asked = works && scope.teamIds.includes(consult.askedTeamId);
  const asking = works && scope.teamIds.includes(consult.askingTeamId);
  return {
    id: consult.id,
    status: consult.status,
    patient: pathwayPatient(consult.patientId),
    pathwayId: consult.pathwayId,
    pathwayTitle: pathway?.title ?? '',
    askingTeamId: consult.askingTeamId,
    askingTeamName: teamName(consult.askingTeamId),
    askedTeamId: consult.askedTeamId,
    askedTeamName: teamName(consult.askedTeamId),
    question: consult.question,
    requestedByUsername: usernameOf(consult.requestedByUserId),
    requestedAtUtc: consult.requestedAtUtc,
    reply: consult.reply,
    repliedByUsername: usernameOf(consult.repliedByUserId),
    repliedAtUtc: consult.repliedAtUtc,
    closedByUsername: usernameOf(consult.closedByUserId),
    closedAtUtc: consult.closedAtUtc,
    version: consult.version,
    canReply: asked && consult.status === 'Open' && !isRecordLocked(consult.patientId),
    canClose: asking && consult.status === 'Replied',
    canWithdraw: asking && consult.status === 'Open',
  };
}

/** The consult, or a 404: also when its pathway is behind the viewer's team wall (a closed consult's asked team, say). */
function findConsult(id: string): DemoConsult {
  const consult = db().consults.find((c) => c.id === id);
  if (!consult) refuse(notFound('The consult'));
  if (!canSeePathway({id: consult.pathwayId})) refuseAcrossWall('Consult', id, 'The consult');
  return consult;
}

function ensureVersion(consult: DemoConsult, expectedVersion: number | undefined) {
  if (consult.version !== expectedVersion) refuse(problem(409, CHANGED_MEANWHILE));
}

/** Only the asking team closes or withdraws a consult. */
function ensureAskingTeam(consult: DemoConsult) {
  if (!myScope().teamIds.includes(consult.askingTeamId))
    refuse(problem(409, `This consult is ${teamName(consult.askingTeamId)}’s: only the team that asked closes or withdraws it.`));
}

/** Ends the asked team's reading of the pathway (the consult's grant). */
function endGrant(consult: DemoConsult, atUtc: string) {
  for (const a of db().pathwayAccess) if (a.consultId === consult.id && a.endedAtUtc === null) a.endedAtUtc = atUtc;
}

/** Tells people of a consult: a fixed title, and who and which team; never the question, the reply or the patient. */
function tell(userIds: Iterable<string>, kind: 'ConsultRequested' | 'ConsultReplied', message: string, consult: DemoConsult, atUtc: string) {
  for (const userId of new Set(userIds))
    db().notifications.push({
      id: newId(),
      userId,
      kind,
      title: kind === 'ConsultRequested' ? 'Consult requested' : 'Consult replied',
      message,
      pathwayId: consult.pathwayId,
      createdAtUtc: atUtc,
      isRead: false,
    });
}

const newestFirst = (a: DemoConsult, b: DemoConsult) => b.requestedAtUtc.localeCompare(a.requestedAtUtc) || b.id.localeCompare(a.id);

/**
 * Every consult's audit entries (on its pathway): asked, replied, closed or withdrawn, with the teams by ID and name
 * (`{consultId, askingTeamId, askingTeam, askedTeamId, askedTeam}`); never the question or the reply.
 */
export function consultAuditEntries(): AuditEntry[] {
  const entries: AuditEntry[] = [];
  for (const c of db().consults) {
    const pathway = db().pathways.find((p) => p.id === c.pathwayId);
    const details = JSON.stringify({
      consultId: c.id,
      askingTeamId: c.askingTeamId,
      askingTeam: teamName(c.askingTeamId),
      askedTeamId: c.askedTeamId,
      askedTeam: teamName(c.askedTeamId),
    });
    const add = (key: string, action: string, atUtc: string | null, userId: string | null) => {
      if (!atUtc) return;
      entries.push({
        id: `${c.pathwayId}:consult:${c.id}:${key}`,
        patientPathwayId: c.pathwayId,
        pathwayTitle: pathway?.title ?? null,
        patientId: c.patientId,
        stageId: null,
        stageName: null,
        action,
        userId: userId ?? undefined,
        username: usernameOf(userId),
        atUtc,
        details,
      });
    };
    add('requested', 'ConsultRequested', c.requestedAtUtc, c.requestedByUserId);
    add('replied', 'ConsultReplied', c.repliedAtUtc, c.repliedByUserId);
    add(c.status === 'Withdrawn' ? 'withdrawn' : 'closed', c.status === 'Withdrawn' ? 'ConsultWithdrawn' : 'ConsultClosed', c.closedAtUtc, c.closedByUserId);
  }
  return entries;
}

export function registerConsults() {
  on('get', '/api/consults/pathways/{pathwayId}', ({params}) => {
    requirePermission('ViewPatients');
    const pathway = findPathway(params.pathwayId);
    const scope = myScope();
    return {
      consults: db()
        .consults.filter((c) => c.pathwayId === pathway.id)
        .sort(newestFirst)
        .map((c) => consultResponse(c, scope)),
      canRequest: canRequestOn(pathway, scope),
    };
  });
  on('get', '/api/consults/incoming', ({query}) => {
    requirePermission('ViewPatients');
    const scope = myScope();
    // One of the viewer's own teams (an admin: any), or all of them.
    const teamId = query.get('teamId')?.trim().toLowerCase() || null;
    if (teamId && !scope.seesAllTeams && !scope.teamIds.some((t) => t.toLowerCase() === teamId))
      return invalid({teamId: ['Choose one of your own teams, or all of them.']});
    const items = db()
      .consults.filter(
        (c) =>
          isOpen(c) &&
          (scope.seesAllTeams || scope.teamIds.includes(c.askedTeamId)) &&
          (!teamId || c.askedTeamId.toLowerCase() === teamId),
      )
      .sort((a, b) => -newestFirst(a, b))
      .map((c) => consultResponse(c, scope));
    for (const c of items) logActivity('PatientListed', {patientId: c.patient?.id ?? null});
    return items;
  });

  on('post', '/api/consults', ({body}) => {
    requirePermission('WorkPathways');
    const viewer = me();
    const errors: Record<string, string[]> = {};
    const question = body.question?.trim() ?? '';
    if (!body.pathwayId) errors.pathwayId = ['Choose the pathway.'];
    if (!body.askedTeamId) errors.askedTeamId = ['Choose the team to ask.'];
    if (!question) errors.question = ['Write the question for the team.'];
    else if (question.length > MAX_QUESTION) errors.question = [`The question must not exceed ${MAX_QUESTION} characters.`];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const pathway = findPathway(body.pathwayId!);
    if (!canChangePathway(pathway))
      return problem(409, `This pathway is ${teamName(pathway.teamId)}’s: only its own team asks another team about it.`);
    if (CLOSED_PATHWAY.includes(pathway.status)) return problem(409, "A discharged, cancelled or archived pathway can't have a consult asked.");
    ensureRecordCanChange(pathway.patientId);
    const team = db().teams.find((t) => t.id === body.askedTeamId);
    if (!team || team.isArchived) return invalid({askedTeamId: ['Choose a team that’s in use.']});
    if (team.id === pathway.teamId) return invalid({askedTeamId: ['Choose another team: this pathway is your own team’s.']});
    if (db().consults.some((c) => c.pathwayId === pathway.id && c.askedTeamId === team.id && isOpen(c)))
      return problem(409, `This pathway already has an open consult with ${team.name}: close or withdraw it first.`);
    const now = nowUtc();
    const consult: DemoConsult = {
      id: newId(),
      pathwayId: pathway.id,
      patientId: pathway.patientId,
      askingTeamId: pathway.teamId,
      askedTeamId: team.id!,
      question,
      status: 'Open',
      requestedByUserId: viewer.id,
      requestedAtUtc: now,
      reply: null,
      repliedByUserId: null,
      repliedAtUtc: null,
      closedByUserId: null,
      closedAtUtc: null,
      version: 1,
    };
    db().consults.push(consult);
    // The asked team reads the pathway from now until the consult closes: written with it, as the API's ConsultChange does.
    db().pathwayAccess.push(consultAccess(consult));
    tell(
      PEOPLE.filter((p) => p.id !== viewer.id && p.permissions.includes('WorkPathways') && teamIdsOf(p).includes(consult.askedTeamId)).map((p) => p.id),
      'ConsultRequested',
      `${viewer.username} from ${teamName(consult.askingTeamId)} asked your team for a consult.`,
      consult,
      now,
    );
    return new DemoReply(201, consultResponse(consult));
  });
  on('post', '/api/consults/{id}/reply', ({params, body}) => {
    requirePermission('WorkPathways');
    const viewer = me();
    const reply = body.reply?.trim() ?? '';
    if (!reply) return invalid({reply: ['Write the reply.']});
    if (reply.length > MAX_REPLY) return invalid({reply: [`The reply must not exceed ${MAX_REPLY} characters.`]});
    const consult = findConsult(params.id);
    if (!myScope().teamIds.includes(consult.askedTeamId))
      return problem(409, `This consult was asked of ${teamName(consult.askedTeamId)}: only that team replies.`);
    ensureVersion(consult, body.expectedVersion);
    if (consult.status !== 'Open') return problem(409, 'This consult has a reply already, or is closed.');
    ensureRecordCanChange(consult.patientId);
    const now = nowUtc();
    Object.assign(consult, {status: 'Replied', reply, repliedByUserId: viewer.id, repliedAtUtc: now, version: consult.version + 1});
    // Who asked, and whoever has the pathway, are told: never the reply.
    const pathway = db().pathways.find((p) => p.id === consult.pathwayId);
    tell(
      [consult.requestedByUserId, ...(pathway?.assignments ?? []).map((a) => a.userId)].filter((id) => id !== viewer.id),
      'ConsultReplied',
      `${viewer.username} from ${teamName(consult.askedTeamId)} replied to your team’s consult.`,
      consult,
      now,
    );
    return consultResponse(consult);
  });
  on('post', '/api/consults/{id}/close', ({params, body}) => {
    requirePermission('WorkPathways');
    const viewer = me();
    const consult = findConsult(params.id);
    ensureAskingTeam(consult);
    ensureVersion(consult, body.expectedVersion);
    if (consult.status !== 'Replied')
      return problem(409, consult.status === 'Open' ? 'It has no reply yet: withdraw it if it’s no longer needed.' : 'This consult is closed already.');
    // Allowed on a restricted record: it ends the other team's reading.
    const now = nowUtc();
    Object.assign(consult, {status: 'Closed', closedByUserId: viewer.id, closedAtUtc: now, version: consult.version + 1});
    endGrant(consult, now);
    return consultResponse(consult);
  });
  on('post', '/api/consults/{id}/withdraw', ({params, body}) => {
    requirePermission('WorkPathways');
    const viewer = me();
    const consult = findConsult(params.id);
    ensureAskingTeam(consult);
    ensureVersion(consult, body.expectedVersion);
    if (consult.status !== 'Open')
      return problem(409, consult.status === 'Replied' ? 'It has a reply: close it instead.' : 'This consult is closed already.');
    const now = nowUtc();
    Object.assign(consult, {status: 'Withdrawn', closedByUserId: viewer.id, closedAtUtc: now, version: consult.version + 1});
    endGrant(consult, now);
    return consultResponse(consult);
  });
}
