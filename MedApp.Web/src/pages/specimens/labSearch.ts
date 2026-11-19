import {SAMPLE_DUE, SPECIMEN_SOURCES, SPECIMEN_STATUSES, SPECIMEN_VIEWS, type SampleDue, type SpecimenSource, type SpecimenView} from '../../api/specimens';

/** A view of the samples page: one of the worklist's, or All (its view filter removed), newest taken first. */
export type LabView = SpecimenView | 'All';

/** The columns the samples page sorts by, as the API names them (`time` is the view's own time). */
export const LAB_SORTS = ['number', 'patient', 'source', 'test', 'status', 'due', 'time'] as const;
export type LabSortColumn = (typeof LAB_SORTS)[number];

/**
 * The samples page's address (`/lab/samples`): its view, its filters (MC14: where it came from, the clinic, the test,
 * the status and when it's due), the page and the sort. With no view, it opens on To receive. The page's text search
 * is never here: it may be a sample number, which stays out of URLs (the page keeps it, and sends it in a request body).
 */
export interface LabSearch {
  view?: LabView;
  source?: SpecimenSource;
  /** The clinic that asked for it (a team ID). */
  clinic?: string;
  /** The lab test asked for (its ID). */
  test?: string;
  status?: string;
  due?: SampleDue;
  page?: number;
  sort?: LabSortColumn;
  asc?: boolean;
}

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);
const oneOf = <T extends string>(value: unknown, options: readonly T[]) => {
  const lower = text(value)?.toLowerCase();
  return options.find((o) => o.toLowerCase() === lower);
};
const anId = (value: unknown) => {
  const id = text(value)?.toLowerCase();
  return id && /^[0-9a-f-]{36}$/.test(id) ? id : undefined;
};

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the default view. */
export function parseLabSearch(raw: Record<string, unknown>): LabSearch {
  const page = Number(text(raw.page));
  const asc = text(raw.asc)?.toLowerCase();
  const search: LabSearch = {
    view: oneOf(raw.view, [...SPECIMEN_VIEWS, 'All' as const]),
    source: oneOf(raw.source, SPECIMEN_SOURCES),
    clinic: anId(raw.clinic),
    test: anId(raw.test),
    status: oneOf(raw.status, SPECIMEN_STATUSES),
    due: oneOf(raw.due, SAMPLE_DUE),
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: oneOf(raw.sort, LAB_SORTS),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as LabSearch;
}

/** The view a search shows: its own, or To receive (MC14: the first tab, for the lab and the clinics alike). */
export const labViewOf = (search: LabSearch): LabView => search.view ?? 'ToReceive';

/** Whether any of the page's filters (beyond the view) is set. */
export const hasLabFilters = (search: LabSearch) => Boolean(search.source || search.clinic || search.test || search.status || search.due);

/**
 * The order the list shows: the address's (a sort named without a direction is ascending), or the view's own, by its
 * time: oldest first, but All newest first.
 */
export function labSortOf(search: LabSearch, view: LabView): {sort: LabSortColumn; asc: boolean} {
  return search.sort ? {sort: search.sort, asc: search.asc ?? true} : {sort: 'time', asc: view !== 'All'};
}
