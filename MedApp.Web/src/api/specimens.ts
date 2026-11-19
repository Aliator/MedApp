import {keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {ApiError} from './errors';
import {api, unwrap, type Schemas} from './client';
import {pathwayKeys} from './pathways';
import {refreshAfterAttachmentChange, sendFile} from './attachments';
import type {paths} from './schema';

/**
 * Specimens (docs/centre-phases-3-5-plan.md, decisions 81 to 90): something taken from a patient at a pathway's stage
 * and sent to a lab, tracked there and back by MedApp's own number (`MA26-000123`) until the lab's report, a file, is
 * attached. Statuses only move forward (Taken, Sent, Received, Reported), or close early (Rejected by the lab, Lost,
 * Entered in error). Every change carries the version the page showed (`expectedVersion`), so a change made meanwhile
 * is a 409. Holders of RecordSpecimens take, send and edit them; holders of ProcessSpecimens receive, reject and report
 * them; either marks one lost. MedApp tracks specimens; it never reads or interprets a report.
 */
export type Specimen = Schemas['SpecimenResponse'];
export type SpecimenSummary = Schemas['SpecimenSummaryResponse'];
export type SpecimenType = Schemas['SpecimenTypeResponse'];
export type SpecimenSearch = NonNullable<paths['/api/specimens/search']['get']['parameters']['query']>;
export type SpecimenWorklistSearch = Schemas['SpecimenWorklistRequest'];

/** The statuses, by the names the API uses. Taken, Sent and Received are open: still to come back from the lab. */
export const SPECIMEN_STATUSES = ['Taken', 'Sent', 'Received', 'Reported', 'RejectedByLab', 'Lost', 'EnteredInError'] as const;
export type SpecimenStatus = (typeof SPECIMEN_STATUSES)[number];

const STATUS_LABELS: Record<SpecimenStatus, string> = {
  Taken: 'Taken',
  Sent: 'Sent',
  Received: 'Received',
  Reported: 'Reported',
  RejectedByLab: 'Rejected by the lab',
  Lost: 'Lost',
  EnteredInError: 'Entered in error',
};

/** A status in words: "RejectedByLab" → "Rejected by the lab". The name itself for one this copy doesn't know. */
export const specimenStatusLabel = (status: string | null | undefined) => STATUS_LABELS[status as SpecimenStatus] ?? status ?? '';

/** Why the lab rejected a specimen, by the names the API takes, in the order the dialog offers them. */
export const REJECTION_REASONS = ['Unlabelled', 'Mislabelled', 'Leaked', 'InsufficientSample', 'WrongContainer', 'Other'] as const;
export type RejectionReason = (typeof REJECTION_REASONS)[number];

const REASON_LABELS: Record<RejectionReason, string> = {
  Unlabelled: 'Unlabelled',
  Mislabelled: 'Mislabelled',
  Leaked: 'Leaked',
  InsufficientSample: 'Insufficient sample',
  WrongContainer: 'Wrong container',
  Other: 'Other',
};

/** A rejection reason in words: "InsufficientSample" → "Insufficient sample". */
export const rejectionReasonLabel = (reason: string | null | undefined) => REASON_LABELS[reason as RejectionReason] ?? reason ?? '';

/** The lab's worklist views (`GET api/specimens/search?view=`), oldest first; All is newest taken first. */
export const SPECIMEN_VIEWS = ['ToSend', 'Expected', 'InLab', 'Overdue', 'RejectedAndLost', 'Reported'] as const;
export type SpecimenView = (typeof SPECIMEN_VIEWS)[number];

const VIEW_LABELS: Record<SpecimenView, string> = {
  ToSend: 'To send',
  Expected: 'Expected',
  InLab: 'In the lab',
  Overdue: 'Overdue',
  RejectedAndLost: 'Rejected and lost',
  Reported: 'Reported',
};

/** A view in words, for its tab: "InLab" → "In the lab". */
export const specimenViewLabel = (view: SpecimenView) => VIEW_LABELS[view];

/** The longest a site can be (the API's 200), request notes (1,000), a reference (50) and a closing detail (500). */
export const MAX_SITE = 200;
export const MAX_REQUEST_NOTES = 1000;
export const MAX_REFERENCE = 50;
export const MAX_CLOSE_DETAIL = 500;

/** How far back a step's time may be typed (the API's `SpecimenWorkflow.MaxBackdating`). */
export const MAX_BACKDATING_DAYS = 7;

/**
 * Every specimen query is keyed under ['specimens'], so one change refreshes the specimen, its pathway's list and every
 * lab view. Setup lists are under ['specimen-setup'].
 */
export const specimenKeys = {
  all: ['specimens'] as const,
  one: (id: string) => ['specimens', id] as const,
  search: (search: SpecimenSearch) => ['specimens', 'search', search] as const,
};

export const specimenSetupKeys = {
  all: ['specimen-setup'] as const,
  types: (includeArchived: boolean) => ['specimen-setup', 'types', includeArchived] as const,
};

/** Leaves out empty filters, so they aren't sent and equal searches share a cache entry. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(Object.entries(search).filter(([, value]) => value !== undefined && value !== null && value !== '')) as T;
}

/**
 * One specimen, with its label's QR code (`GET api/specimens/{id}`). Opening it, or printing its label or request
 * sheet, is recorded as viewing it. A missing one is an ApiError 404.
 */
export function specimenQuery(id: string) {
  return queryOptions({
    queryKey: specimenKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/specimens/{id}', {params: {path: {id}}})),
  });
}

/**
 * A page of specimens (`GET api/specimens/search`): a lab worklist view (oldest first), or all of a pathway's or
 * patient's (newest taken first). Each patient shown is recorded as listed. While the next page loads, the current one
 * stays on screen.
 */
export function specimenSearchQuery(search: SpecimenSearch) {
  const query = compact(search);
  return queryOptions({
    queryKey: specimenKeys.search(query),
    queryFn: () => unwrap(api.GET('/api/specimens/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

/**
 * The Lab page's worklist (`POST api/specimens/search`): a view, searched (the specimen's number, the patient's name or
 * NHS number, the type) and sorted by a column. A POST, so the search, which may be a specimen number, is in the body
 * and never in a URL. Each patient shown is recorded as listed.
 */
export function specimenWorklistQuery(search: SpecimenWorklistSearch) {
  const body = compact(search);
  return queryOptions({
    queryKey: [...specimenKeys.all, 'worklist', body] as const,
    queryFn: () => unwrap(api.POST('/api/specimens/search', {body})),
    placeholderData: keepPreviousData,
  });
}

/** The most of a pathway's specimens its page lists (the API's largest page). */
export const PATHWAY_SPECIMENS_LISTED = 100;

/** Every specimen a pathway's stages took, newest first (the pathway page's Specimens). */
export const pathwaySpecimensQuery = (pathwayId: string) =>
  specimenSearchQuery({View: 'All', PathwayId: pathwayId, PageSize: PATHWAY_SPECIMENS_LISTED});

/** The specimen types by name; archived ones only with `includeArchived` (the setup page). */
export function specimenTypesQuery(includeArchived = false) {
  return queryOptions({
    queryKey: specimenSetupKeys.types(includeArchived),
    queryFn: () => unwrap(api.GET('/api/specimen-types', {params: {query: {includeArchived}}})),
  });
}

/**
 * After any change to a specimen: it (when given, it replaces the cached one), every specimen list and lab view, and
 * the timelines (each change is an entry).
 */
export async function refreshSpecimens(queryClient: QueryClient, specimen?: Specimen) {
  if (specimen?.id) queryClient.setQueryData(specimenKeys.one(specimen.id), specimen);
  await Promise.all([
    queryClient.invalidateQueries({queryKey: specimenKeys.all, predicate: (query) => query.queryKey[1] !== specimen?.id}),
    queryClient.invalidateQueries({queryKey: pathwayKeys.timelines}),
  ]);
}

/** What's typed when a specimen is recorded or its details corrected. Times are UTC ISO strings with `Z`. */
export interface SpecimenDetailsInput {
  specimenTypeId: string;
  /** When it was taken; left out when recording, the API takes now. */
  takenAtUtc?: string;
  site: string;
  requestNotes: string;
  requestingUserId: string;
}

/** The request's details: an empty site or request notes sent as none. */
function toDetailsRequest(input: SpecimenDetailsInput) {
  const optional = (value: string) => value.trim() || null;
  return {
    specimenTypeId: input.specimenTypeId,
    site: optional(input.site),
    requestNotes: optional(input.requestNotes),
    requestingUserId: input.requestingUserId,
  };
}

/**
 * Records a specimen taken at one of a pathway's stages (`POST api/pathways/{id}/specimens`), returning it (201). The
 * API's 400 names `specimenTypeId`, `takenAtUtc`, `site`, `requestNotes` or `requestingUserId`; a stage
 * that can't take one now, or a restricted record, is a 409 in its words.
 */
export function useRecordSpecimen(pathwayId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({stageId, ...input}: SpecimenDetailsInput & {stageId: string}) =>
      unwrap(
        api.POST('/api/pathways/{id}/specimens', {
          params: {path: {id: pathwayId}},
          body: {stageId, ...toDetailsRequest(input), takenAtUtc: input.takenAtUtc ?? null},
        }),
      ),
    onSuccess: (specimen) => refreshSpecimens(queryClient, specimen),
  });
}

/** A step's time and reference: `atUtc` left out is now. */
export interface SpecimenStepInput {
  atUtc?: string;
  reference: string;
  expectedVersion: number;
}

/** The lab's report, sent as the file's bytes, from the version the page showed. */
export interface SpecimenReportInput {
  file: File;
  expectedVersion: number;
  onProgress?: (fraction: number) => void;
  signal?: AbortSignal;
}

/**
 * Everything that changes a specimen, each from the version the page showed (`expectedVersion`) and each returning the
 * specimen, which replaces the cached one: correcting its details before it's sent, marking it sent (with the courier's
 * or batch's reference), received (with the lab's reference), rejected (a reason, and a detail required for Other),
 * lost or entered in error (a detail), and attaching its report (the file's bytes, under every attachment rule). A 409
 * (changed meanwhile, or the record restricted) reads it again, so the page can show what's there now.
 */
export function useSpecimenActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = (specimen: Specimen) => refreshSpecimens(queryClient, specimen);
  const onError = (error: unknown) =>
    error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: specimenKeys.one(id)}) : undefined;
  const path = {params: {path: {id}}};
  const step = ({atUtc, reference, expectedVersion}: SpecimenStepInput) => ({
    atUtc: atUtc ?? null,
    reference: reference.trim() || null,
    expectedVersion,
  });
  return {
    edit: useMutation({
      mutationFn: ({expectedVersion, ...input}: SpecimenDetailsInput & {takenAtUtc: string; expectedVersion: number}) =>
        unwrap(api.PUT('/api/specimens/{id}', {...path, body: {...toDetailsRequest(input), takenAtUtc: input.takenAtUtc, expectedVersion}})),
      onSuccess,
      onError,
    }),
    sent: useMutation({
      mutationFn: (input: SpecimenStepInput) => unwrap(api.POST('/api/specimens/{id}/sent', {...path, body: step(input)})),
      onSuccess,
      onError,
    }),
    received: useMutation({
      mutationFn: (input: SpecimenStepInput) => unwrap(api.POST('/api/specimens/{id}/received', {...path, body: step(input)})),
      onSuccess,
      onError,
    }),
    rejected: useMutation({
      mutationFn: ({reason, detail, expectedVersion}: {reason: string; detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/specimens/{id}/rejected', {...path, body: {reason, detail: detail.trim() || null, expectedVersion}})),
      onSuccess,
      onError,
    }),
    lost: useMutation({
      mutationFn: ({detail, expectedVersion}: {detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/specimens/{id}/lost', {...path, body: {detail: detail.trim(), expectedVersion}})),
      onSuccess,
      onError,
    }),
    enteredInError: useMutation({
      mutationFn: ({detail, expectedVersion}: {detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/specimens/{id}/entered-in-error', {...path, body: {detail: detail.trim(), expectedVersion}})),
      onSuccess,
      onError,
    }),
    report: useMutation({
      mutationFn: ({file, expectedVersion, onProgress, signal}: SpecimenReportInput) =>
        sendFile<Specimen>({url: `/api/specimens/${id}/report?expectedVersion=${expectedVersion}`, file, onProgress, signal}),
      onSuccess: async (specimen: Specimen) => {
        // The report is a file attached to the pathway (or the patient), so the file lists change too, and it's a new
        // result awaiting sign-off (`src/api/results.ts`, keyed under ['results']).
        await Promise.all([onSuccess(specimen), refreshAfterAttachmentChange(queryClient), queryClient.invalidateQueries({queryKey: ['results']})]);
      },
      onError,
    }),
  };
}

/**
 * Finds the specimen with a number, typed or scanned from its label (`POST api/specimens/find`: in the body, never the
 * URL), and puts it in the cache, so its page opens with it. A number nobody has is an ApiError 404.
 */
export function useFindSpecimen() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (number: string) => unwrap(api.POST('/api/specimens/find', {body: {number: number.trim()}})),
    onSuccess: (specimen) => {
      if (specimen.id) queryClient.setQueryData(specimenKeys.one(specimen.id), specimen);
    },
  });
}

/** Refreshes the setup lists, and the specimens, which carry their types' names. */
function useRefreshSetup() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: specimenSetupKeys.all}),
      queryClient.invalidateQueries({queryKey: specimenKeys.all}),
    ]);
  };
}

