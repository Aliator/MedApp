import {keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {refreshAfterAttachmentChange} from './attachments';
import {pathwayKeys} from './pathways';
import {specimenKeys} from './specimens';
import type {paths} from './schema';

/**
 * Results (docs/centre-phases-3-5-plan.md, section 2.3, decisions 91 to 97): a report that came back, either a
 * specimen's report attached by the lab or an attached file marked as a result from elsewhere, awaiting sign-off by a
 * named responsible clinician (a holder of SignOffResults). They mark it seen, then sign it off, recording what was
 * done, chosen by them from a fixed list with none pre-selected. **MedApp never reads, interprets or flags a report**:
 * lists are ordered by dates only. Every change carries the version the page showed (`expectedVersion`), so a change
 * made meanwhile is a 409. What the caller may do is in the response's `can...` flags.
 */
export type Result = Schemas['ResultResponse'];
export type ResultSummary = Schemas['ResultSummaryResponse'];
export type ResultSearch = NonNullable<paths['/api/results/search']['get']['parameters']['query']>;

/** The statuses, by the names the API uses. AwaitingSignOff and Seen both await sign-off. */
export const RESULT_STATUSES = ['AwaitingSignOff', 'Seen', 'SignedOff', 'EnteredInError'] as const;
export type ResultStatus = (typeof RESULT_STATUSES)[number];

const STATUS_LABELS: Record<ResultStatus, string> = {
  AwaitingSignOff: 'Awaiting sign-off',
  Seen: 'Seen',
  SignedOff: 'Signed off',
  EnteredInError: 'Entered in error',
};

/** A status in words: "AwaitingSignOff" → "Awaiting sign-off". The name itself for one this copy doesn't know. */
export const resultStatusLabel = (status: string | null | undefined) => STATUS_LABELS[status as ResultStatus] ?? status ?? '';

/**
 * What the clinician did, by the names the API takes, in the fixed order the sign-off offers them (none pre-selected:
 * the clinician chooses, MedApp never suggests one). Other needs a detail.
 */
export const RESULT_ACTIONS = [
  {value: 'NoFurtherAction', label: 'No further action'},
  {value: 'PatientInformed', label: 'Patient informed'},
  {value: 'AppointmentArranged', label: 'Appointment arranged'},
  {value: 'ForMdtDiscussion', label: 'To be discussed at MDT'},
  {value: 'ReferredOn', label: 'Referred on'},
  {value: 'Other', label: 'Other'},
] as const;
export type ResultAction = (typeof RESULT_ACTIONS)[number]['value'];

/** An action in words: "ForMdtDiscussion" → "To be discussed at MDT". */
export const resultActionLabel = (action: string | null | undefined) => RESULT_ACTIONS.find((a) => a.value === action)?.label ?? action ?? '';

/** Where a result came from, in words: a specimen's report, or a report from elsewhere attached as a file. */
export const resultSourceLabel = (source: string | null | undefined) =>
  source === 'Specimen' ? 'Specimen report' : source === 'ExternalReport' ? 'External report' : (source ?? '');

/**
 * The Results list's views (`GET api/results/search?view=`), in the order of its tabs. The awaiting views and Needs a
 * clinician are oldest report first; Signed off newest signed off first. The API's `All` (newest report first) is for a
 * pathway's or patient's results.
 */
export const RESULT_VIEWS = [
  {value: 'AwaitingMine', label: 'Awaiting my sign-off'},
  {value: 'Awaiting', label: 'Awaiting sign-off (everyone)'},
  {value: 'NeedsClinician', label: 'Needs a clinician'},
  {value: 'SignedOff', label: 'Signed off'},
] as const;
export type ResultView = (typeof RESULT_VIEWS)[number]['value'];

/** A view in words, for its tab. */
export const resultViewLabel = (view: ResultView) => RESULT_VIEWS.find((v) => v.value === view)?.label ?? view;

/** The longest a title can be (the API's 200), a sign-off's detail (1,000) and an entry in error's detail (500). */
export const MAX_RESULT_TITLE = 200;
export const MAX_ACTION_DETAIL = 1000;
export const MAX_ERROR_DETAIL = 500;

/** Every result query is under ['results'], so one change refreshes the result, every list and the count. */
export const resultKeys = {
  all: ['results'] as const,
  one: (id: string) => ['results', id] as const,
  search: (search: ResultSearch) => ['results', 'search', search] as const,
  count: ['results', 'count'] as const,
};

/** Leaves out empty filters, so they aren't sent and equal searches share a cache entry. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(Object.entries(search).filter(([, value]) => value !== undefined && value !== null && value !== '')) as T;
}

/** One result (`GET api/results/{id}`). Opening it is recorded as viewing it. A missing one is an ApiError 404. */
export function resultQuery(id: string) {
  return queryOptions({
    queryKey: resultKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/results/{id}', {params: {path: {id}}})),
  });
}

/**
 * A page of results (`GET api/results/search`), in the API's order (dates only). Each patient shown is recorded as
 * listed. While the next page loads, the current one stays on screen.
 */
export function resultSearchQuery(search: ResultSearch) {
  const query = compact(search);
  return queryOptions({
    queryKey: resultKeys.search(query),
    queryFn: () => unwrap(api.GET('/api/results/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

/** The side nav's count: results awaiting the caller's sign-off (a count, naming no patients; it records nothing). */
export const resultCountQuery = queryOptions({
  queryKey: resultKeys.count,
  queryFn: () => unwrap(api.GET('/api/results/count')),
});

/** The most of a pathway's results its page lists (the API's largest page). */
export const PATHWAY_RESULTS_LISTED = 100;

/** Every result of a pathway, newest report first (the pathway page's Results). */
export const pathwayResultsQuery = (pathwayId: string) => resultSearchQuery({View: 'All', PathwayId: pathwayId, PageSize: PATHWAY_RESULTS_LISTED});

/** Where a result's page is. */
export const resultHref = (id: string) => `/results/${id}`;

/**
 * After any change to a result: it (when given, it replaces the cached one), every result list and the count, the
 * timelines (each change is an entry) and its pathway (a stage may wait for its results).
 */
export async function refreshResults(queryClient: QueryClient, result?: Result) {
  if (result?.id) queryClient.setQueryData(resultKeys.one(result.id), result);
  await Promise.all([
    queryClient.invalidateQueries({queryKey: resultKeys.all, predicate: (query) => query.queryKey[1] !== result?.id}),
    queryClient.invalidateQueries({queryKey: pathwayKeys.timelines}),
    result?.pathwayId ? queryClient.invalidateQueries({queryKey: ['pathways', result.pathwayId]}) : undefined,
  ]);
}

/** Marking an attached file as a result from elsewhere: what it is, and who must sign it off. */
export interface RecordExternalResultInput {
  attachmentId: string;
  title: string;
  /** The pathway it belongs to; left out, the file's own (if it has one). */
  pathwayId?: string | null;
  responsibleUserId: string;
}

/**
 * Marks an attached file as a result from elsewhere (`POST api/results/external`, holders of WorkPathways), returning
 * the new result (201). The API's 400 names `title` or `responsibleUserId`; a file that can't be one (deleted, already
 * a result, a restricted record) is a 409 in its words.
 */
export function useRecordExternalResult() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({attachmentId, title, pathwayId, responsibleUserId}: RecordExternalResultInput) =>
      unwrap(api.POST('/api/results/external', {body: {attachmentId, title: title.trim(), pathwayId: pathwayId || null, responsibleUserId}})),
    onSuccess: (result) => refreshResults(queryClient, result),
  });
}

/**
 * Everything that changes a result, each from the version the page showed (`expectedVersion`) and each returning the
 * result, which replaces the cached one: marking it seen, signing it off (an action, and a detail required for Other),
 * reassigning it (the caller's own ID to take it over) and marking it entered in error (what was wrong: its file is
 * deleted, and a specimen's goes back to the lab to be reported again, so the files and specimens are read again too).
 * A 409 (changed meanwhile, or the record restricted) reads it again, so the page can show what's there now.
 */
export function useResultActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = (result: Result) => refreshResults(queryClient, result);
  const onError = (error: unknown) =>
    error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: resultKeys.one(id)}) : undefined;
  const path = {params: {path: {id}}};
  return {
    seen: useMutation({
      mutationFn: ({expectedVersion}: {expectedVersion: number}) => unwrap(api.POST('/api/results/{id}/seen', {...path, body: {expectedVersion}})),
      onSuccess,
      onError,
    }),
    signOff: useMutation({
      mutationFn: ({action, detail, expectedVersion}: {action: string; detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/results/{id}/sign-off', {...path, body: {action, detail: detail.trim() || null, expectedVersion}})),
      onSuccess,
      onError,
    }),
    reassign: useMutation({
      mutationFn: ({responsibleUserId, expectedVersion}: {responsibleUserId: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/results/{id}/reassign', {...path, body: {responsibleUserId, expectedVersion}})),
      onSuccess,
      onError,
    }),
    enteredInError: useMutation({
      mutationFn: ({detail, expectedVersion}: {detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/results/{id}/entered-in-error', {...path, body: {detail: detail.trim(), expectedVersion}})),
      onSuccess: async (result: Result) => {
        await Promise.all([
          onSuccess(result),
          refreshAfterAttachmentChange(queryClient),
          queryClient.invalidateQueries({queryKey: specimenKeys.all}),
        ]);
      },
      onError,
    }),
  };
}
