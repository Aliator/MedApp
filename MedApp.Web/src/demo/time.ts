import {addDays, ukToday, weekStart} from '../utils/calendarSlots';

/** UK clock times for the light demo's seed and handlers, as the API's `UkTime` does them. */

const LONDON = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/London',
  hourCycle: 'h23',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
});

/** How far UK time is ahead of UTC at a moment, in minutes (0 in winter, 60 in summer). */
function londonOffsetMinutes(at: Date): number {
  const parts = Object.fromEntries(LONDON.formatToParts(at).map((part) => [part.type, part.value]));
  const asUtc = Date.UTC(+parts.year, +parts.month - 1, +parts.day, +parts.hour, +parts.minute);
  return Math.round((asUtc - Math.floor(at.getTime() / 60000) * 60000) / 60000);
}

/** A UK date and time of day as UTC, with a `Z`: `ukTime('2026-11-18', 9, 30)` is "2026-11-18T09:30:00.000Z". */
export function ukTime(date: string, hours: number, minutes = 0): string {
  const [y, m, d] = date.split('-').map(Number);
  const guess = Date.UTC(y, m - 1, d, hours, minutes);
  const offset = londonOffsetMinutes(new Date(guess - londonOffsetMinutes(new Date(guess)) * 60000));
  return new Date(guess - offset * 60000).toISOString();
}

/** The UK date a UTC moment falls on. */
export function ukDateOf(isoUtc: string): string {
  return ukToday(new Date(isoUtc));
}

/** Now, as UTC with a `Z`. */
export const nowUtc = () => new Date().toISOString();

/** Today in the UK. */
export const today = () => ukToday();

/** A date this many days from `from` (default today). */
export const daysFrom = (days: number, from: string = today()) => addDays(from, days);

/** This week's Monday plus `days` (0 is Monday, 4 Friday), so the calendar always has this week's events. */
export const thisWeek = (days: number, from: string = today()) => addDays(weekStart(from), days);

/** The end of a UK day in UTC, as `dueDateUtc` is: the last moment of the target date. */
export const endOfUkDay = (date: string) => new Date(new Date(ukTime(addDays(date, 1), 0)).getTime() - 1).toISOString();
