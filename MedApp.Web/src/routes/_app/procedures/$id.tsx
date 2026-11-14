import {createFileRoute} from '@tanstack/react-router';
import {ProcedurePage} from '../../../pages/procedures/ProcedurePage';

export const Route = createFileRoute('/_app/procedures/$id')({
  component: function ProcedureRoute() {
    const {id} = Route.useParams();
    // Keyed by procedure, so moving to another one starts a fresh visit (the "not assigned to you" check asks again).
    return <ProcedurePage key={id} id={id} />;
  },
});
