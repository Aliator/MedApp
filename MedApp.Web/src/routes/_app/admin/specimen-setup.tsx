import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {SpecimenSetupPage, specimenSetupPageQueries} from '../../../pages/setup/SpecimenSetupPage';

/** For holders of ManageSetup: the specimen types and the labs specimens go to. */
export const Route = createFileRoute('/_app/admin/specimen-setup')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context}) => preload(context.queryClient, ...specimenSetupPageQueries),
  component: SpecimenSetupPage,
});
