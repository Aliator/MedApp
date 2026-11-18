import {DATA_REQUEST_VIEWS, dataRequestSearchQuery, viewFilters, type DataRequestView} from '../../api/dataRequests';

/** The columns the Data Requests page sorts by, as the API names them. */
export const DATA_REQUEST_SORTS = ['patient', 'received', 'due', 'status'] as const;
export type DataRequestSortColumn = (typeof DATA_REQUEST_SORTS)[number];

/**
 * The Data Requests page's address: its view (`view=AwaitingReview`, as the views are named), the patient's name
 * searched for, the page and the sort. Without a view, the page shows the open requests.
 */
export interface DataRequestsListSearch {
  view?: DataRequestView;
  /** The patient's name contains this. */
  patient?: string;
  page?: number;
  sort?: DataRequestSortColumn;
  asc?: boolean;
}

/** The view the page opens on. */
export const DEFAULT_DATA_REQUEST_VIEW: DataRequestView = 'Open';

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the open requests. */
export function parseDataRequestsSearch(raw: Record<string, unknown>): DataRequestsListSearch {
  const page = Number(text(raw.page));
  const view = text(raw.view)?.toLowerCase();
  const sort = text(raw.sort)?.toLowerCase();
  const asc = text(raw.asc)?.toLowerCase();
  const search: DataRequestsListSearch = {
    view: DATA_REQUEST_VIEWS.find((v) => v.value.toLowerCase() === view)?.value,
    patient: text(raw.patient),
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: DATA_REQUEST_SORTS.find((s) => s === sort),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as DataRequestsListSearch;
}

/**
 * The order the list shows: the address's (a sort named without a direction is ascending), or the view's own: open
 * ones by due date, soonest first; closed ones newest received first. None is marked for All, which mixes the two.
 */
export function dataRequestsSortOf(search: DataRequestsListSearch, view: DataRequestView): {sort: DataRequestSortColumn; asc: boolean} | null {
  if (search.sort) return {sort: search.sort, asc: search.asc ?? true};
  if (view === 'Open' || view === 'AwaitingReview') return {sort: 'due', asc: true};
  return view === 'All' ? null : {sort: 'received', asc: false};
}

/** The list's page of requests, for its view, search, page and page size (the route's loader fetches the same). */
export const dataRequestsListQuery = (view: DataRequestView, search: DataRequestsListSearch, pageSize: number) =>
  dataRequestSearchQuery({
    ...viewFilters(view),
    Search: search.patient,
    Sort: search.sort,
    Ascending: search.sort ? (search.asc ?? true) : undefined,
    Page: search.page ?? 1,
    PageSize: pageSize,
  });
