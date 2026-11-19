import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {HospitalsPage, hospitalsPageQuery} from '../../../pages/hospitals/HospitalsPage';

/** For holders of ManageSetup: the hospitals that send the lab samples (MC18). */
export const Route = createFileRoute('/_app/admin/hospitals')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context}) => preload(context.queryClient, hospitalsPageQuery),
  component: HospitalsPage,
});
