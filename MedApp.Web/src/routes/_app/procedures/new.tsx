import {createFileRoute} from '@tanstack/react-router';
import {NewProcedurePage} from '../../../pages/procedures/NewProcedurePage';
import {preload} from '../../../api/preload';
import {patientQuery} from '../../../api/patients';
import {procedureTemplatesQuery} from '../../../api/procedures';

/** `?patientId=`: the patient the new procedure is for. */
export const Route = createFileRoute('/_app/procedures/new')({
  validateSearch: (search: Record<string, unknown>): {patientId?: string} => ({
    patientId: typeof search.patientId === 'string' && search.patientId ? search.patientId : undefined,
  }),
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(context.queryClient, procedureTemplatesQuery(), ...(deps.patientId ? [patientQuery(deps.patientId)] : [])),
  component: function NewProcedureRoute() {
    return <NewProcedurePage patientId={Route.useSearch().patientId} />;
  },
});
