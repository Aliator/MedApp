/**
 * The Calendar and Needs booking pages keep their state in the URL, like the list pages (`listSearch.ts`), so a day,
 * a person's calendar or an open appointment is a link (notifications link to `/calendar?appointment=<id>`). These
 * parse what's in the address bar, dropping anything invalid, so a bad link shows the default.
 */

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function text(value: unknown): string | undefined {
  if (typeof value !== 'string' && typeof value !== 'number') return undefined;
  const trimmed = String(value).trim();
  return trimmed ? trimmed : undefined;
}

function date(value: unknown): string | undefined {
  const t = text(value);
  if (!t || !ISO_DATE.test(t)) return undefined;
  const [year, month, day] = t.split('-').map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  return parsed.getUTCMonth() === month - 1 && parsed.getUTCDate() === day ? t : undefined;
}

function id(value: unknown): string | undefined {
  const t = text(value);
  return t && GUID.test(t) ? t : undefined;
}

/** Undefined entries are left out, so the URL only shows what's set. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as T;
}

export const CALENDAR_VIEWS = ['day', 'week'] as const;
export type CalendarView = (typeof CALENDAR_VIEWS)[number];

/** The Calendar page: the day shown (today by default), day or week (week), whose (yours), and an open appointment. */
export interface CalendarSearch {
  date?: string;
  view?: CalendarView;
  staff?: string;
  appointment?: string;
}

export function parseCalendarSearch(raw: Record<string, unknown>): CalendarSearch {
  const view = text(raw.view)?.toLowerCase();
  return compact({
    date: date(raw.date),
    // Week is the default, so only day is kept.
    view: view === 'day' ? ('day' as const) : undefined,
    staff: id(raw.staff),
    appointment: id(raw.appointment),
  });
}

/** Needs booking's sort columns, by their URL names, and the API's names for them. */
export const NEEDS_BOOKING_SORTS = {Due: 'dueDate', Requested: 'requested', Patient: 'patient'} as const;
export type NeedsBookingSortColumn = keyof typeof NEEDS_BOOKING_SORTS;

/** Needs booking: its page, sort (due date, soonest first, by default) and a patient search. */
export interface NeedsBookingSearch {
  page?: number;
  sort?: NeedsBookingSortColumn;
  asc?: boolean;
  patient?: string;
}

export function parseNeedsBookingSearch(raw: Record<string, unknown>): NeedsBookingSearch {
  const page = Number(text(raw.page));
  const sort = Object.keys(NEEDS_BOOKING_SORTS).find((k) => k.toLowerCase() === text(raw.sort)?.toLowerCase()) as NeedsBookingSortColumn | undefined;
  const asc = text(raw.asc)?.toLowerCase();
  return compact({
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort,
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
    patient: text(raw.patient),
  });
}
