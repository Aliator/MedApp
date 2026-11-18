import {searchNow} from '../../app/beforePathways';
import {CLOSED_REFERRAL_STATUSES, type ReferralSearch, type ReferralStatus} from '../../api/referrals';
import {parseIdChoice} from '../listSearch';

/**
 * The Referrals page's address: its view, page, sort, the patient's name searched for and the teams. The view is
 * Awaiting triage (the inbox) unless the URL says `view=information`, `waiting`, `paused`, `closed` or `all`, so the inbox's own link
 * has no search at all.
 */

/** URL sort names, and the API's names for them. */
export const REFERRAL_SORTS = {
  Wait: 'daysWaited',
  Received: 'receivedOn',
  Target: 'targetOn',
  Review: 'reviewOn',
  Patient: 'patient',
  Team: 'team',
  Priority: 'priority',
  Registered: 'registered',
} as const;
export type ReferralSortColumn = keyof typeof REFERRAL_SORTS;

/**
 * What the list shows: referrals awaiting triage (the inbox), those waiting for more information from the referrer, the waiting list (accepted, clock still running or
 * paused), the paused clocks, the closed ones, or all of them.
 */
export const REFERRAL_VIEWS = ['awaiting', 'information', 'waiting', 'paused', 'closed', 'all'] as const;
export type ReferralView = (typeof REFERRAL_VIEWS)[number];

export interface ReferralsListSearch {
  page?: number;
  sort?: ReferralSortColumn;
  asc?: boolean;
  /** Left out for the inbox (Awaiting triage). */
  view?: Exclude<ReferralView, 'awaiting'>;
  /** The patient's name contains this. */
  patient?: string;
  /** Team IDs: an ID choice (see `parseIdChoice`). */
  team?: string;
}

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

function oneOf<T extends string>(value: unknown, options: readonly T[]): T | undefined {
  const t = text(value)?.toLowerCase();
  return options.find((option) => option.toLowerCase() === t);
}

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the inbox. */
export function parseReferralsSearch(saved: Record<string, unknown>): ReferralsListSearch {
  const raw = searchNow(saved);
  const page = Number(text(raw.page));
  const asc = text(raw.asc)?.toLowerCase();
  const view = oneOf(raw.view, REFERRAL_VIEWS);
  const search: ReferralsListSearch = {
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: oneOf(raw.sort, Object.keys(REFERRAL_SORTS) as ReferralSortColumn[]),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
    view: view === 'awaiting' ? undefined : view,
    patient: text(raw.patient),
    team: parseIdChoice(raw.team),
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as ReferralsListSearch;
}

export const referralViewOf = (search: ReferralsListSearch): ReferralView => search.view ?? 'awaiting';

/** The statuses a view asks the API for: none for All or Paused clocks (only accepted referrals' clocks pause). */
export function viewStatuses(view: ReferralView): ReferralStatus[] | undefined {
  if (view === 'awaiting') return ['AwaitingTriage'];
  if (view === 'information') return ['InformationRequested'];
  if (view === 'waiting') return ['Accepted'];
  if (view === 'closed') return CLOSED_REFERRAL_STATUSES;
  return undefined;
}

/**
 * What a view asks the API for: its statuses, and for the waiting list the clocks not stopped (running or paused), for
 * Paused clocks only those paused.
 */
export function viewFilters(view: ReferralView): Pick<ReferralSearch, 'Statuses' | 'ClockStopped' | 'Paused'> {
  return {
    Statuses: viewStatuses(view),
    ClockStopped: view === 'waiting' ? false : undefined,
    Paused: view === 'paused' ? true : undefined,
  };
}

/**
 * Each view's order when the URL names none: the inbox and the waiting list by the wait, longest first (decision 39);
 * paused clocks by review date, soonest (or most overdue) first (decision 35); the rest by date received, oldest first.
 * Orders are by dates only, never by anything clinical.
 */
export function defaultSort(view: ReferralView): {sort: ReferralSortColumn; asc: boolean} {
  if (view === 'awaiting' || view === 'waiting') return {sort: 'Wait', asc: false};
  if (view === 'paused') return {sort: 'Review', asc: true};
  return {sort: 'Received', asc: true};
}

/** The order the list shows: the URL's (a sort named without a direction is ascending), or the view's own. */
export function sortOf(search: ReferralsListSearch): {sort: ReferralSortColumn; asc: boolean} {
  return search.sort ? {sort: search.sort, asc: search.asc ?? true} : defaultSort(referralViewOf(search));
}
