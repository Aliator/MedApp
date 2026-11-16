# Review: PR #93, staff calendars, appointments and stage visits

**PR:** [#93](https://github.com/Aliator/MedApp/pull/93), branch `feature/staff-calendars`. Reviewed at `13575e6`: the PR's head on GitHub (`8978406`) plus one local commit not yet pushed ("Removed staff IDs from the export, cleared attachments in the demo reset, and fixed the docs"). Compared with `main` at `246beac`.

**Reviewed:** 16 November 2026, as an independent, adversarial review, without the author's conclusions. Every finding below was checked against the code, a build, a test run or the database model. **No finding is high or critical.** 1 finding is medium and 5 are low.

## Summary

MedApp gets staff calendars (an 08:00 to 18:00 UK working day, with blocked time and day and week templates), appointments with patients (Requested, Booked, Attended, Did not attend, Cancelled), and stage visits (a stage can need an attended appointment before it's finished). The core is careful: times are UTC with UK time worked out in one place, clashes are refused on the server under a database lock per member of staff, past days are frozen by dating template rows, and the privacy features (export, anonymisation, restriction, activity log) were joined up. The main weakness is that an appointment itself isn't protected against two people changing it at once: two people booking the same "Needs booking" appointment both succeed, and the second silently moves or overwrites the first (medium).

## What changed, and why

532 files, 39,514 lines added and 718 removed. About 5,300 of the added lines are generated (the migration's designer file 1,691, the snapshot 586, the API types 2,870, the route tree 173), about 18,800 are tests, and about 900 are docs.

- **Why:** the to-do item "Appointments and booking". Staff needed somewhere to book patients' visits, and procedures had no way to say a stage involves one. All 45 decisions are in `docs/calendar-plan.md`.
- **Calendars** (`MedApp.Application/Calendar`, `CalendarController`, the Calendar page): one per member of staff, day and week views, 15-minute rows, weekends greyed. Anyone with a role can read anyone's calendar; only its owner or an admin can change its blocks or templates (403 otherwise).
  - **Blocks** (`CalendarBlock`): time blocked off for an activity from an admin-managed list (`CalendarActivityType`, six seeded: Lunch, Admin time, Training, Meeting, Annual leave, Travel).
  - **Templates:** a day template is a named set of blocks at UK clock times; a week template picks a day template for each weekday; each person can have a default week. A day can use another day template (an "override"), and one template block can be changed or removed for one day (an "exception").
  - **Projection, not generation:** template blocks aren't written into the calendar. They're worked out each time a calendar is read (`CalendarProjection`). Think of a stencil laid over each day as you look at it, rather than paint applied in advance.
  - **Dated rows:** every template row has `ValidFrom` and `ValidTo` (UK dates). An edit ends the changed rows yesterday and adds new ones from today (`TemplateHistory`), so a past day still shows what applied then.
- **Clash checks** (`CalendarClashes`): nothing new may overlap an appointment or a block, stored or projected. Two requests for one person's calendar are put in single file by a SQL Server application lock (`CalendarLock`, `sp_getapplock`): a named lock the database holds for a transaction, like a "one at a time" sign on one person's diary.
- **Appointments** (`MedApp.Application/Appointments`, `AppointmentsController`): a patient, optionally a procedure and stage, a member of staff, a start and an end. Status rules in `AppointmentWorkflow`; every change goes through `AppointmentChange`, which writes a history entry (`AppointmentEvent`, no foreign keys, so it outlives what it's about).
  - **Notifications** to the member of staff when someone else books, moves or cancels their appointment: kinds 1 to 3, fixed titles, no patient name.
  - **Deleting** a patient with appointments is refused; deleting a procedure detaches its appointments; deleting an account with upcoming Booked appointments is refused.
- **Stage visits:** a stage definition's `VisitRequirement` (None, Suggested, Required) is copied onto each stage. When a stage with a visit becomes current, `ProcedureChange.RequestVisit` adds a Requested appointment in the same save. A Required stage can't be finished without an Attended appointment (409), except by an admin with a reason, which is audited. Finishing a stage cancels its Requested appointment; cancelling a procedure cancels all its Requested ones.
- **Needs booking** (a list of Requested appointments), **Home** (your day calendar), and the **timeline** (an Appointments kind, merged with the audit feed by time; staff never see notes or reasons).
- **Privacy joins** (with PR #68's features): the subject access export includes appointments and their history, now with staff account IDs stripped from every history entry's details (`PatientExportMapper.WithoutStaffIds`); anonymisation replaces appointment notes and reasons, in the history too; a restricted patient's appointments can only be cancelled; reading appointments is recorded in the activity log.
- **Migration** `AddedStaffCalendars`: 11 new tables, 3 new columns (`VisitRequirement` twice, `Notifications.AppointmentId`), 6 seeded rows. Additions only.
- **Tools and docs:** `reset_demo_data` clears the new tables (and attachments' records); UI parity screens; `AGENTS.md`, the architecture handbook (ADR-24), hazards H-30 to H-36, DPIA risk 25, the device decision (administrative scheduling, not a device), the retention schedule and the to-do list.

## Design choices

- **Templates projected when read, with history kept by dated rows:**
  - **Instead:** a background job that writes each day's blocks ahead of time, or copying a template's rows on every edit.
  - **Chosen because:** nothing runs in the background, an edit shows at once on every day it applies to, and past days are exact.
  - **Worse at:** every calendar read does more work; and a day's exception points at one version of a template block, so editing that block loses the exception (finding 2).
- **A database lock per member of staff, not a constraint:**
  - **Instead:** a unique index or a database constraint. SQL Server can't express "no two time ranges overlap" as an index, and a serialisable transaction would block far more than needed.
  - **Chosen because:** it works across several copies of the API (unlike an in-memory lock), and serialises exactly the right thing for slot clashes.
  - **Worse at:** it protects a person's calendar, not an appointment. Every write path has to remember to take it, and two requests about the same appointment for different people take different locks (finding 1). It also ties clash checking to SQL Server.
- **One endpoint to book and to reschedule** (`POST api/appointments/{id}/book`):
  - **Instead:** separate book and reschedule calls, or one call that says which it expects.
  - **Chosen because:** less API, and the server works out which it is from the status.
  - **Worse at:** the server can't tell a deliberate reschedule from a stale "book" (finding 1).
- **Staff user IDs without foreign keys,** as with procedure assignments:
  - **Instead:** foreign keys to the users table.
  - **Chosen because:** history must outlive deleted accounts, and it matches the rest of the app.
  - **Worse at:** nothing stops an appointment being left with someone who isn't staff any more; only account deletion is guarded (finding 4).
- **The visit rule enforced on the server, with an admin bypass:**
  - **Instead:** a warning in the UI only.
  - **Chosen because:** it can't be skipped by a stale page or a direct API call, and the bypass is deliberate and audited.
  - **Worse at:** a Required stage blocks the procedure until someone books and attends a visit, even when the visit happened elsewhere; that's what the admin bypass is for.
- **Booking only in the future:**
  - **Instead:** allow recording past appointments (walk-ins).
  - **Chosen because:** simpler rules and no back-dated diaries.
  - **Worse at:** a walk-in can't be recorded, so a Required visit that happened unplanned needs the admin bypass (listed in the plan's section 8).

## How it was checked

- **Read:** the domain, migration, configurations and repositories; every appointment and staff-calendar handler and validator; `CalendarLock`, `CalendarProjection`, `TemplateHistory`, `CalendarClashes`, `WorkingHours`, `UkTime`; the procedure changes (`ProcedureChange`, `StageVisits`, the finish and status handlers); the privacy joins (export, anonymiser, restriction, retention, deletion); the timeline merge; the controllers' authorisation; the key web parts (`calendarSlots.ts`, `BookAppointmentDialog`, `StageVisit`, the grid's layout, the admin route guards); and the docs (the plan, `AGENTS.md`, ADR-24, H-31 to H-33, the device decision, the retention schedule).
- **Merge resolutions** with privacy (#68), NHS (#69), monitoring (#84), attachments (#85) and email and two-factor (#86): notification kinds (1 to 3 beside monitoring's 10 to 17), `Notification.HasFixedTitle`, the anonymiser, the export, the timeline kinds, `DeleteUserHandler` (password confirmation, then the appointments check) and the demo reset. No conflict left a wrong result, apart from the gaps in findings 4 to 6.
- **Build:** `dotnet build MedApp.sln` (Debug, in the review worktree) succeeded with 0 errors and 1 warning, CS0414 in `MedApp.Client.Deprecated`, which this PR doesn't touch.
- **Migration:** `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration." Read by hand: on top of `AddedStaffEmailAndTwoFactorSignIn`, additions only, no renames, so no data can be lost; `Down` drops exactly what `Up` adds.
- **Targeted unit tests:** `dotnet test MedApp.UnitTests --filter` on Calendar, Appointment, PatientExportMapper, StageVisit and FinishProcedureStage: **987 passed**, none failed.
- **Deploy:** nothing under `deploy/`, `.github/`, the AppHost, the Dockerfile or any `appsettings*.json` changed. The feature adds no setting, secret, container, port or outbound connection, so the chart, kind's values and Azure need nothing new (unlike attachments, which needed a secret). `sp_getapplock` needs only the `public` role, which the app's login has, and works on Azure SQL. A read-only look at the kind cluster: it runs `main` (`246beac`), both API pods 2/2 ready; the migrations Job will apply `AddedStaffCalendars` on the first deploy after merging.
- **Not run here:** the full unit and integration suites (running on this commit elsewhere); the web app's lint, type check and tests (this worktree has no `node_modules`); the E2E journeys (the PR reports 20 of 20 at the #85 merge, before the last two merges); the migration up and down on LocalDB (no database writes in this review); the UI.

## Findings

Most severe first. None is high or critical. All six are open at the reviewed commit.

### 1. Two people booking the same "Needs booking" appointment both succeed: the second silently moves or overwrites the first

- **Severity:** medium. **Confidence:** high (read in the code; the "second book becomes a reschedule" path is deterministic and covered by an existing unit test as intended behaviour).
- **What could go wrong:** Needs booking is a shared work queue, so two people working it at once is the normal case.
  - **Same member of staff:** Ana books appointment X with Dr P at 10:00 and phones the patient. Ben, whose list hasn't refreshed, books the same X with Dr P at 14:00. The lock puts them in single file, but Ben's request finds X Booked, and the endpoint treats that as a reschedule. Ben sees "Appointment booked"; X is now at 14:00; Ana is told nothing. The patient was told 10:00.
  - **Different members of staff:** Ana books X with Dr P, Ben books X with Dr Q, at the same moment. Each takes the lock of *their* member of staff, so they don't wait for each other. Both read X as Requested, both save, and the later save wins (a "lost update": the first change is overwritten without anyone knowing). Dr P is told "Ana booked you an appointment on Monday at 10:00", but it isn't on Dr P's calendar, and Dr P is never told it went. The history shows two "Booked" entries and no move.
  - Like two receptionists filling in the same paper slip at once: whoever hands it in last wins, and the other thinks theirs counts.
- **Evidence:**
  - `MedApp.Application/Appointments/Commands/BookAppointment/BookAppointmentHandler.cs:33` takes only the new member of staff's lock; `:35` loads the appointment inside it; `:37` accepts it if it can be booked *or* rescheduled, whatever the caller meant.
  - `MedApp.Contracts/Appointments/Requests/BookAppointmentRequest.cs`: only `StaffUserId`, `StartUtc`, `EndUtc`; nothing says what the caller expected (Requested or Booked) or which version they saw.
  - `MedApp.Infrastructure/Data/Configurations/Appointments/AppointmentConfiguration.cs:7-58`: no row version or concurrency token on `Appointment` (only `Patient` has one, `PatientConfiguration.cs:58-59`).
  - `MedApp.Application/Appointments/Common/AppointmentNotifications.cs:42-44`: each request notifies from the status it read, so both members of staff are told "booked".
  - `MedApp.Web/src/components/calendar/BookAppointmentDialog.tsx:144` decides "reschedule or book" from the page's copy of the appointment; `:245-247` then says "Appointment booked" whatever the server did. `MedApp.Web/src/pages/calendar/NeedsBookingPage.tsx:154-156` opens it with the list's copy.
  - `MedApp.UnitTests/Application/Appointments/Commands/BookAppointment/BookAppointmentHandlerTests.cs:144`: rescheduling through the same call is tested as intended; no test books one appointment twice. `MedApp.IntegrationTests/Calendar/CalendarClashTests.cs:173` and `:196` race two *different* bookings for one slot only.
  - The docs describe the race as covered: `AGENTS.md` ("so one wins and the other gets the 409") and hazard H-31's controls (`docs/clinical-safety/hazard-log.md`, "Racing bookings are serialised").
- **Fix:**
  - **Detect concurrent changes to an appointment:** add a row version to `Appointment` (`builder.Property(x => x.RowVersion).IsRowVersion()`, in a new migration), and turn `DbUpdateConcurrencyException` into a 409 ("Someone else changed this appointment; it's now …"), as patients do. That covers cancel, outcome and notes racing too.
  - **Say what the caller expects:** add `ExpectedStatus` (Requested or Booked) to `BookAppointmentRequest`, sent by the dialog from its copy, and refuse with a 409 when the appointment's status differs. A stale "book" then fails instead of moving someone else's booking.
  - **Tests:** an integration test that books one Requested appointment twice at once with two different members of staff (one 200, one 409, one "Booked" entry, one notification), and one that books it again with a stale expected status (409).
  - **Docs:** add the case to H-31 or H-33, with the control.
- **What happened to it:** fixed in the PR (`6228f7d`). `Appointment` has a row version (`IsRowVersion()`, as `Patient`), and every appointment save turns a lost race into a 409 ("Someone else changed this appointment just now..."). Saving a procedure with its appointments has its own 409. The book request now carries a required `ExpectedStatus` (Requested or Booked), checked inside the lock: a second "book" of an appointment someone has just booked gets "Someone else has already booked this appointment..." instead of becoming a reschedule. `BookAppointmentDialog`, and dragging on the Calendar page, send it. On a 409 the dialog stays open with the message and refreshes. An integration test books one Requested appointment for two different staff at once, over 4 rounds: one success and one 409 each time, one Booked history entry, and only the winner's member of staff notified.

### 2. Editing a template block brings it back on days where someone had removed or moved it

- **Severity:** low. **Confidence:** high (read in the code and the migration).
- **What could go wrong:** Dr P's default week has Lunch 12:00 to 13:00. For next Thursday, Dr P removes it (an exception for that day), and a patient is booked at 12:00. Later an admin changes the template's lunch note to "Staff room". The edit ends the old lunch row yesterday and adds a new row from today. Thursday's exception points at the old row, so it no longer applies: lunch is back on Thursday, over the appointment, flagged as overlapping. Where Dr P had *moved* lunch to 13:00 for the day, there are now two lunches. Nobody is told; the admin can't see it happen. Like a sticky note on one page of a diary that falls off when the printed page is reprinted.
- **Evidence:**
  - `MedApp.Application/Calendar/Common/TemplateHistory.cs:49-63`: a changed row is closed (or deleted, if it started today) and a new row with a new ID is added.
  - `MedApp.Application/Calendar/Common/CalendarProjection.cs:64-67`: exceptions match a template block by its row ID.
  - `MedApp.Infrastructure/Migrations/20261116143719_AddedStaffCalendars.cs:277-281`: deleting a row cascades to its exceptions.
  - `MedApp.Application/Calendar/DayTemplates/Commands/UpdateDayTemplate/UpdateDayTemplateHandler.cs:29-36`: the edit applies the change with no look at exceptions.
  - The docs say only that exceptions on blocks the edit *didn't* change are kept (`docs/calendar-plan.md:33`, `AGENTS.md:184`), not what happens to the others.
- **Fix:**
  - **Keep a block's identity across versions:** give `DayTemplateBlock` a `LineageId` that new versions of the same block inherit (the editor sends each block's ID back), and key exceptions by lineage. Edits then keep the day's changes.
  - **Or, at least, warn:** before saving, count the future exceptions the edit will drop and list them for the admin, as H-31's proposed control does for template changes over appointments.
  - **Docs:** say in decision 12 what happens to exceptions on changed blocks.
- **What happened to it:** left as a to-do (`docs/todo.md`, Calendar follow-ups): keep a block's identity across template versions, or at least warn the admin before an edit drops future days' changes. It's low, and the fix changes the template model.

### 3. "One open appointment per stage" is checked, not enforced, and its 409 doesn't point to the open one

- **Severity:** low. **Confidence:** medium (the race window is small; read in the code).
- **What could go wrong:** two requests that each add an open appointment for one stage can both pass the check: two people pressing "Book visit" on the same stage, or someone adding a visit to Needs booking as the stage before is finished (which requests one automatically). The stage then has two open appointments: Needs booking shows both, and the patient can end up booked twice. The procedure page's visit card shows only one of them. Separately, decision 18 says the 409 "points to the one there is"; the message doesn't.
- **Evidence:**
  - `MedApp.Application/Appointments/Commands/CreateAppointment/CreateAppointmentHandler.cs:59-60`: the check is a read before the save, outside any lock; `:30` the message names nothing.
  - `MedApp.Application/Procedures/PatientProcedures/Commands/FinishProcedureStage/FinishProcedureStageHandler.cs:39` and `:63` with `MedApp.Application/Procedures/Common/StageVisits.cs:28-32`: the automatic request reads the stage's appointments without a lock either.
  - `AppointmentConfiguration.cs:57`: the index on `StageId` isn't unique.
  - `docs/calendar-plan.md`, decision 18.
- **Fix:**
  - **Enforce it in the database:** a filtered unique index, `HasIndex(x => x.StageId).IsUnique().HasFilter("[StageId] IS NOT NULL AND [Status] IN (0, 1)")`, and turn its violation into the existing 409.
  - **Point to it:** put the open appointment's ID (and its time, if booked) in the 409, so the UI can open it.
- **What happened to it:** fixed in the PR (`6228f7d`). A filtered unique index, `IX_Appointments_StageId_Open` (one Requested or Booked appointment per stage), enforces it in the database, in the new migration `AddedAppointmentRowVersionAndOpenStageIndex`. A violation becomes the same 409 as the check. The 409 now describes the open appointment (when and with whom, or that it's in Needs booking), in words: its ID isn't in the response, which would need a change to `ConflictException` and the error middleware. Tested with two creates at once over 4 stages, and directly against the index. The migration was tested up and down on LocalDB.

### 4. Removing someone's roles leaves their upcoming appointments on a calendar nobody can open

- **Severity:** low. **Confidence:** high (read in the code).
- **What could go wrong:** a member of staff leaves, and an admin removes their roles instead of deleting the account (deleting is refused while they have upcoming Booked appointments). Their Booked appointments stay with them. Calendars are only for "staff" (anyone with a role), so their calendar is now a 404 and they're gone from the staff picker: the appointments are on no calendar anyone can open. They're still findable from the patient's page, but nothing prompts anyone to move them. H-35's control ("no appointment is left with nobody") covers deletion only.
- **Evidence:**
  - `MedApp.Application/Authentication/Users/Commands/DeleteUser/DeleteUserHandler.cs:30`: the upcoming-appointments check is in deletion only.
  - `MedApp.Application/Authentication/Roles/Commands/SetUserRoles/SetUserRolesHandler.cs:26`: role changes don't look at appointments.
  - `MedApp.Application/Calendar/StaffCalendars/Queries/GetCalendar/GetCalendarHandler.cs:26` with `MedApp.Application/Calendar/Common/CalendarStaff.cs:12-17`: a calendar is a 404 for anyone without a role.
- **Fix:** refuse a role change that leaves neither Admin nor User while the account has upcoming Booked appointments, with the same `HasUpcomingAppointments` error; or let a calendar be read for anyone who has appointments on it. Add the case to H-35.
- **What happened to it:** fixed in the PR (`f42cb52`). A role change that would leave an account with no staff role (Admin or User) is refused, with the delete's message, while it has upcoming Booked appointments. The delete and the role change share one check.

### 5. Restricting a record never tells the admin that its open appointments freeze

- **Severity:** low. **Confidence:** high (read in the server and the dialog; the server side is tested as intended).
- **What could go wrong:** a patient has two Booked appointments. An admin restricts the record. From then on those appointments can't be moved or given an outcome, only cancelled, so on the day staff can't record whether the patient came. The admin is never told:
  - **With no procedure under way** (appointments booked from the patient page), there's no confirmation step at all.
  - **With procedures under way,** the dialog's warning and checkbox speak only of procedures. Once the box is ticked it sends the confirmation, so the server's new message, which counts the appointments too, is never shown; it only appears if the dialog's own count was out of date.
- **Evidence:**
  - `MedApp.Application/Patients/Commands/SetPatientFlag/SetPatientFlagHandler.cs:38-47`: open appointments are counted only inside `if (underWay > 0)`, and only when the request isn't confirmed.
  - `MedApp.UnitTests/Application/Patients/Commands/SetPatientFlag/SetPatientFlagHandlerTests.cs:207-218` (`Handle_RestrictWithNothingUnderWay_DoesNotCountAppointments`) pins the first case.
  - `MedApp.Web/src/components/patients/PatientFlagDialog.tsx:81-83` decides from the procedures count alone; `:106-118` the warning and checkbox mention only procedures; `:97` sends the confirmation once ticked.
- **Fix:** have the dialog fetch the open appointments too, say "N procedures under way and M open appointments" and ask to confirm when either is above zero; and have the server ask for the confirmation on the same condition.
- **What happened to it:** fixed in the PR (`3089224`). Restricting asks for confirmation when procedures are under way or appointments are open, and the dialog's warning names both counts. If the API asks for confirmation when the dialog's counts were zero, it shows the server's message.

### 6. The retention review still ignores appointments

- **Severity:** low. **Confidence:** high (read in the code; already listed as a to-do).
- **What could go wrong:** the retention review works out "the conclusion of treatment" from procedures, their audit and the patient record. Appointments don't count, so a patient seen last month (an appointment not for a procedure), or with one booked next week, can be listed as due for review, with a due date that's too early. Anonymising is refused while an appointment is open, but not after an attended one. Today nobody reaches the review for about 8 years, so the effect is far off, but the admin making the decision isn't told.
- **Evidence:** `MedApp.Infrastructure/Data/Repositories/PatientRepository.cs:92-118` (no appointments in the filters or the dates); documented as a gap in `docs/governance/retention-schedule.md:125` (section 6, item 3) and `docs/todo.md:67`; nothing on the Retention Review page or the anonymise dialog says so.
- **Fix:** build the to-do (leave out patients with Requested or Booked appointments; take the latest appointment history entry into the conclusion of treatment), or until then add a line to the Retention Review page, as it already does for specialty records.
- **What happened to it:** left as the existing to-do (`docs/todo.md`, "Count appointments in the retention review"). Nobody reaches the review for about 8 years with today's data, so it isn't urgent.

## Remaining risks

- **Appointments have no protection against concurrent edits at all** (finding 1's root cause): cancel, outcome and notes also overwrite each other silently. Procedures have the same gap, so it's a wider debt, but appointments are where people work a shared queue.
- **Two admins editing the same template at once** can each add the new rows, giving duplicated blocks from today. Templates have no lock or row version.
- **A Did not attend isn't followed up by the app:** it leaves the stage with no open appointment and nothing in Needs booking. A Required stage stays blocked (which forces a decision), but a Suggested one can just be finished. H-30 lists this; the control is the "Book another" button.
- **Every calendar read and booking loads every staff account** (`IdentityReadService.GetStaffAsync`, once per role), to check one ID. Fine at today's size; slow with thousands of staff.
- **The hazards H-30 to H-36 are drafts from the design,** not yet checked against the code or rated by a clinical safety officer (`docs/clinical-safety/hazard-log.md:83`). This review found one cause H-31 and H-33 don't list (finding 1).
- **Web checks and E2E weren't re-run here,** and the PR's E2E result predates the last two merges.
- **Calendar blocks, overrides and exceptions are kept for ever** until the retention jobs exist (DPIA risk 5).

## Say it like this

"Staff calendars project templates onto each day when it's read, with history kept by dating template rows, and refuse double bookings on the server under a SQL Server lock per member of staff, so it works across several API copies. The trade-off is that the lock protects a person's diary, not an appointment: the review found that two people booking the same 'needs booking' appointment both succeed, and the fix is a row version on the appointment so the second gets a conflict instead."

## Detail

### Finding 1, step by step

Different members of staff, at the same moment:

```
Ana: POST /api/appointments/X/book {Dr P, 10:00}     Ben: POST /api/appointments/X/book {Dr Q, 14:00}
  lock "medapp-calendar-<P>"  (granted)                lock "medapp-calendar-<Q>"  (granted: a different name)
  load X: Requested                                    load X: Requested
  clash check on P's calendar: free                    clash check on Q's calendar: free
  UPDATE X SET staff=P, 10:00, Booked ...              UPDATE X SET staff=Q, 14:00, Booked ...  (waits for Ana's row lock)
  commit                                               commit: overwrites Ana's booking
  notify P: "Ana booked you an appointment"            notify Q: "Ben booked you an appointment"
  history: Booked (P, 10:00)                           history: Booked (Q, 14:00)
```

Result: X is with Dr Q at 14:00; Dr P holds a notification for an appointment that isn't theirs; Ana believes 10:00. With the same member of staff the two requests wait for each other, and Ben's becomes a "Rescheduled" entry with Ana's time as the previous one; Ben's screen still says "Appointment booked".

A sketch of the smallest fix:

```csharp
// Appointment.cs
public byte[] RowVersion { get; set; } = [];

// AppointmentConfiguration.cs
builder.Property(x => x.RowVersion).IsRowVersion();

// BookAppointmentRequest.cs
/// <summary>What the caller saw: Requested to book it, Booked to move it.</summary>
public string? ExpectedStatus { get; set; }

// BookAppointmentHandler.cs, inside the lock, after loading
if (request.ExpectedStatus is { } expected && appointment.Status.ToString() != expected)
    throw new ConflictException($"Someone else has {AppointmentWorkflow.Describe(appointment.Status)} this appointment meanwhile.");
```

plus catching `DbUpdateConcurrencyException` in `AppointmentRepository.SaveAsync` and turning it into a `ConflictException`, and a migration `AddedAppointmentRowVersion`.

### Finding 2, the data

```
DayTemplateBlocks                      CalendarBlockExceptions
L1  Lunch 12-13  2026-11-01 .. null    E1  Dr P  2026-11-26  -> L1
  admin edits the note today (16 Nov):
L1  Lunch 12-13  2026-11-01 .. 11-15   E1  Dr P  2026-11-26  -> L1   (L1 isn't valid on 26 Nov any more)
L2  Lunch 12-13  2026-11-16 .. null    (no exception for L2)          => 26 Nov projects L2 again
```

### What was checked and found sound

- **Time zones and clock changes:** `UkTime` converts with `Europe/London`; the working day (08:00 to 18:00) never meets the 01:00 and 02:00 changes, so no invalid or ambiguous local times arise. The browser formats with `timeZone: 'Europe/London'` and lays each day out from its own `workStartUtc`, so a week with a Sunday clock change is drawn correctly. Unit tests use the real 25 October 2026 change; the integration test finds the next one.
- **Slot clashes:** create, update, change-for-a-day and book all load, check and save inside the lock; the lock is per person, ordered, released at commit, and retried as a whole by the execution strategy with the change tracker cleared. Two bookings racing for one slot are integration-tested.
- **Authorisation:** every calendar and appointment endpoint needs the Staff policy; setup writes need Admin; block, exception, override and default-week changes check owner-or-admin against the stored record (403), so a block ID or user ID from another calendar gets nothing. The admin pages' routes use `requireAdmin`.
- **Past days:** blocks, exceptions and overrides before today (UK) are refused, including a changed template block moved onto another day (`ChangeTemplateBlockForDayValidator`'s same-day rule).
- **The visit rule:** checked on the server from the stage's own copy of the requirement; only `IsInRole("Admin")` with `withoutVisit` and a non-blank reason passes; the reason is admin-only on the timeline and scrubbed by anonymisation; re-finishing after an unlock doesn't ask again (`IsCompleted` stays true on unlock).
- **Privacy:** the export carries usernames, not IDs, for appointments and their history, and `WithoutStaffIds` removes `userId`, `previousStaffUserId` and any other `…UserId(s)` key at any depth; appointment notification titles are fixed, so anonymisation leaves them; notes, reasons and titles in the appointment history are replaced; the calendar, Needs booking, search and a single appointment are recorded as reads.
- **Migration:** additive, on top of `main`'s last migration, and in step with the model.
