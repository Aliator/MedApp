import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
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
  countForPatient: (patientId: string) => ['patients', patientId, 'procedures', 'count'] as const,
};

/** One procedure in full (`GET api/procedures/patient-procedures/{id}`). A missing one is an ApiError with status 404. */
export function procedureQuery(id: string) {
  return queryOptions({
    queryKey: ['procedures', id],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedures/{id}', {params: {path: {id}}})),
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
