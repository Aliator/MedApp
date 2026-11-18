import type {Schemas} from '../../api/client';
import {addDays, dayOfWeek} from '../../utils/calendarSlots';
import {PEOPLE, personById, usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId, ROOMS} from '../ids';
import {ACTIVITY_TYPE_LIST, ROOM_LIST, SITE} from '../seed';
import {db} from '../store';
import {nowUtc, today, ukDateOf, ukTime} from '../time';
import {can, contains, int, list, me, paged, patientName, requirePermission, sorted} from './common';
import {findPathway} from './pathways';

/**
 * Calendars, appointments and meetings. Simplified: everyone works 08:00 to 18:00 on weekdays at the one site, there
 * are no day or week templates, closed days or clinic sessions, and nothing checks for clashes or takes a lock (the
 * full version refuses a booking or meeting that clashes, under `ICalendarLock`).
 */

type Appointment = Schemas['AppointmentResponse'];
type Meeting = Schemas['MeetingResponse'];

const WORK_START = 8;
const WORK_END = 18;
const isWeekday = (date: string) => dayOfWeek(date) <= 5;

/** Each date from `from` to `to`, inclusive (at most 62). */
function datesBetween(from: string, to: string): string[] {
  const dates: string[] = [];
  for (let date = from; date <= to && dates.length < 62; date = addDays(date, 1)) dates.push(date);
  return dates;
}

function day(date: string): Schemas['CalendarDayResponse'] {
  const working = isWeekday(date);
  return {
    date,
    isWorkingDay: working,
    closedDayName: null,
    isPast: date < today(),
    isToday: date === today(),
    workStartUtc: ukTime(date, WORK_START),
    workEndUtc: ukTime(date, WORK_END),
    hasHours: working,
    siteId: SITE.id,
    gridStartUtc: ukTime(date, WORK_START),
    gridEndUtc: ukTime(date, WORK_END),
    dayTemplateId: null,
    dayTemplateName: null,
    isDayTemplateOverridden: false,
  };
}

/** An appointment as the API answers it, with what the signed-in person can do with it. */
export function appointmentResponse(appointment: Appointment): Appointment {
  const canBook = can('BookAppointments');
  const status = appointment.status;
  const started = appointment.startUtc ? appointment.startUtc <= nowUtc() : false;
  return {
    ...appointment,
    canBook: canBook && status === 'Requested',
    canReschedule: canBook && status === 'Booked',
    canRecordOutcome: canBook && status === 'Booked' && started,
    canCancel: canBook && (status === 'Requested' || status === 'Booked'),
    canRecordDnaFollowUp: false,
    canEditNotes: canBook && status !== 'EnteredInError',
    canMarkEnteredInError: false,
    canRecorderMarkEnteredInError: false,
  };
}

