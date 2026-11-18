import {keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {statusLabel} from '../utils/statusWords';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {pathwayKeys} from './pathways';
import type {paths} from './schema';

/**
 * Referrals: requests from a referrer (usually a GP practice) for the centre to see one patient in one specialty,
 * registered by staff from the letter (docs/centre-phase2-plan.md, decisions 8 to 17). A referral waits for triage
 * (Awaiting triage, the only open status); before then it can be edited, withdrawn or marked entered in error. Every
 * change carries the version the page showed (`expectedVersion`), so a change made meanwhile is a 409. The letter is a
 * file attached to it (`attachmentsQuery({referralId})`).
 */
export type Referral = Schemas['ReferralResponse'];
export type ReferralSummary = Schemas['ReferralSummaryResponse'];
export type ReferralEvent = Schemas['ReferralEventResponse'];
export type ReferralPage = Schemas['ReferralSummaryResponsePagedResponse'];
export type ReferralSearch = NonNullable<paths['/api/referrals/search']['get']['parameters']['query']>;

/** The statuses, by the names the API uses. Only Awaiting triage is open. */
export const REFERRAL_STATUSES = ['AwaitingTriage', 'Accepted', 'Rejected', 'RedirectedElsewhere', 'Withdrawn', 'EnteredInError'] as const;
export type ReferralStatus = (typeof REFERRAL_STATUSES)[number];
export const OPEN_REFERRAL_STATUSES: ReferralStatus[] = ['AwaitingTriage'];
export const CLOSED_REFERRAL_STATUSES: ReferralStatus[] = REFERRAL_STATUSES.filter((s) => s !== 'AwaitingTriage');

/** The priorities a referrer can ask for, on the pathway scale. */
export const REFERRAL_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;

/** The longest a withdrawal's or an entry in error's detail can be (the API's `Referral.MaxDetailLength`). */
export const MAX_CLOSE_DETAIL = 500;
/** The longest a reason for referral can be (the API's `Referral.MaxReasonLength`). */
export const MAX_REASON = 2000;
/** The longest a practice's or a clinician's name can be. */
export const MAX_REFERRER = 200;
/** The longest an ODS code can be. */
export const MAX_ODS_CODE = 10;

/** A status in words: "AwaitingTriage" → "Awaiting triage", "RedirectedElsewhere" → "Redirected elsewhere". */
export const referralStatusLabel = (status: string | null | undefined) => statusLabel(status ?? '');

/**
 * Every referral query is keyed under ['referrals'], so a change to any referral refreshes them all: the lists, the
 * referral, a patient's card and the side nav's count.
 */
export const referralKeys = {
  all: ['referrals'] as const,
  one: (id: string) => ['referrals', id] as const,
  search: (search: ReferralSearch) => ['referrals', 'search', search] as const,
  count: ['referrals', 'awaiting-triage', 'count'] as const,
};

/** Leaves out empty filters, so they aren't sent and equal searches share a cache entry. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(
    Object.entries(search).filter(([, value]) => value !== undefined && value !== null && value !== '' && !(Array.isArray(value) && value.length === 0)),
  ) as T;
}

/** One referral, with its history (`GET api/referrals/{id}`; opening it is recorded). A missing one is an ApiError 404. */
export function referralQuery(id: string) {
  return queryOptions({
    queryKey: referralKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/referrals/{id}', {params: {path: {id}}})),
  });
}

/**
 * A page of referrals (`GET api/referrals/search`), sorted by the API: by received date, oldest first, unless the
 * search says otherwise. While the next page loads, the current one stays on screen.
 */
export function referralSearchQuery(search: ReferralSearch) {
  const query = compact(search);
  return queryOptions({
    queryKey: referralKeys.search(query),
    queryFn: () => unwrap(api.GET('/api/referrals/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

/** How many referrals await triage in the caller's specialties (every one's, if they have none): the side nav's count. */
export const awaitingTriageCountQuery = queryOptions({
  queryKey: referralKeys.count,
  queryFn: () => unwrap(api.GET('/api/referrals/awaiting-triage/count')),
});

/** The most of a patient's referrals their page lists. */
export const PATIENT_REFERRALS_LISTED = 20;

/** A patient's referrals, the most recently registered first (the patient page's card). */
export const patientReferralsQuery = (patientId: string) =>
  referralSearchQuery({PatientId: patientId, Sort: 'registered', Ascending: false, PageSize: PATIENT_REFERRALS_LISTED});

/** A patient's referrals awaiting triage, oldest first (shown while registering another, to spot a duplicate). */
export const patientOpenReferralsQuery = (patientId: string) =>
  referralSearchQuery({PatientId: patientId, Statuses: OPEN_REFERRAL_STATUSES, PageSize: PATIENT_REFERRALS_LISTED});

/**
 * Open first (oldest received first, the longest wait at the top), then the closed ones as the API gave them (most
 * recently registered first).
 */
export function openFirst(referrals: readonly ReferralSummary[]): ReferralSummary[] {
  const open = referrals.filter((r) => r.status === 'AwaitingTriage').sort((a, b) => (a.receivedOn ?? '').localeCompare(b.receivedOn ?? ''));
  return [...open, ...referrals.filter((r) => r.status !== 'AwaitingTriage')];
}

/** After any change to a referral: it (when given), every referral list and count, and the patients' timelines. */
async function refreshReferrals(queryClient: QueryClient, referral?: Referral) {
  if (referral?.id) queryClient.setQueryData(referralKeys.one(referral.id), referral);
  await Promise.all([
    queryClient.invalidateQueries({queryKey: referralKeys.all, predicate: (query) => query.queryKey[1] !== referral?.id}),
    queryClient.invalidateQueries({queryKey: pathwayKeys.timelines}),
  ]);
}

/** What staff type from the letter: the same for registering and editing. Dates are YYYY-MM-DD. */
export interface ReferralDetailsInput {
  specialtyId: string;
  receivedOn: string;
  /** The referring practice chosen from the directory, or '' for none (the practice typed from the letter only). */
  practiceId: string;
  referrerPractice: string;
  referrerClinician: string;
  referrerOdsCode: string;
  requestedPriority: string;
  reason: string;
}

/**
 * The request's details: optional fields left blank are sent as null, and the ODS code in capitals. The directory's
 * practice is its ID, or null for none (the API fills an empty practice name or ODS code from it).
 */
export function toReferralRequest(input: ReferralDetailsInput) {
  const optional = (value: string) => (value.trim() ? value.trim() : null);
  return {
    specialtyId: input.specialtyId || undefined,
    receivedOn: input.receivedOn || undefined,
    practiceId: input.practiceId || null,
    referrerPractice: input.referrerPractice.trim(),
    referrerClinician: optional(input.referrerClinician),
    referrerOdsCode: optional(input.referrerOdsCode.toUpperCase()),
    requestedPriority: input.requestedPriority,
    reason: input.reason.trim(),
  };
}

/** A referral's details, as the form starts editing them. */
export const referralInput = (referral: Referral): ReferralDetailsInput => ({
  specialtyId: referral.specialtyId ?? '',
  receivedOn: referral.receivedOn ?? '',
  practiceId: referral.practiceId ?? '',
  referrerPractice: referral.referrerPractice ?? '',
  referrerClinician: referral.referrerClinician ?? '',
  referrerOdsCode: referral.referrerOdsCode ?? '',
  requestedPriority: referral.requestedPriority ?? 'Medium',
  reason: referral.reason ?? '',
});

/** The 400's key when the patient already has an open referral to the specialty (`confirmPossibleDuplicate` unset). */
export const POSSIBLE_DUPLICATE_FIELD = 'confirmPossibleDuplicate';

/**
 * Registers a referral (`POST api/referrals`) for a patient already in MedApp, returning it. A possible duplicate (an
 * open referral to the same specialty) is a 400 on `confirmPossibleDuplicate` unless it's confirmed; a restricted or
 * anonymised record is a 409 whose title says to give the letter to an admin.
 */
export function useRegisterReferral() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({patientId, confirmPossibleDuplicate, ...input}: ReferralDetailsInput & {patientId: string; confirmPossibleDuplicate: boolean}) =>
      unwrap(api.POST('/api/referrals', {body: {patientId, confirmPossibleDuplicate, ...toReferralRequest(input)}})),
    onSuccess: (referral) => refreshReferrals(queryClient, referral),
  });
}

/**
 * Edits, withdraws or marks entered in error a referral awaiting triage, each from the version the page showed. Each
 * returns the referral, which replaces the cached one. A 409 (changed meanwhile, triaged, or the record restricted)
 * reads it again, so the page can show what's there now.
 */
export function useReferralActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = (referral: Referral) => refreshReferrals(queryClient, referral);
  const onError = (error: unknown) =>
    error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: referralKeys.one(id)}) : undefined;
  const path = {params: {path: {id}}};
  return {
    edit: useMutation({
      mutationFn: ({expectedVersion, ...input}: ReferralDetailsInput & {expectedVersion: number}) =>
        unwrap(api.PUT('/api/referrals/{id}', {...path, body: {...toReferralRequest(input), expectedVersion}})),
      onSuccess,
      onError,
    }),
    withdraw: useMutation({
      mutationFn: (body: {detail: string; expectedVersion: number}) => unwrap(api.POST('/api/referrals/{id}/withdraw', {...path, body})),
      onSuccess,
      onError,
    }),
    enteredInError: useMutation({
      mutationFn: (body: {detail: string; expectedVersion: number}) => unwrap(api.POST('/api/referrals/{id}/entered-in-error', {...path, body})),
      onSuccess,
      onError,
    }),
  };
}

