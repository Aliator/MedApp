# Review: PR #68, privacy features: an activity log, exports, anonymisation, flags and a retention review

**PR:** [#68](https://github.com/Aliator/MedApp/pull/68), branch `feature/privacy-features`, reviewed at `7b09528` on top of `main` at `0c86f70` (the merge of #65). `main` moved to `29d2d50` (the merge of #66) during the review, and merging it conflicts in seven files (see [How it was checked](#how-it-was-checked)).

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, the SQL that EF generates, a run of the built tools, or the GitHub API. **No finding is high or critical;** 5 are medium and 10 are low.

## Summary

The PR adds roadmap step 7's first privacy features: an activity log of who read or changed patient data (with an access report on each patient's page), a subject access export, Restricted and Disputed flags, anonymisation in place, a retention review list, and clearing the browser's storage at sign-out, with pins moved into the account's settings. The building blocks are sound: changes are logged in the same save, the log holds IDs rather than names, anonymisation is one transaction that reaches the audit trail, notifications and timeline, and every procedure command honours the restriction. The weak points are what the new records mean: the side nav's pin lookups are logged as views nobody made, several reads aren't logged at all although the DPIA now says every read is, the retention list treats every record as adult-general or child, restricting a record freezes treatment under way, and the JSON export carries staff network addresses.

## What changed, and why

210 files, 10,984 lines added and 366 removed. About 1,770 of the added lines are generated: the migration and its designer file (989), the snapshot (73), the API types (645) and the route tree (63).

- **Why:** the ICO's warning about record snooping (12 November 2026), NHS buyers' audit requirements, and the rights the governance pack documented as manual. The work was scoped so that today's features and permissions stay as they are.
- **The activity log** (`ActivityEntries`, `IActivityLog`):
  - **Reads:** `PatientViewed` (a patient's record), `PatientListed` (each patient on a page of patient search or retention review results), `PatientsSearched` (the names of the filters used and the count, never the terms), `ProcedureViewed` (against its patient) and `PatientExported`. A repeat of the same view by the same person within 5 minutes (`Activity:ViewWindowMinutes`) adds nothing.
  - **Changes:** patient create, edit (the names of the fields changed, never the values) and delete, saved in the same `SaveChangesAsync` as the change; restriction, dispute, keep and anonymise with their reasons.
  - **Accounts:** sign-ins, refused sign-ins (with the username tried and why), sign-outs, account creation and deletion, password resets and changes, and role changes (from and to). These are saved on their own, after the change.
  - **Who:** the user from the session's claims and the address Kestrel saw (`HttpActivityContext`).
  - **Reading it:** `GET api/activity/search` (admins), the Activity Log page, and an access report on the patient's page (its ten latest entries).
- **Restricted and Disputed** (`PUT api/patients/{id}/restriction` and `.../dispute`, admins, with a reason): a restricted or anonymised record can't be changed, nor its procedures (`PatientRules.EnsureCanChange`, applied by `GetForUpdateOrThrowAsync` and by creating a procedure; 409). The procedure response carries `ReadOnlyReason` with every `Can...` flag false. Disputed is a banner.
- **The subject access export** (`GET api/patients/{id}/export`, admins, `no-store`): the patient, procedures with stages and assignments, the procedure history (deleted procedures included), the activity entries about the patient, and notifications about their procedures, gathered by `PatientRecordRepository`. It shows as a printable page and downloads as JSON, and is itself logged.
- **Anonymisation** (`POST api/patients/{id}/anonymise`, admins, the full name typed, a reason): `PatientAnonymiser` replaces the name and email, keeps only the year of birth, gives each procedure its template's name, replaces notes, and scrubs `title`, `notes` and `reason` (and notes' `from` and `to`) in audit and activity details, and notification titles. Refused while a procedure is under way, while restricted, or if already done.
- **The retention review** (`GET api/patients/retention-review`, admins): patients past the minimum period (adults 8 years after the conclusion of treatment; children until their 25th birthday, or 26th if 17 then), computed in memory after SQL narrows to patients unchanged for 8 years. Keep (`POST .../retention/keep`) sets a review date up to 2 years ahead with a reason.
- **The browser:** sign-out and session expiry clear every `medapp.` key in `localStorage` and `sessionStorage` except the four visual settings; signing in loads the account's settings before opening the start page. Pins became an account setting (`medapp.pins`), with a one-time move from the old per-user keys.
- **The migration** `AddedActivityLogAndPatientPrivacyFlags`: seven nullable columns on `Patients`, and the `ActivityEntries` table with three indexes.
- **Docs:** `AGENTS.md` (a new "Activity log and patient privacy" section), `MedApp.Web/AGENTS.md`, the governance pack, the hazard log and safety case, the handbook and the to-do list.

