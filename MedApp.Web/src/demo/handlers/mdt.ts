import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoMdtItem} from '../slices/clinical';
import {db} from '../store';
import {daysFrom, nowUtc, ukTime} from '../time';
import {can, me, pathwayPatient, requirePermission} from './common';
import {findPathway} from './pathways';

/**
 * The MDT list (`api/mdt`), simplified from the API's `MdtRules`: pathways listed for a specialty's MDT (a calendar
 * meeting with `isMdt` and `specialtyId`), deferred to a later one, withdrawn, and once the meeting has started, the
 * team's outcome recorded as written, with who was present from its attendees (final). Every change carries
 * `expectedVersion`. **MedApp never suggests an outcome or changes the pathway because of one;** the pathway's
 * assignees are told (a fixed title). A deleted MDT leaves its items to list again ("Meeting cancelled").
 */

type Meeting = Schemas['MeetingResponse'];

const CHANGED_MEANWHILE = 'Someone else changed this MDT item since you opened it. Reload it, then try again.';
const MAX_QUESTION = 1000;
const MAX_OUTCOME = 2000;

const isOpen = (item: DemoMdtItem) => item.status === 'Listed' || item.status === 'Deferred';

/** An MDT: a meeting marked as a specialty's. */
const isMdt = (meeting: Meeting | undefined): meeting is Meeting => Boolean(meeting?.isMdt && meeting.specialtyId);

/** The MDT an item is on now: none when it was cancelled (deleted from the calendar) or never had one. */
const meetingOf = (item: DemoMdtItem) => (item.meetingId ? db().meetings.find((m) => m.id === item.meetingId && isMdt(m)) : undefined);

const specialtyName = (specialtyId: string | null | undefined) => db().specialties.find((s) => s.id === specialtyId)?.name ?? '';

/** An item as the API answers it, for the signed-in person (`MdtMapper.ToResponse`). */
function itemResponse(item: DemoMdtItem): Schemas['MdtItemResponse'] {
  const meeting = meetingOf(item);
  const pathway = db().pathways.find((p) => p.id === item.pathwayId);
  const open = isOpen(item);
  return {
    id: item.id,
    status: item.status,
    patient: pathwayPatient(item.patientId),
    pathwayId: item.pathwayId,
    pathwayTitle: pathway?.title ?? '',
    specialtyId: item.specialtyId,
    specialtyName: specialtyName(item.specialtyId),
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
    canWithdraw: can('WorkPathways') && open,
    canDefer: can('WorkPathways') && open,
    canRecordOutcome: can('RecordMdtOutcomes') && open && meeting !== undefined && (meeting.startUtc ?? '') <= nowUtc(),
    readOnlyReason: null,
  };
}

/** An MDT with its counts and attendees (`MdtMeetingResponse`). */
function meetingResponse(meeting: Meeting): Schemas['MdtMeetingResponse'] {
  const items = db().mdtItems.filter((i) => meetingOf(i)?.id === meeting.id);
  return {
    id: meeting.id,
    title: meeting.title,
    startUtc: meeting.startUtc,
    endUtc: meeting.endUtc,
    specialtyId: meeting.specialtyId ?? undefined,
    specialtyName: specialtyName(meeting.specialtyId),
    openItemCount: items.filter(isOpen).length,
    itemCount: items.length,
    attendees: (meeting.attendees ?? [])
      .map((a) => ({userId: a.userId, username: a.username}))
      .sort((a, b) => (a.username ?? '').localeCompare(b.username ?? '')),
  };
}

function findItem(id: string): DemoMdtItem {
  const item = db().mdtItems.find((i) => i.id === id);
  if (!item) refuse(notFound('The MDT item'));
  return item;
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
    const specialtyId = query.get('specialtyId');
    const from = ukTime(daysFrom(-60), 0);
    const to = ukTime(daysFrom(180), 0);
    return db()
      .meetings.filter(isMdt)
      .filter((m) => (!specialtyId || m.specialtyId === specialtyId) && (m.startUtc ?? '') >= from && (m.startUtc ?? '') < to)
      .sort((a, b) => (a.startUtc ?? '').localeCompare(b.startUtc ?? '') || (a.id ?? '').localeCompare(b.id ?? ''))
      .map(meetingResponse);
  });
  on('get', '/api/mdt/meetings/{meetingId}/items', ({params}) => {
    requirePermission('ViewPatients');
    const meeting = findMeeting(params.meetingId);
    if (!isMdt(meeting)) return problem(409, "That meeting isn't an MDT.");
    return {
      meeting: meetingResponse(meeting),
      items: db()
        .mdtItems.filter((i) => meetingOf(i)?.id === meeting.id)
        .sort(byListed)
        .map(itemResponse),
    };
  });
  on('get', '/api/mdt/awaiting-meeting', ({query}) => {
    requirePermission('ViewPatients');
    const specialtyId = query.get('specialtyId');
    return db()
      .mdtItems.filter((i) => isOpen(i) && !meetingOf(i) && (!specialtyId || i.specialtyId === specialtyId))
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
    if (!body.pathwayId) errors.pathwayId = ['Pathway id is required.'];
    if (!body.meetingId) errors.meetingId = ['Choose an MDT.'];
    if (!question) errors.question = ['Give the question for the team.'];
    else if (question.length > MAX_QUESTION) errors.question = [`The question must not exceed ${MAX_QUESTION} characters.`];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const pathway = findPathway(body.pathwayId!);
    if (['Completed', 'Cancelled', 'Archived'].includes(pathway.status))
      return problem(409, "A discharged, cancelled or archived pathway can't be listed for an MDT.");
    const meeting = findMeeting(body.meetingId!);
    ensureCanListFor(meeting);
    ensureNotListed(pathway.id, meeting.id!);
    const item: DemoMdtItem = {
      id: newId(),
      patientId: pathway.patientId,
      pathwayId: pathway.id,
      specialtyId: meeting.specialtyId!,
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
      version: 1,
    };
    db().mdtItems.push(item);
    return new DemoReply(201, itemResponse(item));
  });
  on('post', '/api/mdt/items/{id}/withdraw', ({params, body}) => {
    requirePermission('WorkPathways');
    const viewer = me();
    const item = findItem(params.id);
    ensureVersion(item, body.expectedVersion);
    ensureOpen(item);
    Object.assign(item, {status: 'Withdrawn', withdrawnByUserId: viewer.id, withdrawnAtUtc: nowUtc(), version: item.version + 1});
    return itemResponse(item);
  });
  on('post', '/api/mdt/items/{id}/defer', ({params, body}) => {
    requirePermission('WorkPathways');
    if (!body.meetingId) return invalid({meetingId: ['Choose an MDT.']});
    const item = findItem(params.id);
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
      specialtyId: meeting.specialtyId,
      version: item.version + 1,
    });
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
    const specialty = specialtyName(item.specialtyId) || 'team';
    for (const userId of new Set((pathway?.assignments ?? []).map((a) => a.userId)))
      if (userId !== viewer.id)
        db().notifications.push({
          id: newId(),
          userId,
          kind: 'MdtOutcomeRecorded',
          title: 'MDT outcome recorded',
          message: `${viewer.username} recorded the ${specialty} MDT's outcome on a pathway you're assigned to.`,
          pathwayId: item.pathwayId,
          createdAtUtc: now,
          isRead: false,
        });
    return itemResponse(item);
  });
}
