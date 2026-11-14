import {createFileRoute} from '@tanstack/react-router';
import {MyTasksPage} from '../../pages/tasks/MyTasksPage';
import {parseTasksSearch} from '../../pages/listSearch';

export const Route = createFileRoute('/_app/tasks')({
  validateSearch: parseTasksSearch,
  component: MyTasksRoute,
});

function MyTasksRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <MyTasksPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
