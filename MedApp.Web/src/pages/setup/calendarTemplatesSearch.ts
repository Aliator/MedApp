import {parseSetupSearch, type SetupListSearch} from '../listSearch';

/**
 * Calendar templates are each person's own (docs/calendar-plan.md), on `/calendar/templates` and its editors. Whose
 * templates they are is the `staff` search parameter (a user ID; none means the signed-in user's own), as on the
 * Calendar page. These parse the address bar, dropping anything invalid, so a bad link shows your own list, and build
 * the links between the list and the editors, keeping whose they are.
 */

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function staffId(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined;
  const trimmed = value.trim();
  return GUID.test(trimmed) ? trimmed : undefined;
}

/** Undefined entries are left out, so the URL only shows what's set. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as T;
}

/** The page's tabs: Day templates (when the URL has none), Week templates, and the person's Default week. */
export const CALENDAR_TEMPLATE_TABS = ['day', 'week', 'default'] as const;
export type CalendarTemplatesTab = (typeof CALENDAR_TEMPLATE_TABS)[number];

/** The editors: whose template a new one is, and whose list Back goes to. */
export interface CalendarTemplateEditorSearch {
  staff?: string;
}

/** The list: whose templates, the tab, and that tab's list search. */
export interface CalendarTemplatesSearch extends SetupListSearch, CalendarTemplateEditorSearch {
  tab?: Exclude<CalendarTemplatesTab, 'day'>;
}

function tab(value: unknown): CalendarTemplatesSearch['tab'] {
  const t = typeof value === 'string' ? value.trim().toLowerCase() : undefined;
  if (t === 'week') return 'week';
  // `staff` was the admin page's tab of everyone's default weeks, which this tab replaces.
  return t === 'default' || t === 'staff' ? 'default' : undefined;
}

export function parseCalendarTemplatesSearch(raw: Record<string, unknown>): CalendarTemplatesSearch {
  return compact({...parseSetupSearch(raw), tab: tab(raw.tab), staff: staffId(raw.staff)});
}

export function parseCalendarTemplateEditorSearch(raw: Record<string, unknown>): CalendarTemplateEditorSearch {
  return compact({staff: staffId(raw.staff)});
}

/** `?a=1&b=2` from the values that are set, or nothing. */
function query(values: Record<string, string | undefined>): string {
  const params = new URLSearchParams(Object.entries(values).filter((entry): entry is [string, string] => entry[1] !== undefined));
  const text = params.toString();
  return text ? `?${text}` : '';
}

/** The list on a tab, for whoever `staff` is (none: your own). */
export const calendarTemplatesHref = (tab: CalendarTemplatesTab = 'day', staff?: string) =>
  `/calendar/templates${query({tab: tab === 'day' ? undefined : tab, staff})}`;

/** A day or week template's editor (`new` for a new one), for whoever `staff` is (none: your own). */
export const calendarTemplateEditorHref = (kind: 'day' | 'week', id: string, staff?: string) =>
  `/calendar/templates/${kind}/${id}${query({staff})}`;
