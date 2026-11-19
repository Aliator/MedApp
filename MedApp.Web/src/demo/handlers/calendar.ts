import type {Schemas} from '../../api/client';
import {addDays} from '../../utils/calendarSlots';
import {PEOPLE, personById, usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId, ROOMS} from '../ids';
import {teamName} from '../seed';
import type {DemoAppointment} from '../state';
import {db} from '../store';
import {nowUtc, today, ukDateOf} from '../time';
import {logActivity} from './adminLogs';
import {can, contains, int, list, me, paged, patientName, requirePermission, sorted, teamIdsOf} from './common';
import {pathwayToChange} from './pathways';
import {busyTimeOf, canChangeRecord, canSeeRecord, ensureCanChangeRecord, isPickedTeam, myScope, pickedTeam, seeRecord} from './teamWalls';
import {activityType, calendarDay, findSession, sessionsFor, sharedWorkingTime, templateBlocksOn, workingDayOf} from './setupCalendar';

/**
 * Calendars, appointments and meetings. Working hours, closed days, day and week templates and clinic sessions come
 * from `setupCalendar.ts`. Simplified: nothing checks for clashes or takes a lock (the full version refuses a booking
 * or meeting that clashes, under `ICalendarLock`), and free times are worked out from everyone's hours, blocks
 * (template ones too), booked appointments, meetings and sessions.
 */

type Appointment = DemoAppointment;
type Meeting = Schemas['MeetingResponse'];

/** How long after the event a visit recorded after it can be withdrawn by whoever recorded it. */
const RECORDER_DAYS = 30;

/** Each date from `from` to `to`, inclusive (at most 62). */
function datesBetween(from: string, to: string): string[] {
  const dates: string[] = [];
  for (let date = from; date <= to && dates.length < 62; date = addDays(date, 1)) dates.push(date);
  return dates;
}

const isPatientAnonymised = (patientId: string | undefined) => db().patients.find((p) => p.id === patientId)?.isAnonymised === true;

/** An appointment as the API answers it, with what the signed-in person can do with it. */
export function appointmentResponse(appointment: Appointment): Appointment {
  // Only the appointment's own team changes it (an admin, or the lab reading a pathway passed to it, only reads).
  const canBook = can('BookAppointments') && canChangeRecord(appointment);
  const status = appointment.status;
  const started = appointment.startUtc ? appointment.startUtc <= nowUtc() : false;
  const recordedAfter = appointment.isRecordedAfterEvent === true && (status === 'Attended' || status === 'DidNotAttend');
  return {
    ...appointment,
    canBook: canBook && status === 'Requested',
    canReschedule: canBook && status === 'Booked',
    canRecordOutcome: canBook && status === 'Booked' && started,
    canCancel: canBook && (status === 'Requested' || status === 'Booked'),
    // The follow-up of a missed appointment is the person's to decide; MedApp suggests none.
    canRecordDnaFollowUp: canBook && status === 'DidNotAttend',
    canEditNotes: canBook && status !== 'EnteredInError',
    canMarkEnteredInError: recordedAfter && !isPatientAnonymised(appointment.patientId),
    canRecorderMarkEnteredInError:
      recordedAfter && Date.parse(appointment.createdAtUtc ?? '') > Date.now() - RECORDER_DAYS * 86400_000,
  };
}

/** A meeting as the API answers it, for the signed-in person. */
function meetingResponse(meeting: Meeting): Meeting {
  const viewer = me();
  const mine = meeting.attendees?.find((a) => a.userId === viewer.id);
  return {
    ...meeting,
    isMdt: meeting.isMdt ?? false,
    teamId: meeting.teamId ?? null,
    canEdit: meeting.organiserUserId === viewer.id && (meeting.endUtc ?? '') > nowUtc(),
    myReply: mine?.reply ?? null,
    canReply: mine !== undefined && (meeting.endUtc ?? '') > nowUtc(),
    overlapsAppointment: false,
  };
}

const involves = (meeting: Meeting, userId: string) =>
  meeting.organiserUserId === userId || (meeting.attendees ?? []).some((a) => a.userId === userId && a.reply !== 'Declined');

const inRange = (startUtc: string | null | undefined, from: string, to: string) =>
  !!startUtc && ukDateOf(startUtc) >= from && ukDateOf(startUtc) <= to;

/** A stored block with its activity's current name and colour. */
function blockWithType(block: Schemas['CalendarBlockResponse']): Schemas['CalendarBlockResponse'] {
  const type = activityType(block.activityTypeId);
  return type ? {...block, activityName: type.name, colour: type.colour} : block;
}

