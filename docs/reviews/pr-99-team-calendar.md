# Review: PR #99, personal calendar templates, meetings, the team view and suggested meeting times

**PR:** [#99](https://github.com/Aliator/MedApp/pull/99), branch `feature/team-calendar`, reviewed at `7ef0cac` against `main` at `af1da36` (the merge of #96).

**Reviewed:** 17 November 2026, independently of the PR description's conclusions. Each claim below was checked against the code, the PR's CI run, or the failed end-to-end test's trace. **One finding is high:** the required `e2e` check fails, because the new meeting journey reads the API before its own save has finished (finding 1). Five are medium and five are low. None is critical.

**Out of scope:** `main` has since gained #97 (a rebuilt demo data set in `tools/MedApp.DevMcp/DemoData/`). Merging it in, and seeding meetings and personal templates, gets its own check (Remaining risks says what CI already shows about it).

## Summary

Day and week templates become each member of staff's own, with a migration that gives everyone a copy of what they used, so no calendar changes. Admins can now arrange **meetings** (busy time on several people's calendars), see a **team view** (several people's day side by side) and get **suggested meeting times** (when everyone chosen is free). The server-side rules are careful and well tested, and the migration is correct. The weak points are a failing CI journey, meeting edits that can silently undo each other, and a meeting's free-text title copied into notifications.

## What changed, and why

231 files, 15,410 lines added and 1,457 removed (`git diff --stat af1da36...7ef0cac`).

- **Personal templates** (decision 50 in `docs/calendar-plan.md`):
  - **What it is:** a *template* is a reusable pattern of blocked time. A *day template* is one day ("Clinic day": lunch at 12:00). A *week template* says which day template each weekday uses.
  - **Before:** templates were the organisation's, written by admins, and anyone could be given any of them.
  - **Now:** each template has an owner (`OwnerUserId` on `DayTemplate` and `WeekTemplate`). A person's week templates, day overrides and default week can only use their own templates (a 404 otherwise). The owner or an admin changes them (a 403 for anyone else, `CalendarGuards.EnsureCanChangeTemplatesOf`); everyone can read them.
  - **An analogy:** before, the office had one shared timetable on the wall; now everyone keeps their own diary page, and the manager can write in anyone's.
  - **The API:** lists and adding move under the person (`GET`/`POST api/calendar/staff/{userId}/day-templates` and `.../week-templates`). One template's get, edit, archive, restore and delete stay at `api/calendar/day-templates/{id}` and `week-templates/{id}`, checked in the handlers.
  - **The page** moved from Administration to `/calendar/templates` (`?staff=` for someone else's), with tabs for day templates, week templates and the default week. `/admin/calendar-templates` redirects there.
- **The migration `MadeCalendarTemplatesPersonal`** (decision 51):
  - **What it does:** works out who used each template (through any dated default-week row or a day override), then gives each of them their own copy, with every dated row. The first user by ID keeps the original, so the common case (one user) only gains an owner. Their default-week rows, overrides and block exceptions are pointed at their copies. Templates nobody used go to the longest-standing admin, or are deleted if there's no admin.
  - **An analogy:** photocopying a shared timetable once for each person who used it, and redirecting each person's bookmarks to their own copy.
  - **`Down`** makes every template organisation-wide again. It can't merge copies back, so it renames duplicates "Name (username)". Its comment says so honestly.
- **Meetings** (decisions 53 to 56): `Meeting` and `MeetingAttendee`, admin-only to arrange, change and cancel (`MeetingsController`), readable by all staff.
  - **Busy time:** a meeting counts as busy on each attendee's calendar. Appointments and blocks can't be put over it, and it can't be put over them (`CalendarClashes`, which now includes meetings).
  - **Locks:** adding or changing one takes every attendee's *calendar lock* (a SQL Server application lock per person, `sp_getapplock`), in a fixed order so two requests can't deadlock. An analogy: picking up every attendee's diary before writing in any of them, always in alphabetical order.
  - **Row version:** a number the database changes on every save, so a save based on an out-of-date read can be refused (a 409). See finding 2 for how far that goes here.
  - **Notifications:** kinds 4 to 6 (`MeetingInvited`, `MeetingChanged`, `MeetingCancelled`), with fixed titles and a link to the Calendar on the meeting's day (`Notification.CalendarDate`).
  - **Leavers:** deleting an account, or removing every staff role, takes the person off meetings that haven't started (`RemoveAttendeeFromUpcomingAsync`). A meeting left with nobody is deleted.
- **The team view** (decision 58): `GET api/calendar/team` (admins) returns several people's day and `everyoneFree`. Reading it records `PatientListed` for each patient with an appointment shown, as one calendar does. In the app, it's a third view, **Team**, with an "Everyone free" column.
- **Suggested times** (decision 59): `GET api/calendar/availability` (admins).
  - **How it works** (`Availability`): take each working day after now, subtract everyone's busy time, and offer up to 10 start times on 15-minute steps, at most 3 a day. An analogy: laying everyone's diary pages on a light box and looking for the gaps that go all the way through.
  - **Busy times never name a patient:** an appointment is only "another appointment". A block is its activity ("Lunch"), and a meeting is "the meeting 'title'".
  - It shares `CalendarClashes.BusyTimes` with the clash check, so a suggestion is always a time the server accepts.
- **The grid takes columns** (`CalendarGrid`): a column can be a person on one day, not only a day. PR #96's held-selection behaviour is ported in.
- **Docs:** `AGENTS.md`, `MedApp.Web/AGENTS.md`, the plan, the data inventory, the DPIA (risk 25), the retention schedule, the device decision (version 0.7, no new hazard) and `docs/todo.md`.
- **Tests:** unit tests for every new handler, validator, mapper and helper; integration tests for meetings, access and the migration (`TemplateOwnershipMigrationTests`, up and down against SQL Server); and an end-to-end journey (`MeetingTests`).

## Design choices

- **Personal templates, rather than shared ones with personal defaults:**
  - **Instead:** keep templates organisation-wide and let staff pick from them, or add a "shared" flag.
  - **Chosen because:** staff can shape their own week without asking an admin, and one person's edit can't change twenty calendars by accident.
  - **Worse at:** anything done for many people at once. There's no copy or share tool, so a pattern used by twelve people is twelve sets of templates to keep in step (finding 5).
- **Copy per person in the migration, the first owner keeping the original:**
  - **Instead:** give each template to one owner and leave other users pointing at it.
  - **Chosen because:** it keeps the rule "your calendar only uses your own templates" true from day one, and keeps IDs stable in the common case.
  - **Worse at:** which user keeps the original is decided by SQL Server's GUID order, which is arbitrary. Harmless, since copies are complete, but surprising.
- **Templates nobody used go to the longest-standing admin:**
  - **Instead:** delete them, or keep a pseudo-owner for "the organisation".
  - **Chosen because:** nothing is lost, and it needs no special owner.
  - **Worse at:** that admin can't give them to anyone else (finding 5), and templates used only by leavers go to the leavers, out of reach (finding 10).
- **A lock per attendee, rather than one lock per meeting or a global one:**
  - **Chosen because:** bookings, blocks and meetings already serialise on the person's lock, so one rule covers every clash between them. The integration test races two meetings for one slot.
  - **Worse at:** a 100-person meeting takes 100 locks. Fine at this scale.
- **A row version, but no version in the request:**
  - **Instead:** have the client send the version it saw, as appointments' `expectedStatus` does.
  - **Chosen because:** simpler, and the locks already serialise two changes to the same meeting.
  - **Worse at:** a change based on a stale screen overwrites a newer one silently (finding 2).
- **Cancelling deletes the meeting:**
  - **Instead:** a Cancelled status with a history, as appointments have.
  - **Chosen because:** meetings are staff data, kept no longer than needed.
  - **Worse at:** no record of who changed what (`docs/todo.md` lists a history), and the title lives on only in notifications (finding 3).
- **Suggestions worked out on the server:**
  - **Instead:** the browser could work them out from the calendars it loads.
  - **Chosen because:** the server's clash check and its suggestions use the same busy time, so they can't disagree.
  - **Worse at:** one more endpoint, and up to 100 people's calendars per call.

## How it was checked

- **Read:** the PR description; the whole server side of the diff (domain, configurations, both migrations and the snapshot, the repositories, `CalendarLock`, every new and changed handler, validator and mapper, the controllers, `DeleteUserHandler` and `SetUserRolesHandler`); `TemplateOwnershipMigrationTests` and the meeting and access integration tests; the governance documents the PR changed and the ones it didn't (`data-subject-rights.md`, `privacy-notice-staff-template.md`); and the web app's API client, `CalendarPage`, `MeetingDialog`, `NotificationBell` and the calendar search.
- **CI** (`gh pr checks 99`, run 44014734803 on `7ef0cac`): `web`, `image` (with Trivy), `helm`, `unit-tests`, `integration-tests`, `dependency-review`, CodeQL and the coverage comment pass. **`e2e` fails** (finding 1). Coverage: every new handler, validator and helper is at 100% lines (the `MeetingAttendee` entity is at 66.6%, an unused navigation property); the web app is at 99.8% lines with no file under 80%. CI tests the PR merged into today's `main`, so it includes #97.
- **The failing journey's trace** (the `e2e-traces` artifact, downloaded and read in a scratch folder): the requests, their bodies and statuses, the test's actions and the last screen frames. That's where finding 1's cause comes from.
- **Astryx 0.7.15's `Dialog`** (`node_modules/@astryxdesign/core/dist/Dialog/Dialog.js:679-680`): `purpose="required"` makes the dialog an `alertdialog`.
- **Not run:** no local tests. CI ran the full unit, integration and web suites at this commit, and nothing read here needed a narrower run to settle it. Nothing that uses Docker was run (no integration or end-to-end tests, AppHost, browsers or screenshots).

## Findings

### 1. The new meeting journey fails the required `e2e` check: it reads the API before its save has finished

- **Severity:** high (a required check is red, so the PR can't merge; the product behaves correctly). **Confidence:** high (from the trace, the screen frames and Astryx's code).
- **What goes wrong:** `NewMeeting_FromTheTeamViewAtASuggestedTime_…` fails at `MeetingTests.cs:48` with "Sequence contains no matching element".
  - **The sequence, from the trace:** the test clicks Add Meeting (`:44`). The `POST /api/calendar/meetings` leaves at 04:09:41.438 with the right body (both attendees, Tuesday 17 November, 08:00 to 09:00). `Expect(dialog).ToBeHiddenAsync()` (`:45`) passes at once. The test reads the staff member's calendar (`:48`), finds no meeting and fails, about half a second later. The POST still has no response when the trace ends.
  - **The last frame** shows the dialog still open, with a spinner on Add Meeting and its close button gone.
  - **Why "hidden" passed:** while saving, `MeetingDialog` sets `purpose={isSaving ? 'required' : 'form'}` (`MedApp.Web/src/components/calendar/MeetingDialog.tsx:472`). Astryx gives a `required` dialog the role `alertdialog`. So `GetByRole(AriaRole.Dialog, { Name = "New Meeting" })` stops matching the moment the save starts, and Playwright counts that as hidden.
  - **Why it bites here and not elsewhere:** `BookAppointmentDialog`, `BlockDialog` and others use the same pattern, but their journeys follow "hidden" with an assertion that waits (something appearing on the calendar). This one follows it with a single API read, which doesn't wait. The first meeting saved by a fresh API is also slow (the locks, the transaction and the first run of new code).
  - **Clean-up:** the meeting's clean-up is registered only after it's found (`:49`). If the save lands after a failure, the meeting stays. The comment at `:19` says deleting the staff account takes it off "any meeting the test leaves behind", but the E2E admin is an attendee too, so the meeting stays on the admin's calendar (locally, the dev `admin`).
  - **The PR description** says "The E2E journey is written, and CI runs it": it hadn't been seen passing.
- **Evidence:** `MedApp.E2ETests/Calendar/MeetingTests.cs:19, 44-49`; `MeetingDialog.tsx:472`; Astryx `Dialog.js:679-680`; CI run 44014734803, job 133809819032, and its trace.
- **Fix:**
  - **Wait for the save itself:** wrap the click in `Page.RunAndWaitForResponseAsync(…)` for the POST to `/api/calendar/meetings`, check it's a 201, and take the meeting's ID from its body.
  - **Register the clean-up straight away** from that ID, before any step that can fail.
  - **Optionally,** note in `MedApp.E2ETests/README.md` that a saving dialog is an `alertdialog`, so `ToBeHiddenAsync` on a `Dialog` passes at once and must be followed by something that waits.
- **Status:** open at `7ef0cac`.

### 2. A meeting change made from an out-of-date screen silently undoes someone else's change

- **Severity:** medium. **Confidence:** high (from the code; no test covers it).
- **What could go wrong:**
  - Two admins have the Calendar open. Admin B adds k.ashford to "MDT" and saves; k.ashford is invited.
  - Admin A's page still has the copy from before. A drags "MDT" half an hour later on the grid. The drag sends the cached title, notes and attendees with the new time (`CalendarPage.tsx:256-262`).
  - The server takes the locks of everyone going before and after, so k.ashford's lock is held and the "changed meanwhile" check passes. It saves A's attendee list. k.ashford is taken off and told "admin took you off 'MDT'". B's change is gone, and nobody is told why.
  - The same happens with a long-open Change Meeting dialog, and with title and notes.
- **Why the row version doesn't catch it:** the handler loads the meeting inside the lock and saves it there (`UpdateMeetingHandler.cs:40-61`), so the version it checks is always the newest. Nothing in the request says which version the client saw (`MeetingRequest` has none). The plan says "the meeting's row version catches any other overlapping save" (decision 54); that's true only for saves inside the same few milliseconds.
- **Evidence:** `MedApp.Application/Calendar/Meetings/Commands/UpdateMeeting/UpdateMeetingHandler.cs:23-24, 38-61`; `MedApp.Contracts/Calendar/Requests/MeetingRequest.cs`; `MedApp.Web/src/pages/calendar/CalendarPage.tsx:256-262`; `docs/calendar-plan.md` decision 54.
- **Fix:**
  - **Send what the client saw:** add the meeting's `lastUpdated` (or its row version) to the update request. Compare it inside the lock, and refuse a mismatch with the existing `MeetingLimits.ChangedMeanwhile` 409.
  - **Make a drag change only the time:** a `PUT api/calendar/meetings/{id}/time`, or have the drag send the expected version too.
  - **Test it:** an integration test that saves a stale copy after another admin's change and expects a 409.
- **Status:** open.

### 3. A meeting's free-text title is copied into notifications, clash messages and busy times, and outlives the meeting

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:**
  - An admin arranges "MDT: J. Bloggs' scan results". In a hospital, an MDT (multidisciplinary team meeting) about named patients is common.
  - **The title has no warning.** Notes say "Don't put patient details here", but the Title field says nothing (`MeetingDialog.tsx:311-316` against `:344-346`).
  - **Each attendee's notification quotes it:** "admin invited you to 'MDT: J. Bloggs' scan results', Tuesday 17 November, 09:00 to 10:00" (`MeetingNotifications.cs:29, 56, 64, 67, 81`).
  - **Cancelling deletes the meeting, but not the copies.** The "cancelled" notification quotes the title again. Notifications are kept 6 months by policy, and indefinitely until the purge job exists.
  - **Out of reach of the patient's rights:** the subject access export and anonymisation find notifications by procedure or appointment. These have neither, so a patient named in a title can't be found or removed.
  - **Elsewhere:** the title is in the 409 anyone gets when booking over the meeting (`CalendarClashes.cs:60`), and in the busy times admins see (`GetAvailabilityHandler.cs:41`).
- **Why it matters here:** `AGENTS.md:166` says "Notification text never carries free text or the patient", and the data inventory's `Notification` row lists only assignment, appointment and monitoring notifications, ending "no message holds free text" (`docs/governance/data-inventory.md:35`). Meeting notifications break the rule, and the inventory doesn't list them.
- **Evidence:** as above.
- **Fix:**
  - **Leave the title out of the message:** "admin invited you to a meeting, Tuesday 17 November, 09:00 to 10:00." The link opens that day, where the meeting shows with its title to everyone allowed to see it.
  - **Add the warning to the Title field,** as for notes.
  - **If the title stays in messages:** change the rule in `AGENTS.md`, add meeting notifications to the inventory's `Notification` row, and say in DPIA risk 25 that copies outlive the meeting.
- **Status:** open.

### 4. The staff rights procedure and the staff privacy notice don't cover meetings or personal templates

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:**
  - A member of staff makes a subject access request (asks for all their data). The procedure lists what to search for a staff user: "calendar blocks, default week templates and day overrides…" (`docs/governance/data-subject-rights.md:71`). It doesn't list their meetings (`MeetingAttendees.StaffUserId`, `Meetings.OrganiserUserId`, with titles and notes) or their own templates (`OwnerUserId`). The answer misses them.
  - The rights summary for staff says the same (`:46`).
  - **The staff privacy notice template** ("Your calendar", `privacy-notice-staff-template.md:23`; "Who can see it", `:38`) doesn't say that admins put meetings on their calendar, that every member of staff sees them with their titles and notes, or that their templates are visible to all.
  - `AGENTS.md` (feature slices, step 8) asks for "any other document it makes untrue" to be updated in the same PR.
- **Evidence:** `docs/governance/data-subject-rights.md:46, 71`; `docs/governance/privacy-notice-staff-template.md:23, 38`.
- **Fix:** add meetings (as attendee and organiser) and personal templates to both lists in `data-subject-rights.md`, and a sentence on meetings and templates to the staff notice template, with a version line in each.
- **Status:** open.

### 5. Personal templates have no way to share or copy a template, so a team's pattern becomes many copies to keep in step

- **Severity:** medium (judgement). **Confidence:** high.
- **What could go wrong:**
  - **After the migration:** twelve nurses who shared "Clinic week" each have their own copy, with copies of its day templates.
  - **A change for everyone:** lunch moves to 12:30. Before, one admin edit. Now, twelve, made one person at a time on each person's templates page.
  - **A new starter:** an admin can't give them "Clinic week". Setting their default to someone else's template is a 404 (`SetStaffWeekTemplateHandler`, `GetOwnWeekTemplateOrThrowAsync`). The admin has to rebuild the day templates and the week template by hand in the newcomer's set.
  - **Unused templates** went to one admin, who can't apply them to anyone else either.
  - **Not weighed in the plan:** decision 50 gives the benefits of personal templates; neither the plan nor `docs/todo.md` mentions this cost.
- **Evidence:** `MedApp.Application/Calendar/Common/CalendarRepositoryExtensions.cs` (`GetOwnWeekTemplateOrThrowAsync`, `GetOwnDayTemplateOrThrowAsync`); `TemplateChecks.CheckDayTemplatesAsync`; `docs/calendar-plan.md` decisions 50 and 51; no copy action in `MedApp.Web/src/pages/setup/`.
- **Fix:**
  - **A "Copy to…" action** for admins: copy a day template, or a week template with its day templates, into chosen people's sets (new rows from today, names kept).
  - **At least,** add it to `docs/todo.md`, and a line in decision 50 on what personal templates cost.
- **Status:** open.

### 6. The old API, during the rollout or after a rollback, makes templates nobody owns

- **Severity:** medium. **Confidence:** medium (from the code and the deploy docs; not run).
- **What could go wrong:**
  - **The deploy order:** the migrations Job runs before the new pods, so the old API serves on the new schema for a while. After a rollback by reverting on `main`, it does so until the next deploy: "the database isn't rolled back" (`deploy/argocd/README.md:23`). The chart's README asks for changes the old API can't handle to be split over two releases (`deploy/helm/medapp/README.md:97`).
  - **This migration isn't add-only:** it changes the unique index from `Name` to `(OwnerUserId, Name)` and leaves `OwnerUserId` with a database default of all zeros (`AddColumn` with `defaultValue`, migration lines 46-58).
  - **Old code making a template** doesn't know the column, so the default fills it: a template owned by nobody. Once the new code is back, it's in nobody's list, and only an admin who knows its ID can change it.
  - **Old code's admin page** lists everyone's copies as organisation templates, several with the same name. An admin can set someone's default to another person's copy.
  - **Old code knows no meetings:** appointments can be booked over them, and they don't show.
- **Evidence:** `MedApp.Infrastructure/Migrations/20261116212859_MadeCalendarTemplatesPersonal.cs:46-58`; `deploy/argocd/README.md:23`; `deploy/helm/medapp/README.md:97`.
- **Fix:**
  - **Drop the default constraint at the end of `Up`,** so an insert without an owner fails loudly instead of making an orphan.
  - **Say in the plan and the PR** that a rollback keeps templates personal and hides meetings, and how to give any template owned by nobody to an admin (a one-line SQL `UPDATE`, or a `medapp-dev` tool).
- **Status:** open.

### 7. The team view shows everyone by default, with no limit

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the web app sends no IDs when Who is "everyone" (`MedApp.Web/src/api/calendar.ts`, `teamCalendarQuery`). The API then loads every member of staff (`GetTeamCalendarHandler.cs:25-27`). The 100-person limit applies only to a list given explicitly (`GetTeamCalendarValidator.cs:13-14`).
  - With 400 staff, one page load reads 400 calendars, draws 400 columns, and records `PatientListed` for every patient booked with anyone that day.
  - `docs/todo.md:24` says it draws "a column for each person (100 at most)", which isn't so.
- **Evidence:** as above.
- **Fix:** with no IDs, show the first 100 by name and say "Showing 100 of 400: choose who in Who", or ask for a choice above 100. Correct the to-do.
- **Status:** open.

### 8. Removing a leaver from meetings runs after the account change, and its failure skips the activity log

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** `DeleteUserHandler` deletes the account, then takes the person off upcoming meetings, then records `AccountDeleted` (`DeleteUserHandler.cs:37-50`). `SetUserRolesHandler` does the same for roles (`:44-56`).
  - The clean-up saves with `SaveChangesOrConflictAsync` (`MeetingRepository.cs:47`). If another admin changes one of those meetings at that moment, it throws a 409.
  - **The result:** the account is deleted (or its roles changed), but the admin sees an error and the activity log has no entry for it. A retry gets "user not found".
- **Evidence:** `MedApp.Application/Authentication/Users/Commands/DeleteUser/DeleteUserHandler.cs:37-50`; `…/SetUserRoles/SetUserRolesHandler.cs:44-56`; `MedApp.Infrastructure/Data/Repositories/MeetingRepository.cs:32-48`.
- **Fix:** record the activity entry first. Make the meeting clean-up best effort: catch, log, carry on (a stale attendee is harmless and shows as "Deleted user").
- **Status:** open.

### 9. A meeting with a deleted attendee can't be changed or moved

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** a training day runs 09:00 to 17:00 as a meeting. At 11:00 a leaver's account is deleted. They stay on it, because only meetings that haven't started lose them (`MeetingRepository.cs:36`).
  - At 12:00 the admin cuts it short to 15:00. The dialog keeps the deleted attendee in Who (`MeetingDialog.tsx:233-242`), and the update requires every attendee to be staff (`UpdateMeetingHandler.cs:33`). The result is a 404, "Staff member(s) not found: 3f2a…", with an ID the admin can't read.
  - Dragging it fails the same way, every time (`CalendarPage.tsx:259-261`).
- **Evidence:** as above.
- **Fix:** check only the attendees being added, as monitoring rules check only what an edit newly names (`AGENTS.md`, Monitoring). Keep the ones already going.
- **Status:** open.

### 10. Templates used only by leavers end up out of everyone's reach

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - Deleting an account leaves its default-week rows (nothing deletes them). The migration makes everyone with such a row an owner (migration line 134), whether or not the account still exists or has a role.
  - So "Theatre week", used only by a nurse who left last month, becomes the leaver's. Lists of a non-staff person are a 404 (`GetDayTemplatesHandler.cs:17`), so it disappears from the page where admins used to find it, and can't be given to the replacement.
  - **The retention schedule** says personal templates are kept "while the owner's account exists" (`retention-schedule.md:90`), but deleting an account removes none of them.
- **Evidence:** as above.
- **Fix:** in the migration, give templates whose only users have no staff role to the admin, as for unused ones. Delete, or give to an admin, a person's templates when their account goes, or correct the retention row.
- **Status:** open.

### 11. Some docs still say templates are admin-only

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** someone reading these adds `[Authorize(Roles = "Admin")]` back, or expects the old page.
  - `AGENTS.md:365` (Auth) lists "calendar setup (activity types, and day and week templates; staff can read them)" as admin-only. It doesn't list meetings, the team view or suggested times, which are.
  - `docs/architecture.md:50` says "Admins write day and week templates", and `:652` still lists the `/admin/calendar-templates` page as Admin.
- **Evidence:** as above.
- **Fix:** update the three lines.
- **Status:** open.

## Remaining risks

- **The demo data, in the merged state:** CI tests this PR merged into today's `main`. There, #97's `DemoCalendars` makes templates without an owner (`tools/MedApp.DevMcp/DemoData/DemoCalendars.cs:256, 272` on `main`), so after `reset_demo_data` every template is owned by nobody (finding 6's default). Being handled separately.
- **`Down` can stop part-way:** renaming a duplicate to "Name (username)" fails the unique index if a template already has that name. The migration's transaction then rolls back, so nothing is lost. `Down` is otherwise honest about what it can't restore.
- **A block over a meeting isn't flagged.** `OverlapsAppointment` counts only appointments (`CalendarMapper.cs:36-38, 152-153`). With personal templates, anyone can put template blocks over their own meetings and appointments from today. Both still show, side by side.
- **A race between removing a leaver and changing their meeting:** an update that checked the leaver was staff can add them back after `RemoveAttendeeFromUpcomingAsync`. Unlikely.
- **Copies from closed rows:** someone who once used a template, even years ago, gets an active copy. Their lists may show templates they no longer use.
- **The migration test** covers two people sharing templates, an override, exceptions and a clock change. It doesn't cover an admin keeping an original that other people also use, or a template used only by a deleted account.
- **No meeting history:** who changed or cancelled a meeting isn't recorded (`docs/todo.md`).

## Say it like this

"PR #99 made calendar templates each person's own and added admin-arranged meetings, a team view and suggested meeting times. The migration copies every shared template for each person who used it, so nobody's calendar changes, and meetings take every attendee's calendar lock, so they can't double-book anyone. The trade-off is that a team's shared pattern is now many copies with no copy tool, and the review found a CI journey racing its own save, meeting edits that can silently undo each other, and meeting titles copied into notifications."

## Detail

### Finding 1: the failing run, step by step

| Time (UTC) | What happened |
|---|---|
| 04:09:41.4 | Add Meeting clicked; `POST /api/calendar/meetings` sent with `{"title":"E2E Meeting 9e497c08","startUtc":"2026-11-17T08:00:00Z","endUtc":"2026-11-17T09:00:00Z","attendeeUserIds":[both]}` |
| at once | The dialog becomes an `alertdialog` while saving, so `Expect(dialog).ToBeHiddenAsync()` passes |
| 04:09:41.9 | The test reads the staff member's calendar through the API (17 November to 7 December): no meeting, so `Single` throws |
| end of trace | The POST still has no response (`status: -1`) |

The availability call before it had returned 10 suggestions, the first being today at 08:00 (CI ran at 04:09 UK time), and no busy times.

### Finding 2: why the locks don't help

`UpdateMeetingHandler` locks `previousAttendees ∪ requestedAttendees`, with `previousAttendees` read from the database just before. If another admin added someone, they're in `previousAttendees`, so their lock is taken. The "changed meanwhile" check (`tracked.Attendees.Any(a => !locked.Contains(a.StaffUserId))`) passes, and the stale list is saved. The check only catches attendees added between the first read and taking the locks.

### The migration, on an example

Week template W is used by A and B; day template D by W; C has an override to D; admin M exists. In SQL Server's GUID order, A comes before B and C.

| Row | Owners found | Kept by | Copies |
|---|---|---|---|
| W | A, B (default-week rows) | A | W′ for B, its days pointing at D for B |
| D | A, B (through W), C (override) | A | D for B, D for C, each with every block row |

Then B's default-week rows point at W′, C's override at C's copy of D, and B's and C's exceptions at their copies' blocks. A's rows don't change. Unused templates go to M. Each person's calendar then projects exactly as before. That's what `TemplateOwnershipMigrationTests` checks over 19 October to 4 December 2026, across the clock change.
