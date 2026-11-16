import {createFileRoute} from '@tanstack/react-router';
import {appointmentsQuery} from '../../../api/appointments';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {parseNeedsBookingSearch} from '../../../pages/calendar/calendarSearch';
import {NeedsBookingPage, needsBookingQuery} from '../../../pages/calendar/NeedsBookingPage';

export const Route = createFileRoute('/_app/calendar/needs-booking')({
  validateSearch: parseNeedsBookingSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, appointmentsQuery(needsBookingQuery(deps, listSettingsFor(context.queryClient).pageSize))),
  component: NeedsBookingRoute,
});

function NeedsBookingRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <NeedsBookingPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
