import {createFileRoute} from '@tanstack/react-router';
import {can, whoamiQuery} from '../../../auth/auth';
import {CalendarPage, preloadCalendarPage} from '../../../pages/calendar/CalendarPage';
import {parseCalendarSearch} from '../../../pages/calendar/calendarSearch';

export const Route = createFileRoute('/_app/calendar/')({
  validateSearch: parseCalendarSearch,
  loaderDeps: ({search}) => ({date: search.date, view: search.view, staff: search.staff, who: search.who}),
  loader: async ({context, deps}) => {
    // Signed in already (the layout's guard), so this is cached: whether the team view is theirs to see.
    const me = await context.queryClient.ensureQueryData(whoamiQuery);
    return preloadCalendarPage(context.queryClient, deps, can(me, 'ArrangeMeetings'));
  },
  component: CalendarRoute,
});

function CalendarRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  // The grid keeps its own place as the address changes (the time at the top when the row length changes), so the
  // router mustn't put back the scroll position it saved for the address before.
  return <CalendarPage search={search} onSearchChange={(next, options) => void navigate({search: next, resetScroll: false, replace: options?.replace})} />;
}
