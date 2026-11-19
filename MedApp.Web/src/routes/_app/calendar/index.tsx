import {createFileRoute} from '@tanstack/react-router';
import {CalendarPage, preloadCalendarPage} from '../../../pages/calendar/CalendarPage';
import {parseCalendarSearch} from '../../../pages/calendar/calendarSearch';

export const Route = createFileRoute('/_app/calendar/')({
  validateSearch: parseCalendarSearch,
  loaderDeps: ({search}) => ({date: search.date, view: search.view, staff: search.staff, team: search.team, who: search.who}),
  loader: ({context, deps}) => preloadCalendarPage(context.queryClient, deps),
  component: CalendarRoute,
});

function CalendarRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  // The grid keeps its own place as the address changes (the time at the top when the row length changes), so the
  // router mustn't put back the scroll position it saved for the address before.
  return <CalendarPage search={search} onSearchChange={(next, options) => void navigate({search: next, resetScroll: false, replace: options?.replace})} />;
}
