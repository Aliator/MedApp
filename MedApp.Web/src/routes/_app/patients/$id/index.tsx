import {createFileRoute} from '@tanstack/react-router';
import {ViewPatientPage} from '../../../../pages/patients/ViewPatientPage';
import {attachmentsQuery} from '../../../../api/attachments';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';
import {patientAppointmentsQuery} from '../../../../api/appointments';
import {patientTimelineOverviewQuery, patientTimelineQuery} from '../../../../api/timeline';
import {can, whoamiQuery} from '../../../../auth/auth';
import {patientAccessQuery} from '../../../../components/patients/PatientAccessReport';

export const Route = createFileRoute('/_app/patients/$id/')({
  // The patient, their appointments and files, what's due, and the first page of their timeline (unfiltered, as the
  // page opens); for holders of ViewAudit, the record's access report too.
  loader: ({context, params}) => {
    const me = context.queryClient.getQueryData(whoamiQuery.queryKey);
    const canViewAudit = me !== undefined && can(me, 'ViewAudit');
    return preload(
      context.queryClient,
      patientQuery(params.id),
      patientAppointmentsQuery(params.id),
      attachmentsQuery({patientId: params.id}),
      patientTimelineOverviewQuery(params.id),
      patientTimelineQuery(params.id),
      ...(canViewAudit ? [patientAccessQuery(params.id)] : []),
    );
  },
  component: function ViewPatientRoute() {
    const {id} = Route.useParams();
    // Keyed by patient, so moving to another one starts with the timeline unfiltered.
    return <ViewPatientPage key={id} id={id} />;
  },
});
