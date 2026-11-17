import type {BusyTime, TimeRange} from '../api/calendar';
import {addDays, formatTimeRange, intervalOf, minutesBetween, overlaps} from './calendarSlots';
import {parseUtc, ukDay} from './dates';

/**
 * Meeting times, for `MeetingDialog` (team calendar plan, section 4): the lengths offered, the working day's start
 * times on 5-minute steps, the two weeks of suggested times and what comes after them, and who's busy at a time.
 *
 * A meeting is on one weekday, 08:00 to 18:00 UK time, starting and ending on 5-minute boundaries (the server checks
 * the same). Its time is held as a UK date and minutes from 08:00 that day, and turned into UTC only to save it or
 * compare it with times from the API. The UK's clocks change at 01:00 UTC on a Sunday, never inside a working day.
 */

/** The lengths a meeting can have, in minutes. */
export const MEETING_LENGTHS = [15, 30, 45, 60, 90, 120] as const;
/** A new meeting's length, unless it starts with a time: an hour. */
export const DEFAULT_MEETING_LENGTH = 60;
/** A meeting starts and ends on these steps. */
export const MEETING_STEP_MINUTES = 5;
/** The working day: 08:00 to 18:00 UK time, in minutes. */
export const WORKING_DAY_MINUTES = 600;
/** How many days of suggested times are asked for at once: two weeks. */
export const SUGGESTION_DAYS = 14;

const WORKING_DAY_START_HOUR = 8;
const MINUTE = 60_000;
const pad = (n: number) => String(n).padStart(2, '0');

const UK_PARTS = new Intl.DateTimeFormat('en-GB', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: 'Europe/London',
});

/** How far UK time is ahead of UTC at a moment, in minutes (0 in winter, 60 in summer time). */
function ukOffsetMinutes(at: number): number {
  const part = Object.fromEntries(UK_PARTS.formatToParts(new Date(at)).map(({type, value}) => [type, Number(value)]));
  return (Date.UTC(part.year, part.month - 1, part.day, part.hour, part.minute) - at) / MINUTE;
}

/** A time on a UK working day, `minutes` after 08:00 UK time, as an ISO string in UTC. */
export function workingTime(date: string, minutes: number): string {
  const [year, month, day] = date.split('-').map(Number);
  const asIfUtc = Date.UTC(year, month - 1, day, WORKING_DAY_START_HOUR, minutes);
  return new Date(asIfUtc - ukOffsetMinutes(asIfUtc) * MINUTE).toISOString();
}

/** A moment from the API as its UK date and the minutes after 08:00 UK time that day (negative before 08:00). */
export function workingDayOffset(isoUtc: string): {date: string; minutes: number} {
  const date = ukDay(isoUtc);
  return {date, minutes: minutesBetween(workingTime(date, 0), isoUtc)};
}

/** Minutes after 08:00 as a UK clock time: 90 is "09:30". */
export function clockTime(minutes: number): string {
  const total = WORKING_DAY_START_HOUR * 60 + minutes;
  return `${pad(Math.floor(total / 60))}:${pad(total % 60)}`;
}

/** A length in words: "45 minutes", "1 hour", "1 hour 30 minutes", "2 hours". */
export function lengthLabel(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return `${rest} minutes`;
  const hourWords = hours === 1 ? '1 hour' : `${hours} hours`;
  return rest === 0 ? hourWords : `${hourWords} ${rest} minutes`;
}

/** The lengths to offer: `MEETING_LENGTHS`, and `length` too (in order) when it's another (a meeting's own, or a selection's). */
export function lengthOptions(length: number): number[] {
  const lengths: number[] = [...MEETING_LENGTHS];
  return lengths.includes(length) ? lengths : [...lengths, length].sort((a, b) => a - b);
}

/**
 * The start times for a meeting `length` minutes long, in minutes after 08:00: every 5 minutes, ending by 18:00, and
 * `keep` too (the time already chosen) when it isn't one of them but is inside the working day.
 */
export function startOptions(length: number, keep?: number): number[] {
  const starts = new Set<number>();
  for (let m = 0; m + length <= WORKING_DAY_MINUTES; m += MEETING_STEP_MINUTES) starts.add(m);
  if (keep !== undefined && keep >= 0 && keep < WORKING_DAY_MINUTES) starts.add(keep);
  return [...starts].sort((a, b) => a - b);
}

/** The days to ask for suggestions on, from `from`: two weeks (`SUGGESTION_DAYS`). */
export function suggestionRange(from: string): {from: string; to: string} {
  return {from, to: addDays(from, SUGGESTION_DAYS - 1)};
}

/**
 * Where Later suggestions looks from: the day after the last suggestion (the API gives at most three a day, so the
 * rest of that day's are left for a shorter list), or, when nothing fitted, the day after the range.
 */
export function laterFrom(range: {to: string}, suggestions: readonly TimeRange[]): string {
  const last = suggestions.at(-1)?.startUtc;
  return addDays(last ? ukDay(last) : range.to, 1);
}

const SHORT_DAY = new Intl.DateTimeFormat('en-GB', {weekday: 'short', day: 'numeric', month: 'short', timeZone: 'Europe/London'});

/** A suggested time, for its button: "Tue 17 Nov, 10:00 to 11:00". Put together from parts, as Node and browsers differ. */
export function formatSuggestion(startUtc: string, endUtc: string): string {
  const part = Object.fromEntries(SHORT_DAY.formatToParts(parseUtc(startUtc)).map(({type, value}) => [type, value]));
  return `${part.weekday} ${part.day} ${part.month}, ${formatTimeRange(startUtc, endUtc)}`;
}

/**
 * Who's busy between two moments, in words, earliest first (then by name): "k.ashford has Lunch, 10:00 to 11:00". A
 * deleted account is "Deleted user". The same line twice (one person's two blocks reading the same) shows once.
 */
export function busyLines(busy: readonly BusyTime[], startUtc: string, endUtc: string): string[] {
  const meeting = intervalOf(startUtc, endUtc);
  const lines = busy
    .filter((b) => b.startUtc && b.endUtc)
    .map((b) => ({
      interval: intervalOf(b.startUtc!, b.endUtc!),
      line: `${b.username ?? 'Deleted user'} has ${b.what ?? 'something on'}, ${formatTimeRange(b.startUtc!, b.endUtc!)}`,
    }))
    .filter((b) => overlaps(meeting, b.interval))
    .sort((a, b) => a.interval.start - b.interval.start || a.line.localeCompare(b.line))
    .map((b) => b.line);
  return [...new Set(lines)];
}

/**
 * The first start on a day at which a meeting `length` minutes long fits into the free time given (the API's, for
 * everyone at once), on a 5-minute step, in minutes after 08:00; undefined when it fits nowhere.
 */
export function firstFit(date: string, free: readonly TimeRange[], length: number): number | undefined {
  const dayStart = workingTime(date, 0);
  for (const range of free) {
    if (!range.startUtc || !range.endUtc) continue;
    const start = Math.max(0, Math.ceil(minutesBetween(dayStart, range.startUtc) / MEETING_STEP_MINUTES) * MEETING_STEP_MINUTES);
    const end = Math.min(WORKING_DAY_MINUTES, minutesBetween(dayStart, range.endUtc));
    if (start + length <= end) return start;
  }
  return undefined;
}
