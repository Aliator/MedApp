# Review: PR #97, the demo data set rebuilt as a believable clinic's history

**PR:** [#97](https://github.com/Aliator/MedApp/pull/97), branch `feature/demo-data`. Reviewed at `c2264d6`, compared with `main` at `3d081d4` (also the merge base).

**Reviewed:** 16 November 2026, as an independent, adversarial review, without the author's conclusions. Every finding below was checked against the code, a test run, or the seed itself, run against throwaway databases. **One finding is high:** the new integration test fails on every UK Saturday and Sunday, and `integration-tests` is a required check. 3 findings are medium and 6 are low.

## Summary

`medapp-dev`'s `reset_demo_data` now wipes the local database and fills it with about 18 months of an invented clinic, made by re-running each record's history through the app's own rules with a clock the seed moves. The data are carefully made and plainly synthetic, but the story is told relative to "now", so what it makes depends on the day it runs: at a weekend nobody is locked out (and the new test fails), on some weekdays a visit is double-booked, and every leave day breaks the calendar's no-overlap rule.

## What changed, and why

38 files, 5,652 lines added and 548 removed. About 4,700 of the added lines are the new seed in `tools/MedApp.DevMcp/DemoData/`, about 720 are tests and about 190 are docs.

- **Why:** the old seed (60 patients and about 80 procedures) left most pages thin, and made nothing for the features added since: calendars, visits, monitoring, files, two-factor and the privacy features. A demo, the UI parity screenshots and manual testing all need every page to show something believable.
- **The seed, in steps** (`DemoDataSeeder`): wipe, staff, stage definitions and templates, calendars, monitoring rules, plans, patients, then the procedures' histories, privacy events, files, the activity log, settings and today's sign-ins. Each step is its own file.
- **A moving clock** (`SeedServices`, `SeedClock`): the seed builds its own copy of the app's services in which "now" (`TimeProvider`) and "who's acting" (`IActivityContext`) are set before each call. Like a film set where the clock on the wall is changed between takes, so every scene is stamped with the right time and actor. Email goes to a sender that drops it (`DiscardedEmail`).
- **Replay, mostly in memory** (`ProcedureReplay`, `DemoProcedures`): each procedure's story (created, assigned, stages started, visits booked, attended, notes, stages finished, completed, archived; and the twists: blocked, cancelled, unlocked, reassigned, deleted) is applied with the shared domain helpers the handlers use (`ProcedureChange`, `StageVisits`, `AppointmentChange`, `AppointmentNotifications`, `MonitoringRuleMatcher`), then saved in batches. Much faster than sending each of the ~20,000 changes through a real handler.
- **Real handlers where only they will do:** accounts, roles, email confirmation, two-factor, the password reset, notification choices, patients, monitoring rules, restriction and dispute, anonymisation, the retention "keep", exports, deleting procedures and the leaver's account, file uploads and downloads, settings, and today's sign-ins all go through MediatR at the moved clock.
- **Booking** (`ScheduleAllocator`, `DemoCalendars`): a model of each person's diary, built from the stored calendar rows through the app's own `CalendarProjection`, hands out free times so appointments never overlap. Like a receptionist's appointment book where every booked slot is crossed through.
- **What it makes:** 13 accounts (one deleted), about 250 patients (NHS numbers only from the 999 test range, `@example.com` emails), 15 stage definitions (some with SNOMED CT codes), 6 templates with distinct weekly volumes, about 650 procedures, 2,500 appointments, 400 notifications, 13,000 activity entries and, when Azurite answers, 36 files.
- **The tool's entry point** (`Tools/DemoDataTools.cs`) shrank to the contract: confirm, snapshot, run.
- **Files' storage:** `devmcp.json` gives the tool Azurite's published development connection string, and the AppHost now pins Azurite's blob port to 14331 (bound to 127.0.0.1), so the tool can find it. Files are uploaded only if that store is on this machine (a loopback address) and answers.
- **Tests:** 53 unit tests for the pure helpers (allocator, volume curves, note builder, reference data, synthetic files); `DemoDataSeedTests`, an integration test that runs the whole seed twice on a database of its own in the test SQL Server and checks its invariants. `MedApp.IntegrationTests` now references the tool's project.
- **Docs:** `docs/demo-data.md` (a guided tour), the tool's README, `AGENTS.md`, and a note in `deploy/kind/README.md` that snapshots carry no file bytes.

## Design choices

- **Replaying history in memory, not through every handler:**
  - **Instead:** send every change as a real command through MediatR, as the account and privacy steps do.
  - **Chosen because:** about 20 to 30 seconds instead of, at a guess, several minutes, and the shared helpers already hold the rules (statuses, visits, audit, appointment history).
  - **Worse at:** the parts of each handler that aren't in a shared helper (how a procedure is built from a template, the audit details of "created" and "details updated", the assignment message, the monitor's filtering, the activity log's sign-in entries) are copied into the seed. Nothing notices when the handler changes and the copy doesn't (finding 4).
- **A moved clock instead of fixing timestamps afterwards:**
  - **Instead:** run everything at the real time, then rewrite the dates.
  - **Chosen because:** every table a handler touches (audit, activity, appointment history, notifications) gets consistent times in one go.
  - **Worse at:** code that reads the real clock directly (`CreatePatientHandler`'s dates, `IdentityUserService`'s `CreatedAt`) needs patching afterwards, which the seed does with direct updates.
- **A story relative to "now":**
  - **Instead:** a fixed date, so the same seed always gives the same rows.
  - **Chosen because:** the data always look current ("overdue", "today's diary", "last week" all mean something).
  - **Worse at:** the data depend on the day and time of the run: weekends, Thursdays and some times of day give different shapes, and the integration test inherits that, because it uses the real time (findings 1 and 2).
- **An allocator built from the calendar rows:**
  - **Instead:** book visits at random times and drop clashes afterwards.
  - **Chosen because:** no appointment overlaps anything, and preferred day types (theatre, endoscopy) come out naturally.
  - **Worse at:** it checks appointments, not the stored blocks themselves; the leave blocks it reads were never checked against the templates (finding 3), and one appointment wasn't entered in it at all (finding 2).
- **A fixed Azurite port and the published key in `devmcp.json`:**
  - **Instead:** ask Aspire or Docker for the emulator's current port, or read the API's connection string at run time.
  - **Chosen because:** simple, and works while the AppHost is stopped (the container is persistent), like SQL Server's 14330.
  - **Worse at:** a fixed port can be taken by something else (then Azurite can't start, and the API waits for it), and the emulator's key now sits in the repo (it's published by Microsoft, so this exposes nothing new, but secret scanners may flag it).
- **Checking the whole seed in the integration tests:**
  - **Instead:** unit tests of the pure helpers only.
  - **Chosen because:** only the real database shows whether the rows obey the app's rules (visits before finishing, no clashes, Insights).
  - **Worse at:** about 40 seconds more in CI (two seedings), and a required check that now depends on when it runs.

## How it was checked

- **Read:** every file in `tools/MedApp.DevMcp/DemoData/`, the tool's entry point, `Program.cs`, `devmcp.json`, the AppHost change, the tests and the docs. Compared the copied logic with the handlers it mirrors: creating, assigning, starting, finishing, editing and changing the status of procedures; creating and booking appointments; `ProcedureMonitor`; `NotificationSender`; sign-in (both steps); patient search; anonymisation (`PatientAnonymiser`); flags; calendar block commands and `CalendarClashes`. Checked the saved settings against the web app's keys and shapes (`favourites.ts`, `pins.ts`, `listSettings.tsx`, the list URL keys).
- **Build:** `dotnet build tools/MedApp.DevMcp`: 0 warnings, 0 errors.
- **Unit tests:** `dotnet test MedApp.UnitTests --filter "FullyQualifiedName~Tools.DemoData|FullyQualifiedName~DevMcp"`: **56 passed**.
- **Integration test:** `dotnet test MedApp.IntegrationTests --filter "FullyQualifiedName~DemoData"` on Monday 16 November, 22:10 UTC: **7 of 7 passed** in 3.5 minutes. `SameSeed_GivesTheSameCounts` ran first (43 s). Testcontainers removed its containers afterwards; nothing was left running.
- **CI:** the PR's run [43964473696](https://github.com/Aliator/MedApp/actions/runs/43964473696) passed every job on Monday 21:52 UTC. `integration-tests` took 6 min 51 s; its log also shows `SameSeed_GivesTheSameCounts` running before the other six.
- **The seed at 12 different moments (15 runs):** a scratch console program outside the repo ran `DemoDataSeeder.RunAsync` against throwaway LocalDB databases (`MedApp_ReviewSeed_*`), with no attachment store configured (so files were skipped and no Azurite was touched), then queried them with the app's own `CalendarClashes`, `WorkingHours` and Insights query. Moments: Monday 14:00, Saturday 11:00, Sunday 23:30, Monday 00:20 (after midnight), Sunday 25 October (the autumn clock change), the Monday after it, Monday 30 March (after the spring change), Tuesday, Wednesday, Thursday and Friday afternoon of the coming week, and Monday 4 January 2027. Every run finished without an error; every database was dropped afterwards (checked: none left). Results in [Detail](#the-seed-at-12-moments).
- **SNOMED CT codes:** the six codes used (3457005, 225390008, 11429006, 390906007, 91251008, 58000006) are the right concepts for their terms.
- **Read-only look at Docker:** the AppHost's SQL Server and Azurite publish their ports on 127.0.0.1 only; the running AppHost (from `main`) still has Azurite on a random port.
- **Not run here:** the seed against the dev `MedApp` database (out of bounds for this review); file uploads against the AppHost's Azurite (they ran only against the test Azurite, inside the integration test); the full unit and integration suites; the web checks; the UI itself.

## Findings

Most severe first. All are open at the reviewed commit.

### 1. At a weekend nobody is locked out or signed in, so the integration test fails every Saturday and Sunday

- **Severity:** high. **Confidence:** high (the code path is unconditional; reproduced at three weekend moments).
- **What could go wrong:**
  - **The test:** someone pushes a PR on a Saturday. `DemoDataSeedTests` seeds at the real time; `SignInTodayAsync` sees a weekend and returns before it locks out the healthcare assistant. `Story_HasItsSpecialCases` expects exactly one locked-out account and finds none, so `integration-tests` fails. It's a required check, so no PR can merge at a weekend, whatever it changes, until someone re-runs it on a weekday. Like a fire drill that only works on weekdays, checked by an inspector who also visits at weekends.
  - **The demo:** run at a weekend, the reset leaves `c.dunmore` unlocked and nobody signed in, yet the summary's note for `c.dunmore` still says "Locked out after wrong passwords, until tomorrow evening", and the tour promises a locked account and live sessions.
- **Evidence:**
  - `tools/MedApp.DevMcp/DemoData/DemoActivity.cs:192-193`: returns `lockedOut = null` on a non-working day, before the lockout at `:222-248`.
  - `MedApp.IntegrationTests/Tools/DemoDataSeedTests.cs:61` seeds at `DateTime.UtcNow`; `:206` counts accounts with `LockoutEnd > DateTimeOffset.UtcNow`; `:214` expects exactly 1.
  - `AGENTS.md:486`: `integration-tests` is a required check on `main`.
  - `tools/MedApp.DevMcp/DemoData/DemoDataSeeder.cs:130` (the note), `docs/demo-data.md:133-134` (locked out; "most staff signed in today").
  - Reproduced: seeding at Saturday 14 November 11:00, Sunday 15 November 23:30 and Sunday 25 October 10:00 gave `today = { signedIn: [], lockedOut: null }`, and no account had a `LockoutEnd`. Every weekday run locked `c.dunmore` until 18:00 the next day.
- **Fix:**
  - **Lock out the account on any day:** move the wrong-password run above the weekday check. The sign-in handler has no weekday rule, so the attempts at "now minus 20 minutes" work any day.
  - **Pin the test's clock:** seed at a fixed weekday moment (the review harness ran the seed at fixed past and future moments without trouble), and compare the lockout with that moment, not `DateTimeOffset.UtcNow`. Add a second, cheap case at a weekend moment that checks only what should hold then.
  - **Docs:** say that at a weekend the diary is Monday's and nobody has signed in today.
- **What happened to it:** fixed in the PR (`53f56f9`, `38d693b`). The healthcare assistant is locked out on any day, until 18:00 tomorrow, and the summary says until when. Sign-ins stay on working days, and not before 07:30. The integration tests now run the seed at fixed moments instead of the real time: Saturday 14 November 2026 at 11:00, Monday 26 October 2026 at 00:20 (just after midnight, the day after the autumn clock change) and Friday 20 November 2026 at 16:30 (seeded twice). The lockout is checked against the seed's moment. A new test checks that the sessions match the summary's sign-ins, and that nobody signs in at a weekend or before 07:30.

### 2. The nurse's freed slot isn't reserved, so a visit can be double-booked on top of it

- **Severity:** medium. **Confidence:** high (reproduced twice).
- **What could go wrong:** the seed removes `s.brightwell`'s admin time on the day before the diary day and books a telephone review at 17:00 in its place. It never tells the allocator, so that half hour still looks free, and a procedure visit can be booked over it. Like pencilling a patient into a gap in the book without crossing the slot out: the next receptionist books the same gap.
  - **On the calendar:** two appointments overlap on the nurse's diary, which the app never allows.
  - **In CI:** `Calendars_NoAppointmentOverlapsAnotherOrABlock_OrFallsOutsideTheWorkingDay` then fails. Whether it happens depends on the moment the seed runs, so the required check fails now and then, and a re-run later usually passes.
- **Evidence:**
  - `tools/MedApp.DevMcp/DemoData/DemoProcedures.cs:87-96`: `BookFreedSlot` calls `Standalone` without `world.Allocator.Reserve`, though its comment (`:84-85`) says the slot is "reserved before the procedures are replayed". `BookStandalone` reserves first (`:121`, `:137`).
  - `tools/MedApp.DevMcp/DemoData/DemoCalendars.cs:177-184`: the block exception frees 17:00 to 18:00 in the allocator's view.
  - Reproduced: seeding at Friday 20 November 16:30 gave a "Telephone review" 17:00 to 17:30 and a procedure visit 16:30 to 17:30 on Thursday 19 November, both Attended, for `s.brightwell`; the app's `CalendarClashes` reports both. Seeding at Tuesday 17 November 10:00 gave 16:15 to 17:15 against 17:00 to 17:30 on Monday 16 November. 2 of the 12 moments tried.
- **Fix:** in `BookFreedSlot`, call `world.Allocator.Reserve(freed.StaffUserId, freed.StartUtc, freed.EndUtc)` before `Standalone`. Pinning the test's clock (finding 1) also makes any such failure repeatable instead of intermittent.
- **What happened to it:** fixed in the PR (`6668c3d`). A standalone booking reserves its time itself, so the freed slot can't be booked twice, and the allocator's `Reserve` refuses a clash. `BookFreedSlot_BooksTheNursesFreedTime_AndReservesIt` runs at 12 moments, and all 12 fail with the fix removed.

### 3. Every leave and training day sits on top of the day's template blocks, which the app refuses

- **Severity:** medium. **Confidence:** high (read in the code; 102 of 106 stored blocks clash in every run).
- **What could go wrong:** the seed writes each day of leave or training as one 08:00 to 18:00 block, straight into the table, over a day whose template still has Lunch, Admin time or a Ward round. The app's rule is that a block may not overlap anything, template blocks included: through the app, the owner would first set the day to "no template", then add the leave. Like writing "on holiday" across a printed timetable without crossing out lunch.
  - **On the calendar:** the tour sends people to `k.ellison`'s leave next month and `d.ashworth`'s training day. Those days show "Annual leave" drawn side by side with "Lunch" and "Admin time" (the grid shares the width between overlapping items), as if the person were on leave and at lunch at once. Trying to recreate that day in the app gets a 409, for example "That time overlaps Lunch, 12:30 to 13:15."
  - **The moved lunch:** when the diary day is a Thursday, `r.calloway`'s lunch is moved to 13:45 on a Friday, whose Admin day template has an MDT meeting until 14:00 and admin time from 14:00, so it overlaps both.
  - **The claim:** the PR and docs say there are no calendar clashes; the integration test only checks appointments.
- **Evidence:**
  - `tools/MedApp.DevMcp/DemoData/DemoCalendars.cs:261-266` (`Leave`: 08:00 to 18:00 on each working day), `:268-275` (`Block` checks only other stored blocks), `:129-141`, `:150-163` (past leave, next month's leave, this week's day off, the training day), `:169-175` (the moved lunch: the exception removes lunch, the new block isn't checked against the rest of the day).
  - `MedApp.Application/Calendar/Common/CalendarClashes.cs:39-43`: template blocks count as clashes. `CreateCalendarBlockHandler.cs:37` and `ChangeTemplateBlockForDayHandler.cs:38` refuse them.
  - `AGENTS.md:178`: "A new or moved appointment or block may not overlap ... a block (stored or from a template)".
  - Measured: in all 10 runs that counted them, `CalendarClashes.Find(data, block, ignoreBlockId: block.Id)` reported a clash for 101 leave blocks and the training block, of 106 stored blocks; the Thursday run added the moved lunch ("Lunch": 1).
- **Fix:**
  - **Leave as the app records it:** for each leave or training day, also store a `CalendarDayOverride` with `DayTemplateId = null` (what "Apply a day template to this day: none" saves), then the block.
  - **The moved lunch:** check the new time with `CalendarClashes.Find` against that day's projected template (as `TemplateBlockOn` already builds it), or only move it on a clinic or theatre day.
  - **Test it:** in `DemoDataSeedTests`, run `CalendarClashes.Find(..., ignoreBlockId: b.Id)` for every stored block, as the review harness did.
- **What happened to it:** fixed in the PR (`6668c3d`). A leave or training day first gets a day override with no day template (as "This day's template: none" does), then its block. Every new block is checked with the app's `CalendarClashes` against that day's calendar, and the moved lunch goes only into free time. The integration test now checks every stored block, not just appointments, against each person's whole calendar.

### 4. The replay copies parts of eight handlers, and nothing notices when they change

- **Severity:** medium. **Confidence:** high that no test compares the copies with the handlers; medium on how soon it bites.
- **What could go wrong:** the shared helpers (`ProcedureChange`, `StageVisits`, `AppointmentChange`) are used for real, but around them the seed re-types what each handler does. Next month someone adds a field to the "created" audit entry, changes the assignment notification's wording, or changes how the monitor picks its admins. The app writes the new shape; the demo data keep the old one, so the timeline, the audit log and the notifications look subtly different from real use, and every screenshot taken from the demo shows the old behaviour. No test fails. `AGENTS.md` tells future contributors the opposite: "A change to a rule it replays (a stage's visit, a calendar clash, an audit format) shows up there."
- **Evidence:** each pair is a copy, not a call:
  - `ProcedureReplay.cs:67-100` and `:141-152` (building the procedure and its "created" audit) against `CreatePatientProcedureHandler.cs:40-84`.
  - `ProcedureReplay.cs:516-526` (assignment notification, its message at `:523`) against `AssignPatientProcedureHandler.cs:42-54` (`:50`).
  - `ProcedureReplay.cs:446-468` (finishing a stage) against `FinishProcedureStageHandler.cs:36-63`.
  - `ProcedureReplay.cs:532-558` (details edits) against `UpdatePatientProcedureHandler.cs:27-60`.
  - `DemoWorld.cs:179-215` (rules switched on then, owners who were admins then) against `ProcedureMonitor.cs:53-68`.
  - `DemoActivity.cs:80` (a hard-coded 5-minute view window) against `ActivityOptions.ViewWindowMinutes`; `:109-119` (sign-in entries) against `LoginHandler` and `LoginTwoFactorHandler.cs:121-126`; `:136-145` (search filter names) against `PatientSearch.FiltersUsed`.
  - `DemoDataSeedTests` checks visits, appointment clashes, bookings, Insights, identifiers, special cases and counts; none of these compares an audit entry's details, a notification's text or an activity entry's format with what a handler writes.
  - `AGENTS.md:435`, last sentence.
- **Fix:**
  - **Share instead of copy:** move each copied piece into the Application layer as a small pure helper that the handler and the seed both call (for example building a procedure from a template, the assignment notification, a details edit, the monitor's matching given the enabled rules and the admins).
  - **Or prove the copies:** a parity test that plays one short story (create, assign, start, book, attend, finish, block, unblock, edit, cancel) through the real handlers and through `ProcedureReplay` at the same clock, and compares the audit details, appointment history and notifications, ignoring IDs.
  - **Either way:** reword `AGENTS.md:435` to say which rules are shared and which are copied.
- **What happened to it:** fixed in the PR (`a1947e3`, `53f56f9`). The create, update, finish and assign handlers and the monitor now call shared helpers in Application (`ProcedureCreation`, `ProcedureDetails`, `StageFinishing`, `AssignmentNotifications`, `ProcedureMonitor.FindMatchesAsync`), and so does the seed. They're extractions of the handlers' own code, unchanged in behaviour. Notification choices use the domain's `NotificationPreferences`, and the activity window and search filters come from the app's own options and `PatientSearch.FiltersUsed`. The sign-in and search entries are still copied, and `DemoActivityParityTests` runs the real login, two-factor and search handlers and compares what they write. `AGENTS.md` says which is shared and which is checked.

### 5. The Insights test can't fail: every response already differs by what it was asked

- **Severity:** low. **Confidence:** high (read in the response type; the data were checked separately and do differ).
- **What could go wrong:** `Insights_DifferForEveryPeriodAndTemplate` serialises 21 responses and checks they're all different. But each response carries its own question (`Weeks`, `TemplateId`, `From`), so they always differ, even if a change made every chart identical. The second check compares the array properties within a period, but `StageTimes` carries each template's ID and name, and the `"[][]"` filter can never match (there are four arrays, and `OverdueByPriority` always lists every priority). Like checking that two letters are different because their envelopes have different addresses.
- **Evidence:** `MedApp.IntegrationTests/Tools/DemoDataSeedTests.cs:163`, `:166`, `:170-171`; `MedApp.Contracts/Procedures/Insights/Responses/ProcedureInsightsResponse.cs:11`, `:14`, `:17`, `:40`, `:76`, `:78`. The review harness removed those echoed fields (and the weeks' Monday dates) in 5 runs: the 21 pictures were still all different, so the claim is true today; the test just doesn't prove it.
- **Fix:** before comparing, clear `Weeks`, `TemplateId`, `From`, `To`, each `StageTimes` entry's `TemplateId` and `TemplateName`, and each throughput week's `WeekStart`; drop the dead `"[][]"` filter.
- **What happened to it:** fixed in the PR (`38d693b`). The question each response repeats (weeks, template, from and to, names, week starts) is cleared before comparing, so only the data is compared.

### 6. The test marked "last" runs first

- **Severity:** low. **Confidence:** high (NUnit's rule, and both the CI log and a local run show it).
- **What could go wrong:** `SameSeed_GivesTheSameCounts` has `[Order(100)] // last: it seeds the database again`. NUnit 3 starts tests that have an `Order` before tests that don't, so it runs first, and the other six check the second seeding, not the first. Harmless today (the counts match), but the comment misleads: a later test comparing database rows with `_summary` (the first run's summary) would compare two different seedings.
- **Evidence:** `MedApp.IntegrationTests/Tools/DemoDataSeedTests.cs:226-233`. CI run 43964473696's log and the local run list `SameSeed_GivesTheSameCounts` first.
- **Fix:** move the re-seed to a fixture (and database) of its own, which also proves the wipe of a full database on its own terms; or put `Order` on every test, the re-seed with the highest; and fix the comment.
- **What happened to it:** fixed in the PR (`38d693b`). No `Order` attributes: the re-seed fixture seeds twice in its set-up, checks the second seeding, and compares its counts with the first.

### 7. The history shows patients before they existed, and the anonymised record after it left the review

- **Severity:** low. **Confidence:** high (counted in every run).
- **What could go wrong:**
  - **Listed before registered:** the activity log's searches and procedure-list views pick from today's patients, so someone registered last week is "listed" on days up to 60 days ago. A patient's access report then shows staff seeing them before they were registered: 398 to 607 such entries per run.
  - **Booked before registered:** standalone appointments pick any current patient, including those registered in the last four weeks, for bookings made up to 60 days ago. Their timeline shows an appointment booked and attended before they were registered: 1 to 3 appointments per run.
  - **The anonymised record:** the lead "lists" every imported record due for review each Monday, including the one anonymised three weeks ago, which the review no longer shows (`docs/demo-data.md:145` says it's "no longer listed"): 2 or 3 entries per run.
- **Evidence:** `tools/MedApp.DevMcp/DemoData/DemoActivity.cs:58-60`, `:127-129`, `:146-147`, `:158-161`; `DemoProcedures.cs:116-122`, `:176-180`.
- **Fix:** only list, view or book patients registered by that moment (`CreatedUtc <= at`), and skip the anonymised record after its date.
- **What happened to it:** fixed in the PR (`57d0beb`). Views, lists, searches and standalone bookings only touch a patient from their registration until their anonymisation, and an integration check says so. This check also caught an export and possibly disputes picking a patient not yet registered, now fixed.

### 8. The summary names patients by name alone, and names repeat

- **Severity:** low. **Confidence:** high (counted in 5 runs).
- **What could go wrong:** the summary's `privacy` and `tour` say which records to open as "First Last". With 60 first names and 56 surnames for about 250 people, 7 to 12 names are shared in every run. In the Monday 16 November run, `privacy.restricted` starts with "Iris Walker", and two patients are called Iris Walker; the tour's "visit never booked" also points at "Iris Walker". A presenter searching for the name may open the wrong record, and show an unrestricted patient as the restricted one.
- **Evidence:** `tools/MedApp.DevMcp/DemoData/DemoTour.cs:192-195`; `DemoPrivacy.cs:114-120`; `DemoPatients.cs:56-72`, `:99-100` (names drawn independently; only emails are kept unique).
- **Fix:** add the NHS number (or the ID, as `anonymisedPatientId` already does) to each named record, or redraw a name already taken, as the email is.
- **What happened to it:** fixed in the PR (`57d0beb`). Every patient's name is unique (redrawn if taken), and a test checks each name in the summary's `privacy` section finds exactly one record of the right kind.

### 9. The wipe leaves the admins' sign-in rules from before

- **Severity:** low. **Confidence:** high (read in the code).
- **What could go wrong:** if anyone had turned on "Require two-factor for admins" on the local database, the reset keeps it (`SecuritySettings` isn't wiped). After the reset, `admin` (no two-factor) is held at sign-in until it sets two-factor up, and the security page names a "last changed by" account that may have been deleted. The same seed then gives a different first experience, depending on what the database held.
- **Evidence:** `tools/MedApp.DevMcp/DemoData/DemoWipe.cs:247-286` deletes neither the row nor anything about it; `MedApp.Infrastructure/Data/MedAppDbContext.cs:47`; `MedApp.Domain/Authentication/SecuritySettings.cs:4-5` ("With no row, every rule is off, as a new database starts"); `MedApp.Application/Authentication/TwoFactor/Common/TwoFactorRequirement.cs:13`.
- **Fix:** delete the `SecuritySettings` row in the wipe, and list it in the README's "It removes".
- **What happened to it:** fixed in the PR (`57d0beb`). The wipe deletes the `SecuritySettings` row; with no row every rule is off, the documented default. The test adds a "require two-factor for admins" row before seeding and checks it's gone.

### 10. The docs promise about 280 patients; the seed makes about 250

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the counts read from the summary don't match the docs (247 to 274 across 10 runs; 251 for Monday 16 November), and `docs/demo-data.md:151` explains a choice of "280 rather than 250" that the code doesn't make. Separately, `docs/demo-data.md:20` says Azurite answers on 14331 "from this branch on", which won't mean anything once merged.
- **Evidence:** `AGENTS.md:435`, `tools/MedApp.DevMcp/README.md:61`, `tools/MedApp.DevMcp/Tools/DemoDataTools.cs:17`, `docs/demo-data.md:151`, the PR description; the `counts.patients` values in [Detail](#the-seed-at-12-moments).
- **Fix:** say "about 250" (or raise the waiting-list and repeat-referral numbers to reach 280); say "since #97" instead of "from this branch on".
- **What happened to it:** fixed in the PR (`ef8bb83`). The docs give ranges checked against runs: about 260 patients, about 650 to 670 procedures and 2,500 to 2,600 appointments, 30 seconds to a minute a run. "From this branch on" is gone.

## Remaining risks

- **"Local" means "a loopback address":** `DevGuard` (unchanged here) accepts any `localhost` or `127.0.0.1` server, and the files check accepts any loopback blob endpoint. A port-forward or SSH tunnel to a shared database or storage account would pass both, and the reset would wipe it (after a snapshot). The integration test itself only ever uses its own Testcontainers database and Azurite.
- **The fixed Azurite port** can collide with something already on 14331, and then the AppHost waits for an Azurite that can't start. It's the same pattern as SQL Server's 14330, and documented, but it's a new way for the AppHost to fail.
- **The emulator key in `devmcp.json`** is Microsoft's published one and only opens the local emulator (the kind chart turns that account off), but a secret scanner may flag it. Anything local can read the dev files with it, as before; only the port is new.
- **Orphaned files grow:** each reset leaves the previous run's 36 files in Azurite, unreferenced (documented, and kept so the pre-reset snapshot can find them).
- **No way back but the snapshot:** the seed isn't one transaction, so a failure part-way leaves a half-made database. The `pre-reset` snapshot is there, but the error doesn't say so.
- **CI time:** the two seedings add about 40 seconds in CI (90 locally) to a job capped at 15 minutes.
- **Clinical realism:** notes carry plausible doses and preparation instructions. They're in synthetic records only, and the files say they're synthetic, but screenshots of notes don't. Children can be given adult-sounding notes (a joint injection for osteoarthritis, a back programme "back at work").
- **The restricted record is often the "visit never booked" one** (both are chosen for being quiet), so the tour can send a presenter to book a visit on a record that refuses new bookings.
- **Not yet run against the dev database or looked at in the UI;** the PR says this comes after merging.

## Say it like this

"`reset_demo_data` replays 18 months of an invented clinic through the app's own rules, with a clock it moves, so every page has consistent, believable history in about half a minute. The main choice was to replay most of it in memory through the shared domain helpers instead of calling every handler: it's fast, but some handler logic is copied, and because the story is relative to 'now', the test has to pin the clock, which the review found it didn't, so it fails at weekends."

## Detail

### The seed at 12 moments

Each run: a new LocalDB database, all migrations, `DemoDataSeeder.RunAsync(seed 2026, that moment)`, then queries, then the database dropped. Times are UK time. "Block clashes" counts stored blocks that `CalendarClashes.Find` (ignoring the block itself) says overlap something.

| Moment (UK) | Signed in / locked out | Appointment clashes | Block clashes (of 106) | Patients |
|---|---|---|---|---|
| Mon 16 Nov, 14:00 | 11 / `c.dunmore` | 0 | 102 | 251 |
| Sat 14 Nov, 11:00 | none / none | 0 | 102 | 267 |
| Sun 15 Nov, 23:30 | none / none | 0 | 102 | 255 |
| Mon 16 Nov, 00:20 | 10 / `c.dunmore` | 0 | 102 | 248 |
| Sun 25 Oct (clock change), 10:00 | none / none | 0 | 102 | 247 |
| Mon 26 Oct, 07:30 | 11 / `c.dunmore` | 0 | 102 | 259 |
| Mon 30 Mar (after the spring change), 08:10 | 11 / `c.dunmore` | 0 | 102 | 259 |
| Thu 19 Nov, 10:00 | 11 / `c.dunmore` | 0 | 103 (the moved lunch) | 274 |
| Fri 20 Nov, 16:30 | 11 / `c.dunmore` | **2** (the freed slot) | 102 | 266 |
| Mon 4 Jan 2027, 09:00 | 11 / `c.dunmore` | 0 | 102 | 259 |

A second harness (Insights, names and the freed slot only) ran at Mon 16 Nov 14:00, Tue 17 Nov 10:00, Wed 18 Nov 10:00, Fri 20 Nov 16:30 and Sat 14 Nov 11:00. The freed slot overlapped a visit on Tuesday's run (16:15 to 17:15 against 17:00 to 17:30, on Monday 16 November) and again on Friday's, and not on the others.

In every run of the first harness:
- **No history in the future:** no audit entry, appointment history entry, notification or activity entry after "now".
- **Visits:** every finished stage needing a visit had an attended one before it finished, but the two audited bypasses.
- **Notifications:** none for an account that doesn't exist.
- **Restriction:** no procedure or appointment change after a record was restricted.
- **Appointments for closed procedures:** none still Requested, none still Booked ahead.

### Insights, with the echoed fields removed

The second harness ran the Insights query for each of the 21 combinations (3 periods, all templates and each of 6), as the test does, at 5 moments. With `Weeks`, `TemplateId`, `From`, each stage time's template ID and name, and each week's start date cleared, all 21 responses were still different every time (21 of 21). The test's `"[][]"` filter matched nothing.

### Duplicate names, as counted

Patients (not anonymised) sharing a full name, per run: 7 names (14 patients), 8 (17), 7 (14), 11 (24), 12 (24). Monday 16 November's included "Iris Walker" twice; that run's `privacy.restricted` was `["Iris Walker", "Noah Begum"]`.

### Checked and found sound

- **Synthetic identifiers:** NHS numbers are generated from 999 100 0000 upwards with a valid check digit, in the range kept for testing; patient emails are `@example.com`, staff `@clinic.example`, sign-in addresses from the documentation ranges (192.0.2.x, 198.51.100.x). Names are invented combinations; no real organisation, place or brand appears in notes or files, and every file says it's synthetic.
- **Nothing leaves the machine:** email goes to `DiscardedEmail` (the app's email queue and its sender aren't used, and the seed's services have no host to run them); files upload only to a loopback store that answers; `DemoDataSeeder` re-checks `DevGuard` itself.
- **Replayed rules:** statuses, stage locks, unlocks, blocks and unblocks go through `ProcedureChange` and match the handlers' allowed changes; visits through `StageVisits` and the finish logic; appointment history and notifications through `AppointmentChange` and `AppointmentNotifications`; assignment notifications respect the one optional kind (`ProcedureAssigned`) and the endoscopist's choice; monitoring notifications use the rules as they were switched on at the time and only owners who were admins then.
- **Accounts:** generated passwords meet the rules; the password change goes through the app's validator; the two-factor key is turned on with a real code at the moved clock; the lockout is the app's own, then stretched (documented); every forced-change exemption but the new starter's is documented in `AGENTS.md`.
- **Privacy:** restriction, dispute, export, anonymisation and the retention "keep" go through their commands at their moments; the anonymised record is an imported one with nothing open; the retention review shows six, as the test says.
- **The wipe:** deletes children before parents, keeps `admin` and `keepUsers` with their settings, keeps the seeded "Definition missing" stage and activity types, and now also removes the settings and sign-in challenges of the accounts it deletes. The tool saves the `pre-reset` snapshot before calling the seed.
- **Saved settings:** the keys and shapes match the web app (`medapp.favourites`, `medapp.pins`, `medapp.pageSize`), and the saved searches use the lists' real URL keys (`mine`, `templateId` in lower case, `overdue`, `status`, `priority`, `dobFrom`, `dobTo`).
- **Clock changes and midnight:** runs on both sides of both 2026 clock changes and just after midnight finished without errors, with every appointment inside UK working hours.
