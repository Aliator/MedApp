import {queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {pathwayKeys} from './pathways';
import {refreshAfterAttachmentChange, uploadAttachment} from './attachments';

/**
 * Consent records (docs/centre-phases-3-5-plan.md, decisions 145 to 147): a patient's consent to a stage of their
 * pathway, recorded by a holder of RecordConsent with the form, the date it was signed, who signed it and the scanned
 * form (a file on the pathway). A stage that needs consent can't be finished without a Valid record. MedApp records
 * what the clinician states: it never assesses capacity, validity or whether the form fits. Every change carries the
 * version the page showed (`expectedVersion`), so a change made meanwhile is a 409.
 */
export type ConsentRecord = Schemas['ConsentRecordResponse'];
export type ConsentForm = Schemas['ConsentFormResponse'];

/** Who signed, by the names the API takes, in the order the dialog offers them. */
export const CONSENT_SIGNED_BY = ['Patient', 'PersonWithParentalResponsibility', 'BestInterestsDecision'] as const;
export type ConsentSignedBy = (typeof CONSENT_SIGNED_BY)[number];

const SIGNED_BY_LABELS: Record<ConsentSignedBy, string> = {
  Patient: 'The patient',
  PersonWithParentalResponsibility: 'A person with parental responsibility',
  BestInterestsDecision: 'Best-interests decision by a clinician',
};

/** Who signed, in words. The name itself for one this copy doesn't know. */
export const signedByLabel = (signedBy: string | null | undefined) => SIGNED_BY_LABELS[signedBy as ConsentSignedBy] ?? signedBy ?? '';

const STATUS_LABELS: Record<string, string> = {Valid: 'Valid', Withdrawn: 'Withdrawn', EnteredInError: 'Entered in error'};

/** A status in words: "EnteredInError" → "Entered in error". */
export const consentStatusLabel = (status: string | null | undefined) => STATUS_LABELS[status ?? ''] ?? status ?? '';

/** A form's name with its version: "Consent for a skin procedure (version 3)". */
export const consentFormLabel = (name: string | null | undefined, version: string | null | undefined) =>
  version ? `${name ?? ''} (version ${version})` : (name ?? '');

/** The longest a withdrawal's or correction's detail can be (the API's 500), and a form's name (200) and version (50). */
export const MAX_CONSENT_DETAIL = 500;
export const MAX_FORM_NAME = 200;
export const MAX_FORM_VERSION = 50;

export const consentKeys = {
  all: ['consent'] as const,
  pathway: (pathwayId: string) => ['consent', 'pathway', pathwayId] as const,
  forms: (includeArchived: boolean) => ['consent', 'forms', includeArchived] as const,
};

/** A pathway's consent records, oldest first (`GET api/pathways/{id}/consent-records`), recorded as viewing the pathway. */
export function pathwayConsentQuery(pathwayId: string) {
  return queryOptions({
    queryKey: consentKeys.pathway(pathwayId),
    queryFn: () => unwrap(api.GET('/api/pathways/{id}/consent-records', {params: {path: {id: pathwayId}}})),
  });
}

/** The consent forms by name then version; archived ones only with `includeArchived` (the setup page). */
export function consentFormsQuery(includeArchived = false) {
  return queryOptions({
    queryKey: consentKeys.forms(includeArchived),
    queryFn: () => unwrap(api.GET('/api/consent-forms', {params: {query: {includeArchived}}})),
  });
}

/** After a change to consent: every consent list, the pathway (its stages' gate) and the timelines. */
export async function refreshConsent(queryClient: QueryClient) {
  await Promise.all([
    queryClient.invalidateQueries({queryKey: consentKeys.all}),
    queryClient.invalidateQueries({queryKey: pathwayKeys.timelines}),
  ]);
}

export interface RecordConsentInput {
  stageId: string;
  consentFormId: string;
  /** The UK date it was signed, YYYY-MM-DD. */
  signedOn: string;
  signedBy: string;
  /** The scanned, signed form, sent first as a file on the pathway; optional. */
  scan?: File | null;
}

/**
 * Records consent to one of a pathway's stages (`POST api/pathways/{id}/consent-records`). A scan, if chosen, is
 * attached to the pathway first and named in the record. The API's 400 names `stageId`, `consentFormId`, `signedOn`,
 * `signedBy` or `scanAttachmentId`; a stage that can't take consent now, or a restricted record, is a 409.
 */
export function useRecordConsent(pathwayId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({scan, ...input}: RecordConsentInput) => {
      const scanAttachmentId = scan ? (await uploadAttachment({target: {pathwayId}, file: scan})).id : null;
      return unwrap(
        api.POST('/api/pathways/{id}/consent-records', {
          params: {path: {id: pathwayId}},
          body: {...input, scanAttachmentId},
        }),
      );
    },
    onSuccess: async (_, input) => {
      await Promise.all([refreshConsent(queryClient), input.scan ? refreshAfterAttachmentChange(queryClient) : undefined]);
    },
  });
}

