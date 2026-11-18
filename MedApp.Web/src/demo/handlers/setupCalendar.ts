import type {Schemas} from '../../api/client';
import {addDays, dayOfWeek} from '../../utils/calendarSlots';
import {usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {specialtyName} from '../seed';
import {
  WEEKDAYS,
  type DemoClinicPattern,
  type DemoClinicSession,
  type DemoDayTemplate,
  type DemoStaffHours,
  type DemoWeekTemplate,
} from '../slices/setup';
import {db} from '../store';
import {nowUtc, today, ukDateOf, ukTime} from '../time';
import {can, me, patientName, requirePermission} from './common';

/**
 * The calendar's setup: working hours (staff and site), closed days, activity types, day and week templates (applied
 * as a calendar is read, as the API's `CalendarProjection` does), and clinic sessions and patterns. Simplified: no
 * clash checks beyond sessions against sessions, no history of template changes (an edit applies to past days too),
 * and nobody is told about a closed day or a cancelled session.
 */

export const DAY_NAMES = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
const dayName = (date: string) => DAY_NAMES[dayOfWeek(date) - 1];

/** The default working day: Monday to Friday, 08:00 to 18:00, at no particular site. */
export const DEFAULT_START = '08:00:00';
export const DEFAULT_END = '18:00:00';

/** "HH:mm" or "HH:mm:ss" as "HH:mm:ss" (null when it isn't a time). */
function asClock(time: string | null | undefined): string | null {
  const match = /^(\d{1,2}):(\d{2})(?::\d{2})?$/.exec(time ?? '');
  return match ? `${match[1].padStart(2, '0')}:${match[2]}:00` : null;
}
const minutesOf = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));
/** A UK date and clock time as UTC. */
export const atClock = (date: string, time: string) => ukTime(date, Number(time.slice(0, 2)), Number(time.slice(3, 5)));

/** Whether a clock time is a working day's (06:00 to 23:00, on 5-minute boundaries). */
const inBounds = (time: string) => minutesOf(time) >= 6 * 60 && minutesOf(time) <= 23 * 60 && minutesOf(time) % 5 === 0;

/** The name of the day closed at a site (or everywhere) on a date, or null. */
export function closedDayName(date: string, siteId: string | null = null): string | null {
  const closed = db().closedDays.find((c) => c.date === date && (c.siteId === null || (siteId !== null && c.siteId === siteId)));
  return closed?.name ?? null;
}

export interface WorkingDay {
  working: boolean;
  /** UK clock times. */
  start: string;
  end: string;
  siteId: string | null;
  closedName: string | null;
}

/** A site's opening hours on a date: its own, or the default. Null when it's closed that weekday. */
function siteOpens(siteId: string, date: string): {opens: string; closes: string} | null {
  const site = db().sites.find((s) => s.id === siteId);
  if (!site?.hours) return dayOfWeek(date) <= 5 ? {opens: DEFAULT_START, closes: DEFAULT_END} : null;
  const day = site.hours.find((h) => h.dayOfWeek === dayName(date));
  return day ? {opens: day.opens, closes: day.closes} : null;
}

/** Someone's working day on a date (`WorkingTimeSet.For`): their hours, their site's, and the closed days. */
export function workingDayOf(userId: string | null | undefined, date: string): WorkingDay {
  const own = userId ? db().staffHours[userId] : undefined;
  const row = own?.days.find((d) => d.dayOfWeek === dayName(date));
  const siteId = row?.siteId ?? null;
  const closedName = closedDayName(date, siteId);
  if (!own) return {working: dayOfWeek(date) <= 5 && !closedName, start: DEFAULT_START, end: DEFAULT_END, siteId: null, closedName};
  if (!row) return {working: false, start: DEFAULT_START, end: DEFAULT_END, siteId: null, closedName};
  const site = siteOpens(row.siteId, date);
  const start = site && site.opens > row.starts ? site.opens : row.starts;
  const end = site && site.closes < row.ends ? site.closes : row.ends;
  return {working: !!site && !closedName && start < end, start, end, siteId, closedName};
}

/**
 * The working time everyone given shares on a date, as UTC (for free times): from the latest start to the earliest
 * end. Null when anyone isn't working (with nobody given: the default day, unless it's closed everywhere).
 */
export function sharedWorkingTime(userIds: string[], date: string): {startUtc: string; endUtc: string} | null {
  const days = (userIds.length > 0 ? userIds : [null]).map((userId) => workingDayOf(userId, date));
  if (days.some((d) => !d.working)) return null;
  const start = days.map((d) => d.start).sort().at(-1)!;
  const end = days.map((d) => d.end).sort()[0];
  return start < end ? {startUtc: atClock(date, start), endUtc: atClock(date, end)} : null;
}

/** The day template someone's calendar uses on a date: an override's, else their default week's; and whether overridden. */
function dayTemplateOn(userId: string, date: string): {template: DemoDayTemplate | null; overridden: boolean} {
  const state = db();
  const key = `${userId}|${date}`;
  if (key in state.dayOverrides) {
    const templateId = state.dayOverrides[key];
    return {template: state.dayTemplates.find((t) => t.id === templateId) ?? null, overridden: true};
  }
  const week = state.defaultWeeks[userId];
  if (!week || week.since > date || dayOfWeek(date) > 5) return {template: null, overridden: false};
  const weekTemplate = state.weekTemplates.find((w) => w.id === week.weekTemplateId);
  const templateId = weekTemplate?.days[WEEKDAYS[dayOfWeek(date) - 1]];
  return {template: state.dayTemplates.find((t) => t.id === templateId) ?? null, overridden: false};
}

/** One day of someone's calendar (`CalendarDayResponse`). */
export function calendarDay(userId: string | null, date: string): Schemas['CalendarDayResponse'] {
  const working = workingDayOf(userId, date);
  const template = userId ? dayTemplateOn(userId, date) : {template: null, overridden: false};
  const gridStart = working.start < DEFAULT_START ? working.start : DEFAULT_START;
  const gridEnd = working.end > DEFAULT_END ? working.end : DEFAULT_END;
  return {
    date,
    isWorkingDay: working.working,
    closedDayName: working.closedName ? `Closed: ${working.closedName}` : null,
    isPast: date < today(),
    isToday: date === today(),
    workStartUtc: atClock(date, working.start),
    workEndUtc: atClock(date, working.end),
    hasHours: working.working,
    siteId: working.siteId,
    gridStartUtc: atClock(date, gridStart),
    gridEndUtc: atClock(date, gridEnd),
    dayTemplateId: template.template?.id ?? null,
    dayTemplateName: template.template?.name ?? null,
    isDayTemplateOverridden: template.overridden,
  };
}

