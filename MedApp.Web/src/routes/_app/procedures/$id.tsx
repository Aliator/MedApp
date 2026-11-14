import {createFileRoute} from '@tanstack/react-router';
import {ProcedurePage} from '../../../pages/procedures/ProcedurePage';

export const Route = createFileRoute('/_app/procedures/$id')({
  component: function ProcedureRoute() {
    return <ProcedurePage id={Route.useParams().id} />;
  },
});
