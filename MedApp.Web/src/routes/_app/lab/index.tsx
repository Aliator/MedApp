import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {LabHomePage, labHomeQueries} from '../../../pages/lab/LabHomePage';

/** The lab team's home (MC17), for anyone who can see patients; the side nav leads with it for lab staff. */
export const Route = createFileRoute('/_app/lab/')({
  beforeLoad: requirePermission('ViewPatients'),
  loader: ({context}) => preload(context.queryClient, ...labHomeQueries()),
  component: LabHomePage,
});
