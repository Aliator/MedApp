import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoMdtItem} from '../slices/clinical';
import {db} from '../store';
import {daysFrom, nowUtc, ukTime} from '../time';
import {can, me, pathwayPatient, requirePermission} from './common';
import {findPathway, pathwayToChange} from './pathways';
import {canChangeRecord, canSeeRecord, ensureCanChangeRecord, myScope, seeRecord, type TeamScope} from './teamWalls';

/**
 * The MDT list (`api/mdt`), simplified from the API's `MdtRules`: pathways listed for a team's MDT (a calendar
 * meeting with `isMdt` and `teamId`), deferred to a later one, withdrawn, and once the meeting has started, the
 * team's outcome recorded as written, with who was present from its attendees (final). Every change carries
 * `expectedVersion`. **MedApp never suggests an outcome or changes the pathway because of one;** the pathway's
 * assignees are told (a fixed title). A deleted MDT leaves its items to list again ("Meeting cancelled").
 * MC21: an item's presenter, one of its meeting's attendees (a lab member may attend any MDT, and reads the pathways
 * listed on it: `hasMdtGrant`); each item shows the lab's samples and reports on its pathway. Only the item's own team
 * lists, moves, withdraws or chooses the presenter; attendees from elsewhere only read.
 * MC26: an attendee from any team reads the cases listed on the MDTs they attend, and only those (`mdtGrantedItems`):
 * the list and counts show them only what they may see, with why (`readOnlyReason`).
 */

type Meeting = Schemas['MeetingResponse'];

const CHANGED_MEANWHILE = 'Someone else changed this MDT item since you opened it. Reload it, then try again.';
const MAX_QUESTION = 1000;
const MAX_OUTCOME = 2000;

const isOpen = (item: DemoMdtItem) => item.status === 'Listed' || item.status === 'Deferred';

/** An MDT: a meeting marked as a team's. */
const isMdt = (meeting: Meeting | undefined): meeting is Meeting => Boolean(meeting?.isMdt && meeting.teamId);

/** The MDT an item is on now: none when it was cancelled (deleted from the calendar) or never had one. */
const meetingOf = (item: DemoMdtItem) => (item.meetingId ? db().meetings.find((m) => m.id === item.meetingId && isMdt(m)) : undefined);

const teamName = (teamId: string | null | undefined) => db().teams.find((s) => s.id === teamId)?.name ?? '';

/** The lab's samples on an item's pathway, with their reports (MC21), oldest taken first; none entered in error. */
function labWorkOf(pathwayId: string): Schemas['MdtLabWorkResponse'][] {
  const state = db();
  return state.specimens
    .filter((s) => s.pathwayId === pathwayId && s.status !== 'EnteredInError')
    .sort((a, b) => a.takenAtUtc.localeCompare(b.takenAtUtc) || a.id.localeCompare(b.id))
    .map((s) => ({
      specimenId: s.id,
      number: s.number,
      typeName: state.specimenTypes.find((t) => t.id === s.specimenTypeId)?.name ?? null,
      status: s.status,
      reportedAtUtc: s.reportedAtUtc,
      resultId: state.results.find((r) => r.specimenId === s.id && r.status !== 'EnteredInError')?.id ?? null,
    }));
}

/** An item as the API answers it, for the signed-in person (`MdtMapper.ToResponse`). */
function itemResponse(item: DemoMdtItem): Schemas['MdtItemResponse'] {
  const meeting = meetingOf(item);
  const pathway = db().pathways.find((p) => p.id === item.pathwayId);
  const open = isOpen(item);
  const ours = canChangeRecord(item);
  return {
    id: item.id,
    status: item.status,
    patient: pathwayPatient(item.patientId),
    pathwayId: item.pathwayId,
    pathwayTitle: pathway?.title ?? '',
    teamId: item.teamId,
    teamName: teamName(item.teamId),
    meetingId: meeting?.id ?? null,
    meetingTitle: meeting?.title ?? null,
    meetingStartUtc: meeting?.startUtc ?? null,
    meetingCancelledStartUtc: meeting ? null : item.meetingStartUtc,
    question: item.question,
    listedByUsername: usernameOf(item.listedByUserId),
    listedAtUtc: item.listedAtUtc,
    outcome: item.outcome,
    outcomeByUsername: usernameOf(item.outcomeByUserId),
    outcomeAtUtc: item.outcomeAtUtc,
    present: item.presentUserIds.map((id) => usernameOf(id) ?? 'Deleted user').sort((a, b) => a.localeCompare(b)),
    withdrawnByUsername: usernameOf(item.withdrawnByUserId),
    withdrawnAtUtc: item.withdrawnAtUtc,
    version: item.version,
    canWithdraw: can('WorkPathways') && ours && open,
    canDefer: can('WorkPathways') && ours && open,
    canRecordOutcome: can('RecordMdtOutcomes') && open && meeting !== undefined && (meeting.startUtc ?? '') <= nowUtc(),
    readOnlyReason: ours || myScope().seesAllTeams ? null : attendeeReadOnly(item),
    presenterUserId: item.presenterUserId ?? null,
    presenterUsername: usernameOf(item.presenterUserId),
    canChoosePresenter: can('WorkPathways') && ours && open && meeting !== undefined,
    labWork: labWorkOf(item.pathwayId),
  };
}