## Design choices

- **A separate activity log, not one audit trail:**
  - **Instead:** extend the procedure audit to every read and change, as the handbook's step 7 plan proposed.
  - **Chosen because:** the procedure audit holds free text and per-procedure detail; the activity log holds IDs, so anonymising leaves almost nothing to scrub there, and it can be searched and kept on its own terms.
  - **Worse at:** two logs to search for one patient's history, and the old value of a patient edit can't be recovered from either.
- **Logging reads inside the query handlers:**
  - **Instead:** an endpoint filter or middleware keyed by route.
  - **Chosen because:** the handler knows exactly which patients it returned (a search's page, a procedure's patient).
  - **Worse at:** every caller of those endpoints records a view, whatever it wanted the data for, and a handler that forgets to call it records nothing. Both happen (findings 1 and 5). A GET that writes also fails when the write fails: patient pages and sign-ins return 500 if the log can't be written (fail closed, which isn't documented).
- **One entry per person per 5 minutes:**
  - **Instead:** every view.
  - **Chosen because:** a readable log. Like a visitors' book that takes one signature per person per five minutes.
  - **Worse at:** it loses how often a record was opened in that time. The check and the insert aren't atomic, so two requests at once (a page rendered on the server and the browser's own fetch) can both write.
- **Anonymise in place, not delete:**
  - **Instead:** destroy the record across every table.
  - **Chosen because:** the structure, statistics and audit trail survive, and the patient ID alone identifies nobody.
  - **Worse at:** the year of birth, templates, exact stage times and the staff involved stay. In a small clinic that can be enough to recognise someone, so the result is closer to pseudonymised than anonymous (finding 8).
- **Restriction freezes everything:**
  - **Instead:** hide the record from lists for most roles, or stop only edits to the contested details.
  - **Chosen because:** simple, enforced in one place, and easy to explain.
  - **Worse at:** treatment under way stops too (finding 2), and hiding the record, which the rights procedure planned, isn't built.
- **Retention worked out on reading, not stored:**
  - **Instead:** a stored due date per patient, updated as procedures close.
  - **Chosen because:** no extra column to keep in step, and periods come from configuration.
  - **Worse at:** sorting and paging happen in memory, every record is adult-general or child (finding 3), and the SQL narrowing depends on the shortest period being computed correctly (it is; see Detail).
- **Pins in the account's settings, cleared at sign-out:**
  - **Instead:** keep the per-user browser keys and exempt them from clearing.
  - **Chosen because:** a shared computer keeps nothing of the last user, and pins now follow people between browsers.
  - **Worse at:** a pin now depends on the save to the account reaching the server (finding 11).
- **The export as a page that records an export when opened:**
  - **Instead:** an explicit "Make export" action that records and returns a file.
  - **Chosen because:** one fetch serves the printable page and the download.
  - **Worse at:** a reload records another export, and the JSON carries more than the page shows (finding 4).

## How it was checked