/** An activity type by ID (archived ones too, as blocks keep theirs). */
export const activityType = (activityTypeId: string | null | undefined) => db().activityTypes.find((t) => t.id === activityTypeId);

/** The blocks someone's templates put on a date (none on a day not worked), less those changed or removed that day. */
export function templateBlocksOn(userId: string, date: string, canEdit: boolean): Schemas['CalendarBlockResponse'][] {
  if (!workingDayOf(userId, date).working) return [];
  const {template} = dayTemplateOn(userId, date);
  const hidden = db().templateExceptions;
  return (template?.blocks ?? [])
    .filter((b) => !hidden.includes(`${userId}|${date}|${b.id}`))
    .map((b) => {
      const type = activityType(b.activityTypeId);
      return {
        blockId: null,
        templateBlockId: b.id,
        isFromTemplate: true,
        staffUserId: userId,
        date,
        startUtc: atClock(date, b.startTime),
        endUtc: atClock(date, b.endTime),
        activityTypeId: b.activityTypeId,
        activityName: type?.name ?? null,
        colour: type?.colour ?? null,
        note: b.note,
        canEdit: canEdit && date >= today(),
        overlapsAppointment: false,
      };
    });
}

/** Whether the signed-in person may change someone's calendar and templates: their own, or with ManageAnyCalendar. */
export const canChangeCalendarOf = (userId: string | null | undefined) => userId === me().id || can('ManageAnyCalendar');

function requireCalendarOf(userId: string) {
  if (!canChangeCalendarOf(userId)) refuse(problem(403, 'You can change only your own calendar and templates.'));
}

function requireFuture(date: string) {
  if (date < today()) refuse(problem(409, 'Past days don’t change.'));
}

// Clinic sessions.

const roomOf = (roomId: string | null | undefined) => db().rooms.find((r) => r.id === roomId);
const siteOf = (siteId: string | null | undefined) => db().sites.find((s) => s.id === siteId);

/** A session's slots' starts, in UTC. */
const slotStarts = (s: DemoClinicSession) => {
  const starts: string[] = [];
  for (let at = Date.parse(s.startUtc); at + s.slotMinutes * 60000 <= Date.parse(s.endUtc); at += s.slotMinutes * 60000)
    starts.push(new Date(at).toISOString());
  return starts;
};

/** The appointments booked into a session (not cancelled or entered in error). */
const bookingsIn = (s: DemoClinicSession) =>
  db().appointments.filter((a) => a.clinicSessionId === s.id && ['Booked', 'Attended', 'DidNotAttend'].includes(a.status ?? ''));

export function sessionResponse(s: DemoClinicSession): Schemas['ClinicSessionResponse'] {
  const room = roomOf(s.roomId);
  const site = siteOf(room?.siteId);
  const booked = bookingsIn(s).map((a) => a.startUtc);
  const starts = slotStarts(s);
  const free = s.status === 'Planned' ? starts.filter((at) => !booked.includes(at) && at > nowUtc()) : [];
  return {
    id: s.id,
    staffUserId: s.staffUserId,
    staffUsername: usernameOf(s.staffUserId),
    roomId: s.roomId,
    roomName: room?.name ?? null,
    siteId: site?.id,
    siteName: site?.name ?? null,
    specialtyId: s.specialtyId,
    specialtyName: db().specialties.find((x) => x.id === s.specialtyId)?.name ?? specialtyName(s.specialtyId),
    patternId: s.patternId,
    date: s.date,
    startUtc: s.startUtc,
    endUtc: s.endUtc,
    slotMinutes: s.slotMinutes,
    slotCount: starts.length,
    bookedCount: booked.length,
    freeSlotCount: free.length,
    freeSlotStartsUtc: free,
    status: s.status,
    cancelReason: s.cancelReason,
    cancelledAtUtc: s.cancelledAtUtc,
    lastUpdated: s.lastUpdated,
    canChange: can('ManageClinics') && s.status === 'Planned' && s.date >= today(),
  };
}

/** Someone's planned sessions from one date to another, for their calendar. */
export const sessionsFor = (userId: string, from: string, to: string) =>
  db()
    .clinicSessions.filter((s) => s.staffUserId === userId && s.status === 'Planned' && s.date >= from && s.date <= to)
    .sort((a, b) => a.startUtc.localeCompare(b.startUtc))
    .map(sessionResponse);

export function findSession(sessionId: string): DemoClinicSession {
  const session = db().clinicSessions.find((s) => s.id === sessionId);
  if (!session) refuse(notFound('The clinic session'));
  return session;
}

const patientsToTell = (appointmentIds: string[]): Schemas['PatientToTellResponse'][] =>
  db()
    .appointments.filter((a) => appointmentIds.includes(a.id ?? ''))
    .map((a) => ({
      appointmentId: a.id,
      patientId: a.patientId,
      patientName: patientName(a.patientId),
      nhsNumber: db().patients.find((p) => p.id === a.patientId)?.nhsNumber ?? null,
      startUtc: a.startUtc ?? undefined,
    }));

/** Why a session can't go at that time, in that room, or null (`SessionPlacement`, simplified: no locks). */
function whySessionCant(s: Omit<DemoClinicSession, 'id'> & {id?: string}): string | null {
  const room = roomOf(s.roomId);
  if (!room) return "The room wasn't found.";
  if (room.isArchived) return `${room.name} is archived.`;
  if (s.startUtc <= nowUtc()) return 'A session must be in the future.';
  const working = workingDayOf(s.staffUserId, s.date);
  if (!working.working) return `It isn't one of ${usernameOf(s.staffUserId)}'s working days.`;
  if (closedDayName(s.date, room.siteId)) return 'The site is closed that day.';
  const clash = db().clinicSessions.find(
    (o) =>
      o.id !== s.id &&
      o.status === 'Planned' &&
      (o.staffUserId === s.staffUserId || o.roomId === s.roomId) &&
      o.startUtc < s.endUtc &&
      s.startUtc < o.endUtc,
  );
  if (clash) return clash.roomId === s.roomId ? `${room.name} is taken by another session then.` : 'That time overlaps another of their clinic sessions.';
  return null;
}

