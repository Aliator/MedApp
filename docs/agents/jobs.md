# Scheduled jobs

Jobs the API runs on a schedule, inside its own process (`MedApp.Application/Jobs`; plan: `docs/centre-phases-3-5-plan.md`, decisions 109 to 112). The first is the operational purge.

## The runner and the lease

- **A job** is an `IScheduledJob`, registered as a **scoped** service in `AddApplication`: a code name (lower-case words with hyphens, e.g. `operational-purge`: the row's key and the setting's), a title and description for the page, a `JobSchedule`, and `RunAsync(nowUtc, ct)` returning how many items it handled.
- **Schedules** (`JobSchedule`): `Every(interval)` (at least a minute; a new job runs at once) or `DailyAt(ukTime)` (UK clock time, never 01:00 to 03:00, which the clock changes skip or repeat). After a failure it's tried again sooner: the interval, or 30 minutes if that's sooner.
- **`ScheduledJobHost`** (a `BackgroundService` in the API) calls `IScheduledJobRunner.RunDueAsync` every 30 seconds. A round that fails (the database away, or not migrated) is logged as a warning once, then every 10 minutes, and tried again; the API keeps serving.
- **The lease:** for each due job, `ScheduledJobRepository.TryTakeLeaseAsync` takes it in **one UPDATE** (`WHERE Name = @name AND NextRunUtc <= @now AND (LeaseUntilUtc IS NULL OR LeaseUntilUtc < @now)`), and the runner runs the job only if that changed a row. So exactly one copy of the API (one runner) runs each job at a time, with no new pod, port or connection in the chart. The lease lasts 10 minutes and is renewed every 3 while the job runs; a runner that finds its lease gone cancels the run. A runner that dies leaves the lease to end, and another copy takes the job. Each runner's holder name is the machine (pod), process and a random part (`ScheduledJobRunner.Holder`). New rows are added with `INSERT ... WHERE NOT EXISTS (... WITH (UPDLOCK, HOLDLOCK))`, safe to race.
- **Each run gets a scope of its own,** marked as a job's (`ScheduledJobScope.IsJob`), where the activity log's actor is the system (`SystemActivityContext`, "medapp-jobs") instead of a request's user (`ServiceExtensions.ActivityContextFor` in the API).
- **Idempotent:** a job marks each item done in the same save as the work (or deletes it in the same statement), so a run cut short and run again repeats nothing. Stopping the API gives the lease up without recording a run.
- **What's recorded** (`ScheduledJobs`, one row per job, no personal data): next run, lease holder and end, last start and finish, outcome (`ScheduledJobOutcome`: `Succeeded = 0`, `Failed = 1`, `Skipped = 2`, stored by number, never renumbered), count, failures in a row, and the exception's **type name only**: never its message, which can quote data (the log has the type and stack trace, without the message).
- **After three failures in a row** the holders of `ManageSetup` are told once (`ScheduledJobFailing = 40`, a fixed title "Scheduled job failing", with the job's title and the count; `ScheduledJobNotifications`), until it succeeds again (`IPermissionHolders`).
- **`/health`** is Degraded (still a 200, so the readiness probe keeps the pod in service) while a job is switched on, not running, and more than two intervals past its next run, or the jobs can't be read (`ScheduledJobsHealthCheck`). Never Unhealthy.

## Settings

- `Jobs:Enabled` (default true): whether this copy runs jobs on their schedules. The integration tests turn it off and call `IScheduledJobRunner.RunNowAsync`.
- `Jobs:{Name}:Enabled` (default true), e.g. `Jobs:operational-purge:Enabled`: a job switched off is recorded as Skipped when it's due, and Run now refuses it (409).
- No chart change is needed: every setting has a default, and jobs use the API's existing database connection.

## Running one by hand

- **Administration, Scheduled Jobs** (`/admin/jobs`; the endpoints need `ManageSetup`, which the centre plan names for setup lists and scheduled jobs; the web route, like the other setup pages, is for admins): each job's schedule, status, last run and next, with **Run now** (`GET api/jobs`, `POST api/jobs/{name}/run`). Run now runs it in the request, taking the lease as a scheduled run does: 409 if another copy is running it, 404 for an unknown name; a failed run is a 200 showing the failure. Classified in `EndpointCoverageTests` (it reads no patient data).
- **`dotnet MedApp.API.dll run-job <name>`** runs one and exits (0 on success), e.g. through `kubectl exec`.
- **`medapp-dev`'s `run_job`** runs one against the local database, with the same runner and lease.

## The operational purge

`OperationalPurgeJob` (`operational-purge`, daily at 04:30 UK time) deletes, at the periods in `docs/governance/retention-schedule.md` (section 3), from the `Retention:Operational` settings:
- sessions created more than `SessionMonths` (12) ago;
- the activity log's **sign-in and security events** (`OperationalPurgeJob.SignInAndSecurityActions`: sign-ins and refusals, the two-factor step, sign-outs and "sign out everywhere", and every `ISecurityEventLog` event) older than `SignInEventMonths` (12);
- notifications older than `NotificationMonths` (6), read or not;
- notepads unchanged for `Notepad:KeepDays` (30);
- sign-in challenges and emailed codes expired more than `ExpiredCodeDays` (1) ago.

**Never patient records,** and nothing the schedule doesn't name for it: the activity log's entries about patients, its account and role changes, calendars, meetings and deleted files' contents all stay. It deletes in batches of 4,000 (`OperationalDataRepository`, a `DELETE TOP (n)` each, under SQL Server's lock escalation) and logs counts only.

**It ships as a dry run** (`Retention:Operational:DryRun`, true by default): it counts what it would delete and deletes nothing, because the periods are for the customer's DPO to confirm. Turning it off is a deploy setting (`docs/todo.md`).

## Patient reminders

`PatientRemindersJob` (`patient-reminders`, every 15 minutes) makes, sends and follows the delivery of texts and emails to patients while `PatientMessages:Mode` isn't Off: see [Calendar and appointments](calendar.md), Patient reminders. It changes only its own rows (`PatientMessages`), each saved as it goes, so a run cut short sends nothing twice; it logs counts and exception type names only.

`DataRequestRemindersJob` (`data-request-reminders`, daily at 07:00 UK time) tells the holders of ManagePrivacy about each open data request due within 7 days, once per due date: see [privacy](privacy.md), Data requests. It saves the notifications with the mark on each request (`DueSoonNotifiedFor`) in one save, so a run cut short tells nobody twice; it logs counts only.

## Service alerts

`ServiceAlertsJob` (`service-alerts`, every 15 minutes) finds, for each switched-on service alert rule, the items it reports and hasn't yet, and sends its owner one notification with the count, saved with the items' `MonitoringAlertsSent` rows in one save: see [Monitoring](monitoring.md). It reads referrals, results, appointments and specimens, changes only its own rows and notifications, and logs counts only. A convenience: nothing clinical relies on it (H-29).

## Adding a job

1. A class implementing `IScheduledJob` (in its area's `Application` folder), registered with `services.AddScoped<IScheduledJob, YourJob>()` in `AddApplication`. Pick a name that's never reused.
2. Make it idempotent and log counts only; never patient details or free text.
3. Unit tests for what it does; an integration test for its SQL. The runner's tests (`ScheduledJobRunnerTests`, and `ScheduledJobsTests` in the integration tests, with two runners against one database) cover the lease.
4. A job that deletes or changes data updates `docs/governance/` (the retention schedule, the DPIA's risk 33).
