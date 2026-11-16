import {keepPreviousData, queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {normaliseNhsNumber} from '../utils/nhsNumber';
import {api, unwrap, type Schemas} from './client';
import type {paths} from './schema';
import {PAGE_SIZE} from './search';

export type Patient = Schemas['PatientResponse'];
export type PatientExport = Schemas['PatientExportResponse'];
export type RetentionReviewItem = Schemas['RetentionReviewItemResponse'];

/** The fields a patient is created or updated with. Dates are ISO YYYY-MM-DD. */
export interface PatientInput {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  email: string;
  /** Optional: as typed, spaces allowed. Blank means none. */
  nhsNumber: string;
}

/**
 * The API's DateOnly can't be empty, and its validator treats the default date (0001-01-01) as "not provided"
 * ("Date of birth must be provided."). An empty date would fail JSON parsing with a technical error instead,
 * so send the default, as the Blazor client did.
 */
export const NO_DATE = '0001-01-01';

export type PatientRequest = Schemas['CreatePatientRequest'] & Schemas['UpdatePatientRequest'];

/**
 * The request body for a new patient (`create`) or a change to one (`update`). The NHS number goes without its
 * spaces. A blank one is left out of a new patient; on an update it's sent as "" so it's removed, since the API
 * reads a missing (null) NHS number as "leave it as it is", and the edit form always shows the whole record.
 */
export function toPatientRequest(input: PatientInput, mode: 'create' | 'update'): PatientRequest {
  const nhsNumber = normaliseNhsNumber(input.nhsNumber);
  return {
    ...input,
    dateOfBirth: input.dateOfBirth || NO_DATE,
    nhsNumber: nhsNumber || (mode === 'update' ? '' : undefined),
  };
}

/** One patient, by ID (`GET api/patients/{id}`). A missing patient is an ApiError with status 404. */
export function patientQuery(id: string) {
  return queryOptions({
    queryKey: ['patients', id],
    queryFn: () => unwrap(api.GET('/api/patients/{id}', {params: {path: {id}}})),
  });
}

/**
 * These patients' names (`GET api/patients/labels`), for the side nav's pins: labels, not a view of the records, so
 * the API records nothing. Patients that no longer exist are left out. One request for every pin; keyed under
 * ['patients'], so patient writes refresh it.
 */
export function patientLabelsQuery(ids: readonly string[]) {
  const sorted = [...new Set(ids)].sort();
  return queryOptions({
    queryKey: ['patients', 'labels', sorted],
    queryFn: () => unwrap(api.GET('/api/patients/labels', {params: {query: {ids: sorted}}})),
    enabled: sorted.length > 0,
    placeholderData: keepPreviousData,
  });
}

/** Every patient query (lists, searches and single patients) is keyed under ['patients'], so writes refresh them all. */
function useInvalidatePatients() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({queryKey: ['patients']});
}

export function useCreatePatient() {
  const invalidate = useInvalidatePatients();
  return useMutation({
    mutationFn: (input: PatientInput) => unwrap(api.POST('/api/patients', {body: toPatientRequest(input, 'create')})),
    onSuccess: invalidate,
  });
}

export function useUpdatePatient(id: string) {
  const invalidate = useInvalidatePatients();
  return useMutation({
    mutationFn: (input: PatientInput) =>
      unwrap(api.PATCH('/api/patients/{id}', {params: {path: {id}}, body: toPatientRequest(input, 'update')})),
    onSuccess: invalidate,
  });
}

export function useDeletePatient(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/patients/{id}', {params: {path: {id}}})),
    onSuccess: async () => {
      // The deleted patient's own query would only 404 if refetched, so drop it rather than refresh it.
      queryClient.removeQueries({queryKey: ['patients', id]});
      await queryClient.invalidateQueries({queryKey: ['patients']});
    },
  });
}

/** What a patient's record can't have done to it now, and why: restricted or anonymised records can't be changed. */
export function whyCantChange(patient: Pick<Patient, 'isRestricted' | 'isAnonymised'>): string | null {
  if (patient.isAnonymised) return "This patient's record has been anonymised, so it can't be changed.";
  if (patient.isRestricted) return "This patient's record is restricted, so it can't be changed until an admin lifts the restriction.";
  return null;
}

/**
 * Refreshes everything a change to a patient's flags or record touches: the patient (with its new state from the
 * response), the lists, their procedures (which become read-only with them) and the activity log.
 */