/**
 * What changes a consent record, each from the version the page showed: withdrawing it (a date and what was said),
 * marking it entered in error (what was wrong; its scan is deleted with it) and attaching its scan (a file sent first to
 * the pathway). A 409 refreshes the list, so the page shows what's there now.
 */
export function useConsentActions(record: ConsentRecord) {
  const queryClient = useQueryClient();
  const id = record.id ?? '';
  const path = {params: {path: {id}}};
  const onSuccess = () => refreshConsent(queryClient);
  const onError = (error: unknown) =>
    error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: consentKeys.all}) : undefined;
  return {
    withdraw: useMutation({
      mutationFn: ({withdrawnOn, detail}: {withdrawnOn: string; detail: string}) =>
        unwrap(api.POST('/api/consent-records/{id}/withdraw', {...path, body: {withdrawnOn, detail: detail.trim(), expectedVersion: record.version ?? 0}})),
      onSuccess,
      onError,
    }),
    enteredInError: useMutation({
      mutationFn: ({detail}: {detail: string}) =>
        unwrap(api.POST('/api/consent-records/{id}/entered-in-error', {...path, body: {detail: detail.trim(), expectedVersion: record.version ?? 0}})),
      onSuccess: async () => {
        await Promise.all([onSuccess(), refreshAfterAttachmentChange(queryClient)]);
      },
      onError,
    }),
    attachScan: useMutation({
      mutationFn: async ({file}: {file: File}) => {
        const scan = await uploadAttachment({target: {pathwayId: record.pathwayId ?? ''}, file});
        return unwrap(
          api.POST('/api/consent-records/{id}/scan', {...path, body: {attachmentId: scan.id, expectedVersion: record.version ?? 0}}),
        );
      },
      onSuccess: async () => {
        await Promise.all([onSuccess(), refreshAfterAttachmentChange(queryClient)]);
      },
      onError,
    }),
  };
}

/** Adds a consent form (no `id`) or changes one's name or version (holders of ManageSetup). A name and version taken is a 409. */
export function useSaveConsentForm() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({id, ...body}: {id?: string; name: string; formVersion: string}) =>
      id ? unwrap(api.PUT('/api/consent-forms/{id}', {params: {path: {id}}, body})) : unwrap(api.POST('/api/consent-forms', {body})),
    onSuccess: () => queryClient.invalidateQueries({queryKey: consentKeys.all}),
  });
}

/** Archive, restore and delete for consent forms. Deleting one a record has used is a 409 (its `canBeDeleted` says so). */
export function useConsentFormActions() {
  const queryClient = useQueryClient();
  const onSuccess = () => queryClient.invalidateQueries({queryKey: consentKeys.all});
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/consent-forms/{id}/archive', path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/consent-forms/{id}/restore', path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE('/api/consent-forms/{id}', path(id))), onSuccess}),
  };
}