- **Read:** the whole diff, the PR description, `AGENTS.md` and `MedApp.Web/AGENTS.md`, `docs/governance/`, the hazard log and safety case, and the code around the change: every procedure command and `ProcedureChange`, the repositories, the timeline and procedure search handlers, the side nav, the settings store and its sync, the router and query client, the server rendering forwarder, the chart's forwarded headers setting, and `tools/MedApp.DevMcp`.
- **Build:** `dotnet build MedApp.sln --configuration Release`: 0 errors, 1 warning (CS0414 in `MedApp.Client.Deprecated`, a file this PR doesn't touch). `tools/MedApp.DevMcp` builds.
- **.NET tests:** `dotnet test MedApp.UnitTests --configuration Release`: 1,129 passed. `dotnet test MedApp.IntegrationTests --configuration Release`: 196 passed in 2 min 4 s, against SQL Server 2022 in its own Testcontainers container, which was removed afterwards. The dev database on port 14330 wasn't touched.
- **Web:** `npm run lint` and `npm run typecheck` clean; `npx vitest run`: 930 tests in 100 files passed.
- **The migration:** `dotnet ef migrations has-pending-model-changes` reports no changes. `dotnet ef migrations script IndexedAuditEntriesByPatientAndTime AddedActivityLogAndPatientPrivacyFlags` is one transaction of seven `ADD` columns, one `CREATE TABLE` and three `CREATE INDEX`: additions only. Its ID sorts after `main`'s last migration (`20261116082648_IndexedAuditEntriesByPatientAndTime`), and #66 adds none.
- **medapp-dev:** the PR's build, and `main`'s (exported to a scratch folder and built there), were each started against an unreachable database (`127.0.0.1`, port 1), and sent the MCP `initialize` and a `create_patient` call. Both stop at start-up (finding 10; output under Detail).
- **CI:** run [43841033623](https://github.com/Aliator/MedApp/actions/runs/43841033623) on `7b09528` passed all seven jobs (`web`, `helm`, `unit-tests`, `image`, `integration-tests`, `e2e`, `coverage-comment`). The coverage comment gives .NET 99.8% of lines and 98.7% of branches.
- **Merging:** a trial merge of `origin/main` at `29d2d50` (aborted; nothing committed) conflicts in `AGENTS.md`, `MedApp.Web/src/api/schema.d.ts`, `AppSideNav.tsx` and its test, `listSearch.ts` and its test, and `routeTree.gen.ts`. They're source files, so they weren't resolved here. #66's insights return counts, not patient details, so it adds no unlogged read.
- **Not done:** a real browser, UI parity screenshots, the axe audit and a screen reader (they need the AppHost and the dev database); the migration against a database (the PR says it ran up and down on LocalDB); the address recorded for a page rendered on the server, which was read from the code, not observed.

## Findings

Most severe first. None is high or critical. All fifteen were open when this review was written.

### 1. Pinned patients and procedures are recorded as "viewed" whenever the side nav loads their names

- **Severity:** medium. **Confidence:** high (the code path read end to end; not run in a browser).
- **What could go wrong:** the side nav shows each pinned patient by name, and each pinned procedure by the patient's initials and its acronym. To get the names it calls the same endpoints a page does, `GET api/patients/{id}` and `GET api/procedures/patient-procedures/{id}`, which now record `PatientViewed` and `ProcedureViewed`. So whenever the nav loads or refreshes its pins (each full page load or new tab, signing in, any patient change made in that browser, which refreshes every patient query, deleting a procedure, folding the section away and back), each pinned record gets a "Patient viewed" or "Procedure viewed" entry, at most once per 5 minutes. A member of staff who pins five patients for the week and opens MedApp in a new tab 20 times a day gives each of them up to 20 "viewed" entries a day, none of them a real view. The access report, which admins use to spot snooping, and the export, which goes to the patient, show them all as views. Like a library stamping "read" on every book on someone's desk shelf each time they walk into the room. Investigating a complaint, or answering "who looked at my record?", then rests on entries that don't mean what they say.
- **Evidence:**
  - `MedApp.Web/src/components/navigation/AppSideNav.tsx:318-319` (`PatientNavItem` fetches `patientQuery`), `:334-335` (`ProcedureNavItem` fetches `procedureQuery`), `:423` (rendered for every pin, unfolded by default)
  - `MedApp.Application/Patients/Queries/GetPatientById/GetPatientByIdHandler.cs:20` and `GetPatientProcedureByIdHandler.cs:25` (each records a view)
  - `MedApp.Web/src/api/patients.ts:37` (writes refresh every `['patients']` query) and `useAfterPatientChange`
  - `docs/governance/privacy-notice-staff-template.md`: "MedApp also records which patient records and procedures you open"
  - No test covers what the nav's lookups record.
- **Fix:** give the nav a lookup that returns only labels and doesn't record a view, for example `GET api/patients/labels?ids=…` and one for procedures, recording `PatientListed` (de-duplicated, as search results are) or nothing. Add an API test that it writes no `PatientViewed`, and a web test that the nav uses it.
- **What happened to it:** open.

### 2. Restricting a record freezes its procedures mid-treatment, and no hazard covers it

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** an admin can restrict a record while its procedures are under way: `SetPatientFlagHandler` checks only that it isn't anonymised. From then on every procedure change is refused with 409: starting or finishing a stage, saving notes, assigning, and even blocking or cancelling with a reason, because no status change is offered. A patient contests their recorded date of birth; an admin restricts the record while it's checked (Article 18(1)(a)); the procedure's next stages (pre-assessment, consent) can't be recorded, and once its due date passes it sits overdue on My Tasks with nothing anyone but an admin can do. Like padlocking a folder half-way through a treatment, so the next step can't be written down. Article 18(2) still allows processing with the patient's consent or to protect another person's rights, so a total freeze is stricter than the law asks. The hazard log names the restriction only as a control (H-03), not as a cause of delayed care, and the restrict dialog doesn't say how many procedures are under way. Anonymisation, by contrast, is refused while any are.
- **Evidence:**
  - `MedApp.Application/Patients/Commands/SetPatientFlag/SetPatientFlagHandler.cs:24-42`
  - `MedApp.Application/Procedures/Common/ProcedureRepositoryExtensions.cs:14-21`; `ProcedureMapper.cs:43-73` (no status changes or actions offered)
  - `MedApp.Web/src/components/patients/PatientFlagDialog.tsx:15-21`
  - `docs/clinical-safety/hazard-log.md:156`
  - `AnonymisePatientHandler.cs:38-42` (refused while procedures are under way)
- **Fix:** add a hazard for care delayed because a restricted record can't be worked, with its controls. Show the number of procedures under way in the restrict dialog and make the admin confirm it. At least allow a restricted record's procedures to be blocked or cancelled with a reason. Consider letting an admin record consent for treatment to continue, with the restriction then stopping only edits to the patient's details. Which of these is a clinical safety decision.
- **What happened to it:** open.

### 3. The retention review treats every record as adult-general or child, so specialty records are listed 12 to 22 years early

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the retention schedule gives mental health 20 years, maternity 25 and oncology 30, by the template's class. The code has no classes yet: every patient is adult (8 years) or child (25th or 26th birthday). So an oncology patient whose treatment ended 8 years ago appears on Retention Review under "N records past their retention period", with Anonymise one click, a reason and a typed name away. The schedule's status column admits "no other classes", but the page, its empty state and the hazard log all say the list holds only records past their minimum period. A clinic using MedApp for oncology follow-up could anonymise records 22 years before the Code allows, and it can't be undone. Like one kitchen timer, set for the quickest dish, for everything in the oven.
- **Evidence:**
  - `MedApp.Application/Patients/Common/RetentionSchedule.cs:22-39`; `RetentionOptions.cs` (adult and child settings only)
  - `docs/governance/retention-schedule.md:32-40` (the classes) and `:108-110` (status: no other classes)
  - `MedApp.Web/src/pages/retention/RetentionReviewPage.tsx:112, 164`; `AnonymiseDialog.tsx:62` (no warning)
  - `docs/clinical-safety/hazard-log.md:724`: "The retention review lists only records past their minimum period"
- **Fix:** until classes exist, say on the page and in the anonymise dialog that only the adult and children periods are applied, and that a record in a longer class must be kept (Keep, with the class as the reason). Tell customers in the schedule to set `Retention:AdultYears` to their longest class meanwhile. Correct the hazard log's line. Classes per template are already on the to-do list.
- **What happened to it:** open.

### 4. The JSON export gives the patient staff network addresses, internal IDs and read times that the printable page doesn't show

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the export's access log is the raw activity log for the patient, with each entry's `ipAddress` and `userId`. Its history carries staff `userId`s, and its notifications say whom each was for and when they read it. The page shows only who and when, and warns to check for "other people's details" before giving it to the patient. A clinician checks the printed page; an admin downloads the JSON for the patient (portability); the patient receives every staff member's network address (a home address for anyone working remotely) and internal account IDs. Those are the staff's personal data, not the patient's, and nothing on screen shows they're there. Like photocopying a folder for a patient with the staff sign-in sheet still inside.
- **Evidence:**
  - `MedApp.Application/Patients/Common/PatientExportMapper.cs:36-49`; `ActivityEntryMapper.cs:16-31` (`UserId`, `IpAddress`)
  - `MedApp.Contracts/Patients/Responses/PatientExportResponse.cs:52-63` (`ReadAtUtc`); `PatientProcedureAuditEntryResponse.cs:12` (`UserId`)
  - `MedApp.Web/src/pages/patients/PatientExportPage.tsx:31-38` (downloads the whole response), `:89-93` (the warning), `:216-237` (the page shows username and time only)
  - `MedApp.IntegrationTests/Privacy/PatientPrivacyTests.cs:124-140` checks what's in the export, not what's left out
- **Fix:** give the export its own access entry without the address and user ID, and notifications without read times, keeping usernames (staff act under them professionally). Keep the full entries in the activity log for admins. Make the JSON match the page, and test that the export holds no `ipAddress`.
- **What happened to it:** open.

### 5. "Every read is recorded" isn't true yet: procedure lists, the list of all patients and the timeline aren't recorded per patient

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the DPIA lowers risk 1 (snooping) from high to medium because "every read of patient data" is recorded, and the clinical safety case says reads are recorded. Six read handlers record something; others that return patient data don't:
  - The procedure lists (My Tasks, Procedures and a patient's procedures) return each procedure's patient name and title, and can be filtered by patient name, without any entry.
  - `GET api/patients` returns every patient's name, date of birth and email, and records one "searched: all" entry, so it appears in no patient's access report.
  - The timeline and its overview record nothing. The patient page that shows them does, but the API doesn't need the page.

  A member of staff curious about a neighbour searches Procedures by the neighbour's name, sees what they're being treated for, and never appears in the neighbour's access report. The rule in `AGENTS.md` (a handler that returns a patient's data records it) isn't applied to the handlers that were already there.
- **Evidence:**
  - `SearchPatientProceduresHandler.cs:15-26`; `PatientProcedureSummaryResponse.cs:8-9`
  - `GetAllPatientsHandler.cs:21-23`; `PatientTimelineController.cs:18-38`
  - `docs/governance/dpia.md:18, 149, 155`; `docs/clinical-safety/clinical-safety-case-report.md:82`; `AGENTS.md:163`
  - The table of read endpoints under Detail.
- **Fix:** record `PatientListed` for each patient on a page of procedure search results, and for every patient `GET api/patients` returns (or retire that endpoint: only the deprecated client uses it); record opening a timeline as a view. Otherwise narrow the docs to what's recorded and keep risk 1 at high. A test listing every endpoint that returns patient data, with what it records, would stop the next one slipping through.
- **What happened to it:** open.

### 6. The disputed banner shows only on the patient's page

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** `PatientFlagBanners` is meant "for every page about the patient", and the rights procedure says every member of staff sees a dispute on a banner. It's used only on View Patient. The procedure page shows a restricted or anonymised reason but nothing about a dispute (the procedure response doesn't carry it), and neither do Edit Patient, the patient's procedures or the lists. Staff who work from My Tasks go straight to the procedure page and don't see that the patient disputes, say, a recorded allergy.
- **Evidence:** `MedApp.Web/src/components/patients/PatientFlagBanners.tsx:17-20`; `ViewPatientPage.tsx:93` (its only use); `ProcedurePage.tsx` (the `readOnlyReason` message only); `MedApp.Web/AGENTS.md:31`; `docs/governance/data-subject-rights.md:38`; `docs/clinical-safety/hazard-log.md:156`.
- **Fix:** add the patient's flags (with the dispute's reason) to the procedure response and show the banners on the procedure, edit and patient-procedures pages; or correct the docs.
- **What happened to it:** open.

