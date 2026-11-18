import {createFileRoute} from '@tanstack/react-router';
import {NewPathwayPage} from '../../../pages/pathways/NewPathwayPage';
import {preload} from '../../../api/preload';
import {patientQuery} from '../../../api/patients';
import {pathwayTemplatesQuery} from '../../../api/pathways';

/** `?patientId=`: the patient the new pathway is for. */
export const Route = createFileRoute('/_app/pathways/new')({
  validateSearch: (search: Record<string, unknown>): {patientId?: string} => ({
    patientId: typeof search.patientId === 'string' && search.patientId ? search.patientId : undefined,
  }),
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(context.queryClient, pathwayTemplatesQuery(), ...(deps.patientId ? [patientQuery(deps.patientId)] : [])),
  component: function NewPathwayRoute() {
    return <NewPathwayPage patientId={Route.useSearch().patientId} />;
  },
});
