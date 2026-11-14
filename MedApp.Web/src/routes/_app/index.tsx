import {createFileRoute} from '@tanstack/react-router';
import {HomePage} from '../../pages/home/HomePage';
import {parseHomeSearch} from '../../pages/listSearch';

export const Route = createFileRoute('/_app/')({
  validateSearch: parseHomeSearch,
  component: HomeRoute,
});

function HomeRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <HomePage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
