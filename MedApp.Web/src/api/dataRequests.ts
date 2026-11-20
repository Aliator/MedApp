import {keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import type {paths} from './schema';

/**
 * Patients' data requests (subject access, UK GDPR Article 15; docs/centre-phases-3-5-plan.md, decisions 160 to 163):
 * handled only by holders of ManagePrivacy (admins): logged with the date it was received (due one calendar month on),
 * the requester's identity checked, reviewed (withholding only what meets the serious harm test, on an appropriate
 * health professional's opinion taken outside MedApp), and answered with a pack (a ZIP: the report as a PDF, the record as JSON and the files) made on request and
 * never stored. Every change carries the version the page showed (`expectedVersion`), so one made meanwhile is a 409.
 * What the caller may do is in the response's `can...` flags: pages show nothing they don't allow.
 */
export type DataRequest = Schemas['DataRequestResponse'];
export type DataRequestSummary = Schemas['DataRequestSummaryResponse'];
export type DataRequestReviewItem = Schemas['DataRequestReviewItemResponse'];
export type WithheldItemRequest = Schemas['WithheldItemRequest'];
export type DataRequestSearch = NonNullable<paths['/api/data-requests/search']['get']['parameters']['query']>;

/** The list's views: a status the API filters by, the open ones no clinician has reviewed yet, or every request. */
export const DATA_REQUEST_VIEWS = [
  {value: 'Open', label: 'Open'},
  {value: 'AwaitingReview', label: 'Awaiting review'},
  {value: 'Sent', label: 'Sent'},
  {value: 'Refused', label: 'Refused'},
  {value: 'Withdrawn', label: 'Withdrawn'},
  {value: 'All', label: 'All'},
] as const;
export type DataRequestView = (typeof DATA_REQUEST_VIEWS)[number]['value'];

/** A view in words ("AwaitingReview" → "Awaiting review"). */
export const dataRequestViewLabel = (view: DataRequestView) => DATA_REQUEST_VIEWS.find((v) => v.value === view)?.label ?? view;

/** What a view asks the API for: Awaiting review is the open ones not yet reviewed; All filters nothing. */
export function viewFilters(view: DataRequestView): Pick<DataRequestSearch, 'Status' | 'AwaitingReview'> {
  if (view === 'All') return {};
  if (view === 'AwaitingReview') return {AwaitingReview: true};
  return {Status: view};
}

/** Where a request stands, in words. */
export const dataRequestStatusWords = (status: string | null | undefined) =>
  ({Open: 'Open', Sent: 'Sent', Refused: 'Refused', Withdrawn: 'Withdrawn'})[status ?? ''] ?? status ?? '';

/** How the requester's identity was checked, by the names the API takes, with their words. */
export const IDENTITY_CHECK_METHODS = [
  {value: 'InPerson', label: 'In person'},
  {value: 'Documents', label: 'Documents'},
  {value: 'KnownToService', label: 'Known to the service'},
  {value: 'Other', label: 'Other'},
] as const;

export const identityCheckMethodWords = (method: string | null | undefined) =>
  IDENTITY_CHECK_METHODS.find((m) => m.value === method)?.label ?? method ?? '';

/** How the response was sent, by the names the API takes, with their words. */
export const SEND_METHODS = [
  {value: 'Post', label: 'Post'},
  {value: 'HandedOver', label: 'Handed over'},
  {value: 'SecureEmail', label: 'Secure email'},
  {value: 'Email', label: 'Email'},
  {value: 'Other', label: 'Other'},
] as const;

export const sendMethodWords = (method: string | null | undefined) => SEND_METHODS.find((m) => m.value === method)?.label ?? method ?? '';

/** What an item of the record is, in words, by the kinds the API names. */
const ITEM_KINDS: Record<string, string> = {
  Note: 'Note',
  File: 'File',
  Letter: 'Letter',
  Result: 'Result',
  MdtItem: 'MDT item',
  Referral: 'Referral',
};

export const reviewItemKindWords = (kind: string | null | undefined) => ITEM_KINDS[kind ?? ''] ?? kind ?? '';

/** The longest a reason (an extension's, a refusal's, a withheld item's) or an identity check's detail can be. */
export const MAX_DATA_REQUEST_REASON = 500;
/** The longest a requester's name can be. */
export const MAX_REQUESTER_NAME = 200;
/** The longest a requester's relationship to the patient can be. */
export const MAX_REQUESTER_RELATIONSHIP = 100;

/** When an item may be withheld, in plain words: the serious harm test, or information about a third party. */
export const SERIOUS_HARM_TEST =
  'Withhold an item only where providing it would be likely to cause serious harm to the physical or mental health of the patient or another person, or where it would identify someone else (not a health professional caring for the patient) who hasn’t agreed, and sharing it without their agreement isn’t reasonable.';

/** Every data request query is under ['data-requests'], so one change refreshes them all. */
export const dataRequestKeys = {
  all: ['data-requests'] as const,
  one: (id: string) => ['data-requests', id] as const,
  reviewItems: (id: string) => ['data-requests', id, 'review-items'] as const,
  search: (search: DataRequestSearch) => ['data-requests', 'search', search] as const,
  count: ['data-requests', 'count'] as const,
};

/** Leaves out empty filters, so they aren't sent and equal searches share a cache entry. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(Object.entries(search).filter(([, value]) => value !== undefined && value !== null && value !== '')) as T;
}

/**
 * A page of data requests (`GET api/data-requests/search`), open ones first by due date, then the rest, newest
 * received first. While the next page loads, the current one stays on screen.
 */
export function dataRequestSearchQuery(search: DataRequestSearch) {
  const query = compact(search);
  return queryOptions({
    queryKey: dataRequestKeys.search(query),
    queryFn: () => unwrap(api.GET('/api/data-requests/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

/** One data request (`GET api/data-requests/{id}`), with what the caller may do. A missing one is a 404. */
export function dataRequestQuery(id: string) {
  return queryOptions({
    queryKey: dataRequestKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/data-requests/{id}', {params: {path: {id}}})),
  });
}

/**
 * The items of the record a clinician may withhold, with their text (`GET api/data-requests/{id}/review-items`), for
 * the review. Read only while the review is open, and never kept once it's closed.
 */
export function dataRequestReviewItemsQuery(id: string) {
  return queryOptions({
    queryKey: dataRequestKeys.reviewItems(id),
    queryFn: () => unwrap(api.GET('/api/data-requests/{id}/review-items', {params: {path: {id}}})),
    gcTime: 0,
  });
}

/** The home page's counts of open data requests and those due within 7 days or overdue (`GET api/data-requests/count`): names no patients, records nothing. */
export const dataRequestCountQuery = queryOptions({
  queryKey: dataRequestKeys.count,
  queryFn: () => unwrap(api.GET('/api/data-requests/count')),
});

/** Where a data request's page is. */
export const dataRequestHref = (id: string) => `/data-requests/${id}`;

/** After a change: the request is cached as the API returned it, and every list is read again. */
async function refreshDataRequests(queryClient: QueryClient, request?: DataRequest) {
  if (request?.id) queryClient.setQueryData(dataRequestKeys.one(request.id), request);
  await queryClient.invalidateQueries({queryKey: dataRequestKeys.all, predicate: (query) => query.queryKey[1] !== request?.id});
}

/** What logging a request sends. */
export interface CreateDataRequestInput {
  patientId: string;
  receivedOn: string;
  requestedByPatient: boolean;
  requesterName?: string | null;
  requesterRelationship?: string | null;
}

/** Logs a data request as received (`POST api/data-requests`), returning it. */
export function useCreateDataRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateDataRequestInput) => unwrap(api.POST('/api/data-requests', {body})),
    onSuccess: (request) => refreshDataRequests(queryClient, request),
  });
}

/**
 * The changes to one data request, each sending the version the page showed: the identity check, an extension,
 * recording it sent, refusing it, withdrawing it, and the clinician review. A 409 reads the request again.
 */
export function useDataRequestActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = (request: DataRequest) => refreshDataRequests(queryClient, request);
  const onError = (error: unknown) =>
    error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: dataRequestKeys.one(id)}) : undefined;
  const path = {params: {path: {id}}};
  return {
    identityCheck: useMutation({
      mutationFn: (body: {checkedOn: string; method: string; detail: string | null; expectedVersion: number}) =>
        unwrap(api.POST('/api/data-requests/{id}/identity-check', {...path, body})),
      onSuccess,
      onError,
    }),
    extend: useMutation({
      mutationFn: (body: {months: number; reason: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/data-requests/{id}/extension', {...path, body})),
      onSuccess,
      onError,
    }),
    sent: useMutation({
      mutationFn: (body: {sentOn: string; method: string; expectedVersion: number}) => unwrap(api.POST('/api/data-requests/{id}/sent', {...path, body})),
      onSuccess,
      onError,
    }),
    refuse: useMutation({
      mutationFn: (body: {reason: string; expectedVersion: number}) => unwrap(api.POST('/api/data-requests/{id}/refuse', {...path, body})),
      onSuccess,
      onError,
    }),
    withdraw: useMutation({
      mutationFn: (body: {expectedVersion: number}) => unwrap(api.POST('/api/data-requests/{id}/withdraw', {...path, body})),
      onSuccess,
      onError,
    }),
    review: useMutation({
      mutationFn: (body: {withheld: WithheldItemRequest[]; expectedVersion: number}) => unwrap(api.PUT('/api/data-requests/{id}/review', {...path, body})),
      onSuccess: async (request: DataRequest) => {
        queryClient.removeQueries({queryKey: dataRequestKeys.reviewItems(id)});
        await onSuccess(request);
      },
      onError,
    }),
  };
}

/** The pack's file name when the API names none. */
export const dataRequestPackFile = (id: string) => `data-request-${id}.zip`;

/** The file name in a `Content-Disposition` header (`filename*` first, then `filename`), or null. */
export function attachmentFileName(header: string | null): string | null {
  if (!header) return null;
  const encoded = /filename\*\s*=\s*(?:UTF-8'')?([^;]+)/i.exec(header)?.[1];
  if (encoded) {
    try {
      return decodeURIComponent(encoded.trim().replace(/^"|"$/g, ''));
    } catch {
      // Falls back to the plain name.
    }
  }
  const plain = /filename\s*=\s*("?)([^";]+)\1/i.exec(header)?.[2];
  return plain?.trim() || null;
}