/** Why an MDT's attendee from another team (MC26) reads an item but can't change it. */
const attendeeReadOnly = (item: DemoMdtItem) =>
  `${teamName(item.teamId) || 'Another team'}’s case: you attend its MDT, so you read it until it’s discussed or withdrawn. Only its team changes it.`;

/** Whether the viewer sees an MDT's whole list: its team's members and admins (attendees from elsewhere see the cases listed only while their grant lasts). */
const seesWholeList = (meeting: Meeting, scope: TeamScope) => scope.seesAllTeams || scope.teamIds.includes(meeting.teamId ?? '');

/**
 * Why the viewer sees only part of an MDT's list, or none of it (MC26): an attendee from another team reads the cases
 * listed, read only, while their grant lasts; anyone else from another team sees none.
 */
function listReadOnlyReason(meeting: Meeting, scope: TeamScope): string | null {
  if (seesWholeList(meeting, scope)) return null;
  const team = teamName(meeting.teamId) || 'another team';
  return (meeting.attendees ?? []).some((a) => a.userId === scope.userId)
    ? `You attend ${team}’s MDT, so you can read the cases listed on it (not change them) until each is discussed or withdrawn and the meeting has ended.`
    : `This is ${team}’s MDT and you aren’t invited: only its team and its attendees see the cases listed on it.`;
}

/** An MDT with its counts (of the items the viewer may see) and attendees (`MdtMeetingResponse`). */
function meetingResponse(meeting: Meeting): Schemas['MdtMeetingResponse'] {
  const scope = myScope();
  const items = db().mdtItems.filter((i) => meetingOf(i)?.id === meeting.id && canSeeRecord(i, scope));
  return {
    id: meeting.id,
    title: meeting.title,
    startUtc: meeting.startUtc,
    endUtc: meeting.endUtc,
    teamId: meeting.teamId ?? undefined,
    teamName: teamName(meeting.teamId),
    openItemCount: items.filter(isOpen).length,
    itemCount: items.length,
    attendees: (meeting.attendees ?? [])
      .map((a) => ({userId: a.userId, username: a.username}))
      .sort((a, b) => (a.username ?? '').localeCompare(b.username ?? '')),
  };
}

/** The item, or a 404: also when its pathway is behind the viewer's team wall. */
function findItem(id: string): DemoMdtItem {
  return seeRecord(db().mdtItems.find((i) => i.id === id), 'MdtItem', 'The MDT item');
}

function findMeeting(id: string): Meeting {
  const meeting = db().meetings.find((m) => m.id === id);
  if (!meeting) refuse(notFound('The meeting'));
  return meeting;
}

function ensureVersion(item: DemoMdtItem, expectedVersion: number | undefined) {
  if (item.version !== expectedVersion) refuse(problem(409, CHANGED_MEANWHILE));
}

function ensureOpen(item: DemoMdtItem) {
  if (!isOpen(item)) refuse(problem(409, "This MDT item has been discussed or withdrawn, so it can't change."));
}

/** An MDT that hasn't ended (and, when moving an item, starts after the one it's on). */
function ensureCanListFor(meeting: Meeting, after?: string | null) {
  if (!isMdt(meeting)) refuse(problem(409, "That meeting isn't an MDT."));
  if ((meeting.endUtc ?? '') <= nowUtc()) refuse(problem(409, 'That MDT has already ended: choose a later one.'));
  if (after && (meeting.startUtc ?? '') <= after) refuse(problem(409, "Choose a later MDT than the one it's on."));
}

