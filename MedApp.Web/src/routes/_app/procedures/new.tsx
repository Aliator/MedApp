import {createFileRoute} from '@tanstack/react-router';
import {NewProcedurePage} from '../../../pages/procedures/NewProcedurePage';

/** `?patientId=`: the patient the new procedure is for. */
export const Route = createFileRoute('/_app/procedures/new')({
  validateSearch: (search: Record<string, unknown>): {patientId?: string} => ({
    patientId: typeof search.patientId === 'string' && search.patientId ? search.patientId : undefined,
  }),
  component: function NewProcedureRoute() {
    return <NewProcedurePage patientId={Route.useSearch().patientId} />;
  },
});