function useAfterPatientChange(id: string) {
  const queryClient = useQueryClient();
  return async (patient: Patient) => {
    queryClient.setQueryData(['patients', id], patient);
    await Promise.all([
      queryClient.invalidateQueries({queryKey: ['patients']}),
      queryClient.invalidateQueries({queryKey: ['procedures']}),
      queryClient.invalidateQueries({queryKey: ['activity']}),
    ]);
  };
}

/** Which of a patient's flags: restricted (Article 18) or disputed (Article 16). */
export type PatientFlag = 'restriction' | 'dispute';

export interface PatientFlagInput {
  flag: PatientFlag;
  /** True to set it, false to clear it. */
  set: boolean;
  reason: string;
  /** Restricting a record with procedures under way: the admin confirms their work stops (but for blocking and cancelling). */
  confirmOpenProcedures?: boolean;
}

/** Sets or clears a patient's restricted or disputed flag, with a reason (admins only). */
export function useSetPatientFlag(id: string) {
  const after = useAfterPatientChange(id);
  return useMutation({
    mutationFn: ({flag, set, reason, confirmOpenProcedures = false}: PatientFlagInput) => {
      const body = {set, reason: reason.trim() || null, confirmOpenProcedures};
      return unwrap(
        flag === 'restriction'
          ? api.PUT('/api/patients/{id}/restriction', {params: {path: {id}}, body})
          : api.PUT('/api/patients/{id}/dispute', {params: {path: {id}}, body}),
      );
    },
    onSuccess: after,
  });
}

/** Why a record is anonymised: a fixed list (as the API takes it), so the reason can't hold a patient's details. */
export const ANONYMISATION_REASONS = [
  {value: 'RetentionPeriodEnded', label: 'Past its retention period'},
  {value: 'ErasureRequest', label: 'The patient asked for erasure'},
  {value: 'CreatedInError', label: 'Created in error'},
] as const;
export type AnonymisationReason = (typeof ANONYMISATION_REASONS)[number]['value'];

export interface AnonymiseInput {
  reason: AnonymisationReason;
  /** An optional note for the activity log, which must hold no patient details; sent as none when empty. */
  note?: string;
  /** The patient's full name, typed to confirm. */
  confirmation: string;
}

/** Anonymises a patient's record (admins only): its identifying details and free text become placeholders. */
export function useAnonymisePatient(id: string) {
  const after = useAfterPatientChange(id);
  return useMutation({
    mutationFn: ({reason, note, confirmation}: AnonymiseInput) =>
      unwrap(api.POST('/api/patients/{id}/anonymise', {params: {path: {id}}, body: {reason, note: note?.trim() || null, confirmation}})),
    onSuccess: after,
  });
}

/**
 * Everything held about a patient (`GET api/patients/{id}/export`, admins only), for a subject access request.
 * Every fetch is recorded in the activity log as an export, so it's fetched once per visit to the export page: never
 * refetched or retried. It's kept while the page is open (long enough for the route's loader and server rendering to
 * hand it to the page), and removed when the page closes, since it holds the whole clinical record.
 */
export function patientExportQuery(id: string) {
  return queryOptions({
    queryKey: ['patient-export', id],
    queryFn: () => unwrap(api.GET('/api/patients/{id}/export', {params: {path: {id}}})),
    staleTime: Infinity,
    gcTime: 5 * 60_000,
    retry: false,
  });
}

export type RetentionSearch = NonNullable<paths['/api/patients/retention-review']['get']['parameters']['query']>;

/**
 * One page of the patients whose records are past their retention period (`GET api/patients/retention-review`,
 * admins only), the longest overdue first. Keyed under ['patients'], so a change to a patient refreshes it.
 */
export function retentionReviewQuery(search: RetentionSearch) {
  const query = Object.fromEntries(
    Object.entries({PageSize: PAGE_SIZE, ...search}).filter(([, value]) => value !== undefined),
  ) as RetentionSearch;
  return queryOptions({
    queryKey: ['patients', 'retention-review', query],
    queryFn: () => unwrap(api.GET('/api/patients/retention-review', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

export interface KeepRecordInput {
  /** The date to review it again (ISO YYYY-MM-DD). */
  keepUntil: string;
  reason: string;
}

/** Keeps a record past its retention period until a review date, with a reason (admins only). */
export function useKeepPatientRecord(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: KeepRecordInput) => unwrap(api.POST('/api/patients/{id}/retention/keep', {params: {path: {id}}, body})),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({queryKey: ['patients']}),
        queryClient.invalidateQueries({queryKey: ['activity']}),
      ]),
  });
}
