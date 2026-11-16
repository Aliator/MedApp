import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {staffQuery} from '../../../api/calendar';
import {CalendarPage, calendarPageQuery, needsBookingCountQuery} from '../../../pages/calendar/CalendarPage';
import {parseCalendarSearch} from '../../../pages/calendar/calendarSearch';

export const Route = createFileRoute('/_app/calendar/')({
  validateSearch: parseCalendarSearch,
  loaderDeps: ({search}) => ({date: search.date, view: search.view, staff: search.staff}),
  loader: ({context, deps}) => preload(context.queryClient, calendarPageQuery(deps), staffQuery, needsBookingCountQuery),
  component: CalendarRoute,
});

function CalendarRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <CalendarPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