function ensureNotListed(pathwayId: string, meetingId: string, exceptId?: string) {
  if (db().mdtItems.some((i) => i.id !== exceptId && i.pathwayId === pathwayId && i.meetingId === meetingId && isOpen(i)))
    refuse(problem(409, "This pathway is already on that MDT's list."));
}

const byListed = (a: DemoMdtItem, b: DemoMdtItem) => a.listedAtUtc.localeCompare(b.listedAtUtc) || a.id.localeCompare(b.id);

export function registerMdt() {
  on('get', '/api/mdt/meetings', ({query}) => {
    me();
    const teamId = query.get('teamId');
    const from = ukTime(daysFrom(-60), 0);
    const to = ukTime(daysFrom(180), 0);
    return db()
      .meetings.filter(isMdt)
      .filter((m) => (!teamId || m.teamId === teamId) && (m.startUtc ?? '') >= from && (m.startUtc ?? '') < to)
      .sort((a, b) => (a.startUtc ?? '').localeCompare(b.startUtc ?? '') || (a.id ?? '').localeCompare(b.id ?? ''))
      .map(meetingResponse);
  });
  on('get', '/api/mdt/meetings/{meetingId}/items', ({params}) => {
    requirePermission('ViewPatients');
    const meeting = findMeeting(params.meetingId);
    if (!isMdt(meeting)) return problem(409, "That meeting isn't an MDT.");
    const scope = myScope();
    return {
      meeting: meetingResponse(meeting),
      readOnlyReason: listReadOnlyReason(meeting, scope),
      items: db()
        .mdtItems.filter((i) => meetingOf(i)?.id === meeting.id && canSeeRecord(i, scope))
        .sort(byListed)
        .map(itemResponse),
    };
  });
  on('get', '/api/mdt/awaiting-meeting', ({query}) => {
    requirePermission('ViewPatients');
    const teamId = query.get('teamId');
    const scope = myScope();
    return db()
      .mdtItems.filter((i) => isOpen(i) && !meetingOf(i) && (!teamId || i.teamId === teamId) && canSeeRecord(i, scope))
      .sort(byListed)
      .map(itemResponse);
  });
  on('get', '/api/mdt/pathways/{pathwayId}/items', ({params}) => {
    requirePermission('ViewPatients');
    findPathway(params.pathwayId);
    return db()
      .mdtItems.filter((i) => i.pathwayId === params.pathwayId)
      .sort((a, b) => -byListed(a, b))
      .map(itemResponse);
  });

  on('post', '/api/mdt/items', ({body}) => {
    requirePermission('WorkPathways');
    const viewer = me();
    const errors: Record<string, string[]> = {};
    const question = body.question?.trim() ?? '';
    if (!body.pathwayId) errors.pathwayId = ['Choose the pathway.'];
    if (!body.meetingId) errors.meetingId = ['Choose an MDT.'];
    if (!question) errors.question = ['Give the question for the team.'];
    else if (question.length > MAX_QUESTION) errors.question = [`The question must not exceed ${MAX_QUESTION} characters.`];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const pathway = pathwayToChange(body.pathwayId!);
    if (['Completed', 'Cancelled', 'Archived'].includes(pathway.status))
      return problem(409, "A completed, cancelled or archived pathway can't be listed for an MDT.");
    const meeting = findMeeting(body.meetingId!);
    ensureCanListFor(meeting);
    ensureNotListed(pathway.id, meeting.id!);
    const item: DemoMdtItem = {
      id: newId(),
      patientId: pathway.patientId,
      pathwayId: pathway.id,
      teamId: meeting.teamId!,
      meetingId: meeting.id!,
      meetingStartUtc: meeting.startUtc ?? null,
      question,
      status: 'Listed',
      listedByUserId: viewer.id,
      listedAtUtc: nowUtc(),
      outcome: null,
      outcomeByUserId: null,
      outcomeAtUtc: null,
      presentUserIds: [],
      withdrawnByUserId: null,
      withdrawnAtUtc: null,
      presenterUserId: null,
      version: 1,
    };
    db().mdtItems.push(item);
    return new DemoReply(201, itemResponse(item));
  });
  on('post', '/api/mdt/items/{id}/withdraw', ({params, body}) => {
    requirePermission('WorkPathways');
    const viewer = me();
    const item = findItem(params.id);
    ensureCanChangeRecord(item, 'This MDT item');
    ensureVersion(item, body.expectedVersion);
    ensureOpen(item);
    Object.assign(item, {status: 'Withdrawn', withdrawnByUserId: viewer.id, withdrawnAtUtc: nowUtc(), version: item.version + 1});
    return itemResponse(item);
  });
  on('post', '/api/mdt/items/{id}/defer', ({params, body}) => {
    requirePermission('WorkPathways');
    if (!body.meetingId) return invalid({meetingId: ['Choose an MDT.']});
    const item = findItem(params.id);
    ensureCanChangeRecord(item, 'This MDT item');
    ensureVersion(item, body.expectedVersion);
    ensureOpen(item);
    const meeting = findMeeting(body.meetingId);
    const current = meetingOf(item);
    ensureCanListFor(meeting, current?.startUtc);
    ensureNotListed(item.pathwayId, meeting.id!, item.id);
    // Deferred by the team when it was on a meeting; listed again, as it was, when its meeting was cancelled.
    Object.assign(item, {
      status: current ? 'Deferred' : item.status,
      meetingId: meeting.id,
      meetingStartUtc: meeting.startUtc ?? null,
      teamId: meeting.teamId,
      // The presenter stays only if they're invited to the new MDT too.
      presenterUserId: (meeting.attendees ?? []).some((a) => a.userId === item.presenterUserId) ? item.presenterUserId : null,
      version: item.version + 1,
    });
    return itemResponse(item);
  });
  on('post', '/api/mdt/items/{id}/presenter', ({params, body}) => {
    requirePermission('WorkPathways');
    const item = findItem(params.id);
    ensureCanChangeRecord(item, 'This MDT item');
    ensureVersion(item, body.expectedVersion);
    ensureOpen(item);
    const meeting = meetingOf(item);
    if (!meeting) return problem(409, 'Its meeting was cancelled: list it for another MDT first.');
    const presenterUserId = body.presenterUserId ?? null;
    if (presenterUserId && !(meeting.attendees ?? []).some((a) => a.userId === presenterUserId))
      return invalid({presenterUserId: ["Choose the presenter from the MDT's attendees."]});
    Object.assign(item, {presenterUserId, version: item.version + 1});
    return itemResponse(item);
  });
  on('post', '/api/mdt/items/{id}/outcome', ({params, body}) => {
    requirePermission('RecordMdtOutcomes');
    const viewer = me();
    const errors: Record<string, string[]> = {};
    const outcome = body.outcome?.trim() ?? '';
    const present = [...new Set(body.presentUserIds ?? [])];
    if (!outcome) errors.outcome = ["Write the team's outcome."];
    else if (outcome.length > MAX_OUTCOME) errors.outcome = [`The outcome must not exceed ${MAX_OUTCOME} characters.`];
    if (present.length === 0) errors.presentUserIds = ['Choose who was present.'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const item = findItem(params.id);
    ensureVersion(item, body.expectedVersion);
    ensureOpen(item);
    const meeting = meetingOf(item);
    if (!meeting) return problem(409, 'Its meeting was cancelled: list it for another MDT first.');
    if ((meeting.startUtc ?? '') > nowUtc()) return problem(409, 'Its outcome can be recorded once its meeting has started.');
    const invited = new Set((meeting.attendees ?? []).map((a) => a.userId));
    if (present.some((id) => !invited.has(id))) return invalid({presentUserIds: ["Choose who was present from the meeting's attendees."]});
    const now = nowUtc();
    Object.assign(item, {status: 'Discussed', outcome, outcomeByUserId: viewer.id, outcomeAtUtc: now, presentUserIds: present, version: item.version + 1});
    // The pathway's assignees are told, so someone acts on it: never the outcome, the question or the patient.
    const pathway = db().pathways.find((p) => p.id === item.pathwayId);
    const team = teamName(item.teamId) || 'team';
    for (const userId of new Set((pathway?.assignments ?? []).map((a) => a.userId)))
      if (userId !== viewer.id)
        db().notifications.push({
          id: newId(),
          userId,
          kind: 'MdtOutcomeRecorded',
          title: 'MDT outcome recorded',
          message: `${viewer.username} recorded the ${team} MDT's outcome on a pathway you're assigned to.`,
          pathwayId: item.pathwayId,
          createdAtUtc: now,
          isRead: false,
        });
    return itemResponse(item);
  });
}
