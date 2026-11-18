import type {StaffHours, StaffHoursDayInput} from '../api/calendar';
import type {SiteHours, SiteHoursDayInput} from '../api/sites';

/**
 * Working hours per person and opening hours per site (centre plan decisions 119 to 122): a day of the week, UK clock
 * times (`HH:mm:ss` from the API, `HH:mm` here) and, for staff, the site. Anyone or any site with no hours of their own
 * works the default, Monday to Friday, 08:00 to 18:00. Hours run between 06:00 and 23:00, on 5-minute boundaries; the
 * editor offers 15-minute steps.
 */

/** The days of the week, Monday first, as the API names them, with short names for summaries. */
export const WEEK_DAYS = [
  {name: 'Monday', short: 'Mon'},
  {name: 'Tuesday', short: 'Tue'},
  {name: 'Wednesday', short: 'Wed'},
  {name: 'Thursday', short: 'Thu'},
  {name: 'Friday', short: 'Fri'},
  {name: 'Saturday', short: 'Sat'},
  {name: 'Sunday', short: 'Sun'},
] as const;

export const DEFAULT_START = '08:00';
export const DEFAULT_END = '18:00';
export const EARLIEST = '06:00';
export const LATEST = '23:00';

/** One day in the hours editor: whether it's worked (or open), from and to (`HH:mm`), and for staff the site. */
export interface HoursRow {
  day: (typeof WEEK_DAYS)[number]['name'];
  isOn: boolean;
  from: string;
  to: string;
  siteId: string;
}

const clock = (time: string | null | undefined) => (time ?? '').slice(0, 5);
const minutes = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));
const fromMinutes = (total: number) => `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;

/** Every 15 minutes from 06:00 to 23:00, both included, with `current` too if it's off the steps. */
export function hourOptions(current?: string): {value: string; label: string}[] {
  const times: string[] = [];
  for (let m = minutes(EARLIEST); m <= minutes(LATEST); m += 15) times.push(fromMinutes(m));
  if (current && !times.includes(current)) times.push(current);
  return times.sort().map((t) => ({value: t, label: t}));
}

/** The editor's seven rows: the days given on, the rest off at the default times (and `siteId` for a new day). */
function rowsOf(days: {dayOfWeek?: string | null; from?: string | null; to?: string | null; siteId?: string | null}[], siteId = ''): HoursRow[] {
  return WEEK_DAYS.map(({name}) => {
    const day = days.find((d) => d.dayOfWeek === name);
    return day
      ? {day: name, isOn: true, from: clock(day.from), to: clock(day.to), siteId: day.siteId ?? siteId}
      : {day: name, isOn: false, from: DEFAULT_START, to: DEFAULT_END, siteId};
  });
}

/**
 * A person's hours as the editor's rows. On the default (no hours of their own), the default's days, at
 * `defaultSiteId` (the site a new day starts at).
 */
export const staffRows = (hours: StaffHours | undefined, defaultSiteId = '') =>
  rowsOf((hours?.days ?? []).map((d) => ({dayOfWeek: d.dayOfWeek, from: d.starts, to: d.ends, siteId: d.siteId ?? defaultSiteId})), defaultSiteId);

/** A site's hours as the editor's rows (the default's days when it has none of its own). */
export const siteRows = (hours: SiteHours | undefined) =>
  rowsOf((hours?.days ?? []).map((d) => ({dayOfWeek: d.dayOfWeek, from: d.opens, to: d.closes})));

/** The days worked, to save as someone's hours. */
export const staffInput = (rows: readonly HoursRow[]): StaffHoursDayInput[] =>
  rows.filter((r) => r.isOn).map((r) => ({dayOfWeek: r.day, starts: `${r.from}:00`, ends: `${r.to}:00`, siteId: r.siteId}));

/** The days open, to save as a site's hours. */
export const siteInput = (rows: readonly HoursRow[]): SiteHoursDayInput[] =>
  rows.filter((r) => r.isOn).map((r) => ({dayOfWeek: r.day, opens: `${r.from}:00`, closes: `${r.to}:00`}));

/** What's wrong with a day before it's saved, if anything (the server checks again). */
export function rowProblem(row: HoursRow, needsSite: boolean): string | null {
  if (!row.isOn) return null;
  if (minutes(row.to) <= minutes(row.from)) return 'Ends before it starts.';
  if (needsSite && !row.siteId) return 'Choose a site.';
  return null;
}

/**
 * Hours in a line, the days with the same times together: "Mon to Fri, 08:00 to 18:00", or
 * "Mon, Wed 09:00 to 17:00 · Sat 09:00 to 13:00"; "None" for no days.
 */
export function hoursSummary(days: {dayOfWeek?: string | null; from?: string | null; to?: string | null}[]): string {
  const groups = new Map<string, string[]>();
  for (const {name, short} of WEEK_DAYS) {
    const day = days.find((d) => d.dayOfWeek === name);
    if (!day) continue;
    const times = `${clock(day.from)} to ${clock(day.to)}`;
    groups.set(times, [...(groups.get(times) ?? []), short]);
  }
  if (groups.size === 0) return 'None';
  return [...groups]
    .map(([times, shorts]) => {
      const run = shorts.join(', ') === 'Mon, Tue, Wed, Thu, Fri' ? 'Mon to Fri' : shorts.join(', ');
      return `${run}, ${times}`;
    })
    .join(' · ');
}

/** A site's hours in a line ("Mon to Fri, 08:00 to 18:00"). */
export const siteHoursSummary = (hours: SiteHours) => hoursSummary((hours.days ?? []).map((d) => ({dayOfWeek: d.dayOfWeek, from: d.opens, to: d.closes})));
