import type {Calendar, CalendarDay} from '../api/calendar';
import {formatTime, parseUtc, ukDay} from './dates';

/**
 * Calendar days and free slots, for the calendar grid and the booking dialog (docs/calendar-plan.md, decisions 1 to 6).
 *
 * Days are UK dates (`yyyy-mm-dd`); times are UTC. Each calendar day from the API carries its working day's start and
 * end in UTC (`workStartUtc`, `workEndUtc`), so slots are found by adding minutes in UTC: no time zone arithmetic
 * here, and a clock change only moves where a day starts in UTC. A UK working day never spans a clock change.
 */

/** The calendar's rows, and the steps a booking or block starts on: 15 minutes. */
export const SLOT_MINUTES = 15;

const MINUTE = 60_000;

/** Today in the UK, as `yyyy-mm-dd`. */
export function ukToday(now: Date = new Date()): string {
  return ukDay(now.toISOString());
}

const pad = (n: number) => String(n).padStart(2, '0');

function toUtcDate(date: string): Date {
  const [year, month, day] = date.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

function fromUtcDate(date: Date): string {
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
}

/** A date `days` later (or earlier, when negative). */
export function addDays(date: string, days: number): string {
  const next = toUtcDate(date);
  next.setUTCDate(next.getUTCDate() + days);
  return fromUtcDate(next);
}

/** The day of the week, 1 (Monday) to 7 (Sunday). */
export function dayOfWeek(date: string): number {
  return ((toUtcDate(date).getUTCDay() + 6) % 7) + 1;
}

/** The Monday of a date's week (weeks run Monday to Sunday). */
export function weekStart(date: string): string {
  return addDays(date, 1 - dayOfWeek(date));
}

/** Whether a date is a weekday (Monday to Friday). */
export const isWeekday = (date: string) => dayOfWeek(date) <= 5;

/** The next weekday after `date` (`step` 1), or before it (`step` -1). */
export function nextWeekday(date: string, step: 1 | -1): string {
  let next = addDays(date, step);
  while (!isWeekday(next)) next = addDays(next, step);
  return next;
}

/** A moment `minutes` after another, as an ISO string in UTC. */
export function addMinutes(isoUtc: string, minutes: number): string {
  return new Date(parseUtc(isoUtc).getTime() + minutes * MINUTE).toISOString();
}

/** Whole minutes from one moment to another. */
export function minutesBetween(startUtc: string, endUtc: string): number {
  return Math.round((parseUtc(endUtc).getTime() - parseUtc(startUtc).getTime()) / MINUTE);
}

/** Whether two moments are the same, however they're written ("...:00Z" or "...:00.000Z"). */
export function sameMoment(a: string | null | undefined, b: string | null | undefined): boolean {
  return !!a && !!b && parseUtc(a).getTime() === parseUtc(b).getTime();
}

/** A stretch of time, in milliseconds since the epoch. */
export interface Interval {
  start: number;
  end: number;
}

export const overlaps = (a: Interval, b: Interval) => a.start < b.end && b.start < a.end;

/** An interval from two moments from the API. */
export const intervalOf = (startUtc: string, endUtc: string): Interval => ({
  start: parseUtc(startUtc).getTime(),
  end: parseUtc(endUtc).getTime(),
});

/** Appointments that take up time on a calendar; Requested ones have none, and Cancelled ones give it back. */
const TAKES_TIME = new Set(['Booked', 'Attended', 'DidNotAttend']);

/**
 * What's taken on a calendar: its blocks (stored and from templates), its meetings, and its Booked, Attended and Did
 * not attend appointments, leaving out `ignoreAppointmentId` (the appointment being rescheduled, which may move into
 * its own time).
 */
export function busyTimes(calendar: Pick<Calendar, 'blocks' | 'appointments' | 'meetings'>, ignoreAppointmentId?: string): Interval[] {
  const blocks = (calendar.blocks ?? []).filter((b) => b.startUtc && b.endUtc).map((b) => intervalOf(b.startUtc!, b.endUtc!));
  const meetings = (calendar.meetings ?? []).filter((m) => m.startUtc && m.endUtc).map((m) => intervalOf(m.startUtc!, m.endUtc!));
  const appointments = (calendar.appointments ?? [])
    .filter((a) => a.id !== ignoreAppointmentId && TAKES_TIME.has(a.status ?? '') && a.startUtc && a.endUtc)
    .map((a) => intervalOf(a.startUtc!, a.endUtc!));
  return [...blocks, ...meetings, ...appointments];
}

/**
 * The start times (UTC, ISO) on one day at which something `lengthMinutes` long fits: every 15 minutes (or
 * `stepMinutes`) from the start of the working day, ending by its end, starting after `now`, and overlapping nothing in
 * `busy`. None on a day that isn't a working day or is past.
 */
export function freeStarts(day: CalendarDay, busy: Interval[], lengthMinutes: number, now: Date, stepMinutes: number = SLOT_MINUTES): string[] {
  if (!day.isWorkingDay || day.isPast || !day.workStartUtc || !day.workEndUtc || lengthMinutes <= 0) return [];
  const {start: dayStart, end: dayEnd} = intervalOf(day.workStartUtc, day.workEndUtc);
  const starts: string[] = [];
  for (let start = dayStart; start + lengthMinutes * MINUTE <= dayEnd; start += stepMinutes * MINUTE) {
    const slot = {start, end: start + lengthMinutes * MINUTE};
    if (start > now.getTime() && !busy.some((b) => overlaps(slot, b))) starts.push(new Date(start).toISOString());
  }
  return starts;
}

/** Each day's free start times (`freeStarts`) on a calendar, by date. */
export function freeStartsByDay(
  calendar: Pick<Calendar, 'days' | 'blocks' | 'appointments' | 'meetings'>,
  lengthMinutes: number,
  now: Date,
  ignoreAppointmentId?: string,
  stepMinutes: number = SLOT_MINUTES,
): Record<string, string[]> {
  const busy = busyTimes(calendar, ignoreAppointmentId);
  return Object.fromEntries((calendar.days ?? []).map((day) => [day.date ?? '', freeStarts(day, busy, lengthMinutes, now, stepMinutes)]));
}

const UK_HOUR = new Intl.DateTimeFormat('en-GB', {hour: '2-digit', hourCycle: 'h23', timeZone: 'Europe/London'});

/** Whether a moment is before noon, UK time: the booking dialog groups its times into Morning and Afternoon. */
export function isMorning(isoUtc: string): boolean {
  return Number(UK_HOUR.format(parseUtc(isoUtc))) < 12;
}

const DAY_NAME = new Intl.DateTimeFormat('en-GB', {weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC'});
const SHORT_DAY_NAME = new Intl.DateTimeFormat('en-GB', {weekday: 'short', day: 'numeric', timeZone: 'UTC'});

/** The parts of a date's name, put together by hand: browsers and Node punctuate the whole differently. */
function dayParts(format: Intl.DateTimeFormat, date: string): Record<string, string> {
  return Object.fromEntries(format.formatToParts(toUtcDate(date)).map(({type, value}) => [type, value]));
}

/** A calendar day in words, without the year: "Monday 23 November". */
export function formatCalendarDay(date: string): string {
  const part = dayParts(DAY_NAME, date);
  return `${part.weekday} ${part.day} ${part.month}`;
}

/** A calendar day's weekday, in full: "Monday". */
export function formatWeekday(date: string): string {
  return dayParts(DAY_NAME, date).weekday;
}

/** A calendar day, short, for a column or a button: "Mon 23". */
export function formatShortDay(date: string): string {
  const part = dayParts(SHORT_DAY_NAME, date);
  return `${part.weekday} ${part.day}`;
}

const SHORT_DATE_NAME = new Intl.DateTimeFormat('en-GB', {weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC'});

/** A date with its weekday, short, for a list: "Fri 25 Dec 2026", or without the year, "Fri 25 Dec". */
export function formatShortDayDate(date: string, {withYear = true}: {withYear?: boolean} = {}): string {
  const part = dayParts(SHORT_DATE_NAME, date);
  return `${part.weekday} ${part.day} ${part.month}${withYear ? ` ${part.year}` : ''}`;
}

/** A stretch of time, UK time: "09:00 to 09:30". */
export function formatTimeRange(startUtc: string, endUtc: string): string {
  return `${formatTime(startUtc)} to ${formatTime(endUtc)}`;
}
