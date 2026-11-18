import {keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {formatShortDate} from '../utils/dates';
import {statusLabel} from '../utils/statusWords';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {pathwayKeys, pathwayQuery, type Pathway} from './pathways';
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
/** A referral's waiting-time clock (decisions 27 to 38): whole UK days, against the specialty's target for the priority. */
export type WaitingClock = Schemas['WaitingClockResponse'];
/** One pause of a waiting-time clock: why, its review date, and who paused and resumed it. */
export type ClockPause = Schemas['ClockPauseResponse'];
/** The referral a pathway was accepted from, with its clock and pauses (the pathway page's Referral and wait card). */
export type PathwayReferral = Schemas['PathwayReferralResponse'];

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

/**
 * Why a referral is rejected: the API's fixed choices (stored by number there; sent and read by name), in the order
 * the API declares them. Asking the referrer for more information is a rejection with Insufficient information for now.
 */
export const REFERRAL_REJECTION_REASONS = [
  {value: 'NotOfferedHere', label: 'Not offered here'},
  {value: 'DoesNotMeetCriteria', label: 'Does not meet the criteria'},
  {value: 'InsufficientInformation', label: 'Insufficient information'},
  {value: 'Duplicate', label: 'Duplicate'},
  {value: 'Other', label: 'Other'},
] as const;
export type ReferralRejectionReason = (typeof REFERRAL_REJECTION_REASONS)[number]['value'];

/** A rejection reason in words ("DoesNotMeetCriteria" → "Does not meet the criteria"); anything unknown as it came. */
export const rejectionReasonWords = (reason: string | null | undefined) =>
  REFERRAL_REJECTION_REASONS.find((r) => r.value === reason)?.label ?? reason ?? '';

/** Whether a rejection needs a detail: only Other does. */
export const rejectionNeedsDetail = (reason: string | null | undefined) => reason === 'Other';

/** The longest a triage decision's detail can be (rejecting or redirecting), as the API allows. */
export const MAX_TRIAGE_DETAIL = 500;
/** The longest "where else" a referral redirected elsewhere can name. */
export const MAX_REDIRECTED_TO = 200;

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

/** What a triager chose when accepting a referral: nothing here is filled in for them (hazard H-41). */
export interface AcceptReferralInput {
  templateId: string;
  /**
   * Low, Medium, High or Urgent, as the triager chose it (never the referrer's, unless they chose the same). The
   * pathway's target date comes from the waiting-time clock, for this priority's target in the specialty.
   */
  priority: string;
  /** Blank takes the template's name. */
  title: string;
  /** Someone to assign (holders of AssignPathways only), or blank for nobody. */
  assigneeUserId: string;
  /** The pathway's first note (Markdown), or blank for none. */
  notes: string;
  expectedVersion: number;
}

/** Rejecting a referral: a fixed reason, and a detail (required for Other). */
export interface RejectReferralInput {
  reason: string;
  detail: string;
  expectedVersion: number;
}

/** Redirecting a referral: to another specialty here, or elsewhere (another provider), with why. */
export type RedirectReferralInput = ({specialtyId: string; elsewhere?: undefined} | {elsewhere: string; specialtyId?: undefined}) & {
  detail: string;
  expectedVersion: number;
};

const blankAsNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null);

/** A 409 (triaged by someone else, changed meanwhile, the record restricted) reads the referral again. */
function useRereadOnConflict(id: string) {
  const queryClient = useQueryClient();
  return (error: unknown) =>
    error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: referralKeys.one(id)}) : undefined;
}

/**
 * Accepts a referral into a new pathway (`POST api/referrals/{id}/accept`, TriageReferrals and the referral's
 * specialty), from the version the page showed, returning the new pathway. The referral, every referral list and
 * count, the timelines and the pathway lists then refresh; the new pathway is cached, so its page opens at once.
 */
export function useAcceptReferral(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AcceptReferralInput) =>
      unwrap(
        api.POST('/api/referrals/{id}/accept', {
          params: {path: {id}},
          body: {
            templateId: input.templateId,
            priority: input.priority,
            title: blankAsNull(input.title),
            assigneeUserId: input.assigneeUserId || null,
            notes: blankAsNull(input.notes),
            expectedVersion: input.expectedVersion,
          },
        }),
      ),
    onSuccess: async (pathway: Pathway) => {
      if (pathway.id) queryClient.setQueryData(pathwayQuery(pathway.id).queryKey, pathway);
      await Promise.all([
        refreshReferrals(queryClient),
        queryClient.invalidateQueries({queryKey: ['pathways', 'search']}),
        queryClient.invalidateQueries({queryKey: pathwayKeys.countForPatient(pathway.patientId ?? '')}),
      ]);
    },
    onError: useRereadOnConflict(id),
  });
}

