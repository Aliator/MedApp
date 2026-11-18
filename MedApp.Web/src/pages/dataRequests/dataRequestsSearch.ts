import {DATA_REQUEST_VIEWS, dataRequestSearchQuery, viewFilters, type DataRequestView} from '../../api/dataRequests';

/**
 * The Data Requests page's address: its view (`view=AwaitingReview`, as the views are named) and page. Without a view,
 * the page shows the open requests.
 */
export interface DataRequestsListSearch {
  view?: DataRequestView;
  page?: number;
}

/** The view the page opens on. */
export const DEFAULT_DATA_REQUEST_VIEW: DataRequestView = 'Open';

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the open requests. */
export function parseDataRequestsSearch(raw: Record<string, unknown>): DataRequestsListSearch {
  const page = Number(text(raw.page));
  const view = text(raw.view)?.toLowerCase();
  const search: DataRequestsListSearch = {
    view: DATA_REQUEST_VIEWS.find((v) => v.value.toLowerCase() === view)?.value,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as DataRequestsListSearch;
}

/** The list's page of requests, for its view, page and page size (the route's loader fetches the same). */
export const dataRequestsListQuery = (view: DataRequestView, page: number, pageSize: number) =>
  dataRequestSearchQuery({...viewFilters(view), Page: page, PageSize: pageSize});