/**
 * Adds a specimen type (no `id`) or changes one's name, expected days or SNOMED CT specimen code (holders of
 * ManageSetup; MC13). A name taken is a 409.
 */
export function useSaveSpecimenType() {
  const onSuccess = useRefreshSetup();
  return useMutation({
    mutationFn: ({id, ...body}: {id?: string; name: string; expectedTurnaroundDays: number; snomedCode: string | null}) =>
      id
        ? unwrap(api.PUT('/api/specimen-types/{id}', {params: {path: {id}}, body}))
        : unwrap(api.POST('/api/specimen-types', {body})),
    onSuccess,
  });
}

/** Archive, restore and delete for specimen types. Deleting one a specimen has is a 409 (its `canBeDeleted` says so). */
export function useSpecimenTypeActions() {
  const onSuccess = useRefreshSetup();
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/specimen-types/{id}/archive', path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/specimen-types/{id}/restore', path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE('/api/specimen-types/{id}', path(id))), onSuccess}),
  };
}

/** The label and request sheet pages: `/specimens/{id}/print?sheet=label` (or `request`). */
export type SpecimenSheet = 'label' | 'request';
export const specimenPrintHref = (id: string, sheet: SpecimenSheet) => `/specimens/${id}/print?sheet=${sheet}`;
