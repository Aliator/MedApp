import type {ISODateString} from '@astryxdesign/core/Calendar';

/** Calendars start the week on Monday (plan Q15). Astryx defaults to Sunday whatever the locale. */
export const WEEK_STARTS_ON = 'mon' as const;

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;

function parse(iso: string): {year: number; month: number; day: number} | null {
  const match = ISO_DATE.exec(iso);
  if (!match) return null;
  const [year, month, day] = match.slice(1).map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  // Rejects dates that don't exist, e.g. 2023-02-30.
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) return null;
  return {year, month, day};
}

/** Today's date (local time) as YYYY-MM-DD, the form the API and Astryx's date inputs use. */
export function todayIso(now: Date = new Date()): ISODateString {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}` as ISODateString;
}

/** Whole years between a date of birth and `on` (default today), or null for an invalid date or one in the future. */
export function ageOn(dateOfBirth: string, on: string = todayIso()): number | null {
  const dob = parse(dateOfBirth);
  const at = parse(on);
  if (!dob || !at) return null;
  let age = at.year - dob.year;
  if (at.month < dob.month || (at.month === dob.month && at.day < dob.day)) age--;
  return age >= 0 ? age : null;
}

function format(iso: string, month: 'long' | 'short'): string {
  const date = parse(iso);
  if (!date) return iso;
  return new Intl.DateTimeFormat('en-GB', {day: 'numeric', month, year: 'numeric', timeZone: 'UTC'}).format(
    new Date(Date.UTC(date.year, date.month - 1, date.day)),
  );
}

/** A date as UK users read it, e.g. "14 March 1980"; the input itself if it isn't a valid YYYY-MM-DD date. */
export const formatDate = (iso: string) => format(iso, 'long');

/**
 * A moment (an ISO date-time from the API, in UTC) in UK time, e.g. "3 Nov 2026, 14:05". The input itself if it
 * can't be read.
 */
export function formatDateTime(isoUtc: string): string {
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) return isoUtc;
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Europe/London',
  }).format(date);
}

/** The same, shorter, for tables: "14 Mar 1980". */
export const formatShortDate = (iso: string) => format(iso, 'short');