### 7. A save already under way can undo a restriction or an anonymisation

- **Severity:** low. **Confidence:** high for the mechanism; it needs two requests within moments of each other.
- **What could go wrong:** patient writes load the row without tracking and save it with `Update`, which writes every column, and there's no concurrency check. If an edit, a flag change or a keep decision loaded the patient just before another admin anonymised or restricted it, its save writes the old values back: the name, email and full date of birth return and `AnonymisedAtUtc` is null again (the procedures stay scrubbed), or `RestrictedAtUtc` is null and the record can be changed. Rare, but silent. Like two people saving the same form: the second one's old copy wins. This is the known "last save wins" (risk R8), now reaching the privacy flags.
- **Evidence:** `MedApp.Infrastructure/Data/Repositories/PatientRepository.cs:85-90`; `UpdatePatientHandler.cs:21, 56`; `SetPatientFlagHandler.cs:21, 65`; `KeepPatientRecordHandler.cs:20, 30`; `PatientConfiguration.cs` (no concurrency token).
- **Fix:** a `rowversion` concurrency token on `Patient` (409 on a conflict); or, for edits and flags, update only the changed columns with `ExecuteUpdate` and `WHERE AnonymisedAtUtc IS NULL` (and `RestrictedAtUtc IS NULL` for edits).
- **What happened to it:** open.

