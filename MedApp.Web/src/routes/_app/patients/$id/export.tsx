import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {PatientExportPage} from '../../../../pages/patients/PatientExportPage';
import {preload} from '../../../../api/preload';
import {patientExportQuery} from '../../../../api/patients';

export const Route = createFileRoute('/_app/patients/$id/export')({
  beforeLoad: requirePermission('ExportPatientRecords'),
  loader: ({context, params}) => preload(context.queryClient, patientExportQuery(params.id)),
  component: function PatientExportRoute() {
    return <PatientExportPage id={Route.useParams().id} />;
  },
});