/** Someone's blocks from `from` to `to`: those added by hand, and their templates' (less any changed that day). */
function blocksOf(staffUserId: string, from: string, to: string, canEdit: boolean): Schemas['CalendarBlockResponse'][] {
  const stored = db()
    .blocks.filter((b) => b.staffUserId === staffUserId && (b.date ?? '') >= from && (b.date ?? '') <= to)
    .map((b) => ({...blockWithType(b), canEdit: canEdit && (b.date ?? '') >= today()}));
  const fromTemplates = datesBetween(from, to).flatMap((date) => templateBlocksOn(staffUserId, date, canEdit));
  return [...stored, ...fromTemplates].sort((a, b) => (a.startUtc ?? '').localeCompare(b.startUtc ?? ''));
}

/** One person's calendar from `from` to `to`. */
function calendar(staffUserId: string, from: string, to: string, withAppointments = true): Schemas['CalendarResponse'] {
  const viewer = me();
  const scope = myScope();
  const canEdit = staffUserId === viewer.id || can('ManageAnyCalendar');
  const state = db();
  const week = state.defaultWeeks[staffUserId];
  // Their appointments in the range: the viewer's teams' in full, other teams' as busy time with no patient.
  const theirs = withAppointments
    ? state.appointments.filter(
        (a) => a.staffUserId === staffUserId && inRange(a.startUtc, from, to) && a.status !== 'Cancelled' && a.status !== 'EnteredInError',
      )
    : [];
  return {
    staffUserId,
    staffUsername: usernameOf(staffUserId),
    from,
    to,
    workingDayStart: '08:00:00',
    workingDayEnd: '18:00:00',
    slotMinutes: 15,
    canEdit,
    defaultWeekTemplateId: week?.weekTemplateId ?? null,
    defaultWeekTemplateName: state.weekTemplates.find((w) => w.id === week?.weekTemplateId)?.name ?? null,
    days: datesBetween(from, to).map((date) => calendarDay(staffUserId, date)),
    blocks: blocksOf(staffUserId, from, to, canEdit),
    appointments: theirs.filter((a) => canSeeRecord(a, scope)).map(appointmentResponse),
    otherTeamsBusy: theirs.filter((a) => !canSeeRecord(a, scope)).map(busyTimeOf),
    meetings: state.meetings
      .filter((m) => involves(m, staffUserId) && inRange(m.startUtc, from, to))
      .sort((a, b) => (a.startUtc ?? '').localeCompare(b.startUtc ?? ''))
      .map(meetingResponse),
    sessions: sessionsFor(staffUserId, from, to),
  };
}

/** What makes someone busy on some dates: their blocks (template ones too), booked appointments, meetings and sessions. */
function busyFor(userIds: string[], dates: string[], exceptMeetingId?: string | null): Schemas['BusyTimeResponse'][] {
  const state = db();
  const busy: Schemas['BusyTimeResponse'][] = [];
  const from = dates[0] ?? today();
  const to = dates.at(-1) ?? from;
  for (const userId of userIds) {
    const username = usernameOf(userId);
    for (const b of blocksOf(userId, from, to, false))
      busy.push({staffUserId: userId, username, startUtc: b.startUtc, endUtc: b.endUtc, what: b.activityName});
    for (const a of state.appointments)
      if (a.staffUserId === userId && a.status === 'Booked' && inRange(a.startUtc, from, to))
        busy.push({staffUserId: userId, username, startUtc: a.startUtc!, endUtc: a.endUtc ?? a.startUtc!, what: 'Appointment'});
    for (const m of state.meetings)
      if (m.id !== exceptMeetingId && involves(m, userId) && inRange(m.startUtc, from, to))
        busy.push({staffUserId: userId, username, startUtc: m.startUtc, endUtc: m.endUtc, what: m.title});
    for (const s of sessionsFor(userId, from, to))
      busy.push({staffUserId: userId, username, startUtc: s.startUtc, endUtc: s.endUtc, what: `Clinic session in ${s.roomName}`});
  }
  return busy;
}

