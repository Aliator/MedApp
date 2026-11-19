import {createFileRoute} from '@tanstack/react-router';
import {hospitalsQuery} from '../../../api/hospitals';
import {preload} from '../../../api/preload';
import {specimenTypesQuery} from '../../../api/specimens';
import {requirePermission} from '../../../auth/auth';
import {RegisterHospitalRequestPage} from '../../../pages/hospitals/RegisterHospitalRequestPage';

/** Registering a hospital's request, for the lab (holders of ProcessSpecimens). */
export const Route = createFileRoute('/_app/hospital-requests/new')({
  beforeLoad: requirePermission('ProcessSpecimens'),
  loader: ({context}) => preload(context.queryClient, hospitalsQuery(false), specimenTypesQuery(false)),
  component: RegisterHospitalRequestPage,
});