/** Rejects a referral with a reason (`POST api/referrals/{id}/reject`), from the version the page showed. */
export function useRejectReferral(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({reason, detail, expectedVersion}: RejectReferralInput) =>
      unwrap(api.POST('/api/referrals/{id}/reject', {params: {path: {id}}, body: {reason, detail: blankAsNull(detail), expectedVersion}})),
    onSuccess: (referral) => refreshReferrals(queryClient, referral),
    onError: useRereadOnConflict(id),
  });
}

/**
 * Redirects a referral (`POST api/referrals/{id}/redirect`), from the version the page showed: to another specialty
 * here (it stays awaiting triage, in that specialty's inbox) or elsewhere (it's closed as redirected elsewhere).
 */
export function useRedirectReferral(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({specialtyId, elsewhere, detail, expectedVersion}: RedirectReferralInput) =>
      unwrap(
        api.POST('/api/referrals/{id}/redirect', {
          params: {path: {id}},
          body: {specialtyId: specialtyId || null, elsewhere: blankAsNull(elsewhere), detail: detail.trim(), expectedVersion},
        }),
      ),
    onSuccess: (referral) => refreshReferrals(queryClient, referral),
    onError: useRereadOnConflict(id),
  });
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
 * received date", "Withdrawn by k.ashford", "Marked entered in error by k.ashford"; triage's "Accepted by d.clinician"
 * (the template and the priority chosen), "Rejected by d.clinician" (the reason in words), "Redirected by d.clinician"
 * (to another specialty here) and "Redirected elsewhere by d.clinician". With its specialty where it names one
 * (registered to it, or moved to it). Never free text: the history holds none.
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
    case 'SpecialtyChanged':
      return {
        title: `Redirected by ${by}`,
        detail: event.specialtyName ? `Specialty ${event.previousSpecialtyName ?? 'unknown'} → ${event.specialtyName}` : '',
      };
    case 'Accepted':
      return {
        title: `Accepted by ${by}`,
        detail: [event.templateName, event.priority ? `${event.priority} priority` : null].filter(Boolean).join(', '),
      };
    case 'Rejected':
      return {title: `Rejected by ${by}`, detail: rejectionReasonWords(event.rejectionReason)};
    case 'RedirectedElsewhere':
      return {title: `Redirected elsewhere by ${by}`, detail: ''};
    case 'Withdrawn':
      return {title: `Withdrawn by ${by}`, detail: ''};
    case 'EnteredInError':
      return {title: `Marked entered in error by ${by}`, detail: ''};
    case 'ClockPaused':
      return {
        title: `Clock paused by ${by}`,
        detail: [pauseReasonWords(event.pauseReason) || null, event.reviewOn ? `review ${formatShortDate(event.reviewOn)}` : null]
          .filter(Boolean)
          .join(', '),
      };
    case 'ClockResumed':
      return {title: `Clock resumed by ${by}`, detail: ''};
    case 'ClockStopped':
      return {
        title: 'Clock stopped',
        detail: [
          stopReasonWords(event.stopReason) || null,
          event.daysWaited != null ? `day ${event.daysWaited}${event.targetDays != null ? ` of ${event.targetDays}` : ''}` : null,
          event.breached ? 'breached' : null,
        ]
          .filter(Boolean)
          .join(', '),
      };
    case 'TargetChanged':
      return {
        title: 'Target changed',
        detail:
          event.targetDays != null
            ? `${event.previousTargetDays != null ? `${event.previousTargetDays} → ` : ''}${event.targetDays} days${event.priority ? ` (${event.priority} priority)` : ''}`
            : '',
      };
    default:
      return {
        title: `${statusLabel(event.action ?? 'Changed')} by ${by}`,
        detail: event.toStatus ? `Now ${referralStatusLabel(event.toStatus).toLowerCase()}` : '',
      };
  }
}