### 8. Anonymisation keeps its own reason, and enough detail to point back at the patient

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** every reason recorded earlier is replaced, but the `PatientAnonymised` entry is written afterwards with the reason as typed. An admin who puts the patient's name in the reason ("anonymised at [name]'s request after a complaint") leaves it in the log for good, and `AGENTS.md` says anonymising replaces reasons, with no exception. The record also keeps the year of birth, every procedure's template, the exact time of every stage and who did it. In a small clinic, someone who knows a patient had a knee replacement on a given day can find the record. By the ICO's test (could a motivated intruder re-identify it?), that's pseudonymised, still personal data, rather than anonymous, and the docs don't say so.
- **Evidence:** `AnonymisePatientHandler.cs:48-52`; `PatientAnonymiser.cs:34-71`; `AGENTS.md:165`; `AnonymiseDialog.tsx:72-81`; `docs/governance/retention-schedule.md:113`.
- **Fix:** make the reason a choice (past its retention period, an erasure request, created in error) with an optional note the dialog says must hold no patient details. Say in `AGENTS.md` and the retention schedule that the result is pseudonymised and stays personal data while the patient ID and dates remain.
- **What happened to it:** open.

### 9. Refused sign-ins: rows anyone can add, kept for ever, holding whatever was typed as the username

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** each refused sign-in writes a row. There's no rate limit, no retention, and for unknown usernames no lockout, so a script can add rows without limit without signing in, and fill the log admins have to read. The username tried is stored and shown to admins, so a password typed into the username box by mistake ends up in the log. And `UserId` is null for every refused sign-in, though the entity's and actor's comments say it's null only for accounts that don't exist, so filtering by user misses refused attempts on a real account.
- **Evidence:** `MedApp.Application/Authentication/Sessions/Commands/Login/LoginHandler.cs:54-60` (`new ActivityActor(null, …)`); `MedApp.Domain/Activity/ActivityEntry.cs:16`; `ActivityActor.cs:5`; no rate limiter anywhere in `MedApp.API`; `docs/governance/retention-schedule.md`, section 3 (sign-in events at 12 months, not built).
- **Fix:** record the user ID when the account exists; add a rate limiter on `POST api/auth/login` (ASP.NET Core's, by address); purge sign-in events at 12 months, as the schedule says; consider recording only that an unknown username was tried.
- **What happened to it:** open.

### 10. medapp-dev doesn't start: the activity log needs a service it doesn't register

- **Severity:** low. **Confidence:** high (run).
- **What could go wrong:** the dev tool calls `AddApplication`, which now registers `IActivityLog`, which needs an `IActivityContext`; only the API provides one. The tool checks its services at start-up (it runs as Development), so it stops before serving anything, naming every patient, account and role command. It already stopped on `main`, for a missing `INotificationPublisher` (since live notifications were added), so this PR adds a second gap: fixing the first won't bring the tool back. The PR edits `DemoDataTools` without running it.
- **Evidence:** the run under Detail; `tools/MedApp.DevMcp/Program.cs:51-52`; `MedApp.Application/Common/Extensions/ServiceExtensions.cs` ("the host provides IActivityContext"); `MedApp.API/Common/Extensions/ServiceExtensions.cs:52`.
- **Fix:** in the tool, register an `IActivityContext` whose actor is the tool (no user ID, no address) and a no-op `INotificationPublisher`, and add a test that builds the tool's services with validation on.
- **What happened to it:** open.

### 11. A pin or setting changed just before signing out, or whose save failed, is lost

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** settings reach the account 800 ms after the last change. Signing out leaves the signed-in layout, which cancels a pending save, and then clears the browser. Before this PR, pins lived only in the browser and survived sign-out. Now a pin made just before signing out, or while the account couldn't be reached (a failed save is ignored, and nothing is saved before the account's settings have loaded), is gone from both places. Rare, but a quiet change to a feature meant to stay as it was.
- **Evidence:** `MedApp.Web/src/app/SettingsSync.tsx:30-41`; `MedApp.Web/src/auth/auth.ts:126-135`; `MedApp.Web/src/components/navigation/pins.ts:14, 76-82`.
- **Fix:** save any pending change before signing out (in `useLogout`, before leaving the layout), and test pin-then-sign-out.
- **What happened to it:** open.

