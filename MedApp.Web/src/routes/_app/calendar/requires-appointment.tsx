import {createFileRoute} from '@tanstack/react-router';
import {appointmentsQuery} from '../../../api/appointments';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {parseRequiresAppointmentSearch} from '../../../pages/calendar/calendarSearch';
import {RequiresAppointmentPage, requiresAppointmentPageQuery} from '../../../pages/calendar/RequiresAppointmentPage';
import {clinicFor} from '../../../components/teams/ClinicPicker';

export const Route = createFileRoute('/_app/calendar/requires-appointment')({
  validateSearch: parseRequiresAppointmentSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(
      context.queryClient,
      appointmentsQuery(
        requiresAppointmentPageQuery(deps, listSettingsFor(context.queryClient).pageSize, clinicFor(context.queryClient, 'appointments', deps.clinic)),
      ),
    ),
  component: RequiresAppointmentRoute,
});

function RequiresAppointmentRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <RequiresAppointmentPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
