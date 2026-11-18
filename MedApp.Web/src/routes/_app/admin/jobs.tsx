import {createFileRoute} from '@tanstack/react-router';
import {scheduledJobsQuery} from '../../../api/jobs';
import {preload} from '../../../api/preload';
import {requireAdmin} from '../../../auth/auth';
import {ScheduledJobsPage} from '../../../pages/admin/ScheduledJobsPage';

/** Admin only: the jobs the API runs on a schedule, with Run Now. Anyone else is sent home. */
export const Route = createFileRoute('/_app/admin/jobs')({
  beforeLoad: requireAdmin,
  loader: ({context}) => preload(context.queryClient, scheduledJobsQuery),
  component: ScheduledJobsPage,
});