/** Free times on a day, in the working time everyone given shares. */
function freeOn(date: string, userIds: string[], busy: Schemas['BusyTimeResponse'][]): Schemas['TimeRangeResponse'][] {
  const working = sharedWorkingTime(userIds, date);
  if (!working) return [];
  const taken = busy
    .filter((b) => b.startUtc && ukDateOf(b.startUtc) === date)
    .map((b) => [Date.parse(b.startUtc!), Date.parse(b.endUtc!)] as const)
    .sort((a, b) => a[0] - b[0]);
  const free: Schemas['TimeRangeResponse'][] = [];
  let cursor = Math.max(Date.parse(working.startUtc), Math.ceil(Date.now() / 900_000) * 900_000);
  const end = Date.parse(working.endUtc);
  for (const [start, stop] of taken) {
    if (start > cursor && cursor < end) free.push({startUtc: new Date(cursor).toISOString(), endUtc: new Date(Math.min(start, end)).toISOString()});
    cursor = Math.max(cursor, stop);
  }
  if (cursor < end) free.push({startUtc: new Date(cursor).toISOString(), endUtc: new Date(end).toISOString()});
  return free;
}

/** The appointment, or a 404: also when it's another team's (the team walls). */
function findAppointment(id: string): Appointment {
  return seeRecord(db().appointments.find((a) => a.id === id), 'Appointment', 'The appointment');
}

/** An appointment to change: visible, and its own team's (else a 409: admins and the lab only read it). */
function appointmentToChange(id: string): Appointment {
  const appointment = findAppointment(id);
  ensureCanChangeRecord(appointment, 'This appointment');
  return appointment;
}

/** Refuses a clinician who isn't in the appointment's team: a 400 on who (MC5's booking rule). */
function ensureStaffInTeam(teamId: string | null, staffUserId: string) {
  if (teamId && !teamsOfStaff(staffUserId).includes(teamId))
    refuse(invalid({staffUserId: [`${usernameOf(staffUserId) ?? 'They'} isn’t in ${teamName(teamId) ?? 'the appointment’s team'}, so can’t see its patients. Choose someone in the team.`]}));
}

/**
 * A new appointment's team: its pathway's, or without one the booker's team (the full version asks when there are
 * several), else the clinician's.
 */
const teamForBooking = (pathwayTeamId: string | undefined, staffUserId: string | null | undefined) =>
  pathwayTeamId ?? myScope().teamIds[0] ?? teamsOfStaff(staffUserId)[0] ?? null;
const teamsOfStaff = (staffUserId: string | null | undefined) => {
  const person = personById(staffUserId);
  return person ? teamIdsOf(person) : [];
};

function findMeeting(id: string): Meeting {
  const meeting = db().meetings.find((m) => m.id === id);
  if (!meeting) refuse(notFound('The meeting'));
  return meeting;
}

/** Refuses a change to an appointment of a restricted or anonymised record (cancelling is still allowed). */
function ensurePatientCanChange(appointment: Appointment) {
  const patient = db().patients.find((p) => p.id === appointment.patientId);
  if (patient?.isAnonymised || patient?.isRestricted)
    refuse(problem(409, 'This patient’s record is restricted, so their appointments can only be cancelled.'));
}

/** Tells someone about a change to their calendar, as the full version's notifications do (without the live push). */
function notify(userId: string, notification: Omit<Schemas['NotificationResponse'], 'id' | 'createdAtUtc' | 'isRead'>) {
  if (userId === me().id) return;
  db().notifications.push({...notification, id: newId(), userId, createdAtUtc: nowUtc(), isRead: false});
}

/** Books an appointment at a time (into a clinic session's slot, when one is named: its room comes with it). */
function book(appointment: Appointment, staffUserId: string, startUtc: string, endUtc: string, clinicSessionId?: string | null, roomId?: string | null) {
  const viewer = me();
  ensureStaffInTeam(appointment.teamId, staffUserId);
  const now = nowUtc();
  if (clinicSessionId) {
    const session = findSession(clinicSessionId);
    if (session.staffUserId !== staffUserId || session.status !== 'Planned' || startUtc < session.startUtc || endUtc > session.endUtc)
      refuse(problem(409, 'That isn’t one of the session’s slots.'));
    if (db().appointments.some((a) => a.id !== appointment.id && a.clinicSessionId === session.id && a.startUtc === startUtc && a.status === 'Booked'))
      refuse(problem(409, 'That slot has just been booked.'));
    appointment.clinicSessionId = session.id;
    appointment.roomId = session.roomId;
  } else {
    appointment.clinicSessionId = null;
    appointment.roomId = roomId ?? appointment.roomId ?? ROOMS.clinicRoom1;
  }
  appointment.status = 'Booked';
  appointment.staffUserId = staffUserId;
  appointment.staffUsername = usernameOf(staffUserId);
  appointment.startUtc = startUtc;
  appointment.endUtc = endUtc;
  appointment.durationMinutes = Math.round((Date.parse(endUtc) - Date.parse(startUtc)) / 60000);
  appointment.bookedAtUtc = now;
  appointment.bookedByUsername = viewer.username;
  appointment.lastUpdated = now;
  notify(staffUserId, {
    kind: 'AppointmentBooked',
    title: 'Appointment booked',
    message: `${viewer.username} booked an appointment with you.`,
    appointmentId: appointment.id,
    pathwayId: appointment.pathwayId,
  });
}

