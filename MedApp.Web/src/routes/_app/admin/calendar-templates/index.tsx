import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {parseCalendarTemplatesSearch} from '../../../../pages/listSearch';
import {CalendarTemplatesPage} from '../../../../pages/setup/CalendarTemplatesPage';
import {preload} from '../../../../api/preload';
import {dayTemplatesQuery, staffQuery, weekTemplatesQuery} from '../../../../api/calendar';

export const Route = createFileRoute('/_app/admin/calendar-templates/')({
  beforeLoad: requireAdmin,
  validateSearch: parseCalendarTemplatesSearch,
  loaderDeps: ({search}) => ({tab: search.tab}),
  loader: ({context, deps}) =>
    deps.tab === 'staff'
      ? preload(context.queryClient, staffQuery, weekTemplatesQuery)
      : preload(context.queryClient, deps.tab === 'week' ? weekTemplatesQuery : dayTemplatesQuery),
  component: function CalendarTemplatesRoute() {
    const navigate = Route.useNavigate();
    return <CalendarTemplatesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
