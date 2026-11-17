import {createFileRoute} from '@tanstack/react-router';
import {whoamiQuery} from '../../../auth/auth';
import {CalendarPage, preloadCalendarPage} from '../../../pages/calendar/CalendarPage';
import {parseCalendarSearch} from '../../../pages/calendar/calendarSearch';

export const Route = createFileRoute('/_app/calendar/')({
  validateSearch: parseCalendarSearch,
  loaderDeps: ({search}) => ({date: search.date, view: search.view, staff: search.staff, who: search.who}),
  loader: async ({context, deps}) => {
    // Signed in already (the layout's guard), so this is cached: whether the team view is theirs to see.
    const me = await context.queryClient.ensureQueryData(whoamiQuery);
    return preloadCalendarPage(context.queryClient, deps, me.isAdmin);
  },
  component: CalendarRoute,
});

function CalendarRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <CalendarPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
