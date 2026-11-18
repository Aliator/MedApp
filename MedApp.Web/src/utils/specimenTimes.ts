import {workingTime} from './meetingTimes';
import {minutesBetween} from './calendarSlots';
import {formatTime, parseUtc, ukDay} from './dates';

/**
 * Specimen times (docs/centre-phases-3-5-plan.md, decision 88): how long each step took, in days and hours, and the
 * UK day and clock time a step's time is typed as, turned into UTC to send. Turnaround is date arithmetic on the times
 * staff recorded: it never says anything about what the specimen showed.
 */

const DAY_MINUTES = 24 * 60;
const plural = (count: number, noun: string) => `${count} ${noun}${count === 1 ? '' : 's'}`;

/**
 * How long from one moment to another, in days and hours: "2 days 3 hours", "5 hours", "1 day"; under an hour, in
 * minutes ("40 minutes"). Null when either can't be read, or the second is before the first.
 */
export function durationWords(fromUtc: string | null | undefined, toUtc: string | null | undefined): string | null {
  if (!fromUtc || !toUtc) return null;
  if (Number.isNaN(parseUtc(fromUtc).getTime()) || Number.isNaN(parseUtc(toUtc).getTime())) return null;
  const minutes = minutesBetween(fromUtc, toUtc);
  if (minutes < 0) return null;
  if (minutes < 60) return plural(minutes, 'minute');
  const days = Math.floor(minutes / DAY_MINUTES);
  const hours = Math.floor((minutes % DAY_MINUTES) / 60);
  return [days > 0 ? plural(days, 'day') : null, hours > 0 ? plural(hours, 'hour') : null].filter(Boolean).join(' ');
}

/** The times a specimen's turnaround is worked out from. */
export interface SpecimenStepTimes {
  takenAtUtc?: string | null;
  sentAtUtc?: string | null;
  receivedAtUtc?: string | null;
  reportedAtUtc?: string | null;
}

export interface Turnaround {
  label: string;
  words: string;
}

/**
 * A specimen's turnaround so far: taken to sent, sent to received, received to reported, and taken to reported, each
 * only once both its times are known.
 */
export function specimenTurnarounds(times: SpecimenStepTimes): Turnaround[] {
  const steps: [string, string | null | undefined, string | null | undefined][] = [
    ['Taken to sent', times.takenAtUtc, times.sentAtUtc],
    ['Sent to received', times.sentAtUtc, times.receivedAtUtc],
    ['Received to reported', times.receivedAtUtc, times.reportedAtUtc],
    ['Taken to reported', times.takenAtUtc, times.reportedAtUtc],
  ];
  return steps.flatMap(([label, from, to]) => {
    const words = durationWords(from, to);
    return words ? [{label, words}] : [];
  });
}

/** A UK day and clock time, as a step's time is typed. */
export interface UkDateTime {
  /** yyyy-mm-dd */
  date: string;
  /** HH:MM, 24-hour */
  time: string;
}

/** A moment from the API (or now) as its UK day and clock time: what a step's fields start with. */
export function ukDateTime(isoUtc: string = new Date().toISOString()): UkDateTime {
  return {date: ukDay(isoUtc), time: formatTime(isoUtc)};
}

const CLOCK = /^(\d{1,2}):(\d{2})(?::\d{2})?$/;

/**
 * A UK day and clock time as a moment in UTC, an ISO string with `Z` (`2026-07-01` at `09:30` is
 * `2026-07-01T08:30:00.000Z` in summer time). Null when either is missing or unreadable.
 */
export function ukDateTimeToUtc({date, time}: Partial<UkDateTime>): string | null {
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return null;
  const clock = CLOCK.exec(time?.trim() ?? '');
  if (!clock) return null;
  const hours = Number(clock[1]);
  const minutes = Number(clock[2]);
  if (hours > 23 || minutes > 59) return null;
  // Minutes from 08:00 that day, UK time: before 08:00 is negative, which works the same.
  return workingTime(date, hours * 60 + minutes - 8 * 60);
}