### 12. Retention dates: deleted procedures don't count, and leap-day birthdays come a day early

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** the conclusion of treatment is the latest change to the record or to a procedure that still exists. An archived procedure that an admin then deletes no longer counts, though its audit entries remain. A patient registered at 16 and treated until 17½, whose archived procedure was later deleted, counts as 16 at the conclusion, and is listed at 25 instead of 26. Separately, `DateOnly.AddYears` puts a 29 February birthday on 28 February in other years, while in England such a birthday falls on 1 March, so those records are listed a day early, and ages tip a day early at the 17 and 18 boundaries.
- **Evidence:** `MedApp.Infrastructure/Data/Repositories/PatientRepository.cs:64-83`; `RetentionSchedule.cs:22-48`; `DeletePatientProcedureHandler` (the audit stays).
- **Fix:** include the patient's latest procedure audit entry (by `PatientId`, which outlives deletion) in the conclusion; treat a 29 February birthday as 1 March in other years; test both.
- **What happened to it:** open.

### 13. Retention Review's row buttons don't say which patient, and a failed flag change loses the reason

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** each row has Review, Keep and Anonymise buttons with the same names in every row. Tabbing through, a screen reader hears "Anonymise, button" with no patient, so acting on the wrong row is easy, though the dialog then names the patient. Review also repeats the name's link. When setting a flag fails, the dialog closes and the typed reason is lost, with the error on the page rather than on the field (`errors` is never passed).
- **Evidence:** `MedApp.Web/src/pages/retention/RetentionReviewPage.tsx:101-107`; `ViewPatientPage.tsx:195-207`; `PatientFlagDialog.tsx` (the `errors` prop).
- **Fix:** give each button an accessible name that includes the patient's name ("Keep the record of [name]"), or move the actions into a row menu as other lists do; keep the flag dialog open on an error and pass the field errors.
- **What happened to it:** open.

