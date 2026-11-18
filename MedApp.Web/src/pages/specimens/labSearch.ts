import {SPECIMEN_VIEWS, type SpecimenView} from '../../api/specimens';

/** A view of the Lab page: one of the worklist's, or All (its view filter removed), newest taken first. */
export type LabView = SpecimenView | 'All';

/** The columns the Lab page sorts by, as the API names them (`time` is the view's own time). */
export const LAB_SORTS = ['number', 'patient', 'lab', 'status', 'time'] as const;
export type LabSortColumn = (typeof LAB_SORTS)[number];

/**
 * The Lab page's address: its view, the lab it's narrowed to, the page and the sort. With no view, it opens on Expected
 * for holders of ProcessSpecimens (the lab's own staff) and To send for everyone else (the clinic, who send them), so
 * the Lab item's own link has no search at all. The page's text search is never here: it may be a specimen number,
 * which stays out of URLs (the page keeps it, and sends it in a request body).
 */
export interface LabSearch {
  view?: LabView;
  /** A lab's ID. */
  lab?: string;
  page?: number;
  sort?: LabSortColumn;
  asc?: boolean;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the default view. */
export function parseLabSearch(raw: Record<string, unknown>): LabSearch {
  const view = text(raw.view)?.toLowerCase();
  const page = Number(text(raw.page));
  const lab = text(raw.lab);
  const sort = text(raw.sort)?.toLowerCase();
  const asc = text(raw.asc)?.toLowerCase();
  const search: LabSearch = {
    view: [...SPECIMEN_VIEWS, 'All' as const].find((v) => v.toLowerCase() === view),
    lab: lab && GUID.test(lab) ? lab.toLowerCase() : undefined,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: LAB_SORTS.find((s) => s === sort),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as LabSearch;
}

/** The view a search shows: its own, or the default for the person (Expected for the lab's staff, To send otherwise). */
export const labViewOf = (search: LabSearch, canProcess: boolean): LabView => search.view ?? (canProcess ? 'Expected' : 'ToSend');

/**
 * The order the list shows: the address's (a sort named without a direction is ascending), or the view's own, by its
 * time: oldest first, but All newest first.
 */
export function labSortOf(search: LabSearch, view: LabView): {sort: LabSortColumn; asc: boolean} {
  return search.sort ? {sort: search.sort, asc: search.asc ?? true} : {sort: 'time', asc: view !== 'All'};
}
