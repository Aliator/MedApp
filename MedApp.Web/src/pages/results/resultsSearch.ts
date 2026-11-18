import {searchNow} from '../../app/beforePathways';
import {RESULT_VIEWS, type ResultView} from '../../api/results';

/** A view of the Results page: one of its views, or All (its view filter removed), newest report first. */
export type ResultsView = ResultView | 'All';

/** The columns the Results page sorts by, as the API names them (`status` is then when signed off). */
export const RESULT_SORTS = ['patient', 'title', 'pathway', 'reported', 'status'] as const;
export type ResultSortColumn = (typeof RESULT_SORTS)[number];

/**
 * The Results page's address: its view, the team it's narrowed to, the patient's name searched for, the page and
 * the sort. With no view, it opens on Awaiting my sign-off for holders of SignOffResults (the clinicians who sign
 * results off) and Awaiting sign-off (everyone's) for everyone else, so the Results item's own link has no search at
 * all.
 */
export interface ResultsSearch {
  view?: ResultsView;
  /** A team's ID. */
  team?: string;
  /** The patient's name contains this. */
  patient?: string;
  page?: number;
  sort?: ResultSortColumn;
  asc?: boolean;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the default view. */
export function parseResultsSearch(saved: Record<string, unknown>): ResultsSearch {
  const raw = searchNow(saved);
  const view = text(raw.view)?.toLowerCase();
  const page = Number(text(raw.page));
  const team = text(raw.team);
  const sort = text(raw.sort)?.toLowerCase();
  const asc = text(raw.asc)?.toLowerCase();
  const search: ResultsSearch = {
    view: view === 'all' ? 'All' : RESULT_VIEWS.find((v) => v.value.toLowerCase() === view)?.value,
    team: team && GUID.test(team) ? team.toLowerCase() : undefined,
    patient: text(raw.patient),
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: RESULT_SORTS.find((s) => s === sort),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as ResultsSearch;
}

/** The views the person is offered: Awaiting my sign-off only for holders of SignOffResults (nobody else can be responsible). */
export const resultViewsFor = (canSignOff: boolean) => RESULT_VIEWS.filter((v) => canSignOff || v.value !== 'AwaitingMine');

/**
 * The view a search shows: its own, when it's offered to the person, or their default (Awaiting my sign-off for
 * holders of SignOffResults, Awaiting sign-off otherwise).
 */
export function resultsViewOf(search: ResultsSearch, canSignOff: boolean): ResultsView {
  if (search.view === 'All' || (search.view && resultViewsFor(canSignOff).some((v) => v.value === search.view))) return search.view;
  return canSignOff ? 'AwaitingMine' : 'Awaiting';
}

/**
 * The order the list shows: the address's (a sort named without a direction is ascending), or the view's own: the
 * oldest report first while awaiting sign-off, the newest signed off first, and for All the newest report first.
 */
export function resultsSortOf(search: ResultsSearch, view: ResultsView): {sort: ResultSortColumn; asc: boolean} {
  if (search.sort) return {sort: search.sort, asc: search.asc ?? true};
  if (view === 'SignedOff') return {sort: 'status', asc: false};
  return {sort: 'reported', asc: view !== 'All'};
}
