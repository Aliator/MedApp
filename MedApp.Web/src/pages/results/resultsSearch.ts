import {RESULT_VIEWS, type ResultView} from '../../api/results';

/**
 * The Results page's address: its view, the specialty it's narrowed to and the page. With no view, it opens on Awaiting
 * my sign-off for holders of SignOffResults (the clinicians who sign results off) and Awaiting sign-off (everyone's) for
 * everyone else, so the Results item's own link has no search at all.
 */
export interface ResultsSearch {
  view?: ResultView;
  /** A specialty's ID. */
  specialty?: string;
  page?: number;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the default view. */
export function parseResultsSearch(raw: Record<string, unknown>): ResultsSearch {
  const view = text(raw.view)?.toLowerCase();
  const page = Number(text(raw.page));
  const specialty = text(raw.specialty);
  const search: ResultsSearch = {
    view: RESULT_VIEWS.find((v) => v.value.toLowerCase() === view)?.value,
    specialty: specialty && GUID.test(specialty) ? specialty.toLowerCase() : undefined,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as ResultsSearch;
}

/** The views the person is offered: Awaiting my sign-off only for holders of SignOffResults (nobody else can be responsible). */
export const resultViewsFor = (canSignOff: boolean) => RESULT_VIEWS.filter((v) => canSignOff || v.value !== 'AwaitingMine');

/**
 * The view a search shows: its own, when it's offered to the person, or their default (Awaiting my sign-off for
 * holders of SignOffResults, Awaiting sign-off otherwise).
 */
export function resultsViewOf(search: ResultsSearch, canSignOff: boolean): ResultView {
  if (search.view && resultViewsFor(canSignOff).some((v) => v.value === search.view)) return search.view;
  return canSignOff ? 'AwaitingMine' : 'Awaiting';
}
