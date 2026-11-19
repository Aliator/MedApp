import {keepPreviousData, queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * Hospitals and hospital requests (MC18 of `docs/multi-clinic-plan.md`). A nearby hospital sends the lab samples with
 * a request shaped on FHIR UK Core `ServiceRequest-Lab` and `Specimen`: its patient number, the NHS number, the surgeon
 * (as written), the priority (FHIR's codes), whether it's a cancer pathway, when it was made, when the report is needed
 * (`occurrence`), the collection time and the sample's type. The lab types it in (`ProcessSpecimens`), choosing the
 * patient from the matches or adding a new one (never matched automatically), and it opens a lab case of the lab's own,
 * with no pathway. Hospitals are setup (`ManageSetup`). MedApp records the hospital's priority and dates as given: it
 * never sets or suggests one.
 */

export type Hospital = Schemas['HospitalResponse'];
export type HospitalRequest = Schemas['HospitalRequestResponse'];
export type HospitalRequestSummary = Schemas['HospitalRequestSummaryResponse'];
export type HospitalRequestSearch = Schemas['HospitalRequestSearchRequest'];
export type HospitalPatientMatch = Schemas['HospitalPatientMatchResponse'];
export type CreateHospitalRequest = Schemas['CreateHospitalRequestRequest'];
export type HospitalDetails = Required<Pick<Schemas['HospitalDetailsRequest'], 'name' | 'odsCode' | 'reportDeliveryMethod' | 'defaultDeadlineDays'>> & {
  contact: string | null;
};

/** FHIR's `ServiceRequest.priority` codes (`LabRequestPriority`), in their stored order, with their words. */
export const LAB_REQUEST_PRIORITIES = [
  {value: 'Routine', label: 'Routine'},
  {value: 'Urgent', label: 'Urgent'},
  {value: 'Asap', label: 'As soon as possible'},
  {value: 'Stat', label: 'Immediately (stat)'},
] as const;

export const labRequestPriorityLabel = (priority: string | null | undefined) =>
  LAB_REQUEST_PRIORITIES.find((p) => p.value === priority)?.label ?? priority ?? '';

/** A lab case's status (`LabCaseStatus`, MC15) in words. */
export const labCaseStatusLabel = (status: string | null | undefined) =>
  ({Open: 'Open', Reported: 'Reported', AwaitingSurgeryDetails: 'Awaiting surgery details', Closed: 'Closed', RepeatRequested: 'Repeat requested', EnteredInError: 'Entered in error'})[
    status ?? ''
  ] ?? status ?? '';

/** The limits the API checks. */
export const MIN_DEADLINE_DAYS = 1;
export const MAX_DEADLINE_DAYS = 90;

export const hospitalKeys = {
  all: ['hospitals'] as const,
  list: (includeArchived: boolean) => ['hospitals', includeArchived] as const,
};

export const hospitalRequestKeys = {
  all: ['hospital-requests'] as const,
  one: (id: string) => ['hospital-requests', id] as const,
  search: (search: HospitalRequestSearch) => ['hospital-requests', 'search', search] as const,
};

/** The hospitals by name; archived ones only with `includeArchived` (the setup page). */
export function hospitalsQuery(includeArchived = false) {
  return queryOptions({
    queryKey: hospitalKeys.list(includeArchived),
    queryFn: () => unwrap(api.GET('/api/hospitals', {params: {query: {includeArchived}}})),
  });
}

/** One hospital request and its lab case (`GET api/hospital-requests/{id}`): opening it is recorded as viewing the case. */
export function hospitalRequestQuery(id: string) {
  return queryOptions({
    queryKey: hospitalRequestKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/hospital-requests/{id}', {params: {path: {id}}})),
  });
}

/**
 * A page of hospital requests (`POST api/hospital-requests/search`): a POST, so the search, which may be a case,
 * hospital or NHS number, stays out of URLs. Each patient shown is recorded as listed.
 */
export function hospitalRequestSearchQuery(search: HospitalRequestSearch) {
  const body = Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined && v !== null && v !== '')) as HospitalRequestSearch;
  return queryOptions({
    queryKey: hospitalRequestKeys.search(body),
    queryFn: () => unwrap(api.POST('/api/hospital-requests/search', {body})),
    placeholderData: keepPreviousData,
  });
}

/** The patients a request could be for, for a person to choose from (by NHS number, or last name and date of birth). */
export function useFindHospitalPatients() {
  return useMutation({
    mutationFn: (body: Schemas['HospitalPatientMatchRequest']) => unwrap(api.POST('/api/hospital-requests/patient-matches', {body})),
  });
}

/** Registers a hospital request, opening its lab case. */
export function useCreateHospitalRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateHospitalRequest) => unwrap(api.POST('/api/hospital-requests', {body})),
    onSuccess: async (request) => {
      if (request.id) queryClient.setQueryData(hospitalRequestKeys.one(request.id), request);
      await Promise.all([
        queryClient.invalidateQueries({queryKey: hospitalRequestKeys.all, predicate: (q) => q.queryKey[1] !== request.id}),
        // A new patient may have been added with it.
        queryClient.invalidateQueries({queryKey: ['patients']}),
      ]);
    },
  });
}

function useRefreshHospitals() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({queryKey: hospitalKeys.all});
}

/** Adds a hospital (no `id`) or changes one (holders of ManageSetup). A name or ODS code taken is a 409. */
export function useSaveHospital() {
  const onSuccess = useRefreshHospitals();
  return useMutation({
    mutationFn: ({id, ...body}: HospitalDetails & {id?: string}) =>
      id ? unwrap(api.PUT('/api/hospitals/{id}', {params: {path: {id}}, body})) : unwrap(api.POST('/api/hospitals', {body})),
    onSuccess,
  });
}

/** Archive, restore and delete for hospitals. Deleting one that has sent a request is a 409 (`canBeDeleted`). */
export function useHospitalActions() {
  const onSuccess = useRefreshHospitals();
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/hospitals/{id}/archive', path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/hospitals/{id}/restore', path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE('/api/hospitals/{id}', path(id))), onSuccess}),
  };
}
