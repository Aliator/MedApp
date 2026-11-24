import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {problemMessage} from '../../api/errors';
import {scheduledJobsQuery, useRunScheduledJob, type ScheduledJob} from '../../api/jobs';
import {useAccessibility} from '../../app/accessibility';
import {useAnnounce} from '../../app/announcements';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {formatDateTime} from '../../utils/dates';
import {DisabledReason} from '../../components/shared/DisabledReason';

/** A label on a job's status: its words, colour and (when "Icons on statuses" is on) icon. Never colour alone. */
interface JobLabel {
  label: string;
  color: TokenColor;
  icon: IconType;
}

const RUNNING: JobLabel = {label: 'Running', color: 'blue', icon: AppIcon.StatusInProgress};
const OFF: JobLabel = {label: 'Off', color: 'gray', icon: AppIcon.StatusBlocked};
const OVERDUE: JobLabel = {label: 'Overdue', color: 'red', icon: AppIcon.Overdue};

/** The last run's outcome, as the API names it (null: never run). */
const OUTCOMES: Record<string, JobLabel> = {
  Succeeded: {label: 'Succeeded', color: 'green', icon: AppIcon.Success},
  Failed: {label: 'Failed', color: 'red', icon: AppIcon.StatusCancelled},
  Skipped: {label: 'Skipped', color: 'gray', icon: AppIcon.StatusUnassigned},
};
const NEVER_RUN: JobLabel = {label: 'Not run yet', color: 'gray', icon: AppIcon.StatusUnassigned};

/**
 * What a job's status shows, in order: Running (a copy of the API holds it now), Off (switched off in the API's
 * settings), Overdue (its next run is more than two intervals late, as `/health` reports), then the last run's outcome.
 */
export function jobLabels(job: ScheduledJob): JobLabel[] {
  return [
    job.isRunning ? RUNNING : null,
    job.enabled === false ? OFF : null,
    job.isOverdue ? OVERDUE : null,
    job.lastOutcome ? (OUTCOMES[job.lastOutcome] ?? {label: job.lastOutcome, color: 'default', icon: AppIcon.Info}) : NEVER_RUN,
  ].filter((l): l is JobLabel => l !== null);
}

/** How many a run handled, in words: "1 item", "1,284 items". */
export const countWords = (count: number) => `${count.toLocaleString('en-GB')} ${count === 1 ? 'item' : 'items'}`;

/**
 * The kind of error a run failed with, in plain words, from the exception's type name ("SqlException" gives "database
 * error"); the type itself is in the server's logs.
 */
export function errorWords(errorType: string | null | undefined): string | null {
  if (!errorType) return null;
  if (/Sql|DbUpdate|Database/i.test(errorType)) return 'database error';
  if (/Timeout|Cancel/i.test(errorType)) return 'timed out';
  if (/Http|Socket|Smtp|Request/i.test(errorType)) return 'connection error';
  return 'unexpected error';
}

/** Failures in a row, in words, with the last one's kind: "Failed 3 times in a row (database error)". */
export function failureWords({consecutiveFailures = 0, lastErrorType}: ScheduledJob): string | null {
  if (consecutiveFailures <= 0) return null;
  const times = consecutiveFailures === 1 ? 'Failed once' : `Failed ${consecutiveFailures} times in a row`;
  const error = errorWords(lastErrorType);
  return error ? `${times} (${error})` : times;
}

/** Why Run Now can't be pressed for a job, or null when it can. */
export function whyCantRun(job: ScheduledJob): string | null {
  if (job.isRunning) return 'Already running';
  if (job.enabled === false) return 'Switched off in the server settings';
  return null;
}

/** What a run now did, in words: "Operational data purge succeeded: 12 items." or "… failed (database error)." */
export function runOutcomeWords(job: ScheduledJob): string {
  const title = job.title ?? job.name ?? 'The job';
  switch (job.lastOutcome) {
    case 'Succeeded':
      return job.lastCount != null ? `${title} succeeded: ${countWords(job.lastCount)}.` : `${title} succeeded.`;
    case 'Failed':
      return job.lastErrorType ? `${title} failed (${errorWords(job.lastErrorType)}).` : `${title} failed.`;
    case 'Skipped':
      return `${title} was skipped.`;
    default:
      return `${title} ran.`;
  }
}

/** A run's outcome in two words, the headline over `runOutcomeWords`. */
const runOutcomeLabel = (job: ScheduledJob) =>
  job.lastOutcome === 'Succeeded'
    ? 'Job succeeded'
    : job.lastOutcome === 'Failed'
      ? 'Job failed'
      : job.lastOutcome === 'Skipped'
        ? 'Job skipped'
        : 'Job ran';

function JobStatus({job}: {job: ScheduledJob}) {
  const {flags} = useAccessibility();
  return (
    <HStack gap={1} wrap="wrap" vAlign="center">
      {jobLabels(job).map((l) => (
        <Token key={l.label} size="sm" label={l.label} color={l.color} icon={flags.statusIcons ? <Icon icon={l.icon} /> : undefined} />
      ))}
    </HStack>
  );
}

function LastRun({job}: {job: ScheduledJob}) {
  const failures = failureWords(job);
  const when = job.lastFinishedAtUtc
    ? formatDateTime(job.lastFinishedAtUtc)
    : job.lastStartedAtUtc
      ? `Started ${formatDateTime(job.lastStartedAtUtc)}`
      : 'Never';
  return (
    <VStack gap={0}>
      <Text type="body">{when}</Text>
      {job.lastCount != null && (
        <Text type="supporting" color="secondary">
          {countWords(job.lastCount)}
        </Text>
      )}
      {failures && (
        <Text type="supporting" weight="semibold">
          {failures}
        </Text>
      )}
    </VStack>
  );
}

