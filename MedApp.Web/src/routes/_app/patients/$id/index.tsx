import {createFileRoute} from '@tanstack/react-router';
import {ViewPatientPage} from '../../../../pages/patients/ViewPatientPage';
import {attachmentsQuery} from '../../../../api/attachments';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';
import {patientTimelineOverviewQuery, patientTimelineQuery} from '../../../../api/timeline';
import {whoamiQuery} from '../../../../auth/auth';
import {patientAccessQuery} from '../../../../components/patients/PatientAccessReport';

export const Route = createFileRoute('/_app/patients/$id/')({
  // The patient, their files, what's due, and the first page of their timeline (unfiltered, as the page opens); for
  // admins, the record's access report too.
  loader: ({context, params}) => {
    const isAdmin = context.queryClient.getQueryData(whoamiQuery.queryKey)?.isAdmin === true;
    return preload(
      context.queryClient,
      patientQuery(params.id),
      attachmentsQuery({patientId: params.id}),
      patientTimelineOverviewQuery(params.id),
      patientTimelineQuery(params.id),
      ...(isAdmin ? [patientAccessQuery(params.id)] : []),
    );
  },
  component: function ViewPatientRoute() {
    const {id} = Route.useParams();
    // Keyed by patient, so moving to another one starts with the timeline unfiltered.
    return <ViewPatientPage key={id} id={id} />;
  },
});