### 14. The tests guard the restriction by convention, and the canary doesn't reach every table

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the restriction holds for every procedure command because each loads through `GetForUpdateOrThrowAsync`. Tests check notes, delete and create; a new handler that called `GetForUpdateAsync` directly would bypass the guard and nothing would fail. The anonymisation integration test puts its canary in names, titles, notes and a cancel reason, but not in a restriction, dispute or keep reason, or a notification title (there's no assignment), so those are checked only in memory. Nothing tests what the side nav records (finding 1), what the export leaves out (finding 4), or what a stale save writes back (finding 7).
- **Evidence:** `MedApp.UnitTests/Application/Procedures/Common/ProcedurePatientRestrictionTests.cs:74-114`; `MedApp.IntegrationTests/Privacy/PatientPrivacyTests.cs:69-121`.
- **Fix:** a test that no handler but the extension calls `GetForUpdateAsync` (by reflection), or a restriction test per command; put the canary in every free-text field the anonymiser covers, assign the procedure, and check `GET api/activity/search` and `GET api/notifications` for it too.
- **What happened to it:** open.

### 15. Docs that don't match the code

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the docs say the wrong thing about addresses and logging:
  - `MedApp.API/Common/Authentication/HttpActivityContext.cs:6-10`, `docs/todo.md:34` and `docs/governance/dspt-readiness.md:71` say that behind the ingress the address recorded is the ingress's. The chart already sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED` (`deploy/helm/medapp/templates/api.yaml:82-84`, which makes ASP.NET Core take the client's address from the ingress's `X-Forwarded-For`), so requests from the browser carry the browser's address; only pages rendered on the server record the loopback address, as `docs/governance/data-subject-rights.md:74` correctly says.
  - `docs/architecture.md:1253` (Traceability) still says "Patients and accounts aren't audited; reads aren't logged."
- **Fix:** correct each.
- **What happened to it:** open.

## Remaining risks

- **The activity log grows for ever:** no retention for any of its entries yet, and `PatientListed` adds a row per patient per list page per person every 5 minutes.
- **Access reports fill with list appearances:** the patients at the top of the Patients list's default order get an "In search results" entry from everyone who opens it, so their access report's ten latest entries can hide the real views.
- **A failure to write the log stops the read:** patient pages, searches and sign-ins return 500 if `ActivityEntries` can't be written. That's fail closed, and it isn't written down anywhere.
- **Pages rendered on the server** record the loopback address, not the browser's.
- **The log isn't tamper-evident:** anyone with database access can change it (a ledger table is on the to-do list).
- **Not covered by anonymisation or the export:** saved searches that name a patient, downloaded exports, backups and logs (all documented).
- **A large export:** a patient who is often listed has thousands of access entries, all in one page and one file.
- **The merge with `main`** (#66) conflicts in seven files, among them the generated API types and route tree, which need regenerating; every check should run again after it.
- **Not seen in a browser** here: no screenshots, axe audit or screen reader.

## Say it like this

"MedApp now records who opens, searches and changes patient records, shows admins an access report on each patient, can export everything held about a patient, can mark a record restricted or disputed, can anonymise a record in place, and lists records past their retention period for an admin to keep or anonymise. Signing out also clears the browser. The plumbing is solid: changes and their log entries are saved together, the log holds IDs rather than names, and anonymising reaches the audit trail, notifications and timeline in one step. What needs fixing is what the records mean. The side nav's pinned patients are logged as 'viewed' every time it loads them; some reads, like the procedure lists, aren't logged at all although the documents say every read is; the retention list treats every record as adult or child, so specialty records show up years early; restricting a record also stops treatment that's under way, with no hazard written down for it; and the downloaded export gives the patient staff network addresses that the printed page doesn't show."

## Detail

### What each read records

| Endpoint | Returns | Records |
|---|---|---|
| `GET api/patients/{id}` | A patient | `PatientViewed`, also when the side nav looks up a pinned patient's name (finding 1) |
| `GET api/patients/search` | A page of patients | `PatientsSearched` (filter names and count, only when filters are used) and `PatientListed` for each |
| `GET api/patients` | Every patient | One `PatientsSearched` with `filters: ["all"]`; no patient's report shows it |
| `GET api/patients/retention-review` | A page of patients due for review | `PatientListed` for each |
| `GET api/patients/{id}/export` | Everything about a patient | `PatientExported`, every time (no de-duplication) |
| `GET api/procedures/patient-procedures/{id}` | A procedure, with its patient's name | `ProcedureViewed`, against the patient; also for pinned procedures |
| `GET api/procedures/patient-procedures/search` | Procedures with patient names and titles; filters by patient name or ID | Nothing |
| `GET api/patients/{id}/timeline` and `/overview` | The patient's procedure history and what's due | Nothing |
| `GET api/procedures/audit/search` (admins) | Audit entries with titles and notes | Nothing |
| `GET api/activity/search` (admins) | Entries with patients' current names | Nothing |
| `GET api/notifications` | The user's notifications, with procedure titles | Nothing |

### What anonymising changes

| Where | Field | After |
|---|---|---|
| `Patients` | `FirstName`, `LastName`, `Email` | "Anonymised", "Patient", `anonymised@anonymised.invalid` |
| `Patients` | `DateOfBirth` | 1 January of the same year |
| `Patients` | Restriction, dispute and keep (with their reasons) | Cleared |
| `PatientProcedures` | `Title` | The template's name, or "Anonymised procedure" |
| `PatientProcedures`, `PatientProcedureStages` | `Notes` | "[Removed when the record was anonymised]" (empty stays empty) |
| `PatientProcedureStages` | `Name`, `Description`, `Instructions` | Kept: copied from the stage definition, not about the patient |
| `PatientProcedureAuditEntries` | `ProcedureTitle` | The new title |
| `PatientProcedureAuditEntries` | `Details`: `title`, `notes`, `reason`, and their `from` and `to`; a notes change's top-level `from` and `to` | Placeholder |
| `ActivityEntries` | `Details`: `reason` | Placeholder, except in the `PatientAnonymised` entry written afterwards (finding 8) |
| `Notifications` | `Title` | The new title; `Message` ("x assigned you…") kept |
| `UserSettings` | Saved searches naming the patient | Not covered (documented) |

All of it is loaded tracked and saved in one `SaveChangesAsync`, which EF wraps in a transaction, so a failure part-way changes nothing. The checks (not anonymised, not restricted, no procedure under way, the typed name) run on the same loaded copy before anything changes.

### The retention narrowing is safe for the defaults

SQL keeps only patients with no open procedure and whose record and procedures were all last changed by the end of the UK day `ShortestYears` ago. With the defaults that's 8 years: adults are due exactly 8 years after the conclusion; a child under 17 at the conclusion is due on their 25th birthday, at least 8 years and a day later; a 17-year-old on their 26th, also at least 8 years and a day later. So nobody due today is left out by the narrowing. If the settings are changed, `ShortestYears` takes the smallest of the three, and never goes below 0. The weaknesses are in what counts as the conclusion (finding 12) and the missing classes (finding 3), not in the narrowing.

### medapp-dev at start-up

Both builds were started with `ConnectionStrings__DefaultConnection` set to `Server=127.0.0.1,1;Database=ProbeNoSuchDb;…`, so nothing could reach a real database. The PR's build stops with:

```
Unhandled exception. System.AggregateException: Some services are not able to be constructed
  (… GetPatientProcedureByIdHandler': Unable to resolve service for type
   'MedApp.Application.Activity.Common.IActivityContext' while attempting to activate
   'MedApp.Application.Activity.Common.ActivityLog'.)
  (… AssignPatientProcedureHandler': Unable to resolve service for type
   'MedApp.Application.Notifications.Common.INotificationPublisher' …)
  … the same IActivityContext error for every patient, account, role, sign-in and retention handler
```

`main`'s build stops with the `INotificationPublisher` errors only.

### The merge with `main`

`git merge --no-commit origin/main` at `29d2d50` (then aborted) reported conflicts in `AGENTS.md`, `MedApp.Web/src/api/schema.d.ts`, `MedApp.Web/src/components/navigation/AppSideNav.tsx`, `AppSideNav.test.tsx`, `MedApp.Web/src/pages/listSearch.ts`, `listSearch.test.ts` and `MedApp.Web/src/routeTree.gen.ts`. #66 adds an admin page to the side nav and list searches for its pages, as this PR does, so both sides must be kept; the API types and route tree should be regenerated (`npm run gen:api` and the router's generator) rather than merged by hand.