function meetingInput(body: Partial<Schemas['MeetingRequest']>) {
  const errors: Record<string, string[]> = {};
  if (!body.title?.trim()) errors.title = ['Give the meeting a title.'];
  if (!body.startUtc || !body.endUtc || body.endUtc <= body.startUtc) errors.endUtc = ['The meeting must end after it starts.'];
  if (body.isMdt && !db().teams.some((s) => s.id === body.teamId)) errors.teamId = ['Choose the MDT’s team.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  return {title: body.title!.trim(), notes: body.notes?.trim() || null, startUtc: body.startUtc!, endUtc: body.endUtc!};
}

/** An appointment that has happened, recorded after the event (`RecordPastAppointmentHandler`). */
function recordPast(body: Partial<Schemas['RecordPastAppointmentRequest']>): Appointment {
  const viewer = me();
  const patient = db().patients.find((p) => p.id === body.patientId);
  const errors: Record<string, string[]> = {};
  if (!patient) errors.patientId = ['Choose a patient.'];
  if (!body.staffUserId || !personById(body.staffUserId)) errors.staffUserId = ['Choose who saw the patient.'];
  if (!body.startUtc || !body.endUtc || body.endUtc <= body.startUtc) errors.endUtc = ['Choose when it was: the end after the start.'];
  else if (body.startUtc > nowUtc()) errors.startUtc = ['It must have started already: book one ahead instead.'];
  else if (Date.parse(body.startUtc) < Date.now() - RECORDER_DAYS * 86400_000) errors.startUtc = ['Only a visit in the last 30 days can be recorded.'];
  else if (!workingDayOf(body.staffUserId, ukDateOf(body.startUtc)).working && !workingDayOf(body.staffUserId, ukDateOf(body.startUtc)).closedName)
    errors.startUtc = ['It must be on one of their working days.'];
  if (body.outcome !== 'Attended' && body.outcome !== 'DidNotAttend') errors.outcome = ['Choose whether the patient attended.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  if (patient!.isRestricted || patient!.isAnonymised) refuse(problem(409, 'This patient’s record is restricted, so it can’t be changed.'));
  const pathway = body.pathwayId ? pathwayToChange(body.pathwayId) : undefined;
  const stage = pathway?.stages.find((s) => s.id === body.stageId);
  if (pathway && ukDateOf(body.startUtc!) < ukDateOf(pathway.createdAt)) refuse(problem(409, 'That’s before the pathway was started.'));
  const teamId = teamForBooking(pathway?.teamId, body.staffUserId);
  ensureStaffInTeam(teamId, body.staffUserId!);
  const now = nowUtc();
  return {
    id: newId(),
    teamId,
    patientId: patient!.id,
    patientName: patientName(patient!.id),
    pathwayId: pathway?.id ?? null,
    pathwayTitle: pathway?.title ?? null,
    pathwayDueDate: pathway?.dueDate ?? null,
    stageId: stage?.id ?? null,
    stageName: stage?.name ?? null,
    visitRequirement: stage?.visitRequirement ?? null,
    status: body.outcome!,
    staffUserId: body.staffUserId!,
    staffUsername: usernameOf(body.staffUserId),
    startUtc: body.startUtc!,
    endUtc: body.endUtc!,
    durationMinutes: Math.round((Date.parse(body.endUtc!) - Date.parse(body.startUtc!)) / 60000),
    roomId: null,
    notes: body.notes?.trim() || null,
    isAutomatic: false,
    createdAtUtc: now,
    createdByUsername: viewer.username,
    isRecordedAfterEvent: true,
    bookedAtUtc: now,
    bookedByUsername: viewer.username,
    outcomeAtUtc: now,
    outcomeByUsername: viewer.username,
    lastUpdated: now,
  };
}

export function registerCalendar() {
  on('get', '/api/calendar', ({query}) => {
    const viewer = me();
    const from = query.get('From') ?? today();
    const to = query.get('To') ?? from;
    return calendar(query.get('StaffUserId') || viewer.id, from, to, query.get('WithoutAppointments') !== 'true');
  });

  on('get', '/api/calendar/team', ({query}) => {
    me();
    const date = query.get('Date') ?? today();
    const ids = list(query, 'StaffUserIds');
    const members = (ids.length > 0 ? ids : PEOPLE.slice(0, 4).map((p) => p.id)).filter((id) => personById(id));
    const busy = busyFor(members, [date]);
    return {date, day: calendarDay(null, date), members: members.map((id) => calendar(id, date, date)), everyoneFree: freeOn(date, members, busy)};
  });

  on('get', '/api/calendar/availability', ({query}) => {
    me();
    const ids = list(query, 'StaffUserIds');
    const from = query.get('From') ?? today();
    const to = query.get('To') ?? addDays(from, 13);
    const duration = int(query, 'DurationMinutes', 30);
    const dates = datesBetween(from, to);
    const busy = busyFor(ids, dates, query.get('ExceptMeetingId'));
    const days = dates.map((date) => ({date, free: freeOn(date, ids, busy)}));
    const suggestions = days
      .flatMap((d) => d.free ?? [])
      .filter((range) => Date.parse(range.endUtc!) - Date.parse(range.startUtc!) >= duration * 60000)
      .slice(0, 5)
      .map((range) => ({startUtc: range.startUtc, endUtc: new Date(Date.parse(range.startUtc!) + duration * 60000).toISOString()}));
    return {from, to, durationMinutes: duration, days, suggestions, busy};
  });

  on('get', '/api/calendar/staff', ({query}) => {
    me();
    const permission = query.get('permission');
    const state = db();
    return PEOPLE.filter((p) => !permission || p.permissions.includes(permission as never)).map((p) => {
      const week = state.defaultWeeks[p.id];
      return {
        userId: p.id,
        username: p.username,
        weekTemplateId: week?.weekTemplateId ?? null,
        weekTemplateName: state.weekTemplates.find((w) => w.id === week?.weekTemplateId)?.name ?? null,
      };
    });
  });

  on('post', '/api/calendar/blocks', ({body}) => {
    const viewer = me();
    const staffUserId = body.staffUserId || viewer.id;
    if (staffUserId !== viewer.id && !can('ManageAnyCalendar')) return problem(403, 'You can change only your own calendar.');
    const type = activityType(body.activityTypeId);
    if (!type || type.isArchived || !body.startUtc || !body.endUtc || body.endUtc <= body.startUtc)
      return invalid({endUtc: ['Choose an activity, and an end after the start.']});
    if (ukDateOf(body.startUtc) < today()) return problem(409, 'Past days don’t change.');
    const block: Schemas['CalendarBlockResponse'] = {
      blockId: newId(),
      templateBlockId: null,
      isFromTemplate: false,
      staffUserId,
      date: ukDateOf(body.startUtc),
      startUtc: body.startUtc,
      endUtc: body.endUtc,
      activityTypeId: type.id,
      activityName: type.name,
      colour: type.colour,
      note: body.note ?? null,
      canEdit: true,
      overlapsAppointment: false,
    };
    db().blocks.push(block);
    return block;
  });
  on('put', '/api/calendar/blocks/{id}', ({params, body}) => {
    const viewer = me();
    const block = db().blocks.find((b) => b.blockId === params.id);
    if (!block) return notFound('The block');
    if (block.staffUserId !== viewer.id && !can('ManageAnyCalendar')) return problem(403, 'You can change only your own calendar.');
    if ((block.date ?? '') < today()) return problem(409, 'Past days don’t change.');
    const type = activityType(body.activityTypeId) ?? activityType(block.activityTypeId)!;
    Object.assign(block, {
      activityTypeId: type.id,
      activityName: type.name,
      colour: type.colour,
      startUtc: body.startUtc ?? block.startUtc,
      endUtc: body.endUtc ?? block.endUtc,
      note: body.note ?? null,
      date: ukDateOf(body.startUtc ?? block.startUtc!),
    });
    return blockWithType(block);
  });
  on('delete', '/api/calendar/blocks/{id}', ({params}) => {
    const viewer = me();
    const block = db().blocks.find((b) => b.blockId === params.id);
    if (block && block.staffUserId !== viewer.id && !can('ManageAnyCalendar')) return problem(403, 'You can change only your own calendar.');
    if (block && (block.date ?? '') < today()) return problem(409, 'Past days don’t change.');
    db().blocks = db().blocks.filter((b) => b.blockId !== params.id);
    return undefined;
  });

  on('get', '/api/calendar/meetings/{id}', ({params}) => {
    me();
    return meetingResponse(findMeeting(params.id));
  });
  on('post', '/api/calendar/meetings', ({body}) => {
    const viewer = me();
    requirePermission('ArrangeMeetings');
    const input = meetingInput(body);
    const now = nowUtc();
    const meeting: Meeting = {
      id: newId(),
      ...input,
      date: ukDateOf(input.startUtc),
      organiserUserId: viewer.id,
      organiserUsername: viewer.username,
      attendees: (body.attendeeUserIds ?? [])
        .filter((id) => id !== viewer.id && personById(id))
        .map((userId) => ({userId, username: usernameOf(userId), reply: 'NoReply', repliedAtUtc: null})),
      createdAtUtc: now,
      lastUpdated: now,
      isMdt: body.isMdt ?? false,
      teamId: body.isMdt ? (body.teamId ?? null) : null,
    };
    db().meetings.push(meeting);
    for (const attendee of meeting.attendees ?? [])
      notify(attendee.userId!, {
        kind: 'MeetingInvited',
        title: 'Meeting invitation',
        message: `${viewer.username} invited you to '${meeting.title}'.`,
        calendarDate: meeting.date,
        meetingId: meeting.id,
      });
    return meetingResponse(meeting);
  });
  on('put', '/api/calendar/meetings/{id}', ({params, body}) => {
    const viewer = me();
    const meeting = findMeeting(params.id);
    if (meeting.organiserUserId !== viewer.id) return problem(403, 'Only the organiser changes a meeting.');
    if (body.expectedLastUpdated && body.expectedLastUpdated !== meeting.lastUpdated)
      return problem(409, 'Someone else changed this meeting meanwhile. Look at it again before changing it.');
    // `isMdt` left out of a change keeps it.
    const isMdt = body.isMdt ?? meeting.isMdt ?? false;
    const input = meetingInput({...body, isMdt, teamId: body.teamId ?? meeting.teamId});
    const attendeeIds = (body.attendeeUserIds ?? []).filter((id) => id !== viewer.id && personById(id));
    Object.assign(meeting, input, {
      date: ukDateOf(input.startUtc),
      attendees: attendeeIds.map(
        (userId) => meeting.attendees?.find((a) => a.userId === userId) ?? {userId, username: usernameOf(userId), reply: 'NoReply', repliedAtUtc: null},
      ),
      isMdt,
      teamId: isMdt ? (body.teamId ?? meeting.teamId ?? null) : null,
      lastUpdated: nowUtc(),
    });
    return meetingResponse(meeting);
  });
  on('delete', '/api/calendar/meetings/{id}', ({params}) => {
    const viewer = me();
    const meeting = findMeeting(params.id);
    if (meeting.organiserUserId !== viewer.id) return problem(403, 'Only the organiser cancels a meeting.');
    db().meetings = db().meetings.filter((m) => m.id !== params.id);
    for (const attendee of meeting.attendees ?? [])
      notify(attendee.userId!, {kind: 'MeetingCancelled', title: 'Meeting cancelled', message: `${viewer.username} cancelled '${meeting.title}'.`, calendarDate: meeting.date});
    return undefined;
  });
  on('put', '/api/calendar/meetings/{id}/reply', ({params, body}) => {
    const viewer = me();
    const meeting = findMeeting(params.id);
    const attendee = meeting.attendees?.find((a) => a.userId === viewer.id);
    if (!attendee) return problem(403, 'Only someone invited replies.');
    attendee.reply = body.reply === 'Declined' ? 'Declined' : body.reply === 'Accepted' ? 'Accepted' : 'NoReply';
    attendee.repliedAtUtc = nowUtc();
    return meetingResponse(meeting);
  });

  on('get', '/api/appointments/search', ({query}) => {
    me();
    const statuses = list(query, 'Statuses');
    const from = query.get('From');
    const to = query.get('To');
    const search = query.get('Search');
    // The Did not attend list: missed, with no decision that no further appointment is needed.
    const didNotAttend = query.get('View') === 'didNotAttend';
    const scope = myScope();
    const picked = pickedTeam(query, scope);
    const items = db().appointments.filter(
      (a) =>
        canSeeRecord(a, scope) &&
        isPickedTeam(a.teamId, picked) &&
        (!query.get('PatientId') || a.patientId === query.get('PatientId')) &&
        (!query.get('PathwayId') || a.pathwayId === query.get('PathwayId')) &&
        (!query.get('StageId') || a.stageId === query.get('StageId')) &&
        (!query.get('StaffUserId') || a.staffUserId === query.get('StaffUserId')) &&
        (statuses.length === 0 || statuses.includes(a.status ?? '')) &&
        (!from || (a.startUtc ? ukDateOf(a.startUtc) >= from : true)) &&
        (!to || (a.startUtc ? ukDateOf(a.startUtc) <= to : true)) &&
        (!didNotAttend || (a.status === 'DidNotAttend' && a.dnaFollowUp !== 'NoFurtherAppointment')) &&
        contains(a.patientName, search),
    );
    return paged(
      sorted(
        items.map((a) => ({
          ...appointmentResponse(a),
          missedOnPathwayCount: a.pathwayId ? db().appointments.filter((x) => x.pathwayId === a.pathwayId && x.status === 'DidNotAttend').length : null,
        })),
        query,
        {
          dueDate: (a) => a.pathwayDueDate ?? '9999',
          requested: (a) => a.createdAtUtc ?? '',
          patient: (a) => (a.patientName ?? '').toLowerCase(),
          start: (a) => a.startUtc ?? '',
        },
        'start',
      ),
      query,
    );
  });

  on('get', '/api/appointments/{id}', ({params}) => {
    me();
    const appointment = findAppointment(params.id);
    logActivity('PatientViewed', {patientId: appointment.patientId});
    return appointmentResponse(appointment);
  });
  // A patient's texts and emails about an appointment follow it behind the team walls (none are sent in the light demo).
  on('get', '/api/appointments/{id}/messages', ({params}) => {
    findAppointment(params.id);
    return [];
  });

  on('post', '/api/appointments', ({body}) => {
    const viewer = me();
    requirePermission('BookAppointments');
    const patient = db().patients.find((p) => p.id === body.patientId);
    if (!patient) return invalid({patientId: ['Choose a patient.']});
    if (patient.isRestricted || patient.isAnonymised) return problem(409, 'This patient’s record is restricted, so it can’t be changed.');
    const pathway = body.pathwayId ? pathwayToChange(body.pathwayId) : undefined;
    const stage = pathway?.stages.find((s) => s.id === body.stageId);
    const now = nowUtc();
    const appointment: Appointment = {
      id: newId(),
      teamId: teamForBooking(pathway?.teamId, body.staffUserId),
      patientId: patient.id,
      patientName: patientName(patient.id),
      pathwayId: pathway?.id ?? null,
      pathwayTitle: pathway?.title ?? null,
      pathwayDueDate: pathway?.dueDate ?? null,
      stageId: stage?.id ?? null,
      stageName: stage?.name ?? null,
      visitRequirement: stage?.visitRequirement ?? null,
      status: 'Requested',
      staffUserId: null,
      staffUsername: null,
      startUtc: null,
      endUtc: null,
      durationMinutes: null,
      roomId: body.roomId ?? null,
      notes: body.notes ?? null,
      isAutomatic: false,
      createdAtUtc: now,
      createdByUsername: viewer.username,
      isRecordedAfterEvent: false,
      lastUpdated: now,
    };
    if (body.staffUserId && body.startUtc && body.endUtc)
      book(appointment, body.staffUserId, body.startUtc, body.endUtc, body.clinicSessionId, body.roomId);
    db().appointments.push(appointment);
    return appointmentResponse(appointment);
  });

  on('post', '/api/appointments/past', ({body}) => {
    requirePermission('BookAppointments');
    const appointment = recordPast(body);
    db().appointments.push(appointment);
    notify(appointment.staffUserId!, {
      kind: 'AppointmentRecorded',
      title: 'Appointment recorded',
      message: `${me().username} recorded a past appointment with you.`,
      appointmentId: appointment.id,
      pathwayId: appointment.pathwayId,
    });
    return new DemoReply(201, appointmentResponse(appointment));
  });

  on('post', '/api/appointments/{id}/book', ({params, body}) => {
    requirePermission('BookAppointments');
    const appointment = appointmentToChange(params.id);
    ensurePatientCanChange(appointment);
    if (!body.staffUserId || !body.startUtc || !body.endUtc) return invalid({startUtc: ['Choose who and when.']});
    if (appointment.status !== 'Requested' && appointment.status !== 'Booked') return problem(409, 'This appointment can’t be booked now.');
    if (body.startUtc <= nowUtc()) return problem(409, 'Nothing can be booked in the past: record a visit after the event instead.');
    book(appointment, body.staffUserId, body.startUtc, body.endUtc, body.clinicSessionId, body.roomId);
    return appointmentResponse(appointment);
  });

  on('post', '/api/appointments/{id}/outcome', ({params, body}) => {
    const viewer = me();
    requirePermission('BookAppointments');
    const appointment = appointmentToChange(params.id);
    ensurePatientCanChange(appointment);
    const correcting = appointment.status === 'Attended' || appointment.status === 'DidNotAttend';
    if (appointment.status !== 'Booked' && !correcting) return problem(409, 'Only a booked appointment has an outcome.');
    appointment.status = body.outcome === 'DidNotAttend' ? 'DidNotAttend' : 'Attended';
    // Correcting a missed appointment to attended clears its follow-up.
    if (appointment.status === 'Attended') Object.assign(appointment, {dnaFollowUp: null, dnaFollowUpDetail: null, dnaFollowUpAtUtc: null, dnaFollowUpByUsername: null});
    appointment.outcomeAtUtc = nowUtc();
    appointment.outcomeByUsername = viewer.username;
    appointment.lastUpdated = nowUtc();
    return appointmentResponse(appointment);
  });

  on('post', '/api/appointments/{id}/dna-follow-up', ({params, body}) => {
    const viewer = me();
    requirePermission('BookAppointments');
    const appointment = appointmentToChange(params.id);
    if (appointment.status !== 'DidNotAttend') return problem(409, 'Only a missed appointment has a follow-up.');
    ensurePatientCanChange(appointment);
    const decision = body.decision ?? '';
    const detail = body.detail?.trim() || null;
    if (!['Rebook', 'ClinicianToReview', 'NoFurtherAppointment'].includes(decision)) return invalid({decision: ['Choose what happens next.']});
    if (decision === 'NoFurtherAppointment' && !detail) return invalid({detail: ['Say why no further appointment is needed.']});
    if (detail && detail.length > 500) return invalid({detail: ['At most 500 characters.']});
    const now = nowUtc();
    Object.assign(appointment, {dnaFollowUp: decision, dnaFollowUpDetail: detail, dnaFollowUpAtUtc: now, dnaFollowUpByUsername: viewer.username, lastUpdated: now});
    return appointmentResponse(appointment);
  });

  on('post', '/api/appointments/{id}/entered-in-error', ({params, body}) => {
    const viewer = me();
    requirePermission('BookAppointments');
    const appointment = appointmentToChange(params.id);
    const response = appointmentResponse(appointment);
    if (!response.canMarkEnteredInError) return problem(409, 'Only a visit recorded after the event can be marked entered in error.');
    const isRecorder = appointment.bookedByUsername === viewer.username && response.canRecorderMarkEnteredInError;
    if (!can('CorrectRecords') && !isRecorder)
      return problem(403, 'Only whoever recorded it (in its first 30 days) or someone who can correct records can withdraw it.');
    const reason = body.reason?.trim() ?? '';
    if (!reason || reason.length > 500) return invalid({reason: ['Say why it was entered in error (up to 500 characters).']});
    Object.assign(appointment, {status: 'EnteredInError', cancelReason: reason, lastUpdated: nowUtc()});
    // The full version asks for the stage's visit again when it would be; the light demo leaves the stage as it is.
    return {appointment: appointmentResponse(appointment), visitRequestedAgain: false, stageMessage: null};
  });

  on('post', '/api/appointments/{id}/cancel', ({params, body}) => {
    requirePermission('BookAppointments');
    const appointment = appointmentToChange(params.id);
    if (appointment.status !== 'Requested' && appointment.status !== 'Booked') return problem(409, 'This appointment can’t be cancelled.');
    appointment.status = 'Cancelled';
    appointment.cancelReason = body.reason ?? null;
    appointment.lastUpdated = nowUtc();
    return appointmentResponse(appointment);
  });

  on('put', '/api/appointments/{id}/notes', ({params, body}) => {
    me();
    const appointment = appointmentToChange(params.id);
    ensurePatientCanChange(appointment);
    appointment.notes = body.notes ?? null;
    appointment.lastUpdated = nowUtc();
    return appointmentResponse(appointment);
  });
}
