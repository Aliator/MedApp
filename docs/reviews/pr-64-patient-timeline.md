# Review: PR #64, a timeline on the patient page

**PR:** [#64](https://github.com/Aliator/MedApp/pull/64), branch `feature/patient-timeline`, reviewed at `6aa1e55` on top of `main` at `a506079` (the merge of #61), which is still `main`'s head.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, the SQL that EF generates, a scratch probe of the real classes, or the GitHub API. **No finding is high or critical;** 1 is medium and 8 are low.

## Summary

The patient page gets a timeline: one feed of everything the procedure audit log recorded about that patient's procedures, newest first and grouped by UK day, with what's due at the top, filter chips, and "Load more" paged by cursor. The server decides what each role sees, and the rule holds: no free text from the log reached staff by any route tried. The main gap is paperwork: the governance pack still says only admins can see change history, which this PR changes, and `AGENTS.md` asks for the pack to be updated in the same PR.

## What changed, and why

56 files, 4,125 lines added and 65 removed; 786 of the added lines are the migration's generated designer file, and 173 the regenerated API types.

- **Why:** the to-do list asked for one feed per patient of what happened across their procedures, built from the audit log, with what's coming up at the top and filters by type and by procedure. Until now that history was only on the admin-only audit log page, searched by procedure.
- **The feed, on the server:** `GET api/patients/{id}/timeline` (`PatientTimelineController`, staff policy).
  - `PatientTimelineSearch` reads the patient's audit entries newest first (by time, then by ID), and takes one more than a page, to know whether there's another.
  - **Paging by cursor (keyset):** instead of "page 3", each page ends with a `NextCursor`, an opaque string holding its last entry's time and ID, and the next page starts just after it (`TimelineCursor`). A bookmark rather than a page number: entries added at the front don't move it.
  - **Filters:** `Kinds` (any of nine groups of actions) and `ProcedureIds` (up to 50). `PageSize` is 1 to 100, 20 by default.
  - **Kinds** (`PatientTimelineKind`) group the ten audit actions. Two are read from the entry's JSON details: a status change to or from Blocked is a Blocks entry, and a details edit that includes the due date is a DueDates entry. `PatientTimelineKinds` writes that rule twice, in memory (`Of`) and as SQL (`WhereKindIn`, a `LIKE` on fixed strings, called markers).
  - Each entry carries its procedure's title now, from a small lookup per row; no title means the procedure has been deleted.
- **What each role sees** (`GetPatientTimelineQuery.IsAdmin`, from the Admin role):
  - **Staff:** entries only for procedures that still exist (an `EXISTS` in the SQL, checked again in the handler). `TimelineMapper` sends the kind, who, when, the procedure and structured values (statuses, priorities, due dates, the template, assignees), but never notes text, old or new titles, or block and cancel reasons. For an edit, staff get which field changed, with values only for the due date and priority (`PublicValueFields`).
  - **Admins:** everything, including deleted procedures' entries (each with the title recorded in it) and the free text.
- **What's due:** `GET api/patients/{id}/timeline/overview` lists the open and blocked procedures, soonest due first (five, plus a count), with overdue ones flagged, and the procedures to filter by (deleted ones for admins only).
- **The database:** the migration `IndexedAuditEntriesByPatientAndTime` replaces the audit table's `PatientId` index with one on `(PatientId, AtUtc)`, so a patient's newest entries are read straight from the index, already in order.
- **The web app:**
  - `PatientTimeline` (`src/components/procedures`): "Upcoming", then the chips (`ToggleButton`s in labelled groups; "Deleted" for admins only; procedure chips when there's more than one procedure), then the feed, one list per UK day with the day as its heading, and "Load more". How each entry is worded is in `describeTimelineEntry` (`src/api/timeline.ts`).
  - `patientTimelineQuery` is an infinite query (TanStack Query's paged kind). The route's loader preloads its first page and the overview, so the server renders them; `preload` learnt infinite queries for this.
  - The patient page shows the details and the timeline side by side or, below 1,400 px wide and in a half view, one card with two folding sections. The page holds the filters, so they survive the switch.
  - Procedure changes made in this browser now also refresh every cached timeline (`procedureKeys.timelines`).
  - New date helpers (`ukDay`, `formatDayHeading`, `formatTime`) always use UK time, so the server and the browser render the same text.
- **Docs and tools:** `AGENTS.md` (Lists, Mappers, a new "Patient timeline" section, Auth), `MedApp.Web/AGENTS.md`, the finished to-do item removed, and two new UI parity screens.

## Design choices

- **Cursor paging, not page numbers:**
  - **Instead:** a `PagedResponse` with page numbers and a total, like every other list.
  - **Chosen because:** a feed grows at the top. With page numbers, an entry added while page 1 is open pushes an old entry onto page 2, which then shows it twice. A bookmark doesn't move.
  - **Worse at:** no total, no jumping to page 5, and entries from the same moment come out in random order (finding 3).
- **One endpoint with a role switch, not one per role:**
  - **Instead:** an admin endpoint that returns everything, and a staff one with its own, narrower response type.
  - **Chosen because:** one query, one mapper and one set of tests; the role only decides which fields are filled.
  - **Worse at:** every new field has to remember the switch. Edited fields have an allowlist, but the rest are chosen per action by hand, and no test looks at the whole staff response (finding 6). A separate staff type would make a leak something the code can't express, rather than something a test has to catch.
- **Kinds worked out from the JSON when read, not stored:**
  - **Instead:** a `Kind` column written with each entry, and filled in for old entries by the migration.
  - **Chosen because:** no new column or backfill, and the groups can change freely.
  - **Worse at:** it depends on exactly how `ProcedureAudit` writes details; filtering runs a `LIKE` over each row's `nvarchar(max)` details; and SQL Server compares text ignoring case where the in-memory copy doesn't. That's harmless today (the markers are fixed and typed text is escaped; see Detail), but two copies of one rule must stay in step. The test that runs both over every action guards that.
- **The current title by a lookup per row:**
  - **Instead:** a join, or only the title copied into each entry when it was written.
  - **Chosen because:** staff must see the procedure as it's named now, and "no title" doubles as "deleted".
  - **Worse at:** nothing that matters at 21 rows; each lookup is a primary key seek.
- **Filters held by the page, not in the URL:**
  - **Instead:** the URL, as list pages do.
  - **Chosen because:** the timeline is part of the patient page rather than a list page, and the page keeps the filters across the layout switch.
  - **Worse at:** reloading, going back or sharing a link loses them.
- **Not live:** the feed refreshes after changes made in this browser, not after other people's. Simple, at the cost of a stale feed on a page left open.
- **Swapping the index, not adding one:** one index serves both the old lookups by patient and the new ordered reads, at the cost of a lock on the table while it's built (finding 7).

## How it was checked

- **Read:** the whole diff, the PR description, `AGENTS.md` and `MedApp.Web/AGENTS.md`, and the code around the change: `ProcedureAudit`, `ProcedureChange` and the create, update and delete handlers (everything that writes audit details), `ProcedureWorkflow`, `ProcedureEnums`, the repositories, `PatientProcedureResponse` and `ProcedureMapper` (what the procedure page shows), the query client, the auth guards, the API client, TanStack Query's refetch of paged queries (`infiniteQueryBehavior.js`, version 5.107.1), the governance pack, `docs/architecture.md`, the hazard log and the medical device decision.
- **Build:** `dotnet build MedApp.sln --configuration Release`: 0 errors, 1 warning (CS0414 in `MedApp.Client.Deprecated`, in a file this PR doesn't touch).
- **.NET tests:** `dotnet test MedApp.UnitTests --configuration Release`: 912 passed.
- **Web:** `npm run lint` and `npm run typecheck` clean. `npx vitest run`: 754 tests in 86 files, and every timeline test passed in every run. One unrelated test, AddPatientPage's "shows the API's validation errors on the fields and in a summary", failed in 3 of 5 runs (twice in the full suite, once in three runs of its file alone). Neither that test nor `AddPatientPage` is changed by this PR, and CI passed it.
- **The migration, without a database:** `dotnet ef migrations has-pending-model-changes` reports no changes. `dotnet ef migrations script AddedMustChangePassword IndexedAuditEntriesByPatientAndTime` shows one transaction: `DROP INDEX`, then `CREATE INDEX`. Its ID sorts after `main`'s last migration (`20261116074036_AddedMustChangePassword`), and its designer snapshot includes that migration's column.
- **The SQL, from a scratch probe** outside the repository (the built assemblies and `ToQueryString`, no database): staff queries with and without kinds, a procedure filter and a cursor. Under Detail. The order matches the new index (which carries the primary key too), the cursor becomes `AtUtc < @at OR (AtUtc = @at AND Id < @id)`, the kinds become `LIKE` on fixed strings, and the title lookup sits in the select list, where SQL Server normally works it out only for the 21 rows taken.
- **The markers, from the same probe:** a cancel reason typed as `a"to":"Blocked"` with a full-width quote and an accented letter, recorded through the real `ProcedureChange`. Every quote came out as `\u0022` and every non-ASCII character as `\uXXXX`, so typed text can't match a marker. The same probe filed the cancellation of a blocked procedure under Blocks (finding 2).
- **A hunt for leaks to staff:** every route in the brief, listed under Detail with what was found. None carries free text from the log to staff.
- **Do the tests test the change?** Mostly. The kinds test runs the SQL rule and the in-memory rule over every action, with details written by the real `ProcedureChange`, which is the right guard. Paging through ties is tested, but in memory only, and staff visibility is tested field by field (finding 6). The web tests cover the feed, the chips, loading more, errors and the server render, but not the "Load more" announcement or where focus goes (finding 4).
- **CI:** run [43831710288](https://github.com/Aliator/MedApp/actions/runs/43831710288) on `6aa1e55` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`). The coverage comment has MedApp.Application at 100% of lines.
- **Merging:** `main` hasn't moved since the branch last merged it, so the PR merges cleanly.
- **Not done:** the migration against a database, UI parity screenshots, the axe audit, and a real browser or screen reader. All need the AppHost and the dev database, which were left alone. The PR says the migration was run up, down and up on LocalDB; it doesn't say whether screenshots or the axe audit were run.

## Findings

Most severe first. None is high or critical. All nine were open when this review was written. Each finding's "What happened to it" now says how it was dealt with; all nine are fixed or decided.

### 1. The governance pack still says only admins can see the audit history

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** until now, change history was admin-only, and the pack says so in the places a customer relies on. After this PR, every member of staff sees, for any patient, who did what to each procedure and when, its past statuses, due dates and priorities, who was assigned and unassigned, and when notes were saved. A clinic's data protection officer adopts the customer record of processing, which gives the audit log's recipients as "Your MedApp admins (only admins can read the audit log)", and answers a patient's request or the ICO from it. The answer is wrong. `AGENTS.md` asks for exactly this update in the same PR, and the #61 review named this timeline as the first change that rule would catch. The pack also misses the good news: admins can now see one patient's whole history on screen, which the DPIA's risk 6 and the right-of-access guide both say is missing.
- **Evidence:**
  - `AGENTS.md:83` (step 8: a change to who can see personal data updates `docs/governance/` in the same PR) and `AGENTS.md:314` (reviews check it); the PR changes no file under `docs/governance/`
  - `docs/governance/dpia.md:36` (only admins read the audit log), `:117` (risk 6: the audit screen filters by procedure, not by patient), `:147` and `:184` (the controls "Only admins can read the audit log" and "The audit trail is admin-only")
  - `docs/governance/ropa-customer-template.md:59`, `ropa.md:72`, `dpa-template.md:172`, `security-policies.md:33` and `nhs-context.md:22`: an admin-only audit log
  - `docs/governance/data-subject-rights.md:37`: admins search the audit "by procedure, not by patient"
  - `docs/reviews/pr-61-gdpr-pack.md`, finding 7
- **Fix:** in this PR:
  - `dpia.md`: in Use (line 36), add that staff see a patient's timeline. Reword the controls at lines 147 and 184 to "the full audit trail, with its copies of notes, titles and reasons, is admin-only; staff see a per-patient timeline without that free text (`TimelineMapper`)". Under risk 6, add that admins can now see a patient's whole history on screen, deleted procedures included.
  - `data-subject-rights.md:37`: the same, for the right of access.
  - The customer template's recipients, `ropa.md`, `dpa-template.md`, `security-policies.md` and `nhs-context.md`: "an admin-only audit log; staff see a filtered timeline per patient".
  - `data-inventory.md` needs no change: nothing new is stored.
- **What happened to it:** **Fixed** in this PR. The governance pack now says "the full audit trail is admin-only; staff see a per-patient timeline without its free text": `dpia.md` (Use, the two controls, and risk 6, which now records that admins can see a patient's whole history on screen, deleted procedures included, though there's still no export), `data-subject-rights.md` (right of access), the customer ROPA template's recipients, `ropa.md` (A3.2 and Access), `dpa-template.md`, `security-policies.md` and `nhs-context.md`. Each spells out what staff see (finding 5). The staff privacy notice template's "Who can see it" also says other staff see your username and actions on a patient's timeline. `data-inventory.md` needed no change.

### 2. Cancelling a blocked procedure is filed under Blocks, but worded as an ordinary status change

- **Severity:** low. **Confidence:** high (seen in the probe).
- **What could go wrong:** a procedure is blocked while waiting for results, then cancelled. The server files the cancellation under Blocks, because its details say `"from":"Blocked"`. The web app words it "Status changed to cancelled", with the status-change icon, and its comments call it "a status change like any other". Someone checking when the procedure was cancelled turns on the Status chip and sees nothing; under Blocks they find a line that reads like a status change. Like a letter filed under the department it came from rather than what it's about.
- **Evidence:**
  - `MedApp.Application/Procedures/Timeline/Common/PatientTimelineKinds.cs:20`, `:43` and `:69-70`: from Blocked to anything is Blocks
  - `MedApp.Application/Procedures/Common/ProcedureWorkflow.cs:45`: a blocked procedure can be cancelled
  - `MedApp.Web/src/api/timeline.ts:185-189` and `MedApp.Web/src/components/procedures/PatientTimeline.tsx:58-59`: worded and drawn as a status change
  - `AGENTS.md:134`: "Blocks (to or from Blocked)"
  - `MedApp.UnitTests/Application/Procedures/Timeline/Common/PatientTimelineKindsTests.cs:118-135`: the real entries cancel from In progress, never from Blocked
- **Fix:** make Blocks mean "to Blocked, or back from it", and file a cancellation from Blocked under Status: a third marker, `"to":"Cancelled"`, in `Of` and `WhereKindIn` (diff under Detail). Add a blocked-then-cancelled pair to `RealEntries`, so both copies of the rule are tested on it, and reword `AGENTS.md:134`.
- **What happened to it:** **Fixed** in this PR. A third marker, `"to":"Cancelled"`, in both `Of` and `WhereKindIn`: Blocks is now to Blocked or back from it, and cancelling a blocked procedure is under Status. `RealEntries` gains a blocked-then-cancelled pair recorded through `ProcedureChange`, so both copies of the rule are tested on it, and `AGENTS.md` says so. The web app already worded and drew it as a status change; its test now gives it the Status kind.

### 3. Entries recorded by one action come out in random order

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** one action can write several entries, all stamped with the same moment: the first notes saved on a stage write "Started", an automatic "Status changed to in progress", then "Saved notes"; finishing the last stage writes "Finished" and an automatic "In review". The feed orders a moment's entries by their random IDs, so the newest-first feed can show "Saved notes on Consultation" below "Started Consultation" or the status change at the bottom: read top to bottom, the notes were saved before the stage started. Anyone piecing together what happened, after an incident say, is misled about the sequence. Like three letters posted together, filed by the serial numbers on their envelopes. `AGENTS.md` says the order is kept for entries saved together; it's stable, but it isn't the order they happened in.
- **Evidence:**
  - `MedApp.Application/Procedures/Common/ProcedureChange.cs:25-28`: every entry of one change gets the same `nowUtc`; `:63-66`, `:75-79` and `:91-94`: the sequences above
  - `MedApp.Application/Procedures/Common/ProcedureAudit.cs:23`: IDs are `Guid.NewGuid()`
  - `MedApp.Application/Procedures/Timeline/Queries/GetPatientTimeline/PatientTimelineSearch.cs:43-45`: then by ID
  - `MedApp.UnitTests/Application/Procedures/Timeline/Queries/GetPatientTimeline/PatientTimelineSearchTests.cs:76-83`: the test checks ID order, with hand-picked IDs
- **Fix:** give the entries of one change an order. Either a `Sequence` column that `ProcedureChange.Audit` numbers 0, 1, 2…, ordered between time and ID and added to the cursor (a migration); or, without one, have `ProcedureChange.Audit` add one tick (100 nanoseconds) to each entry after the first, so times are strictly increasing within a change. The second changes recorded times by under a microsecond and needs no schema change. Either way, test it with entries from a real first notes save.
- **What happened to it:** **Fixed** in this PR, without a migration. `ProcedureChange.Audit` stamps each entry a change writes one tick (100 ns) after the one before, so a first notes save reads Started, the automatic status change, then Saved notes, in that order. `ProcedureChangeTests` checks the times and the newest-first order of a real first notes save. `LastUpdated` stays the change's own time. Entries recorded before this still share a time and fall back to ID order; `AGENTS.md` says so.

### 4. "Load more" loses focus at the end, and a failed load is announced as "0 older entries loaded"

- **Severity:** low. **Confidence:** high from the code; not tried with a screen reader.
- **What could go wrong:**
  - **Focus:** someone using the keyboard presses "Load more" and the last page arrives. The button is removed, so focus drops to the page itself, and the next Tab starts again from the top of the page, far from where they were in a long feed.
  - **The announcement:** with screen reader announcements on, a failed load says both "Older entries could not be loaded" (the alert) and "0 older entries loaded." (the announcement), because the count is taken whether or not the load worked.
- **Evidence:**
  - `MedApp.Web/src/components/procedures/PatientTimeline.tsx:244-249`: the count and announcement, with no check for failure; `:261-271`: the button is shown only while there's more
  - `MedApp.Web/src/components/shared/AlertMessage.tsx:27`: the error is an alert
  - `MedApp.Web/src/components/procedures/PatientTimeline.test.tsx:127-157`: neither the announcement nor focus is tested
- **Fix:** announce only when the page arrived; after loading, move focus to the first new entry (a focus target on each entry's row), so it's never left on a removed button. Test both (sketch under Detail).
- **What happened to it:** **Fixed** in this PR. "Load more" announces only when the page arrived and brought entries, and then moves focus to the first new entry (each entry's title is a focus target, `tabIndex={-1}`), so focus is never left on a removed button. Tests check the announcement and focus after the last page, and that a failed load announces nothing and leaves focus on the button.

### 5. Staff see more of the log than the agreed rule and the docs describe

- **Severity:** low. **Confidence:** high on the facts; whether it matters is a judgement call.
- **What could go wrong:** the to-do item this PR finishes said staff would see "no old field values". `AGENTS.md` now says staff see "only values the procedure page shows anyway". Neither is quite what's built, and nothing records the change of mind:
  - **Old values:** a due date edit shows "Due date 1 Dec 2026 → 8 Mar 2027", and status changes and priority edits show from and to; the procedure page shows only the current values.
  - **Past assignees:** "Unassigned" names who was taken off; the procedure page shows only who's assigned now.
  - **Deleted accounts:** each entry shows the username recorded at the time. Once a leaver's account is deleted, the procedure page shows "Deleted user" on their stages, but the timeline shows their username on every entry.

  None of this is free text, and history is the point of a timeline, so showing it is defensible. The risk is that the documented rule and the DPIA (finding 1) say less than the screen shows.
- **Evidence:**
  - `MedApp.Application/Procedures/Timeline/Common/TimelineMapper.cs:21` (due date and priority values for everyone), `:36` (the recorded username), `:46-62` and `:66-67`
  - `MedApp.Application/Procedures/Common/ProcedureMapper.cs:11` and `:144`: "Deleted user" on the procedure page; `docs/clinical-safety/hazard-log.md:675`
  - `AGENTS.md:136`; the removed to-do line (`git diff origin/main...HEAD -- docs/todo.md`)
- **Fix:** decide, and write it down. Either keep it and reword `AGENTS.md:136` to "structured values, old and new (statuses, priorities, due dates, the template, assignees), and the username recorded at the time, never free text", carrying the same wording into the DPIA (finding 1); or leave out `From` values for staff and show deleted accounts as "Deleted user". Keeping it seems the better fit for a history view.
- **What happened to it:** **Kept, and written down** (the owner's decision). Staff keep seeing old and new due dates, priorities and statuses, past assignees, and deleted accounts' usernames: structured values, never free text. `AGENTS.md` and the governance pack now say exactly that.

### 6. The tests check named fields, not the whole staff response, and the SQL is only proven to translate

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **A new leak passes every test:** staff visibility is tested by checking particular fields are empty (`Reason`, a change's `From` and `To`). A later change that adds a field, say the unassign reason or the notes' first line for a preview, and forgets the role switch, leaks free text to staff with every test still green. `AGENTS.md:138` asks for care, but nothing enforces it.
  - **SQL behaviour is assumed:** paging through entries with the same time is tested on in-memory lists, where IDs sort the .NET way. SQL Server sorts its `uniqueidentifier`s differently, and its `LIKE` ignores case. Both are consistent within SQL Server (the order and the cursor use the same comparison), so nothing is wrong today, but nothing runs these queries against SQL Server either.
- **Evidence:**
  - `MedApp.UnitTests/Application/Procedures/Timeline/Common/TimelineMapperTests.cs:86-95`, `:124-130` and `:147-159`; `GetPatientTimelineHandlerTests.cs:80-92`
  - `MedApp.UnitTests/Infrastructure/Data/PatientTimelineTranslationTests.cs:45`: checks the SQL's text only
  - `PatientTimelineSearchTests.cs:85-107`: tie paging, in memory
- **Fix:** add a canary test: record every action through `ProcedureChange` and the handlers' shapes with one unique string as the notes, title, procedure notes and reason, map each entry for staff, serialise the response to JSON, and check the string never appears; and that it does for an admin (sketch under Detail). Later, when `MedApp.IntegrationTests` has a database, page through tied entries on SQL Server.
- **What happened to it:** **Fixed** in part, as decided. `TimelineStaffCanaryTests` records every action through `ProcedureChange` with one string as all its free text (stage notes, procedure notes, titles, reasons), maps each entry for staff, serialises it as the API does, and checks the string never appears; for an admin it appears in every entry that holds it, and a third test checks every action is covered. `AGENTS.md` asks for new actions and fields to be added to it. Running the query on SQL Server (tied paging, the `LIKE` markers) waits for integration tests with a database (#65); it's recorded in `docs/todo.md` under Testing.

### 7. The migration drops the old index before building the new one, locking the audit table meanwhile

- **Severity:** low. **Confidence:** high on the mechanism; low that it matters at today's table sizes.
- **What could go wrong:** EF runs the migration in one transaction: `DROP INDEX`, then `CREATE INDEX`. Dropping an index takes a schema lock that's held until the transaction commits, so nothing can read or write the audit table while the new index is built. The Helm chart runs migrations while the old pods are still serving. Every procedure change saves its audit entries in the same save, so on a large audit table every change waits until the build finishes, and fails if that takes longer than the command timeout (30 seconds by default); the audit log page waits too. Like closing a road to repaint its lines, rather than painting the new lane first.
- **Evidence:**
  - `MedApp.Infrastructure/Migrations/20261116082648_IndexedAuditEntriesByPatientAndTime.cs:13-20`
  - the scripted migration (`BEGIN TRANSACTION; DROP INDEX …; CREATE INDEX …; COMMIT`)
  - `MedApp.Infrastructure/Data/Repositories/PatientProcedureRepository.cs:38-49`: changes and their audit entries are saved together
- **Fix:** create the new index first, then drop the old one, so reads keep an index and aren't blocked during the build (writes still wait for an offline build). For a large table, build it online (`IsCreatedOnline()`), which Azure SQL and the Developer edition support, but Standard and Express don't. At today's sizes, reordering is enough; record the online option for later.
- **What happened to it:** **Fixed** in this PR. The migration now creates `IX_PatientProcedureAuditEntries_PatientId_AtUtc` before dropping the old index, and `Down` does the reverse. `has-pending-model-changes` reports no changes, and it was run up, down and up again on a throwaway LocalDB database (the SQL showed `CREATE INDEX` then `DROP INDEX` each way), which was then dropped. Building online (`IsCreatedOnline()`) is noted in the migration's comment, for when table sizes and supported editions call for it.

### 8. The architecture handbook and one line of `AGENTS.md` lag the change

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the handbook's API table, which lists every endpoint, still says "55 HTTP endpoints in six controllers" and has no timeline row. Its "Who can see what" still says "Only admins read the audit log", its feature table mentions only the admin page, and its list of composites leaves out `PatientTimeline`. Anyone working from the handbook misses an endpoint that shows staff part of the audit log. Separately, `AGENTS.md:137` says admins see a deleted procedure "with the title they had when deleted", while the code and `AGENTS.md:139` give each entry the title recorded in that entry, so a procedure renamed before it was deleted shows under two titles.
- **Evidence:**
  - `docs/architecture.md:50`, `:530`, `:532-546`, `:559` and `:1112`
  - `AGENTS.md:137` against `:139` and `MedApp.Application/Procedures/Timeline/Common/TimelineMapper.cs:38`
- **Fix:** add a row "Patient timeline | `GET api/patients/{id}/timeline`, `…/timeline/overview` | Staff (admins see more)", make the count 57 in seven controllers, reword line 1112 to "Only admins read the audit log; staff see each patient's timeline without its free text", update lines 50 and 559, and make `AGENTS.md:137` say "with the title recorded in each entry".
- **What happened to it:** **Fixed** in this PR. The handbook has the timeline row, "57 HTTP endpoints in seven controllers", the audit log feature row (line 50) mentioning the timeline, `PatientTimeline` among the composites (line 559), "Only admins read the audit log; staff see each patient's timeline without its free text" (line 1112), the cursor-paged exception under Paging, and the handler and validator counts (53 and 30). `AGENTS.md` says a deleted procedure's entries each carry the title recorded in that entry, so a procedure renamed before it was deleted shows under both titles.

### 9. Procedure chips don't scale and can't be told apart

- **Severity:** low. **Confidence:** high from the code; how often it bites depends on the data.
- **What could go wrong:** every procedure the patient has ever had becomes a chip, labelled with its title alone.
  - **Same titles:** a patient with three "Physiotherapy review" procedures over the years gets three identical chips, which neither sight nor a screen reader can tell apart.
  - **Many procedures:** a patient with 60 gets 60 chips between "Upcoming" and the feed, which in a half view pushes the feed far down.
  - **Over 50 chosen:** the server refuses more than 50 procedure IDs, and the feed shows only "The timeline could not be loaded".
- **Evidence:**
  - `MedApp.Web/src/components/procedures/PatientTimeline.tsx:195-212` and `:232`
  - `MedApp.Application/Procedures/Timeline/Common/TimelineMapper.cs:100-113`: the title only
  - `MedApp.Application/Procedures/Timeline/Queries/GetPatientTimeline/GetPatientTimelineValidator.cs:10` and `:27-30`
- **Fix:** send each procedure's created date (or status) in the overview and add it to the label ("Physiotherapy review, 3 Mar 2026"); show the ten newest with a "More procedures" control, or use a multi-select list instead of chips; and stop the chips at 50, or raise the server's cap to match what the chips allow.
- **What happened to it:** **Fixed** in this PR. The overview sends each procedure's UK creation day (`CreatedOn`), or for a deleted one its deletion day (`DeletedOn`), and the chips read "Physio review, 3 Mar 2026" or "Physio review, deleted 1 May 2026". The ten newest show, plus any chosen, with "Show all N" and "Show fewer". Once 50 are chosen (`MAX_TIMELINE_PROCEDURES`, matching the API's `MaxProcedureIds`), the other chips are disabled with a tooltip. Tests cover the labels, the ten-chip rule and the limit.

## Remaining risks

- **Not live:** a page left open doesn't show other people's changes until it's reloaded, or until a change is made in the same browser.
- **Filtering a long history:** for a patient with thousands of entries and a rare kind (Blocks, say), SQL Server reads every entry's details to find a few, because neither the action nor the kind is in the index. Fine at hundreds of entries per patient; a stored `Kind` column, or the action in the index, if histories grow far beyond that.
- **A procedure renamed before it was deleted** shows under two titles in an admin's feed (finding 8 covers the docs, not the behaviour).
- **Reads aren't recorded:** opening a patient's timeline isn't logged, like every read today (roadmap step 7).
- **SQL Server behaviour is untested:** tie ordering and the `LIKE` markers have only been seen as SQL text (finding 6).
- **Not seen in a browser** here: no UI parity screenshots, no axe audit, no screen reader.
- **An unrelated flaky test:** AddPatientPage's validation-errors test failed in 3 of 5 local runs. If CI starts failing on it, that's the cause, not this PR.

## Say it like this

"The patient page now has a timeline: everything that happened to the patient's procedures in one feed, newest first and grouped by day, with what's due at the top, filters by kind and by procedure, and a 'Load more' that uses a bookmark rather than page numbers, so new entries don't shuffle the pages. It's built from the admin-only audit log, so the server trims it for staff: they see who did what and when, and dates, statuses and priorities, but never notes, titles or reasons from the log, and nothing about deleted procedures; admins see it all. That trimming holds. What's left is mostly paperwork and polish: the data protection documents still say only admins see change history, cancelling a blocked procedure lands under the wrong filter, and entries made at the same instant can show in the wrong order."

## Detail

- **Every audit action, what it writes, and its kind:**

  | Action (stored number) | Details written | Kind |
  |---|---|---|
  | Created (0) | `template`, `dueDate`, `priority`, `stageCount` | Created |
  | DetailsUpdated (1) | `changes`: `title`, `notes`, `dueDate`, `priority`, each `{from, to}` | DueDates if `"dueDate":{` is present, otherwise Details |
  | StatusChanged (2) | `from`, `to`, `reason` (when given), `automatic` | Blocks if `"to":"Blocked"` or `"from":"Blocked"` is present, otherwise Status (finding 2) |
  | Assigned (3), Unassigned (4) | `userId`, `username` | Assignments |
  | StageStarted (5), StageFinished (7), StageUnlocked (8) | `{}` | Stages |
  | StageNotesUpdated (6) | `from` (when there were notes), `to` | Notes |
  | Deleted (9) | `status` | Deleted |

  The kinds test runs every defined action through both copies of the rule, so a new action that `Of` files under Details but `WhereKindIn` misses fails it. None of the actions meets the medical device decision's triggers: the timeline shows workflow history and dates staff chose, and calculates nothing from clinical data.

- **Why typed text can't fake a marker:** `ProcedureAudit` serialises with `JsonSerializerDefaults.Web`, whose default encoder writes a quote inside a string as `\u0022` and any non-ASCII character as `\uXXXX`. The probe recorded this cancel reason through `ProcedureChange`:

  ```
  {"from":"Blocked","to":"Cancelled","reason":"a\u0022to\u0022:\u0022Blocked\u0022 \uFF02 \u00E9 \\\u0022","automatic":false} => Blocks
  ```

  So a raw quote only ever comes from the JSON's own structure, and no full-width lookalike reaches SQL Server's case-insensitive comparison. Even with relaxed escaping, a typed quote would be written `\"`, and every marker needs a letter directly before a quote.

- **The hunt for leaks to staff:**

  | Route tried | Result |
  |---|---|
  | Stage notes, old and new | Field name only (`TimelineMapper.cs:70-71`) |
  | Procedure title and notes edits | Field name only; values only for `dueDate` and `priority` (`:21`, `:74-77`) |
  | Block and cancel reasons | Admins only (`:63`) |
  | A title through `ProcedureTitle` | The current title; staff never get an entry whose procedure is gone, so the recorded title never reaches them (`PatientTimelineSearch.cs:25-26`, `GetPatientTimelineHandler.cs:27-28`) |
  | Titles through the overview | Current procedures only for staff; deleted ones for admins (`GetPatientTimelineOverviewHandler.cs:42-52`) |
  | A deleted or another patient's procedure ID guessed into the filter | Still limited to the patient and, for staff, to existing procedures; the same empty page as an unknown ID |
  | The cursor | The time and ID of the last entry on the page. If a procedure is deleted while the page is read, the handler drops its entries after the cursor is taken, so the cursor can hold one such entry's time and ID, but nothing else |
  | The kind filters as a way of testing for words | The markers are fixed strings; nothing typed is compared |
  | Error messages | None repeat anything from the log |
  | The "Deleted" chip | Hidden from staff, and the server would return nothing for it anyway |
  | What staff see that the procedure page doesn't | Old structured values, past assignees, deleted accounts' usernames (finding 5) |

- **The SQL for staff, with two kinds, a procedure and a cursor** (from the probe):

  ```sql
  SELECT TOP(@p) [p].[Id], [p].[Action], [p].[AtUtc], [p].[Details], [p].[PatientId], [p].[PatientProcedureId],
      [p].[ProcedureTitle], [p].[StageId], [p].[StageName], [p].[UserId], [p].[Username], (
      SELECT TOP(1) [p1].[Title]
      FROM [PatientProcedures] AS [p1]
      WHERE [p1].[Id] = [p].[PatientProcedureId])
  FROM [PatientProcedureAuditEntries] AS [p]
  WHERE [p].[PatientId] = @patientId AND EXISTS (
      SELECT 1
      FROM [PatientProcedures] AS [p0]
      WHERE [p0].[Id] = [p].[PatientProcedureId])
    AND (([p].[Action] = 2 AND ([p].[Details] LIKE N'%"to":"Blocked"%' OR [p].[Details] LIKE N'%"from":"Blocked"%'))
      OR ([p].[Action] = 2 AND [p].[Details] NOT LIKE N'%"to":"Blocked"%' AND [p].[Details] NOT LIKE N'%"from":"Blocked"%'))
    AND [p].[PatientProcedureId] = @procedureIds1
    AND ([p].[AtUtc] < @at OR ([p].[AtUtc] = @at AND [p].[Id] < @id))
  ORDER BY [p].[AtUtc] DESC, [p].[Id] DESC
  ```

  `@at` is a `datetime2` with all seven decimal places, so the cursor's time matches the stored one exactly. The kinds that aren't chosen are left out of the SQL altogether, so each combination of kinds is its own SQL text, with its own cached plan (at most 512).

- **Finding 2's fix:**

  ```diff
       /// <summary>A status change from Blocked.</summary>
       public const string FromBlocked = "\"from\":\"Blocked\"";
  +
  +    /// <summary>A status change to Cancelled: from Blocked, that's a cancellation, not an unblock.</summary>
  +    public const string ToCancelled = "\"to\":\"Cancelled\"";
  @@
  -            || (blocks && e.Action == StatusChanged && (e.Details.Contains(ToBlocked) || e.Details.Contains(FromBlocked)))
  -            || (status && e.Action == StatusChanged && !e.Details.Contains(ToBlocked) && !e.Details.Contains(FromBlocked))
  +            || (blocks && e.Action == StatusChanged
  +                && (e.Details.Contains(ToBlocked) || (e.Details.Contains(FromBlocked) && !e.Details.Contains(ToCancelled))))
  +            || (status && e.Action == StatusChanged
  +                && !e.Details.Contains(ToBlocked) && !(e.Details.Contains(FromBlocked) && !e.Details.Contains(ToCancelled)))
  @@
  -    private static bool IsBlockChange(string details) => details.Contains(ToBlocked) || details.Contains(FromBlocked);
  +    private static bool IsBlockChange(string details) =>
  +        details.Contains(ToBlocked) || (details.Contains(FromBlocked) && !details.Contains(ToCancelled));
  ```

- **Finding 4's fix, sketched:**

  ```tsx
  const loadMore = async () => {
    const before = entries.length;
    const result = await feed.fetchNextPage();
    if (result.isError) return; // the alert says so
    const loaded = (result.data?.pages.flatMap((page) => page.items ?? []) ?? []).slice(before);
    announce(`${loaded.length} older ${loaded.length === 1 ? 'entry' : 'entries'} loaded.`);
    // then focus the first of `loaded` (each row a focus target), so focus isn't left on a removed button
  };
  ```

- **Finding 6's canary test, sketched:**

  ```csharp
  [Test]
  public void ToResponse_Staff_NeverCarriesFreeTextFromTheLog()
  {
      const string canary = "canary-text-7f3a";
      // Record every action that can hold free text, the way handlers do: stage notes, a block and a cancel with
      // the canary as reason, a details edit with the canary as old and new title and notes, a creation and a
      // deletion of a procedure titled with the canary.
      foreach (var entry in EntriesHolding(canary))
      {
          var json = JsonSerializer.Serialize(TimelineMapper.ToResponse(new PatientTimelineRow(entry, CurrentTitle), isAdmin: false));
          json.Should().NotContain(canary, entry.Action.ToString());
      }
  }
  ```

- **Finding 7, the online option:** `builder.HasIndex(x => new { x.PatientId, x.AtUtc }).IsCreatedOnline();` makes EF write `WITH (ONLINE = ON)`. The chart's own SQL Server is the Developer edition and Azure SQL supports it, but a customer on Standard or Express would get an error, so it belongs with a decision about supported editions.