/**
 * Asks the API to make the pack now (`GET api/data-requests/{id}/pack`, a ZIP it never stores, recorded as downloaded),
 * then saves it in the browser through a link to it in memory. A refusal (a 409: the identity check or the review is
 * missing) is an ApiError in the API's words.
 */
export async function downloadDataRequestPack(id: string) {
  const result = await api.GET('/api/data-requests/{id}/pack', {params: {path: {id}}, parseAs: 'blob'});
  const zip = await unwrap(Promise.resolve(result));
  const url = URL.createObjectURL(zip as Blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = attachmentFileName(result.response.headers.get('Content-Disposition')) ?? dataRequestPackFile(id);
  link.click();
  URL.revokeObjectURL(url);
}

/** Downloads the pack, as a mutation so the page can show it's working. */
export const useDataRequestPack = () => useMutation({mutationFn: downloadDataRequestPack});

/**
 * What to say when the pack couldn't be made: the API's reason for a refusal (a 409, a missing permission, a request
 * gone, or a 501 where it isn't offered, as in the light demo), otherwise a plain apology.
 */
export function packProblem(error: unknown): string {
  if (error instanceof ApiError && error.title && [403, 404, 409, 501].includes(error.status)) return error.title;
  return 'The pack could not be made. Please try again.';
}