/** The names of the fields a details edit changed, in words. */
const FIELD_WORDS: Record<string, string> = {
  specialty: 'specialty',
  receivedOn: 'received date',
  referrerPractice: 'practice',
  directoryPractice: 'practice from the directory',
  referrerClinician: 'clinician',
  referrerOdsCode: 'ODS code',
  requestedPriority: 'requested priority',
  reason: 'reason',
};

/** A changed field's name in words ("receivedOn" → "received date"). */
export const referralFieldWords = (field: string) => FIELD_WORDS[field] ?? field;

/**
 * One entry of a referral's history, in words: "Registered by k.ashford", "Details edited by k.ashford: reason,
 * received date", "Withdrawn by k.ashford", "Marked entered in error by k.ashford". With its specialty where it names
 * one (registered to it, or moved to it).
 */
export function referralEventWords(event: ReferralEvent): {title: string; detail: string} {
  const by = event.username || 'someone';
  switch (event.action) {
    case 'Registered':
      return {
        title: `Registered by ${by}`,
        detail: [
          event.specialtyName ? `To ${event.specialtyName}` : null,
          event.requestedPriority ? `${event.requestedPriority} priority asked for` : null,
        ]
          .filter(Boolean)
          .join(', '),
      };
    case 'DetailsEdited': {
      const fields = (event.changedFields ?? []).map(referralFieldWords);
      const moved =
        event.specialtyName && event.previousSpecialtyName ? `Specialty ${event.previousSpecialtyName} → ${event.specialtyName}` : null;
      return {title: `Details edited by ${by}${fields.length ? `: ${fields.join(', ')}` : ''}`, detail: moved ?? ''};
    }
    case 'Withdrawn':
      return {title: `Withdrawn by ${by}`, detail: ''};
    case 'EnteredInError':
      return {title: `Marked entered in error by ${by}`, detail: ''};
    default:
      return {
        title: `${statusLabel(event.action ?? 'Changed')} by ${by}`,
        detail: event.toStatus ? `Now ${referralStatusLabel(event.toStatus).toLowerCase()}` : '',
      };
  }
}
