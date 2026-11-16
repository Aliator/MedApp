import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../../api/preload';
import {staffQuery} from '../../../../api/calendar';
import {calendarTemplatesPageQueries, CalendarTemplatesPage} from '../../../../pages/setup/CalendarTemplatesPage';
import {templatesOwnerId} from '../../../../pages/setup/calendarTemplatesOwner';
import {parseCalendarTemplatesSearch} from '../../../../pages/setup/calendarTemplatesSearch';

export const Route = createFileRoute('/_app/calendar/templates/')({
  validateSearch: parseCalendarTemplatesSearch,
  loaderDeps: ({search}) => ({tab: search.tab, staff: search.staff}),
  loader: async ({context, deps}) => {
    const owner = await templatesOwnerId(context.queryClient, deps.staff);
    return preload(context.queryClient, staffQuery, ...(owner ? calendarTemplatesPageQueries(deps.tab ?? 'day', owner) : []));
  },
  component: function CalendarTemplatesRoute() {
    const navigate = Route.useNavigate();
    return <CalendarTemplatesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
