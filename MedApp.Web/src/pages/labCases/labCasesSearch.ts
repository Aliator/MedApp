import {LAB_CASE_VIEWS, type LabCaseView} from '../../api/labCases';

/** The columns Lab Cases sorts by, as the API names them. */
export const LAB_CASE_SORTS = ['number', 'patient', 'source', 'test', 'opened'] as const;
export type LabCaseSort = (typeof LAB_CASE_SORTS)[number];

/**
 * Lab Cases' address: the tab, where the cases came from, the page and the sort. The text searched is never here: it
 * may be an NHS number, which stays out of URLs.
 */
export interface LabCasesSearch {
  view?: LabCaseView;
  source?: 'Clinic' | 'Hospital';
  page?: number;
  sort?: LabCaseSort;
  asc?: boolean;
}

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid. */
export function parseLabCasesSearch(raw: Record<string, unknown>): LabCasesSearch {
  const view = text(raw.view);
  const source = text(raw.source);
  const page = Number(text(raw.page));
  const sort = text(raw.sort)?.toLowerCase();
  const asc = text(raw.asc)?.toLowerCase();
  const search: LabCasesSearch = {
    view: LAB_CASE_VIEWS.find((v) => v === view),
    source: source === 'Clinic' || source === 'Hospital' ? source : undefined,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: LAB_CASE_SORTS.find((s) => s === sort),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as LabCasesSearch;
}

/** The tab shown: the address's, else Open. */
export const labCaseViewOf = (search: LabCasesSearch): LabCaseView => search.view ?? 'Open';
