import type {DayTemplateBlock, WeekTemplate} from '../../api/calendar';

/**
 * Helpers for the calendar setup pages (day and week templates): template times are UK clock times, which the API
 * sends as `HH:mm:ss` and the editors offer in 15-minute steps of the working day (08:00 to 18:00, decision 1).
 */

/** The working day, as clock times. */
export const WORK_START = '08:00';
export const WORK_END = '18:00';

/** `12:00:00` (the API's form) → `12:00`. */
export const clockTime = (time: string | null | undefined) => (time ?? '').slice(0, 5);

/** `12:00` → `12:00:00`, as the API takes it. */
export const apiTime = (time: string) => `${time}:00`;

const minutes = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));
const fromMinutes = (total: number) => `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;

/** Every 15 minutes from `from` to `to`, both included: the times a block can start or end at. */
export function quarterHours(from = WORK_START, to = WORK_END): string[] {
  const times: string[] = [];
  for (let m = minutes(from); m <= minutes(to); m += 15) times.push(fromMinutes(m));
  return times;
}

/** A time moved by some minutes, kept inside the working day. */
export function addMinutes(time: string, by: number): string {
  return fromMinutes(Math.min(Math.max(minutes(time) + by, minutes(WORK_START)), minutes(WORK_END)));
}

/** "Lunch, 12:00–13:00", for a block's token. */
export const blockLabel = (block: Pick<DayTemplateBlock, 'activityName' | 'startTime' | 'endTime'>) =>
  `${block.activityName ?? ''}, ${clockTime(block.startTime)}–${clockTime(block.endTime)}`;

/** The weekdays a week template has, as the API and its request name them, with short names for lists. */
export const WEEKDAYS = [
  {key: 'monday', name: 'Monday', short: 'Mon'},
  {key: 'tuesday', name: 'Tuesday', short: 'Tue'},
  {key: 'wednesday', name: 'Wednesday', short: 'Wed'},
  {key: 'thursday', name: 'Thursday', short: 'Thu'},
  {key: 'friday', name: 'Friday', short: 'Fri'},
] as const;
export type WeekdayKey = (typeof WEEKDAYS)[number]['key'];

/**
 * A week template's days in a line, the days with the same day template together: "Mon to Fri: Standard day", or
 * "Mon, Tue, Thu: Standard day · Wed: Half day · Fri: No template".
 */
export function weekSummary(template: Pick<WeekTemplate, 'days'>): string {
  const groups = new Map<string, string[]>();
  for (const {name, short} of WEEKDAYS) {
    const day = template.days?.find((d) => d.dayOfWeek === name);
    const label = day?.dayTemplateId ? (day.dayTemplateName ?? 'Unknown') : 'No template';
    groups.set(label, [...(groups.get(label) ?? []), short]);
  }
  if (groups.size === 1) return `Mon to Fri: ${[...groups.keys()][0]}`;
  return [...groups].map(([label, days]) => `${days.join(', ')}: ${label}`).join(' · ');
}

/** "1 week template", "3 staff": a count with its noun. */
export const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/** A block being edited: activity, clock times (`HH:mm`) and note, keyed so rows keep their identity. */
export interface BlockRow {
  key: string;
  activityTypeId: string;
  startTime: string;
  endTime: string;
  note: string;
}

/** What's wrong with a block, and which of its times to show it on. */
export interface BlockProblem {
  field: 'startTime' | 'endTime';
  message: string;
}

/**
 * What's wrong with a block in the editor before it's saved, if anything: it ends before it starts, it's outside the
 * working day, or it overlaps another block (named by its activity). The server checks again; this only says so
 * sooner, beside the block.
 */
export function blockProblem(row: BlockRow, rows: readonly BlockRow[], activityName: (id: string) => string): BlockProblem | null {
  if (minutes(row.endTime) <= minutes(row.startTime)) return {field: 'endTime', message: 'Ends before it starts.'};
  if (minutes(row.startTime) < minutes(WORK_START)) return {field: 'startTime', message: `Starts before ${WORK_START}.`};
  if (minutes(row.endTime) > minutes(WORK_END)) return {field: 'endTime', message: `Ends after ${WORK_END}.`};
  const other = rows.find(
    (r) => r.key !== row.key && minutes(r.startTime) < minutes(row.endTime) && minutes(row.startTime) < minutes(r.endTime),
  );
  return other ? {field: 'startTime', message: `Overlaps ${activityName(other.activityTypeId)}, ${other.startTime} to ${other.endTime}.`} : null;
}

/** Blocks by start time (then end), as the editor and the day's preview list them. */
export const byStart = <T extends Pick<BlockRow, 'startTime' | 'endTime'>>(rows: readonly T[]) =>
  [...rows].sort((a, b) => minutes(a.startTime) - minutes(b.startTime) || minutes(a.endTime) - minutes(b.endTime));

/** A stretch of the day for its preview: a block, or free time between blocks. */
export type DayPart = {kind: 'block'; row: BlockRow} | {kind: 'free'; startTime: string; endTime: string};

/** The working day as its blocks and the free time around them, in order (overlapping blocks are listed as they are). */
export function dayParts(rows: readonly BlockRow[]): DayPart[] {
  const parts: DayPart[] = [];
  let at = WORK_START;
  for (const row of byStart(rows)) {
    if (minutes(row.startTime) > minutes(at)) parts.push({kind: 'free', startTime: at, endTime: row.startTime});
    parts.push({kind: 'block', row});
    if (minutes(row.endTime) > minutes(at)) at = row.endTime;
  }
  if (minutes(at) < minutes(WORK_END)) parts.push({kind: 'free', startTime: at, endTime: WORK_END});
  return parts;
}
