import {keepPreviousData, queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {formatMobile, formatPostcode} from '../utils/contactDetails';
import {normaliseNhsNumber} from '../utils/nhsNumber';
import {NO_PRACTICE_ID} from './practices';
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
  /** The postal address's three lines, as typed; blank ones are left out. */
  addressLines: [string, string, string];
  /** Optional: as typed (any case, spaces anywhere). Blank means none. */
  postcode: string;
  /** Optional: a UK mobile as typed (`07…`, `+44 7…`). Blank means none. */
  mobilePhone: string;
  /** The GP practice they're registered with, from the directory, or '' for none. */
  practiceId: string;
  /** How they want appointment messages: None, Text, Email or TextAndEmail (centre plan decision 133). */
  reminderChannel: string;
}

/** A patient's details as the edit form starts with them: the mobile in national form, as people type it. */
export function patientInput(patient: Patient): PatientInput {
  const lines = patient.addressLines ?? [];
  return {
    firstName: patient.firstName ?? '',
    lastName: patient.lastName ?? '',
    dateOfBirth: patient.dateOfBirth ?? '',
    email: patient.email ?? '',
    nhsNumber: patient.nhsNumber ?? '',
    addressLines: [lines[0] ?? '', lines[1] ?? '', lines[2] ?? ''],
    postcode: patient.postcode ?? '',
    mobilePhone: patient.mobilePhone ? formatMobile(patient.mobilePhone) : '',
    practiceId: patient.practiceId ?? '',
    reminderChannel: patient.reminderChannel ?? 'None',
  };
}

/** The address lines that aren't blank, trimmed. */
const keptLines = (lines: readonly string[]) => lines.map((line) => line.trim()).filter(Boolean);

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
 *
 * The contact details (address lines, postcode, mobile and GP practice) are left out of a new patient when blank. On
 * an update, with the record as it was (`saved`), each is sent only when it changed (left out, the API leaves it as it
 * is), so an archived practice the patient already has is never sent as if newly chosen; a removed one is sent as the
 * API removes it: no address lines (`[]`), an empty postcode or mobile (`""`), and the all-zero practice ID.
 *
 * The reminder choice goes the same way: left out of a new patient when it's "None" (the API's default), and on an
 * update sent only when it changed (left out, the API leaves it as it is).
 */
export function toPatientRequest(input: PatientInput, mode: 'create' | 'update', saved?: PatientInput): PatientRequest {
  const nhsNumber = normaliseNhsNumber(input.nhsNumber);
  const {addressLines, postcode, mobilePhone, practiceId, reminderChannel, ...personal} = input;
  const lines = keptLines(addressLines);
  const isCreate = mode === 'create';
  const changed = (same: (before: PatientInput) => boolean) => isCreate || !saved || !same(saved);
  const sameText = (a: string, b: string) => a.trim() === b.trim();
  return {
    ...personal,
    dateOfBirth: input.dateOfBirth || NO_DATE,
    nhsNumber: nhsNumber || (isCreate ? undefined : ''),
    addressLines:
      changed((before) => JSON.stringify(keptLines(before.addressLines)) === JSON.stringify(lines)) && (lines.length > 0 || !isCreate) ? lines : undefined,
    postcode: changed((before) => formatPostcode(before.postcode) === formatPostcode(postcode)) ? postcode.trim() || (isCreate ? undefined : '') : undefined,
    mobilePhone: changed((before) => sameText(before.mobilePhone, mobilePhone)) ? mobilePhone.trim() || (isCreate ? undefined : '') : undefined,
    practiceId: changed((before) => before.practiceId === practiceId) ? practiceId || (isCreate ? undefined : NO_PRACTICE_ID) : undefined,
    reminderChannel:
      changed((before) => before.reminderChannel === reminderChannel) && !(isCreate && reminderChannel === 'None') ? reminderChannel : undefined,
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
    /** `saved`: the record as the form started from it, so only the contact details that changed are sent. */
    mutationFn: ({input, saved}: {input: PatientInput; saved?: PatientInput}) =>
      unwrap(api.PATCH('/api/patients/{id}', {params: {path: {id}}, body: toPatientRequest(input, 'update', saved)})),
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
 * response), the lists, their pathways and appointments (which become read-only with them, so the calendars too)
 * and the activity log.
 */
function useAfterPatientChange(id: string) {
  const queryClient = useQueryClient();
  return async (patient: Patient) => {
    queryClient.setQueryData(['patients', id], patient);
    await Promise.all([
      queryClient.invalidateQueries({queryKey: ['patients']}),
      queryClient.invalidateQueries({queryKey: ['pathways']}),
      queryClient.invalidateQueries({queryKey: ['appointments']}),
      queryClient.invalidateQueries({queryKey: ['calendar']}),
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
  /** Restricting a record with pathways under way: the admin confirms their work stops (but for blocking and cancelling). */
  confirmOpenPathways?: boolean;
}

/** Sets or clears a patient's restricted or disputed flag, with a reason (admins only). */
export function useSetPatientFlag(id: string) {
  const after = useAfterPatientChange(id);
  return useMutation({
    mutationFn: ({flag, set, reason, confirmOpenPathways = false}: PatientFlagInput) => {
      const body = {set, reason: reason.trim() || null, confirmOpenPathways};
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
