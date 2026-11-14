import {createFileRoute} from '@tanstack/react-router';
import {parseProceduresSearch} from '../../../pages/listSearch';
import {ProceduresListPage} from '../../../pages/procedures/ProceduresListPage';

export const Route = createFileRoute('/_app/procedures/')({
  validateSearch: parseProceduresSearch,
  component: ProceduresRoute,
});

function ProceduresRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <ProceduresListPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
