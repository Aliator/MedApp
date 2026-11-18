# PR #182: Results sign-off

https://github.com/Aliator/MedApp/pull/182 (`feature/results-signoff`), head `b66fc871`, reviewed 18 November 2026 (quick review).

Reporting a specimen now creates a `Result` for its requesting clinician in the same save; a file can also be recorded as an external result. `SignOffResults` (Clinician, Senior clinician; not Admin) marks seen, signs off with one of six fixed actions (none pre-selected, Other needs a detail; signed off is final), takes over or reassigns. Entered in error (`CorrectRecords`, or the reporter within 24 hours) soft-deletes the file with an `AttachmentDeleted` audit entry and returns the specimen to Received. `StageGates.WhyResultsAwaiting` refuses finishing a `WaitsForResults` stage, and always the last stage, while a result is AwaitingSignOff or Seen. Migration `AddedResults` creates the table, two stage columns and the role grants.

Findings: none. Checked: `[RequirePermission]` on every `ResultsController` action (entered in error is `ViewPatients` on the endpoint with the 403 in the handler, `ResultWorkflow.MayMarkEnteredInError`); stored numbers are the ones `main` kept for results (audit 24 to 28, `NotificationKind` 26 to 28, `ActivityAction` 46), nothing renumbered; audit details hold no title or detail text; nothing interprets a result or suggests an action, and the web sign-off dialog starts with no action (`ResultDialogs.tsx:87`); `Up` only adds, foreign keys Restrict.

Remaining risk: every query handler's `RecordViewsAsync` call, the timeline mapping and the anonymiser were not traced line by line; `schema.d.ts` was edited by hand; `EndpointCoverageTests` and `RolePermissionsMigrationTests` run first in the phase's full CI.
