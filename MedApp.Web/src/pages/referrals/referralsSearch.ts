import {CLOSED_REFERRAL_STATUSES, OPEN_REFERRAL_STATUSES, type ReferralStatus} from '../../api/referrals';
import {parseIdChoice} from '../listSearch';

/**
 * The Referrals page's address: its view, page, sort, the patient's name searched for and the specialties. The view is Awaiting triage
 * (the inbox) unless the URL says `view=closed` or `view=all`, so the inbox's own link has no search at all.
 */

/** URL sort names, and the API's names for them. Received, oldest first, by default: the longest wait at the top. */
export const REFERRAL_SORTS = {Received: 'receivedOn', Patient: 'patient', Specialty: 'specialty', Priority: 'priority', Registered: 'registered'} as const;
export type ReferralSortColumn = keyof typeof REFERRAL_SORTS;

/** What the list shows: referrals awaiting triage (the inbox), the closed ones, or all of them. */
export const REFERRAL_VIEWS = ['awaiting', 'closed', 'all'] as const;
export type ReferralView = (typeof REFERRAL_VIEWS)[number];

export interface ReferralsListSearch {
  page?: number;
  sort?: ReferralSortColumn;
  asc?: boolean;
  /** Left out for the inbox (Awaiting triage). */
  view?: Exclude<ReferralView, 'awaiting'>;
  /** The patient's name contains this. */
  patient?: string;
  /** Specialty IDs: an ID choice (see `parseIdChoice`). */
  specialty?: string;
}

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

function oneOf<T extends string>(value: unknown, options: readonly T[]): T | undefined {
  const t = text(value)?.toLowerCase();
  return options.find((option) => option.toLowerCase() === t);
}

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the inbox. */
export function parseReferralsSearch(raw: Record<string, unknown>): ReferralsListSearch {
  const page = Number(text(raw.page));
  const asc = text(raw.asc)?.toLowerCase();
  const view = oneOf(raw.view, REFERRAL_VIEWS);
  const search: ReferralsListSearch = {
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: oneOf(raw.sort, Object.keys(REFERRAL_SORTS) as ReferralSortColumn[]),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
    view: view === 'awaiting' ? undefined : view,
    patient: text(raw.patient),
    specialty: parseIdChoice(raw.specialty),
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as ReferralsListSearch;
}

export const referralViewOf = (search: ReferralsListSearch): ReferralView => search.view ?? 'awaiting';

/** The statuses a view asks the API for: none for All. */
export function viewStatuses(view: ReferralView): ReferralStatus[] | undefined {
  if (view === 'awaiting') return OPEN_REFERRAL_STATUSES;
  if (view === 'closed') return CLOSED_REFERRAL_STATUSES;
  return undefined;
}
