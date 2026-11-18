/**
 * The Calendar and Requires Appointment pages keep their state in the URL, like the list pages (`listSearch.ts`), so a day,
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

/** People, by user ID: a list, or one or more IDs separated by commas. Invalid ones and repeats are dropped. */
function ids(value: unknown): string[] | undefined {
  const parts = Array.isArray(value) ? value : typeof value === 'string' ? value.split(',') : [];
  const valid = [...new Set(parts.map(id).filter((v): v is string => v !== undefined).map((v) => v.toLowerCase()))];
  return valid.length ? valid : undefined;
}

/** Day, week or (admins only) team: one day with a column for each member of staff. */
export const CALENDAR_VIEWS = ['day', 'week', 'team'] as const;
export type CalendarView = (typeof CALENDAR_VIEWS)[number];

/**
 * The Calendar page: the day shown (today by default), day, week or team (the week, or the day in a narrow window),
 * whose (yours), an open appointment, who the team view shows (everyone with a staff role, by default), and the
 * grid's row length (15 minutes, or 5).
 */
export interface CalendarSearch {
  date?: string;
  view?: CalendarView;
  staff?: string;
  appointment?: string;
  /** A meeting to open in its dialog (a meeting notification's link: `/calendar?date=…&view=day&meeting=<id>`). */
  meeting?: string;
  who?: string[];
  /**
   * The grid's row length: 5-minute rows, written when they're chosen, or 15, which a link can name; left out, the
   * person's own default (the one they chose last, `useListSettings().calendarRows`).
   */
  rows?: 5 | 15;
}

export function parseCalendarSearch(raw: Record<string, unknown>): CalendarSearch {
  const view = text(raw.view)?.toLowerCase();
  return compact({
    date: date(raw.date),
    // Week is the default in a wide window, but a narrow one opens on a day, so a week is kept when it's named.
    view: view === 'day' || view === 'week' || view === 'team' ? view : undefined,
    staff: id(raw.staff),
    appointment: id(raw.appointment),
    meeting: id(raw.meeting),
    who: ids(raw.who),
    rows: text(raw.rows) === '5' ? 5 : text(raw.rows) === '15' ? 15 : undefined,
  });
}

/** Requires Appointment's sort columns, by their URL names, and the API's names for them. */
export const REQUIRES_APPOINTMENT_SORTS = {Due: 'dueDate', Requested: 'requested', Patient: 'patient'} as const;
export type RequiresAppointmentSortColumn = keyof typeof REQUIRES_APPOINTMENT_SORTS;

/** Requires Appointment: its page, sort (due date, soonest first, by default) and a patient search. */
export interface RequiresAppointmentSearch {
  page?: number;
  sort?: RequiresAppointmentSortColumn;
  asc?: boolean;
  patient?: string;
}

export function parseRequiresAppointmentSearch(raw: Record<string, unknown>): RequiresAppointmentSearch {
  const page = Number(text(raw.page));
  const sort = Object.keys(REQUIRES_APPOINTMENT_SORTS).find((k) => k.toLowerCase() === text(raw.sort)?.toLowerCase()) as RequiresAppointmentSortColumn | undefined;
  const asc = text(raw.asc)?.toLowerCase();
  return compact({
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort,
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
    patient: text(raw.patient),
  });
}
