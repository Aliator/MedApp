# Review: PR #151, the scheduled job runner and the operational purge

**PR:** [#151](https://github.com/Aliator/MedApp/pull/151), branch `feature/scheduled-jobs`, reviewed at `76c41f7` on 18 November 2026. A quick review of the lease, the runner, the purge and the jobs controller; nothing was run. CI (full tier) passed: https://github.com/Aliator/MedApp/actions/runs/44307902172. No finding is high or critical.

Adds a job runner in the API (`ScheduledJobHost`, a `BackgroundService`) with a lease in SQL Server so one copy runs each job, the `ScheduledJobs` table, the Scheduled Jobs page and endpoints (`ManageSetup`), a `run-job` command, and its first job, the operational purge, shipped as a dry run (`Retention:Operational:DryRun = true`).

**Checked:** taking the lease is one conditional UPDATE (lease free or expired, job due) and renewing or completing matches the holder, so two copies can't both run a job. Every purge delete is date-filtered and confined to sessions, sign-in and security activity entries, notifications, notepads, challenges and email codes, in batches of 4,000; nothing holding patient records is touched, and with the dry run on the purge only counts. Only the exception's type name is stored or notified. `NotificationKind.ScheduledJobFailing = 40` is free on `main`.

**For the DPO before the dry run is turned off:** the notification purge deletes every notification older than the cutoff, whatever its kind, including assignment notifications whose title holds a pathway's title; the governance documents should say so.

**Finding, low, accepted:** `RunJobCommand` logs `exception.Message` for `NotFoundException` and `ConflictException` only, whose messages are the app's own.
