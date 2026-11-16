import {keepPreviousData, queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type ProcedureSummary = Schemas['PatientProcedureSummaryResponse'];
export type Procedure = Schemas['PatientProcedureResponse'];
export type ProcedureStage = Schemas['PatientProcedureStageResponse'];

/**
 * Every procedure query is keyed under ['procedures'], so a change to any procedure refreshes them all.
 * A patient's procedure count is under ['patients', id, ...] too, so patient writes refresh it.
 */
export const procedureKeys = {
  all: ['procedures'] as const,
  /** Every patient's timeline (`src/api/timeline.ts`), which a change to any procedure adds to. */
  timelines: ['procedures', 'timeline'] as const,
  countForPatient: (patientId: string) => ['patients', patientId, 'procedures', 'count'] as const,
};

/** One procedure in full (`GET api/procedures/patient-procedures/{id}`). A missing one is an ApiError with status 404. */
export function procedureQuery(id: string) {
  return queryOptions({
    queryKey: ['procedures', id],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedures/{id}', {params: {path: {id}}})),
  });
}

/**
 * The titles and patients' names of these procedures (`GET api/procedures/patient-procedures/labels`), for the side
 * nav's pins: labels, not a view of the records, so the API records nothing. Procedures that no longer exist are left
 * out. One request for every pin; keyed under ['procedures'], so a change to a procedure refreshes it.
 */
export function procedureLabelsQuery(ids: readonly string[]) {
  const sorted = [...new Set(ids)].sort();
  return queryOptions({
    queryKey: ['procedures', 'labels', sorted],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedures/labels', {params: {query: {ids: sorted}}})),
    enabled: sorted.length > 0,
    placeholderData: keepPreviousData,
  });
}

/**
 * How many of a patient's procedures are under way: not completed, cancelled or archived (a search's default; blocked
 * ones count). Keyed under the patient, so patient and procedure writes refresh it.
 */
export function patientOpenProcedureCountQuery(patientId: string) {
  return queryOptions({
    queryKey: ['patients', patientId, 'procedures', 'open-count'],
    queryFn: async () => {
      const page = await unwrap(
        api.GET('/api/procedures/patient-procedures/search', {params: {query: {PatientId: patientId, PageSize: 1}}}),
      );
      return page.totalCount ?? 0;
    },
  });
}

/** How many procedures a patient has, in any status (one row of a search, for its total). */
export function patientProcedureCountQuery(patientId: string) {
  return queryOptions({
    queryKey: procedureKeys.countForPatient(patientId),
    queryFn: async () => {
      const page = await unwrap(
        api.GET('/api/procedures/patient-procedures/search', {
          params: {query: {PatientId: patientId, IncludeClosed: true, PageSize: 1}},
        }),
      );
      return page.totalCount ?? 0;
    },
  });
}

const PROCEDURE = '/api/procedures/patient-procedures/{id}';

/**
 * Everything staff (and admins) can do to a procedure from its page. Each action returns the updated procedure,
 * which replaces the cached one, so the page shows the result at once; lists and searches are refreshed too.
 * Admin-only actions (assign, unassign, unlock) are refused by the API for anyone else.
 */
export function useProcedureActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = async (procedure: Procedure) => {
    queryClient.setQueryData(procedureQuery(id).queryKey, procedure);
    await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
    await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
  };
  const stage = (stageId: string) => ({params: {path: {id, stageId}}});

  return {
    startStage: useMutation({
      mutationFn: (stageId: string) => unwrap(api.POST(`${PROCEDURE}/stages/{stageId}/start`, stage(stageId))),
      onSuccess,
    }),
    saveNotes: useMutation({
      mutationFn: ({stageId, notes}: {stageId: string; notes: string}) =>
        unwrap(api.PUT(`${PROCEDURE}/stages/{stageId}/notes`, {...stage(stageId), body: {notes}})),
      onSuccess,
    }),
    finishStage: useMutation({
      mutationFn: (stageId: string) => unwrap(api.POST(`${PROCEDURE}/stages/{stageId}/finish`, stage(stageId))),
      onSuccess,
    }),
    unlockStage: useMutation({
      mutationFn: (stageId: string) => unwrap(api.POST(`${PROCEDURE}/stages/{stageId}/unlock`, stage(stageId))),
      onSuccess,
    }),
    changeStatus: useMutation({
      mutationFn: (change: {status: string; reason?: string}) =>
        unwrap(api.POST(`${PROCEDURE}/status`, {params: {path: {id}}, body: change})),
      onSuccess,
    }),
    assign: useMutation({
      mutationFn: (userId: string) =>
        unwrap(api.POST(`${PROCEDURE}/assignments/{userId}`, {params: {path: {id, userId}}})),
      onSuccess,
    }),
    unassign: useMutation({
      mutationFn: (userId: string) =>
        unwrap(api.DELETE(`${PROCEDURE}/assignments/{userId}`, {params: {path: {id, userId}}})),
      onSuccess,
    }),
  };
}

export type ProcedureTemplate = Schemas['PatientProcedureStageTemplateResponse'];

/** What New procedure sends: the patient, the template it's built from, and its details. */
export type NewProcedureInput = Schemas['CreatePatientProcedureRequest'];

/** A procedure's editable details: title, notes, due date and priority. */
export interface ProcedureDetailsInput {
  title: string;
  notes: string;
  /** A date, `YYYY-MM-DD`; due by the end of that day, UK time. */
  dueDate: string;
  priority: string;
}

/** The templates staff can create a procedure from (archived ones are left out). */
export function procedureTemplatesQuery() {
  return queryOptions({
    queryKey: ['procedureTemplates'],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedure-stage/templates')),
  });
}

/** Creates a procedure from a template, returning it; lists and the patient's procedure count refresh. */
export function useCreateProcedure() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: NewProcedureInput) => unwrap(api.POST('/api/procedures/patient-procedures', {body: input})),
    onSuccess: async (procedure) => {
      queryClient.setQueryData(procedureQuery(procedure.id ?? '').queryKey, procedure);
      await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
      await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
      await queryClient.invalidateQueries({queryKey: procedureKeys.countForPatient(procedure.patientId ?? '')});
    },
  });
}

/** Saves a procedure's details, returning it updated; it replaces the cached one, and lists refresh. */
export function useUpdateProcedure(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: ProcedureDetailsInput) =>
      unwrap(api.PATCH(PROCEDURE, {params: {path: {id}}, body: input})),
    onSuccess: async (procedure) => {
      queryClient.setQueryData(procedureQuery(id).queryKey, procedure);
      await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
      await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
    },
  });
}

/** Deletes a procedure (admins, and only once it's cancelled or archived). */
export function useDeleteProcedure(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.DELETE(PROCEDURE, {params: {path: {id}}})),
    onSuccess: async () => {
      // Its own query would only 404 if refetched, so drop it rather than refresh it.
      queryClient.removeQueries({queryKey: ['procedures', id]});
      await queryClient.invalidateQueries({queryKey: ['procedures']});
      await queryClient.invalidateQueries({queryKey: ['patients']});
    },
  });
}

/** Decisions 13 and 20: only an admin can delete a procedure, and only one the API allows (cancelled or archived). */
export const canDeleteProcedure = (procedure: Procedure, isAdmin: boolean) => isAdmin && (procedure.canBeDeleted ?? false);

export const PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
