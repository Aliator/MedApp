import {createFileRoute} from '@tanstack/react-router';
import {scheduledJobsQuery} from '../../../api/jobs';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {ScheduledJobsPage} from '../../../pages/admin/ScheduledJobsPage';

/** For holders of ManageSetup: the jobs the API runs on a schedule, with Run Now. Anyone else is told the page needs it. */
export const Route = createFileRoute('/_app/admin/jobs')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context}) => preload(context.queryClient, scheduledJobsQuery),
  component: ScheduledJobsPage,
});