type JobRow = ScheduledJob & Record<string, unknown>;

/** What the page shows after Run Now: the outcome, until the next run or a dismissal. */
interface Outcome {
  status: 'success' | 'warning';
  /** "Job succeeded", "Job failed"... */
  label: string;
  message: string;
}

/**
 * Scheduled Jobs (`/admin/jobs`, admins): the work the API runs by itself on a schedule (the operational data purge,
 * say), each with its schedule, its state (running, off, overdue, the last outcome), its last run (when, how many it
 * handled, failures in a row and the last error's type) and its next run. **Run Now** runs one at once, in the request,
 * after a confirmation that says what it does (a live purge deletes data), then shows what came of it. The API refuses
 * one already running on another copy, or switched off (a 409, shown in its words); the button says why beforehand.
 */
export function ScheduledJobsPage() {
  const {data = [], isPending, isError, refetch, isRefetching} = useQuery(scheduledJobsQuery);
  const run = useRunScheduledJob();
  const announce = useAnnounce();
  const [confirming, setConfirming] = useState<ScheduledJob | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const runNow = (job: ScheduledJob) => {
    run.mutate(job.name ?? '', {
      onSuccess: (result) => {
        const message = runOutcomeWords(result);
        setOutcome({status: result.lastOutcome === 'Failed' ? 'warning' : 'success', label: runOutcomeLabel(result), message});
        announce(message);
        setConfirming(null);
      },
      onError: (e) => {
        setError(problemMessage(e, `${job.title ?? 'The job'} could not be run. Please try again.`));
        setConfirming(null);
      },
    });
  };

  const columns: DataTableColumn<JobRow>[] = [
    {
      key: 'title',
      header: 'Job',
      width: proportional(3, {minWidth: 240}),
      renderCell: (job) => (
        <VStack gap={0}>
          <Text type="body" weight="semibold">
            {job.title}
          </Text>
          <Text type="code" size="sm" color="secondary">
            {job.name}
          </Text>
          <Text type="supporting" color="secondary">
            {job.description}
          </Text>
        </VStack>
      ),
    },
    {key: 'schedule', header: 'Schedule', width: proportional(1, {minWidth: 130}), renderCell: (job) => <Text type="body">{job.schedule}</Text>},
    {key: 'status', header: 'Status', width: pixel(160), renderCell: (job) => <JobStatus job={job} />},
    {key: 'lastRun', header: 'Last run', width: pixel(210), renderCell: (job) => <LastRun job={job} />},
    {
      key: 'nextRun',
      header: 'Next run',
      width: pixel(160),
      renderCell: (job) => (
        <Text type="body" color={job.nextRunUtc ? undefined : 'secondary'}>
          {job.nextRunUtc ? formatDateTime(job.nextRunUtc) : 'Not scheduled'}
        </Text>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(130),
      renderCell: (job) => {
        const why = whyCantRun(job);
        const isThisRunning = run.isPending && run.variables === job.name;
        // Its own size, never squeezed: as a card (a narrow window), it sits beside the title and description.
        return (
          <StackItem>
            <DisabledReason reason={why ?? undefined}>
              <Button
                label={why ? `Run Now: ${job.title}, ${why.charAt(0).toLowerCase()}${why.slice(1)}` : `Run Now: ${job.title}`}
                variant="secondary"
                size="sm"
                icon={<Icon icon={AppIcon.RunNow} />}
                tooltip={why ? undefined : `Run ${job.title} now`}
                isDisabled={why !== null}
                isLoading={isThisRunning}
                onClick={() => {
                  setError(null);
                  setOutcome(null);
                  setConfirming(job);
                }}>
                Run Now
              </Button>
            </DisabledReason>
          </StackItem>
        );
      },
    },
  ];

  const subtitle = isPending ? 'Loading jobs...' : data.length === 1 ? '1 job' : `${data.length} jobs`;

  return (
    <Page
      title="Scheduled Jobs"
      subtitle={subtitle}
      fill
      message={
        error ? (
          <AlertMessage label="Job not run" message={error} />
        ) : (
          isError && <AlertMessage label="Scheduled jobs not loaded" message="The scheduled jobs could not be loaded. Please try again." />
        )
      }
      actions={
        <IconButton
          label="Refresh"
          tooltip="Refresh"
          variant="secondary"
          icon={<Icon icon={AppIcon.Refresh} />}
          isLoading={isRefetching}
          onClick={() => {
            setError(null);
            void refetch();
          }}
        />
      }
      dialogs={
        <ConfirmDialog
          isOpen={confirming !== null}
          title={`Run ${confirming?.title ?? 'the job'} now?`}
          message={confirming?.description}
          icon={AppIcon.RunNow}
          intent="warning"
          confirmText="Run Now"
          isProcessing={run.isPending}
          onConfirm={() => confirming && runNow(confirming)}
          onCancel={() => setConfirming(null)}>
          <Text type="body" color="secondary">
            It runs now, as it would on its schedule, and this waits until it has finished.
          </Text>
        </ConfirmDialog>
      }>
      <DataTable
        data={data as JobRow[]}
        columns={columns}
        idKey="name"
        isLoading={isPending}
        skeletonRows={2}
        search={
          <VStack gap={3} hAlign="stretch">
            <Text type="body" color="secondary">
              These jobs run automatically on a schedule. If one fails three times in a row, everyone who manages setup is
              told.
            </Text>
            {outcome && <AlertMessage status={outcome.status} label={outcome.label} message={outcome.message} onDismiss={() => setOutcome(null)} />}
          </VStack>
        }
        emptyState={
          <EmptyState
            title="No scheduled jobs"
            description="The API has no jobs to run on a schedule."
            icon={<Icon icon={AppIcon.ScheduledJobs} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}
