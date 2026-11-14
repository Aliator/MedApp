import {queryOptions} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type ProcedureSummary = Schemas['PatientProcedureSummaryResponse'];

/**
 * Every procedure query is keyed under ['procedures'], so a change to any procedure refreshes them all.
 * A patient's procedure count is under ['patients', id, ...] too, so patient writes refresh it.
 */
export const procedureKeys = {
  all: ['procedures'] as const,
  countForPatient: (patientId: string) => ['patients', patientId, 'procedures', 'count'] as const,
};

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