/**
 * Why a waiting-time clock is paused: the API's fixed choices (stored by number there; sent and read by name). Only
 * delays the patient chose or caused pause it (decision 33): the centre's own waits always count.
 */
export const CLOCK_PAUSE_REASONS = [
  {value: 'PatientChoseToWait', label: 'Patient chose to wait'},
  {value: 'PatientUnavailable', label: 'Patient unavailable'},
  {value: 'Other', label: 'Other'},
] as const;

/** A pause reason in words ("PatientChoseToWait" → "Patient chose to wait"); anything unknown as it came. */
export const pauseReasonWords = (reason: string | null | undefined) => CLOCK_PAUSE_REASONS.find((r) => r.value === reason)?.label ?? reason ?? '';

/** Whether a pause needs a detail: only Other does. */
export const pauseNeedsDetail = (reason: string | null | undefined) => reason === 'Other';

/** The longest a pause's detail can be. */
export const MAX_PAUSE_DETAIL = 500;
/** How far ahead a pause's review date can be, in days (from tomorrow). */
export const MAX_PAUSE_REVIEW_DAYS = 90;

const STOP_REASON_WORDS: Record<string, string> = {
  StopStageFinished: 'Clock-stop stage finished',
  PathwayDischarged: 'Discharged',
  PathwayCancelled: 'Pathway cancelled',
  Rejected: 'Rejected',
  RedirectedElsewhere: 'Redirected elsewhere',
  Withdrawn: 'Withdrawn',
  EnteredInError: 'Entered in error',
};

/** Why a clock stopped, in words ("StopStageFinished" → "Clock-stop stage finished"); anything unknown as it came. */
export const stopReasonWords = (reason: string | null | undefined) => (reason ? (STOP_REASON_WORDS[reason] ?? statusLabel(reason)) : '');

/** How long a referral has waited against its target: "Day 84 of 126". */
export const waitWords = (clock: WaitingClock) => `Day ${clock.daysWaited ?? 0} of ${clock.targetDays ?? 0}`;

/**
 * After pausing or resuming a clock: the pathway's referral is replaced with the one returned, then the pathway (its
 * target date moved), the referrals, the pathway lists and the timelines are read again.
 */
async function refreshClock(queryClient: QueryClient, pathwayId: string, referral?: PathwayReferral) {
  if (referral) queryClient.setQueryData(pathwayQuery(pathwayId).queryKey, (cached) => (cached ? {...cached, referral} : cached));
  await Promise.all([
    queryClient.invalidateQueries({queryKey: pathwayQuery(pathwayId).queryKey}),
    queryClient.invalidateQueries({queryKey: ['pathways', 'search']}),
    queryClient.invalidateQueries({queryKey: referralKeys.all}),
    queryClient.invalidateQueries({queryKey: pathwayKeys.timelines}),
  ]);
}

/** Pausing a clock: one of the reasons, a detail (required for Other) and a review date, `YYYY-MM-DD`. */
export interface PauseClockInput {
  reason: string;
  detail: string;
  reviewOn: string;
  expectedVersion: number;
}

/**
 * Pausing and resuming the waiting-time clock of the referral a pathway was accepted from (`POST
 * api/referrals/{id}/clock/pause` and `…/resume`, WorkPathways), each from the referral's version the page showed.
 * Each returns the pathway's Referral and wait card, which replaces the cached one; the pathway is then read again,
 * as its target date moved. A 409 (changed meanwhile, already paused or resumed) reads the pathway again too.
 */
export function useClockActions(referralId: string, pathwayId: string) {
  const queryClient = useQueryClient();
  const onSuccess = (referral: PathwayReferral) => refreshClock(queryClient, pathwayId, referral);
  const onError = (error: unknown) => (error instanceof ApiError && error.status === 409 ? refreshClock(queryClient, pathwayId) : undefined);
  const path = {params: {path: {id: referralId}}};
  return {
    pause: useMutation({
      mutationFn: ({reason, detail, reviewOn, expectedVersion}: PauseClockInput) =>
        unwrap(api.POST('/api/referrals/{id}/clock/pause', {...path, body: {reason, detail: blankAsNull(detail), reviewOn, expectedVersion}})),
      onSuccess,
      onError,
    }),
    resume: useMutation({
      mutationFn: (body: {expectedVersion: number}) => unwrap(api.POST('/api/referrals/{id}/clock/resume', {...path, body})),
      onSuccess,
      onError,
    }),
  };
}
