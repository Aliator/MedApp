import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * A scheduled job (admins): work the API runs by itself on a schedule (the operational data purge, say), one copy at a
 * time. Its state is the last run's (when, what came of it, how many it handled) and the next one's.
 */
export type ScheduledJob = Schemas['ScheduledJobResponse'];

export const jobKeys = {
  all: ['jobs'] as const,
};

/** Every scheduled job, by title (admins). A handful, so not paged. */
export const scheduledJobsQuery = queryOptions({
  queryKey: jobKeys.all,
  queryFn: () => unwrap(api.GET('/api/jobs')),
});

/**
 * Runs a job now, in the request (admins), and returns its state after the run. An unknown job is a 404; one already
 * running on another copy of the API, or switched off, is a 409 saying so. Either way the list is read again.
 */
export function useRunScheduledJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => unwrap(api.POST('/api/jobs/{name}/run', {params: {path: {name}}})),
    onSuccess: (job) =>
      queryClient.setQueryData<ScheduledJob[]>(jobKeys.all, (jobs) => jobs?.map((j) => (j.name === job.name ? job : j))),
    onSettled: () => queryClient.invalidateQueries({queryKey: jobKeys.all}),
  });
}
