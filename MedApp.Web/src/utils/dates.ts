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

const plural = (n: number, unit: string) => `${n} ${unit}${n === 1 ? '' : 's'}`;

/**
 * An age as NHS patient banners give it, finer for the youngest: under 4 weeks in days ("6 days"), under 3 months in
 * weeks ("9 weeks"), under 2 years in months ("14 months"), then years ("46 years"). On `on` (default today), as
 * `ageOn`, so a page rendered on the server matches the browser's on the same day. Null for an invalid date or one
 * in the future.
 */
export function ageDescription(dateOfBirth: string, on: string = todayIso()): string | null {
  const dob = parse(dateOfBirth);
  const at = parse(on);
  if (!dob || !at) return null;
  const days = Math.round((Date.UTC(at.year, at.month - 1, at.day) - Date.UTC(dob.year, dob.month - 1, dob.day)) / 86_400_000);
  if (days < 0) return null;
  if (days < 28) return plural(days, 'day');
  const months = (at.year - dob.year) * 12 + (at.month - dob.month) - (at.day < dob.day ? 1 : 0);
  if (months < 3) return plural(Math.floor(days / 7), 'week');
  if (months < 24) return plural(months, 'month');
  return plural(ageOn(dateOfBirth, on)!, 'year');
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

/** An ISO date-time that says its time zone: `Z` or an offset such as `+01:00`. */
const HAS_ZONE = /(?:Z|[+-]\d\d:?\d\d)$/i;

/** An ISO date-time (`yyyy-mm-ddThh:mm...`), not a date-only `yyyy-mm-dd`. */
const ISO_DATE_TIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/;

/**
 * A moment from the API (an ISO date-time) as a Date. The API's times are UTC; one that doesn't say so (no `Z` or
 * offset, as times read from the database once were) is read as UTC, never as this browser's local time, which
 * would put it an hour out during British Summer Time. Anything that isn't a date-time, including a date-only
 * `yyyy-mm-dd` (a whole UK day: use `formatDate`), gives an invalid Date. Parse every time from the API with this,
 * never `new Date(value)`.
 */
export function parseUtc(isoUtc: string): Date {
  if (!ISO_DATE_TIME.test(isoUtc)) return new Date(Number.NaN);
  return new Date(HAS_ZONE.test(isoUtc) ? isoUtc : `${isoUtc}Z`);
}

/**
 * A moment (an ISO date-time from the API, in UTC) in UK time, e.g. "3 Nov 2026, 14:05". The input itself if it
 * can't be read.
 */
export function formatDateTime(isoUtc: string): string {
  const date = parseUtc(isoUtc);
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

/**
 * How long ago a moment was, for recent things like notifications: "Just now", "5 min ago", "3 hours ago", then
 * the date and time (`formatDateTime`) from a day on.
 */
export function timeAgo(isoUtc: string, now: Date = new Date()): string {
  const then = parseUtc(isoUtc);
  if (Number.isNaN(then.getTime())) return isoUtc;
  const minutes = Math.floor((now.getTime() - then.getTime()) / 60_000);
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return hours === 1 ? '1 hour ago' : `${hours} hours ago`;
  return formatDateTime(then.toISOString());
}

const UK_DAY = new Intl.DateTimeFormat('en-CA', {year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'Europe/London'});
const UK_DAY_HEADING = new Intl.DateTimeFormat('en-GB', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  timeZone: 'Europe/London',
});
const UK_TIME = new Intl.DateTimeFormat('en-GB', {hour: '2-digit', minute: '2-digit', timeZone: 'Europe/London'});

/** The UK day a moment from the API falls on, as YYYY-MM-DD: what a feed groups its entries by. The input if unreadable. */
export function ukDay(isoUtc: string): string {
  const date = parseUtc(isoUtc);
  return Number.isNaN(date.getTime()) ? isoUtc : UK_DAY.format(date);
}

/**
 * A UK day as a heading over a day's entries, e.g. "Monday 16 November 2026". Put together from its parts, as
 * browsers and Node punctuate the whole differently, and a page rendered on the server must match. The input if
 * unreadable.
 */
export function formatDayHeading(isoUtc: string): string {
  const date = parseUtc(isoUtc);
  if (Number.isNaN(date.getTime())) return isoUtc;
  const part = Object.fromEntries(UK_DAY_HEADING.formatToParts(date).map(({type, value}) => [type, value]));
  return `${part.weekday} ${part.day} ${part.month} ${part.year}`;
}

/** A moment's time of day in the UK, e.g. "14:05". The input if unreadable. */
export function formatTime(isoUtc: string): string {
  const date = parseUtc(isoUtc);
  return Number.isNaN(date.getTime()) ? isoUtc : UK_TIME.format(date);
}
