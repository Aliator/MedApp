import {createFileRoute} from '@tanstack/react-router';
import {ViewPatientPage} from '../../../../pages/patients/ViewPatientPage';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';
import {patientAppointmentsQuery} from '../../../../api/appointments';
import {patientTimelineOverviewQuery, patientTimelineQuery} from '../../../../api/timeline';

export const Route = createFileRoute('/_app/patients/$id/')({
  // The patient, their appointments, what's due, and the first page of their timeline (unfiltered, as the page opens).
  loader: ({context, params}) =>
    preload(
      context.queryClient,
      patientQuery(params.id),
      patientAppointmentsQuery(params.id),
      patientTimelineOverviewQuery(params.id),
      patientTimelineQuery(params.id),
    ),
  component: function ViewPatientRoute() {
    const {id} = Route.useParams();
    // Keyed by patient, so moving to another one starts with the timeline unfiltered.
    return <ViewPatientPage key={id} id={id} />;
  },
});
