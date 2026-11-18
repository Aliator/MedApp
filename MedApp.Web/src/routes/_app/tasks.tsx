import {createFileRoute} from '@tanstack/react-router';
import {MyTasksPage} from '../../pages/tasks/MyTasksPage';
import {parseTasksSearch} from '../../pages/listSearch';
import {preload} from '../../api/preload';
import {listSettingsFor} from '../../app/listSettings';
import {taskQuery} from '../../pages/tasks/MyTasksPage';
import {pathwaySearchQuery} from '../../api/search';

export const Route = createFileRoute('/_app/tasks')({
  validateSearch: parseTasksSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, pathwaySearchQuery(taskQuery(deps, listSettingsFor(context.queryClient).pageSize))),
  component: MyTasksRoute,
});

function MyTasksRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <MyTasksPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
