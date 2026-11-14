import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type Patient = Schemas['PatientResponse'];

/** The fields a patient is created or updated with. Dates are ISO YYYY-MM-DD. */
export interface PatientInput {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  email: string;
}

/**
 * The API's DateOnly can't be empty, and its validator treats the default date (0001-01-01) as "not provided"
 * ("Date of birth must be provided."). An empty date would fail JSON parsing with a technical error instead,
 * so send the default, as the Blazor client did.
 */
export const NO_DATE = '0001-01-01';

export function toPatientRequest(input: PatientInput): PatientInput {
  return {...input, dateOfBirth: input.dateOfBirth || NO_DATE};
}

/** One patient, by ID (`GET api/patients/{id}`). A missing patient is an ApiError with status 404. */
export function patientQuery(id: string) {
  return queryOptions({
    queryKey: ['patients', id],
    queryFn: () => unwrap(api.GET('/api/patients/{id}', {params: {path: {id}}})),
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
    mutationFn: (input: PatientInput) => unwrap(api.POST('/api/patients', {body: toPatientRequest(input)})),
    onSuccess: invalidate,
  });
}

export function useUpdatePatient(id: string) {
  const invalidate = useInvalidatePatients();
  return useMutation({
    mutationFn: (input: PatientInput) =>
      unwrap(api.PATCH('/api/patients/{id}', {params: {path: {id}}, body: toPatientRequest(input)})),
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