/** Checks a session's times and slot length (`ClinicRules`): a 400 when they don't fit. */
function checkSlots(startUtc: string | undefined, endUtc: string | undefined, slotMinutes: number | undefined) {
  const errors: Record<string, string[]> = {};
  if (!startUtc || !endUtc || endUtc <= startUtc) errors.endUtc = ['The session must end after it starts.'];
  else if (ukDateOf(startUtc) !== ukDateOf(endUtc)) errors.endUtc = ['A session starts and ends on the same day.'];
  const length = startUtc && endUtc ? (Date.parse(endUtc) - Date.parse(startUtc)) / 60000 : 0;
  if (!slotMinutes || slotMinutes < 5 || slotMinutes > 120 || slotMinutes % 5 !== 0)
    errors.slotMinutes = ['Slots are 5 to 120 minutes, in steps of 5.'];
  else if (length > 0 && length % slotMinutes !== 0) errors.slotMinutes = ['The slots must fill the session exactly.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
}

function requireSpecialty(specialtyId: string | null | undefined) {
  const specialty = db().specialties.find((s) => s.id === specialtyId);
  if (!specialty) refuse(invalid({specialtyId: ['Choose a specialty.']}));
  return specialty;
}

function patternResponse(p: DemoClinicPattern): Schemas['ClinicPatternResponse'] {
  const room = roomOf(p.roomId);
  const site = siteOf(room?.siteId);
  return {
    ...p,
    staffUsername: usernameOf(p.staffUserId),
    roomName: room?.name ?? null,
    siteId: site?.id,
    siteName: site?.name ?? null,
    specialtyName: db().specialties.find((x) => x.id === p.specialtyId)?.name ?? null,
    hasEnded: !!p.untilDate && p.untilDate < today(),
  };
}

function patternInput(body: Partial<Schemas['ClinicPatternRequest']>) {
  const starts = asClock(body.starts);
  const ends = asClock(body.ends);
  const errors: Record<string, string[]> = {};
  if (!body.staffUserId || !usernameOf(body.staffUserId)) errors.staffUserId = ['Choose who runs the clinic.'];
  if (!roomOf(body.roomId)) errors.roomId = ['Choose a room.'];
  if (!body.dayOfWeek || !DAY_NAMES.includes(body.dayOfWeek)) errors.dayOfWeek = ['Choose a day of the week.'];
  if (!starts || !ends || ends <= starts || !inBounds(starts) || !inBounds(ends))
    errors.ends = ['Choose times from 06:00 to 23:00, on 5-minute steps, the end after the start.'];
  if (!body.everyWeeks || body.everyWeeks < 1 || body.everyWeeks > 4) errors.everyWeeks = ['Every 1 to 4 weeks.'];
  if (!body.fromDate) errors.fromDate = ['Choose the first date.'];
  if (body.untilDate && body.fromDate && body.untilDate < body.fromDate) errors.untilDate = ['The last date is after the first.'];
  const length = starts && ends ? minutesOf(ends) - minutesOf(starts) : 0;
  const slot = body.slotMinutes ?? 0;
  if (slot < 5 || slot > 120 || slot % 5 !== 0 || (length > 0 && length % slot !== 0))
    errors.slotMinutes = ['Slots are 5 to 120 minutes, in steps of 5, filling the clinic exactly.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  requireSpecialty(body.specialtyId);
  return {
    staffUserId: body.staffUserId!,
    roomId: body.roomId!,
    specialtyId: body.specialtyId!,
    dayOfWeek: body.dayOfWeek!,
    starts: starts!,
    ends: ends!,
    slotMinutes: slot,
    everyWeeks: body.everyWeeks!,
    fromDate: body.fromDate!,
    untilDate: body.untilDate || null,
  };
}

// Templates.

function dayTemplateResponse(t: DemoDayTemplate): Schemas['DayTemplateResponse'] {
  const weekCount = db().weekTemplates.filter((w) => WEEKDAYS.some((d) => w.days[d] === t.id)).length;
  return {
    id: t.id,
    ownerUserId: t.ownerUserId,
    name: t.name,
    isArchived: t.isArchived,
    blocks: t.blocks.map((b) => ({...b, activityName: activityType(b.activityTypeId)?.name ?? null, colour: activityType(b.activityTypeId)?.colour ?? null})),
    weekTemplateCount: weekCount,
    canBeDeleted: !db().usedTemplateIds.includes(t.id) && weekCount === 0,
    canEdit: canChangeCalendarOf(t.ownerUserId),
  };
}

function weekTemplateResponse(w: DemoWeekTemplate): Schemas['WeekTemplateResponse'] {
  const staffCount = Object.values(db().defaultWeeks).filter((d) => d.weekTemplateId === w.id).length;
  return {
    id: w.id,
    ownerUserId: w.ownerUserId,
    name: w.name,
    isArchived: w.isArchived,
    days: WEEKDAYS.map((d, i) => ({
      dayOfWeek: DAY_NAMES[i],
      dayTemplateId: w.days[d],
      dayTemplateName: db().dayTemplates.find((t) => t.id === w.days[d])?.name ?? null,
    })),
    staffCount,
    canBeDeleted: !db().usedTemplateIds.includes(w.id) && staffCount === 0,
    canEdit: canChangeCalendarOf(w.ownerUserId),
  };
}

function findDayTemplate(templateId: string): DemoDayTemplate {
  const template = db().dayTemplates.find((t) => t.id === templateId);
  if (!template) refuse(notFound('The day template'));
  return template;
}

function findWeekTemplate(templateId: string): DemoWeekTemplate {
  const template = db().weekTemplates.find((t) => t.id === templateId);
  if (!template) refuse(notFound('The week template'));
  return template;
}

const markUsed = (templateId: string) => {
  if (!db().usedTemplateIds.includes(templateId)) db().usedTemplateIds.push(templateId);
};

function templateName(name: string | null | undefined, ownerUserId: string, list: {id: string; ownerUserId: string; name: string}[], exceptId?: string) {
  const trimmed = name?.trim() ?? '';
  if (!trimmed || trimmed.length > 100) refuse(invalid({name: ['Give the template a name (up to 100 characters).']}));
  if (list.some((t) => t.id !== exceptId && t.ownerUserId === ownerUserId && t.name.toLowerCase() === trimmed.toLowerCase()))
    refuse(problem(409, `There's already a template called '${trimmed}'.`));
  return trimmed;
}

/** A day template's blocks, checked: known activities, inside the working day's bounds, no overlaps. */
function dayTemplateBlocks(body: Partial<Schemas['DayTemplateRequest']>, existing: DemoDayTemplate['blocks'] = []) {
  const blocks = (body.blocks ?? []).map((b) => ({
    activityTypeId: b.activityTypeId ?? '',
    startTime: asClock(b.startTime) ?? '',
    endTime: asClock(b.endTime) ?? '',
    note: b.note?.trim() || null,
  }));
  const sorted = [...blocks].sort((a, b) => a.startTime.localeCompare(b.startTime));
  const bad = sorted.some(
    (b, i) =>
      !activityType(b.activityTypeId) ||
      !b.startTime ||
      !b.endTime ||
      b.endTime <= b.startTime ||
      !inBounds(b.startTime) ||
      !inBounds(b.endTime) ||
      (i > 0 && sorted[i - 1].endTime > b.startTime),
  );
  if (bad) refuse(invalid({blocks: ['Each block needs an activity, and times from 06:00 to 23:00 that don’t overlap.']}));
  // An unchanged block keeps its ID, so a day's changes to it stay.
  return sorted.map((b) => {
    const same = existing.find((e) => e.activityTypeId === b.activityTypeId && e.startTime === b.startTime && e.endTime === b.endTime && e.note === b.note);
    return {id: same?.id ?? newId(), ...b};
  });
}

function weekDays(body: Partial<Schemas['WeekTemplateRequest']>, ownerUserId: string): DemoWeekTemplate['days'] {
  const days = Object.fromEntries(WEEKDAYS.map((d) => [d, body[d] || null])) as DemoWeekTemplate['days'];
  for (const templateId of Object.values(days)) {
    if (!templateId) continue;
    const template = db().dayTemplates.find((t) => t.id === templateId && t.ownerUserId === ownerUserId);
    if (!template) refuse(notFound('The day template'));
    if (template.isArchived && !db().weekTemplates.some((w) => Object.values(w.days).includes(templateId)))
      refuse(problem(409, `The '${template.name}' day template is archived.`));
  }
  return days;
}

/** What's on everyone's calendars on a date (`DayBookingsResponse`), for closing it. */
export function bookingsOn(date: string): Schemas['DayBookingsResponse'] {
  const state = db();
  const appointments = state.appointments.filter((a) => a.status === 'Booked' && a.startUtc && ukDateOf(a.startUtc) === date);
  const blocks = state.blocks.filter((b) => b.date === date);
  const meetings = state.meetings.filter((m) => m.date === date);
  const userIds = [
    ...new Set([
      ...appointments.map((a) => a.staffUserId!),
      ...blocks.map((b) => b.staffUserId!),
      ...meetings.flatMap((m) => [m.organiserUserId!, ...(m.attendees ?? []).map((a) => a.userId!)]),
    ]),
  ];
  return {
    date,
    appointments: appointments.length,
    blocks: blocks.length,
    meetings: meetings.length,
    staff: userIds.map((userId) => ({
      userId,
      username: usernameOf(userId),
      appointments: appointments.filter((a) => a.staffUserId === userId).length,
      blocks: blocks.filter((b) => b.staffUserId === userId).length,
      meetings: meetings.filter((m) => m.organiserUserId === userId || (m.attendees ?? []).some((a) => a.userId === userId)).length,
    })),
  };
}

function closedDayResponse(c: {id: string; date: string; name: string; siteId: string | null}, withBookings = false): Schemas['ClosedDayResponse'] {
  return {
    id: c.id,
    date: c.date,
    name: c.name,
    siteId: c.siteId,
    siteName: siteOf(c.siteId)?.name ?? null,
    isPast: c.date < today(),
    ...(withBookings ? {bookings: bookingsOn(c.date)} : {}),
  };
}

/** England and Wales's bank holidays in a year (the API's `BankHolidays.EnglandAndWales`). */
export function bankHolidays(year: number): {date: string; name: string}[] {
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  const utc = (month: number, day: number) => new Date(Date.UTC(year, month - 1, day));
  const a = year % 19;
  const b = Math.floor(year / 100);
  const c = year % 100;
  const h = (19 * a + b - Math.floor(b / 4) - Math.floor((b - Math.floor((b + 8) / 25) + 1) / 3) + 15) % 30;
  const l = (32 + 2 * (b % 4) + 2 * Math.floor(c / 4) - h - (c % 4)) % 7;
  const m = Math.floor((a + 11 * h + 22 * l) / 451);
  const easter = utc(Math.floor((h + l - 7 * m + 114) / 31), ((h + l - 7 * m + 114) % 31) + 1);
  const firstMonday = (month: number) => {
    let date = iso(utc(month, 1));
    while (dayOfWeek(date) !== 1) date = addDays(date, 1);
    return date;
  };
  const lastMonday = (month: number) => {
    let date = addDays(iso(utc(month + 1, 1)), -1);
    while (dayOfWeek(date) !== 1) date = addDays(date, -1);
    return date;
  };
  const holidays = [
    {date: addDays(iso(easter), -2), name: 'Good Friday'},
    {date: addDays(iso(easter), 1), name: 'Easter Monday'},
    {date: firstMonday(5), name: 'Early May bank holiday'},
    {date: lastMonday(5), name: 'Spring bank holiday'},
    {date: lastMonday(8), name: 'Summer bank holiday'},
  ];
  const fixed = [
    {date: iso(utc(1, 1)), name: "New Year's Day"},
    {date: iso(utc(12, 25)), name: 'Christmas Day'},
    {date: iso(utc(12, 26)), name: 'Boxing Day'},
  ];
  holidays.push(...fixed.filter((f) => dayOfWeek(f.date) <= 5));
  for (const f of fixed.filter((x) => dayOfWeek(x.date) > 5)) {
    let substitute = addDays(f.date, 1);
    while (dayOfWeek(substitute) > 5 || holidays.some((x) => x.date === substitute)) substitute = addDays(substitute, 1);
    holidays.push({date: substitute, name: `${f.name} (substitute day)`});
  }
  return holidays.sort((x, y) => x.date.localeCompare(y.date));
}

function staffHoursResponse(userId: string): Schemas['StaffHoursResponse'] {
  const own = db().staffHours[userId];
  const canEdit = can('ManageClinics') || can('ManageAnyCalendar');
  const days = own
    ? own.days.map((d) => ({...d, siteName: siteOf(d.siteId)?.name ?? null}))
    : DAY_NAMES.slice(0, 5).map((dayOfWeek) => ({dayOfWeek, starts: DEFAULT_START, ends: DEFAULT_END, siteId: null, siteName: null}));
  return {staffUserId: userId, hasOwnHours: !!own, since: own?.since ?? null, canEdit, days};
}

export function registerSetupCalendar() {
  // Working hours.
  on('get', '/api/calendar/staff/{userId}/hours', ({params}) => {
    me();
    return staffHoursResponse(params.userId);
  });
  on('put', '/api/calendar/staff/{userId}/hours', ({params, body}) => {
    me();
    if (!can('ManageClinics') && !can('ManageAnyCalendar'))
      return problem(403, 'Setting working hours needs the permission to manage clinics, or anyone’s calendar.');
    if (!usernameOf(params.userId)) return notFound('The member of staff');
    const days: DemoStaffHours['days'] = [];
    for (const d of body.days ?? []) {
      const starts = asClock(d.starts);
      const ends = asClock(d.ends);
      const site = siteOf(d.siteId);
      if (!d.dayOfWeek || !DAY_NAMES.includes(d.dayOfWeek) || !starts || !ends || ends <= starts || !inBounds(starts) || !inBounds(ends))
        return invalid({days: ['Each day needs times from 06:00 to 23:00, on 5-minute steps, the end after the start.']});
      if (!site) return invalid({days: ['Choose the site for each day.']});
      if (days.some((x) => x.dayOfWeek === d.dayOfWeek)) return invalid({days: ['Each day of the week once.']});
      const opens = site.hours ? site.hours.find((h) => h.dayOfWeek === d.dayOfWeek) : DAY_NAMES.indexOf(d.dayOfWeek) < 5 ? {opens: DEFAULT_START, closes: DEFAULT_END} : null;
      if (!opens || starts < opens.opens || ends > opens.closes)
        return problem(409, `${site.name} isn't open then on ${d.dayOfWeek}s.`);
      days.push({dayOfWeek: d.dayOfWeek, starts, ends, siteId: site.id});
    }
    if (days.length === 0) delete db().staffHours[params.userId];
    else db().staffHours[params.userId] = {since: today(), days};
    return staffHoursResponse(params.userId);
  });

  // Activity types (the setup list; blocks keep their type when it's archived).
  on('get', '/api/calendar/activity-types', ({query}) => {
    me();
    const state = db();
    const used = new Set([...state.blocks.map((b) => b.activityTypeId), ...state.dayTemplates.flatMap((t) => t.blocks.map((b) => b.activityTypeId))]);
    return state.activityTypes
      .filter((t) => query.get('includeArchived') === 'true' || !t.isArchived)
      .map((t) => ({...t, canBeDeleted: !used.has(t.id)}))
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));
  });
  const findType = (typeId: string) => {
    const type = activityType(typeId);
    if (!type) refuse(notFound('The activity type'));
    return type;
  };
  const typeInput = (body: {name?: string | null; colour?: string | null}, exceptId?: string) => {
    const name = body.name?.trim() ?? '';
    if (!name || name.length > 50) refuse(invalid({name: ['Give the activity a name (up to 50 characters).']}));
    if (!body.colour?.trim()) refuse(invalid({colour: ['Choose a colour.']}));
    if (db().activityTypes.some((t) => t.id !== exceptId && (t.name ?? '').toLowerCase() === name.toLowerCase()))
      refuse(problem(409, `There's already an activity type called '${name}'.`));
    return {name, colour: body.colour!.trim()};
  };
  on('get', '/api/calendar/activity-types/{id}', ({params}) => {
    me();
    return findType(params.id);
  });
  on('post', '/api/calendar/activity-types', ({body}) => {
    requirePermission('ManageSetup');
    const type = {id: newId(), ...typeInput(body), isArchived: false, canBeDeleted: true};
    db().activityTypes.push(type);
    return type;
  });
  on('patch', '/api/calendar/activity-types/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const type = findType(params.id);
    Object.assign(type, typeInput({name: body.name ?? type.name, colour: body.colour ?? type.colour}, type.id));
    return type;
  });
  for (const [path, archived] of [
    ['/api/calendar/activity-types/{id}/archive', true],
    ['/api/calendar/activity-types/{id}/restore', false],
  ] as const)
    on('post', path, ({params}) => {
      requirePermission('ManageSetup');
      const type = findType(params.id);
      type.isArchived = archived;
      return type;
    });
  on('delete', '/api/calendar/activity-types/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const type = findType(params.id);
    const state = db();
    if (state.blocks.some((b) => b.activityTypeId === type.id) || state.dayTemplates.some((t) => t.blocks.some((b) => b.activityTypeId === type.id)))
      return problem(409, `'${type.name}' is on calendars or templates, so it can't be deleted: archive it instead.`);
    state.activityTypes = state.activityTypes.filter((t) => t.id !== type.id);
    return undefined;
  });

  // Closed days.
  on('get', '/api/calendar/closed-days', ({query}) => {
    me();
    const from = query.get('From');
    const to = query.get('To');
    return db()
      .closedDays.filter((c) => (!from || c.date >= from) && (!to || c.date <= to))
      .sort((a, b) => a.date.localeCompare(b.date) || (a.siteId ?? '').localeCompare(b.siteId ?? ''))
      .map((c) => closedDayResponse(c));
  });
  on('get', '/api/calendar/closed-days/bookings', ({query}) => {
    me();
    return bookingsOn(query.get('date') ?? today());
  });
  on('post', '/api/calendar/closed-days', ({body}) => {
    requirePermission('ManageSetup');
    const name = body.name?.trim() ?? '';
    const errors: Record<string, string[]> = {};
    if (!body.date) errors.date = ['Choose a date.'];
    else if (body.date < today()) errors.date = ['A closed day can’t be in the past.'];
    if (!name || name.length > 100) errors.name = ['Give it a name (up to 100 characters).'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const siteId = body.siteId || null;
    if (siteId && !siteOf(siteId)) return notFound('The site');
    const state = db();
    if (state.closedDays.some((c) => c.date === body.date && c.siteId === siteId))
      return problem(409, siteId ? 'That site is already closed that day.' : 'That day is already closed everywhere.');
    if (siteId && state.closedDays.some((c) => c.date === body.date && c.siteId === null)) return problem(409, 'That day is already closed everywhere.');
    const closed = {id: newId(), date: body.date!, name, siteId};
    state.closedDays.push(closed);
    return new DemoReply(201, closedDayResponse(closed, true));
  });
  on('post', '/api/calendar/closed-days/bank-holidays', ({body}) => {
    requirePermission('ManageSetup');
    const year = body.year ?? 0;
    if (year < 2000 || year > 2100) return invalid({year: ['Year must be 2000 to 2100.']});
    const added: Schemas['ClosedDayResponse'][] = [];
    const skipped: Schemas['SkippedBankHolidayResponse'][] = [];
    for (const holiday of bankHolidays(year)) {
      if (holiday.date < today()) skipped.push({...holiday, reason: 'In the past'});
      else if (db().closedDays.some((c) => c.date === holiday.date && c.siteId === null)) skipped.push({...holiday, reason: 'Already closed'});
      else {
        const closed = {id: newId(), ...holiday, siteId: null};
        db().closedDays.push(closed);
        added.push(closedDayResponse(closed, true));
      }
    }
    return {year, added, skipped};
  });
  on('delete', '/api/calendar/closed-days/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const closed = db().closedDays.find((c) => c.id === params.id);
    if (!closed) return notFound('The closed day');
    if (closed.date < today()) return problem(409, 'A closed day in the past stays.');
    db().closedDays = db().closedDays.filter((c) => c.id !== closed.id);
    return undefined;
  });

  // Day templates.
  on('get', '/api/calendar/staff/{userId}/day-templates', ({params, query}) => {
    me();
    return db()
      .dayTemplates.filter((t) => t.ownerUserId === params.userId && (query.get('includeArchived') === 'true' || !t.isArchived))
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(dayTemplateResponse);
  });
  on('get', '/api/calendar/day-templates/{id}', ({params}) => {
    me();
    return dayTemplateResponse(findDayTemplate(params.id));
  });
  on('post', '/api/calendar/staff/{userId}/day-templates', ({params, body}) => {
    requireCalendarOf(params.userId);
    const template: DemoDayTemplate = {
      id: newId(),
      ownerUserId: params.userId,
      name: templateName(body.name, params.userId, db().dayTemplates),
      isArchived: false,
      blocks: dayTemplateBlocks(body),
    };
    db().dayTemplates.push(template);
    return dayTemplateResponse(template);
  });
  on('put', '/api/calendar/day-templates/{id}', ({params, body}) => {
    const template = findDayTemplate(params.id);
    requireCalendarOf(template.ownerUserId);
    template.name = templateName(body.name, template.ownerUserId, db().dayTemplates, template.id);
    template.blocks = dayTemplateBlocks(body, template.blocks);
    return dayTemplateResponse(template);
  });
  on('delete', '/api/calendar/day-templates/{id}', ({params}) => {
    const template = findDayTemplate(params.id);
    requireCalendarOf(template.ownerUserId);
    if (!dayTemplateResponse(template).canBeDeleted) return problem(409, `'${template.name}' has been used, so it can't be deleted: archive it instead.`);
    db().dayTemplates = db().dayTemplates.filter((t) => t.id !== template.id);
    return undefined;
  });

  // Week templates, and someone's default week.
  on('get', '/api/calendar/staff/{userId}/week-templates', ({params, query}) => {
    me();
    return db()
      .weekTemplates.filter((t) => t.ownerUserId === params.userId && (query.get('includeArchived') === 'true' || !t.isArchived))
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(weekTemplateResponse);
  });
  on('get', '/api/calendar/week-templates/{id}', ({params}) => {
    me();
    return weekTemplateResponse(findWeekTemplate(params.id));
  });
  on('post', '/api/calendar/staff/{userId}/week-templates', ({params, body}) => {
    requireCalendarOf(params.userId);
    const template: DemoWeekTemplate = {
      id: newId(),
      ownerUserId: params.userId,
      name: templateName(body.name, params.userId, db().weekTemplates),
      isArchived: false,
      days: weekDays(body, params.userId),
    };
    Object.values(template.days).forEach((d) => d && markUsed(d));
    db().weekTemplates.push(template);
    return weekTemplateResponse(template);
  });
  on('put', '/api/calendar/week-templates/{id}', ({params, body}) => {
    const template = findWeekTemplate(params.id);
    requireCalendarOf(template.ownerUserId);
    template.name = templateName(body.name, template.ownerUserId, db().weekTemplates, template.id);
    template.days = weekDays(body, template.ownerUserId);
    Object.values(template.days).forEach((d) => d && markUsed(d));
    return weekTemplateResponse(template);
  });
  on('delete', '/api/calendar/week-templates/{id}', ({params}) => {
    const template = findWeekTemplate(params.id);
    requireCalendarOf(template.ownerUserId);
    if (!weekTemplateResponse(template).canBeDeleted) return problem(409, `'${template.name}' has been someone's default week, so it can't be deleted: archive it instead.`);
    db().weekTemplates = db().weekTemplates.filter((t) => t.id !== template.id);
    return undefined;
  });
  for (const [kind, archived] of [
    ['day', true],
    ['day', false],
    ['week', true],
    ['week', false],
  ] as const) {
    const handler = ({params}: {params: Record<string, string>}) => {
      const template = kind === 'day' ? findDayTemplate(params.id) : findWeekTemplate(params.id);
      requireCalendarOf(template.ownerUserId);
      template.isArchived = archived;
      return template;
    };
    if (kind === 'day')
      on('post', archived ? '/api/calendar/day-templates/{id}/archive' : '/api/calendar/day-templates/{id}/restore', (request) =>
        dayTemplateResponse(handler(request) as DemoDayTemplate),
      );
    else
      on('post', archived ? '/api/calendar/week-templates/{id}/archive' : '/api/calendar/week-templates/{id}/restore', (request) =>
        weekTemplateResponse(handler(request) as DemoWeekTemplate),
      );
  }
  on('get', '/api/calendar/staff/{userId}/week-template', ({params}) => {
    me();
    const week = db().defaultWeeks[params.userId];
    return {
      staffUserId: params.userId,
      weekTemplateId: week?.weekTemplateId ?? null,
      weekTemplateName: db().weekTemplates.find((w) => w.id === week?.weekTemplateId)?.name ?? null,
      since: week?.since ?? null,
    };
  });
  on('put', '/api/calendar/staff/{userId}/week-template', ({params, body}) => {
    requireCalendarOf(params.userId);
    if (body.weekTemplateId) {
      const template = findWeekTemplate(body.weekTemplateId);
      if (template.ownerUserId !== params.userId) return notFound('The week template');
      if (template.isArchived) return problem(409, `The '${template.name}' week template is archived.`);
      db().defaultWeeks[params.userId] = {weekTemplateId: template.id, since: today()};
      markUsed(template.id);
    } else delete db().defaultWeeks[params.userId];
    const week = db().defaultWeeks[params.userId];
    return {
      staffUserId: params.userId,
      weekTemplateId: week?.weekTemplateId ?? null,
      weekTemplateName: db().weekTemplates.find((w) => w.id === week?.weekTemplateId)?.name ?? null,
      since: week?.since ?? null,
    };
  });

  // One day: Apply Template, and a template block changed or removed there.
  on('put', '/api/calendar/staff/{userId}/days/{date}/day-template', ({params, body}) => {
    requireCalendarOf(params.userId);
    requireFuture(params.date);
    if (body.dayTemplateId) {
      const template = findDayTemplate(body.dayTemplateId);
      if (template.ownerUserId !== params.userId) return notFound('The day template');
      if (template.isArchived) return problem(409, `The '${template.name}' day template is archived.`);
      markUsed(template.id);
    }
    db().dayOverrides[`${params.userId}|${params.date}`] = body.dayTemplateId || null;
    return calendarDay(params.userId, params.date);
  });
  on('delete', '/api/calendar/staff/{userId}/days/{date}/day-template', ({params}) => {
    requireCalendarOf(params.userId);
    requireFuture(params.date);
    const key = `${params.userId}|${params.date}`;
    if (!(key in db().dayOverrides)) return notFound('The day’s template');
    delete db().dayOverrides[key];
    return undefined;
  });
  const hideTemplateBlock = (params: Record<string, string>) => {
    requireCalendarOf(params.userId);
    requireFuture(params.date);
    const block = templateBlocksOn(params.userId, params.date, true).find((b) => b.templateBlockId === params.templateBlockId);
    if (!block) refuse(notFound('The template block'));
    db().templateExceptions.push(`${params.userId}|${params.date}|${params.templateBlockId}`);
    return block;
  };
  on('put', '/api/calendar/staff/{userId}/days/{date}/template-blocks/{templateBlockId}', ({params, body}) => {
    const type = activityType(body.activityTypeId);
    if (!type || !body.startUtc || !body.endUtc || body.endUtc <= body.startUtc || ukDateOf(body.startUtc) !== params.date)
      return invalid({endUtc: ['Choose an activity, and an end after the start, on the same day.']});
    hideTemplateBlock(params);
    const block: Schemas['CalendarBlockResponse'] = {
      blockId: newId(),
      templateBlockId: null,
      isFromTemplate: false,
      staffUserId: params.userId,
      date: params.date,
      startUtc: body.startUtc,
      endUtc: body.endUtc,
      activityTypeId: type.id,
      activityName: type.name,
      colour: type.colour,
      note: body.note?.trim() || null,
      canEdit: true,
      overlapsAppointment: false,
    };
    db().blocks.push(block);
    return block;
  });
  on('delete', '/api/calendar/staff/{userId}/days/{date}/template-blocks/{templateBlockId}', ({params}) => {
    hideTemplateBlock(params);
    return undefined;
  });

  // Clinic sessions and patterns.
  on('get', '/api/clinics/sessions', ({query}) => {
    me();
    const from = query.get('From') ?? today();
    const to = query.get('To') ?? addDays(from, 6);
    if (to < from || addDays(from, 62) <= to) return invalid({to: ['Ask for at most 62 days.']});
    const roomIds = query.get('SiteId') ? db().rooms.filter((r) => r.siteId === query.get('SiteId')).map((r) => r.id) : null;
    return db()
      .clinicSessions.filter(
        (s) =>
          s.date >= from &&
          s.date <= to &&
          (!query.get('StaffUserId') || s.staffUserId === query.get('StaffUserId')) &&
          (!query.get('RoomId') || s.roomId === query.get('RoomId')) &&
          (!roomIds || roomIds.includes(s.roomId)) &&
          (!query.get('SpecialtyId') || s.specialtyId === query.get('SpecialtyId')) &&
          (query.get('IncludeCancelled') === 'true' || s.status === 'Planned'),
      )
      .sort((a, b) => a.startUtc.localeCompare(b.startUtc) || a.id.localeCompare(b.id))
      .map(sessionResponse)
      .filter((s) => query.get('WithFreeSlots') !== 'true' || (s.freeSlotCount ?? 0) > 0);
  });
  on('get', '/api/clinics/sessions/{id}', ({params}) => {
    me();
    const session = findSession(params.id);
    const bookings = bookingsIn(session);
    return {
      session: sessionResponse(session),
      slots: slotStarts(session).map((startUtc) => {
        const booking = bookings.find((a) => a.startUtc === startUtc);
        return {
          startUtc,
          endUtc: new Date(Date.parse(startUtc) + session.slotMinutes * 60000).toISOString(),
          isPast: startUtc <= nowUtc(),
          appointmentId: booking?.id ?? null,
          patientId: booking?.patientId ?? null,
          patientName: booking ? patientName(booking.patientId) : null,
          appointmentStatus: booking?.status ?? null,
        };
      }),
      patientsToTell: patientsToTell(session.cancelledAppointmentIds),
    };
  });
  on('post', '/api/clinics/sessions', ({body}) => {
    requirePermission('ManageClinics');
    if (!body.staffUserId || !usernameOf(body.staffUserId)) return invalid({staffUserId: ['Choose who runs the clinic.']});
    checkSlots(body.startUtc, body.endUtc, body.slotMinutes);
    requireSpecialty(body.specialtyId);
    const session: DemoClinicSession = {
      id: newId(),
      staffUserId: body.staffUserId,
      roomId: body.roomId ?? '',
      specialtyId: body.specialtyId!,
      patternId: null,
      date: ukDateOf(body.startUtc!),
      startUtc: body.startUtc!,
      endUtc: body.endUtc!,
      slotMinutes: body.slotMinutes!,
      status: 'Planned',
      cancelReason: null,
      cancelledAtUtc: null,
      lastUpdated: nowUtc(),
      cancelledAppointmentIds: [],
    };
    const why = whySessionCant(session);
    if (why) return problem(409, why);
    db().clinicSessions.push(session);
    return new DemoReply(201, sessionResponse(session));
  });
  on('put', '/api/clinics/sessions/{id}', ({params, body}) => {
    requirePermission('ManageClinics');
    const session = findSession(params.id);
    if (body.expectedLastUpdated && body.expectedLastUpdated !== session.lastUpdated)
      return problem(409, 'Someone else changed this session meanwhile. Look at it again before changing it.');
    if (session.status !== 'Planned') return problem(409, 'A cancelled session doesn’t change.');
    if (session.date < today()) return problem(409, 'A session on a past day doesn’t change.');
    checkSlots(body.startUtc, body.endUtc, body.slotMinutes);
    requireSpecialty(body.specialtyId);
    const booked = bookingsIn(session);
    if (booked.length > 0 && (body.startUtc !== session.startUtc || body.endUtc !== session.endUtc || body.slotMinutes !== session.slotMinutes))
      return problem(409, 'This session has bookings: only its room and specialty can change.');
    const changed = {...session, roomId: body.roomId ?? session.roomId, specialtyId: body.specialtyId!, startUtc: body.startUtc!, endUtc: body.endUtc!, slotMinutes: body.slotMinutes!, date: ukDateOf(body.startUtc!)};
    const why = whySessionCant(changed);
    if (why) return problem(409, why);
    Object.assign(session, changed, {lastUpdated: nowUtc()});
    // Booked appointments move to its new room.
    for (const a of booked) if (a.status === 'Booked') a.roomId = session.roomId;
    return sessionResponse(session);
  });
  on('post', '/api/clinics/sessions/{id}/cancel', ({params, body}) => {
    requirePermission('ManageClinics');
    const session = findSession(params.id);
    if (body.expectedLastUpdated && body.expectedLastUpdated !== session.lastUpdated)
      return problem(409, 'Someone else changed this session meanwhile. Look at it again before changing it.');
    if (session.status !== 'Planned') return problem(409, 'This session is already cancelled.');
    const reason = body.reason?.trim() ?? '';
    if (!reason || reason.length > 500) return invalid({reason: ['Say why the session is cancelled (up to 500 characters).']});
    const now = nowUtc();
    // Booked appointments that haven't started are cancelled with it; attended and missed ones stay.
    const cancelled = bookingsIn(session).filter((a) => a.status === 'Booked' && (a.startUtc ?? '') > now);
    for (const a of cancelled) Object.assign(a, {status: 'Cancelled', cancelReason: reason, lastUpdated: now});
    Object.assign(session, {status: 'Cancelled', cancelReason: reason, cancelledAtUtc: now, lastUpdated: now, cancelledAppointmentIds: cancelled.map((a) => a.id!)});
    return {session: sessionResponse(session), patientsToTell: patientsToTell(session.cancelledAppointmentIds)};
  });
  on('get', '/api/clinics/patterns', () => {
    me();
    return db()
      .clinicPatterns.map(patternResponse)
      .sort((a, b) => DAY_NAMES.indexOf(a.dayOfWeek ?? '') - DAY_NAMES.indexOf(b.dayOfWeek ?? '') || (a.starts ?? '').localeCompare(b.starts ?? ''));
  });
  on('post', '/api/clinics/patterns', ({body}) => {
    requirePermission('ManageClinics');
    const pattern: DemoClinicPattern = {id: newId(), ...patternInput(body), lastUpdated: nowUtc()};
    db().clinicPatterns.push(pattern);
    return new DemoReply(201, patternResponse(pattern));
  });
  on('put', '/api/clinics/patterns/{id}', ({params, body}) => {
    requirePermission('ManageClinics');
    const pattern = db().clinicPatterns.find((p) => p.id === params.id);
    if (!pattern) return notFound('The clinic pattern');
    if (body.expectedLastUpdated && body.expectedLastUpdated !== pattern.lastUpdated)
      return problem(409, 'Someone else changed this pattern meanwhile. Look at it again before changing it.');
    Object.assign(pattern, patternInput(body), {lastUpdated: nowUtc()});
    return patternResponse(pattern);
  });
  on('post', '/api/clinics/patterns/{id}/create-sessions', ({params, body}) => {
    requirePermission('ManageClinics');
    const pattern = db().clinicPatterns.find((p) => p.id === params.id);
    if (!pattern) return notFound('The clinic pattern');
    const weeks = body.weeks ?? 0;
    if (weeks < 1 || weeks > 13) return invalid({weeks: ['Make sessions for 1 to 13 weeks.']});
    const created: Schemas['ClinicSessionResponse'][] = [];
    const skipped: Schemas['SkippedSessionResponse'][] = [];
    const last = addDays(today(), weeks * 7 - 1);
    let date = pattern.fromDate;
    while (dayName(date) !== pattern.dayOfWeek) date = addDays(date, 1);
    for (; date <= last && (!pattern.untilDate || date <= pattern.untilDate); date = addDays(date, 7 * pattern.everyWeeks)) {
      if (date < today()) continue;
      if (db().clinicSessions.some((s) => s.patternId === pattern.id && s.date === date)) {
        skipped.push({date, reason: 'Already made'});
        continue;
      }
      const session: DemoClinicSession = {
        id: newId(),
        staffUserId: pattern.staffUserId,
        roomId: pattern.roomId,
        specialtyId: pattern.specialtyId,
        patternId: pattern.id,
        date,
        startUtc: atClock(date, pattern.starts),
        endUtc: atClock(date, pattern.ends),
        slotMinutes: pattern.slotMinutes,
        status: 'Planned',
        cancelReason: null,
        cancelledAtUtc: null,
        lastUpdated: nowUtc(),
        cancelledAppointmentIds: [],
      };
      const why = session.startUtc <= nowUtc() ? 'Already started' : whySessionCant(session);
      if (why) skipped.push({date, reason: why});
      else {
        db().clinicSessions.push(session);
        created.push(sessionResponse(session));
      }
    }
    return {created, skipped};
  });
}