/** A meeting as the API answers it, for the signed-in person. */
function meetingResponse(meeting: Meeting): Meeting {
  const viewer = me();
  const mine = meeting.attendees?.find((a) => a.userId === viewer.id);
  return {
    ...meeting,
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

/** One person's calendar from `from` to `to`. */
function calendar(staffUserId: string, from: string, to: string, withAppointments = true): Schemas['CalendarResponse'] {
  const viewer = me();
  const canEdit = staffUserId === viewer.id || can('ManageAnyCalendar');
  const state = db();
  return {
    staffUserId,
    staffUsername: usernameOf(staffUserId),
    from,
    to,
    workingDayStart: '08:00:00',
    workingDayEnd: '18:00:00',
    slotMinutes: 15,
    canEdit,
    defaultWeekTemplateId: null,
    defaultWeekTemplateName: null,
    days: datesBetween(from, to).map(day),
    blocks: state.blocks
      .filter((b) => b.staffUserId === staffUserId && (b.date ?? '') >= from && (b.date ?? '') <= to)
      .map((b) => ({...b, canEdit: canEdit && (b.date ?? '') >= today()}))
      .sort((a, b) => (a.startUtc ?? '').localeCompare(b.startUtc ?? '')),
    appointments: withAppointments
      ? state.appointments
          .filter((a) => a.staffUserId === staffUserId && inRange(a.startUtc, from, to) && a.status !== 'Cancelled')
          .map(appointmentResponse)
      : [],
    meetings: state.meetings
      .filter((m) => involves(m, staffUserId) && inRange(m.startUtc, from, to))
      .sort((a, b) => (a.startUtc ?? '').localeCompare(b.startUtc ?? ''))
      .map(meetingResponse),
    sessions: [],
  };
}

/** What makes someone busy: their blocks, booked appointments and meetings. */
function busyFor(userIds: string[], exceptMeetingId?: string | null): Schemas['BusyTimeResponse'][] {
  const state = db();
  const busy: Schemas['BusyTimeResponse'][] = [];
  for (const userId of userIds) {
    const username = usernameOf(userId);
    for (const b of state.blocks)
      if (b.staffUserId === userId) busy.push({staffUserId: userId, username, startUtc: b.startUtc, endUtc: b.endUtc, what: b.activityName});
    for (const a of state.appointments)
      if (a.staffUserId === userId && a.status === 'Booked' && a.startUtc)
        busy.push({staffUserId: userId, username, startUtc: a.startUtc, endUtc: a.endUtc ?? a.startUtc, what: 'Appointment'});
    for (const m of state.meetings)
      if (m.id !== exceptMeetingId && involves(m, userId))
        busy.push({staffUserId: userId, username, startUtc: m.startUtc, endUtc: m.endUtc, what: m.title});
  }
  return busy;
}

/** Free times on a day, for everyone given, in working hours. */
function freeOn(date: string, busy: Schemas['BusyTimeResponse'][]): Schemas['TimeRangeResponse'][] {
  if (!isWeekday(date)) return [];
  const taken = busy
    .filter((b) => b.startUtc && ukDateOf(b.startUtc) === date)
    .map((b) => [Date.parse(b.startUtc!), Date.parse(b.endUtc!)] as const)
    .sort((a, b) => a[0] - b[0]);
  const free: Schemas['TimeRangeResponse'][] = [];
  let cursor = Math.max(Date.parse(ukTime(date, WORK_START)), Math.ceil(Date.now() / 900_000) * 900_000);
  const end = Date.parse(ukTime(date, WORK_END));
  for (const [start, stop] of taken) {
    if (start > cursor && cursor < end) free.push({startUtc: new Date(cursor).toISOString(), endUtc: new Date(Math.min(start, end)).toISOString()});
    cursor = Math.max(cursor, stop);
  }
  if (cursor < end) free.push({startUtc: new Date(cursor).toISOString(), endUtc: new Date(end).toISOString()});
  return free;
}

function findAppointment(id: string): Appointment {
  const appointment = db().appointments.find((a) => a.id === id);
  if (!appointment) refuse(notFound('The appointment'));
  return appointment;
}

function findMeeting(id: string): Meeting {
  const meeting = db().meetings.find((m) => m.id === id);
  if (!meeting) refuse(notFound('The meeting'));
  return meeting;
}

/** Tells someone about a change to their calendar, as the full version's notifications do (without the live push). */
function notify(userId: string, notification: Omit<Schemas['NotificationResponse'], 'id' | 'createdAtUtc' | 'isRead'>) {
  if (userId === me().id) return;
  db().notifications.push({...notification, id: newId(), userId, createdAtUtc: nowUtc(), isRead: false});
}

function book(appointment: Appointment, staffUserId: string, startUtc: string, endUtc: string) {
  const viewer = me();
  const now = nowUtc();
  appointment.status = 'Booked';
  appointment.staffUserId = staffUserId;
  appointment.staffUsername = usernameOf(staffUserId);
  appointment.startUtc = startUtc;
  appointment.endUtc = endUtc;
  appointment.durationMinutes = Math.round((Date.parse(endUtc) - Date.parse(startUtc)) / 60000);
  appointment.roomId ??= ROOMS.clinicRoom1;
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
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  return {title: body.title!.trim(), notes: body.notes?.trim() || null, startUtc: body.startUtc!, endUtc: body.endUtc!};
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
    const busy = busyFor(members);
    return {date, day: day(date), members: members.map((id) => calendar(id, date, date)), everyoneFree: freeOn(date, busy)};
  });

  on('get', '/api/calendar/availability', ({query}) => {
    me();
    const ids = list(query, 'StaffUserIds');
    const from = query.get('From') ?? today();
    const to = query.get('To') ?? addDays(from, 13);
    const duration = int(query, 'DurationMinutes', 30);
    const busy = busyFor(ids, query.get('ExceptMeetingId'));
    const days = datesBetween(from, to).map((date) => ({date, free: freeOn(date, busy)}));
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
    return PEOPLE.filter((p) => !permission || p.permissions.includes(permission as never)).map((p) => ({
      userId: p.id,
      username: p.username,
      weekTemplateId: null,
      weekTemplateName: null,
    }));
  });

  on('get', '/api/calendar/activity-types', () => ACTIVITY_TYPE_LIST);
  on('get', '/api/calendar/closed-days', () => []);
  on('get', '/api/calendar/staff/{userId}/hours', ({params}) => ({
    staffUserId: params.userId,
    hasOwnHours: false,
    since: null,
    canEdit: false,
    days: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((dayOfWeek) => ({
      dayOfWeek,
      starts: '08:00:00',
      ends: '18:00:00',
      siteId: SITE.id,
      siteName: SITE.name,
    })),
  }));
  on('get', '/api/calendar/staff/{userId}/week-template', ({params}) => ({
    staffUserId: params.userId,
    weekTemplateId: null,
    weekTemplateName: null,
    since: null,
  }));
  on('get', '/api/calendar/staff/{userId}/week-templates', () => []);
  on('get', '/api/calendar/staff/{userId}/day-templates', () => []);

  on('post', '/api/calendar/blocks', ({body}) => {
    const viewer = me();
    const staffUserId = body.staffUserId || viewer.id;
    if (staffUserId !== viewer.id && !can('ManageAnyCalendar')) return problem(403, 'You can change only your own calendar.');
    const type = ACTIVITY_TYPE_LIST.find((t) => t.id === body.activityTypeId);
    if (!type || !body.startUtc || !body.endUtc || body.endUtc <= body.startUtc)
      return invalid({endUtc: ['Choose an activity, and an end after the start.']});
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
    me();
    const block = db().blocks.find((b) => b.blockId === params.id);
    if (!block) return notFound('The block');
    const type = ACTIVITY_TYPE_LIST.find((t) => t.id === body.activityTypeId) ?? ACTIVITY_TYPE_LIST.find((t) => t.id === block.activityTypeId)!;
    Object.assign(block, {
      activityTypeId: type.id,
      activityName: type.name,
      colour: type.colour,
      startUtc: body.startUtc ?? block.startUtc,
      endUtc: body.endUtc ?? block.endUtc,
      note: body.note ?? null,
      date: ukDateOf(body.startUtc ?? block.startUtc!),
    });
    return block;
  });
  on('delete', '/api/calendar/blocks/{id}', ({params}) => {
    me();
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
    const input = meetingInput(body);
    const attendeeIds = (body.attendeeUserIds ?? []).filter((id) => id !== viewer.id && personById(id));
    Object.assign(meeting, input, {
      date: ukDateOf(input.startUtc),
      attendees: attendeeIds.map(
        (userId) => meeting.attendees?.find((a) => a.userId === userId) ?? {userId, username: usernameOf(userId), reply: 'NoReply', repliedAtUtc: null},
      ),
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
    const items = db().appointments.filter(
      (a) =>
        (!query.get('PatientId') || a.patientId === query.get('PatientId')) &&
        (!query.get('PathwayId') || a.pathwayId === query.get('PathwayId')) &&
        (!query.get('StageId') || a.stageId === query.get('StageId')) &&
        (!query.get('StaffUserId') || a.staffUserId === query.get('StaffUserId')) &&
        (statuses.length === 0 || statuses.includes(a.status ?? '')) &&
        (!from || (a.startUtc ? ukDateOf(a.startUtc) >= from : true)) &&
        (!to || (a.startUtc ? ukDateOf(a.startUtc) <= to : true)) &&
        (query.get('View') !== 'didNotAttend' || a.status === 'DidNotAttend') &&
        contains(a.patientName, search),
    );
    return paged(
      sorted(
        items.map(appointmentResponse),
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
    return appointmentResponse(findAppointment(params.id));
  });
  on('get', '/api/appointments/{id}/messages', () => []);

  on('post', '/api/appointments', ({body}) => {
    const viewer = me();
    requirePermission('BookAppointments');
    const patient = db().patients.find((p) => p.id === body.patientId);
    if (!patient) return invalid({patientId: ['Choose a patient.']});
    const pathway = body.pathwayId ? findPathway(body.pathwayId) : undefined;
    const stage = pathway?.stages.find((s) => s.id === body.stageId);
    const now = nowUtc();
    const appointment: Appointment = {
      id: newId(),
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
    if (body.staffUserId && body.startUtc && body.endUtc) book(appointment, body.staffUserId, body.startUtc, body.endUtc);
    db().appointments.push(appointment);
    return appointmentResponse(appointment);
  });

  on('post', '/api/appointments/{id}/book', ({params, body}) => {
    requirePermission('BookAppointments');
    const appointment = findAppointment(params.id);
    if (!body.staffUserId || !body.startUtc || !body.endUtc) return invalid({startUtc: ['Choose who and when.']});
    if (appointment.status !== 'Requested' && appointment.status !== 'Booked') return problem(409, 'This appointment can’t be booked now.');
    book(appointment, body.staffUserId, body.startUtc, body.endUtc);
    return appointmentResponse(appointment);
  });

  on('post', '/api/appointments/{id}/outcome', ({params, body}) => {
    const viewer = me();
    requirePermission('BookAppointments');
    const appointment = findAppointment(params.id);
    if (appointment.status !== 'Booked') return problem(409, 'Only a booked appointment has an outcome.');
    appointment.status = body.outcome === 'DidNotAttend' ? 'DidNotAttend' : 'Attended';
    appointment.outcomeAtUtc = nowUtc();
    appointment.outcomeByUsername = viewer.username;
    appointment.lastUpdated = nowUtc();
    return appointmentResponse(appointment);
  });

  on('post', '/api/appointments/{id}/cancel', ({params, body}) => {
    requirePermission('BookAppointments');
    const appointment = findAppointment(params.id);
    if (appointment.status !== 'Requested' && appointment.status !== 'Booked') return problem(409, 'This appointment can’t be cancelled.');
    appointment.status = 'Cancelled';
    appointment.cancelReason = body.reason ?? null;
    appointment.lastUpdated = nowUtc();
    return appointmentResponse(appointment);
  });

  on('put', '/api/appointments/{id}/notes', ({params, body}) => {
    me();
    const appointment = findAppointment(params.id);
    appointment.notes = body.notes ?? null;
    appointment.lastUpdated = nowUtc();
    return appointmentResponse(appointment);
  });

  on('get', '/api/sites', () => [
    {
      id: SITE.id,
      name: SITE.name,
      addressLines: ['1 Example Way', 'Exampleton'],
      postcode: null,
      phone: null,
      isArchived: false,
      canBeDeleted: false,
      rooms: ROOM_LIST.map((room) => ({...room, siteId: SITE.id, siteName: SITE.name, isArchived: false, canBeDeleted: false})),
    },
  ]);
  on('get', '/api/rooms', () =>
    ROOM_LIST.map((room) => ({...room, siteId: SITE.id, siteName: SITE.name, isArchived: false, canBeDeleted: false})),
  );
  on('get', '/api/clinics/sessions', () => []);
  on('get', '/api/clinics/patterns', () => []);
}
