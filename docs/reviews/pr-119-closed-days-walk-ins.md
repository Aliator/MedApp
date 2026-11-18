# Review: PR #119, closed days and appointments recorded after the event

**PR:** [#119](https://github.com/Aliator/MedApp/pull/119), branch `feature/closed-days-walk-ins`. Reviewed at `83b57f8`, compared with `main` at `1e1a33a` (the merge base, and `main`'s head when reviewed).

**Reviewed:** 18 November 2026, as an independent, adversarial review, without the author's conclusions. Each claim below was checked against the code, CI's results for `83b57f8`, filtered unit test runs and Vitest runs (some with coverage). No browser, AppHost or Docker was started, by the project's rules. **One finding is high:** the required `web` check fails on coverage, so the PR can't merge as it is. None is critical. 5 are medium and 6 low.

## Summary

The PR adds **closed days** (bank holidays and other days the clinic is shut, which nothing new can go on) and **walk-ins** (an appointment recorded after it happened, straight to Attended or Did not attend). The core rules are sound: every command that puts something on a calendar refuses a closed day from one place, the bank holiday maths is right, and walk-ins go through the calendar lock, the restriction guard and the appointment history. The weak points are around the edges: CI's coverage gate fails, the walk-in dialog logs a month of patient reads, a wrong walk-in can't be withdrawn, closing a day tells nobody what was booked on it, a real visit on a closed day can never be recorded, and the patient's export doesn't say a visit was recorded after the event.

## What changed, and why

171 files, 7,952 lines added and 258 removed: the API and its layers, a migration, 120 new unit tests and two integration fixtures, the web app (a page, a dialog, calendar changes), the demo seed and the docs.

- **Some terms first:**
  - **A closed day** is like a "closed today" sign on a shop door: nobody can book a new visit for that day, but appointments already in the diary are still there.
  - **A walk-in** (here, "an appointment recorded after the event") is like writing a visit into a paper diary afterwards, with "added later by J. Smith" beside it.
  - **A validator** is the check that runs before a command does anything, and turns bad input into a 400.
  - **The calendar lock** is a database lock per member of staff, so two requests can't book the same slot at once.
  - **A Requested visit** ("Requires Appointment") is the placeholder appointment the app makes when a stage that needs a visit becomes current; someone then books it.
- **Closed days** (`ClosedDay`: a UK date and a name, one per date):
  - Admins add and remove them on a new page, Administration, Closed Days (`/admin/closed-days`), one year at a time. `POST`/`DELETE api/calendar/closed-days`; anyone reads them (`GET`).
  - **"Add Bank Holidays for <year>"** adds England and Wales's eight bank holidays in one call, worked out in code (`BankHolidays`), and lists what it skipped (already closed, or past).
  - **One rule refuses them everywhere:** `WorkingDays` looks up the day's closed days, and every validator that put something on a calendar now asks it (booking, rescheduling, blocks, a day's template block, meetings, Apply Template). The same 400 as a weekend.
  - **Calendars** show a closed day greyed, "Closed: Christmas Day", with no template blocks and no free or suggested times. What was already on it stays, and the grid flags each item "on a closed day".
  - **Past days don't change:** adding or removing a closed day before today is a 409.
- **Walk-ins** (`POST api/appointments/past`, `RecordPastAppointmentHandler`):
  - Staff record one with the patient (and optionally procedure and stage), the member of staff, a time and the outcome. Its start must have passed, within 30 days, on a working day, inside the working day.
  - It goes through `AppointmentChange.RecordAfterEvent`: one history entry with a new action, `RecordedAfterEvent = 7`, and a flag on the appointment, `IsRecordedAfterEvent`.
  - It may not overlap another appointment of that person (409, under the calendar lock), but may overlap blocked time or a meeting, which then show flagged.
  - **For a stage:** if its visit is waiting in Requires Appointment, that appointment **becomes** the walk-in, so an Attended walk-in satisfies a Required stage. A stage with a visit already Booked is a 409.
  - The member of staff is told when someone else recorded it (`AppointmentRecorded = 9`, a fixed title).
  - From the Calendar page's New menu, and a stage's **Record Past Visit**. The dialog shows the patient as the patient banner does.
- **The migration** (`AddedClosedDaysAndPastAppointments`) adds the table and the flag. Its hand-written `Down` turns walk-in history entries into ordinary Attended or Did not attend ones and deletes kind 9 notifications, which the older code doesn't know.
- **Docs:** calendar plan decisions 72 to 78, a new draft hazard **H-39** (a visit recorded that didn't happen, or on the wrong patient), H-31 to H-35 updated, the data inventory and DPIA, `AGENTS.md`, and the demo seed (bank holidays saved as closed days, and nothing seeded on them).
- **Why:** both were open items in the calendar plan and the to-do list. Without closed days, staff could book Christmas Day. Without walk-ins, a visit that happened but wasn't booked couldn't be recorded at all, so a Required stage could only be finished by the admin bypass.

## Design choices

- **Bank holidays worked out in code, rather than read from gov.uk's JSON feed:**
  - **Chosen because:** no network dependency, no outside service in the threat model, and the rules are stable. Tested against gov.uk's published dates for 2025 to 2028, substitute days included.
  - **Worse at:** one-off changes (a coronation, a jubilee moving the spring bank holiday) are never picked up; admins must remember to add or move them. Only England and Wales.
- **A closed day refused in the validator, as a weekend is, rather than as a busy block on each calendar:**
  - **Chosen because:** one rule, one message, the same 400 everywhere; templates and free times skip the day with no extra rows.
  - **Worse at:** the check reads the database before the handler takes the calendar lock, so a day closed at the same moment as a booking doesn't stop that booking (finding 7). And a new command must remember to use `WorkingDays` (finding 8).
- **What's already on a day that closes stays, rather than being cancelled or moved:**
  - **Chosen because:** the app never cancels a booked appointment by itself, since someone has to tell the patient (the same rule as cancelling procedures).
  - **Worse at:** nobody is told it's there (finding 4).
- **A separate "record after the event" endpoint, rather than letting booking accept past times:**
  - **Chosen because:** it keeps "booking is only in the future" true, keeps the history honest ("recorded after the event", not "booked"), and lets the rules differ (30 days back; overlapping a block is allowed).
  - **Worse at:** it's one more path to keep in step with booking: the export page (finding 6) and the outcome corrections (finding 3) weren't brought along.
- **The stage's Requested visit becomes the walk-in, rather than being cancelled beside a new one:**
  - **Chosen because:** one appointment per visit, a readable history ("requested, then recorded after the event"), and "one open appointment per stage" holds with no special case.
  - **Worse at:** a walk-in on the wrong stage or patient also uses up that stage's real request, and nothing can bring it back (finding 3). The visit's notes are replaced (finding 10).
- **Walk-ins may overlap blocks and meetings, but not appointments:**
  - **Chosen because:** "what happened, happened": a clinician who saw a patient over lunch did see them. Two appointments at once for one person is more likely a mistake.
  - **Worse at:** the same principle isn't applied to closed days (finding 5).
- **Nothing in the activity log for recording a walk-in:** consistent with booking, which records nothing there; the appointment's history says who and when. Reasonable. The dialog's calendar read is another matter (finding 2).

## How it was checked

- **Read:** the whole diff, and the code around it: every calendar and appointment command's validator and handler, `CalendarRepository`, `CalendarMapper`, `Availability`, `StageVisits`, `ProcedureWorkflow`, the export mapper and page, the anonymiser, the timeline mapper, the migration, the demo seed and the governance and hazard documents.
- **Every command that books, moves or blocks time** was checked for the closed-day rule. All nine use `WorkingDays`: `CreateAppointment`, `BookAppointment` (booking, rescheduling and the calendar's drag), `RecordPastAppointment`, `CreateCalendarBlock`, `UpdateCalendarBlock`, `ChangeTemplateBlockForDay`, `ApplyDayTemplate`, `CreateMeeting` and `UpdateMeeting`. The rest only remove things, or make a Requested visit, which has no day. No bypass was found.
- **Bank holidays** were checked by hand against gov.uk's rules: Christmas on a Saturday (2021, 2027: Monday 27 and Tuesday 28), on a Sunday (2022: Boxing Day keeps Monday 26, Christmas moves to Tuesday 27), New Year's Day on a Saturday or Sunday (2022, 2023, 2028), Easter by the anonymous Gregorian algorithm, the first Monday in May and the last Mondays in May and August. All right.
- **UK time:** a closed day is a UK date, and every check turns the start into a UK date first (`UkTime.DateOf`), so British Summer Time can't shift one. Bank holidays never fall on a clock-change Sunday.
- **Unit tests** (Release): `RecordPastAppointment`, `BankHolidays`, `ClosedDay`, `AppointmentNotifications`, `AppointmentChangeTests` and `BookingTimes`: **189 passed**.
- **Vitest:** the walk-in dialog, the Closed Days page, `closedDays`, `CalendarGrid`, `MeetingDialog` and the server rendering test: **115 passed**. Then 15 changed files again with coverage, to find what finding 1's gate misses: 327 passed.
- **CI at `83b57f8`:** `unit-tests`, `integration-tests` (including the new `ClosedDayTests` and `PastAppointmentTests`), `e2e`, `image`, `helm` and CodeQL passed. **`web` failed** (finding 1).
- **Not done:** no browser, axe or parity capture (finding 12), no run of the migration (the PR says it was run up, down and up on LocalDB), no full suite.

## Findings

### 1. High: the required `web` check fails, as statement coverage drops under 99%

- **Severity:** high. **Confidence:** high.
- **What goes wrong:** CI's `web` job runs Vitest with coverage thresholds, and fails at 98.96% statements against 99%. `web` is a required check, so the PR can't merge, and the merge flow stops at its CI step.
- **Evidence:**
  - CI run 44227058681, job `web`, step "Unit tests with coverage": all 2,098 tests pass, then `ERROR: Coverage for statements (98.96%) does not meet global threshold (99%)` (8,051 of 8,135 statements).
  - `MedApp.Web/vite.config.ts:49`: `thresholds: {lines: 99, statements: 99, functions: 99, branches: 92}`.
  - New statements no test runs: `src/pages/setup/ClosedDaysPage.tsx:124` (the save guard), `:147` (the remove guard), `:271` and `:306` (Cancel on both dialogs); `src/components/calendar/RecordPastAppointmentDialog.tsx:289-290` (choosing another member of staff in "With").
- **Fix:** tests that cancel Add Closed Day and Remove Closed Day; one that presses Ctrl+S in Add Closed Day with no name (the shortcut can call the guarded save); and one that changes "With" in the walk-in dialog and checks the other person's calendar is read and the start is cleared. `if (!removing) return;` can't be reached (the dialog only opens with a day), so remove it rather than test it. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. Tests now cancel both Closed Days dialogs, press Ctrl+S in Add Closed Day with no name, and change "With" in the walk-in dialog, alongside tests for everything the later fixes added. The unreachable `if (!removing) return;` is gone. `npm run coverage` passes: statements 99.06%, branches 93.12%, functions 99.33%, lines 99.5% (thresholds 99, 92, 99, 99).

### 2. Medium: opening Record Past Appointment logs a read of every patient on that person's last month

- **Severity:** medium. **Confidence:** high.
- **What goes wrong:**
  - The activity log is MedApp's visitors' book for patient records: each patient's page shows who looked at them. A calendar read signs that book ("listed") for every patient whose appointment it returns.
  - The walk-in dialog reads 31 days of a person's calendar **with its appointments**, only to know which times are taken. It shows none of those patients. So one click records a "listed" entry for every patient that person saw that month.
  - **Scenario:** a nurse opens Record Past Visit from a stage to record one walk-in. The assignee saw 60 patients this month. Those 60 patients' access reports now say the nurse listed them. Changing "With" twice adds two more people's months. An admin looking into a snooping complaint about one of those patients finds the nurse's name with no reason for it.
  - The staff privacy notice says calendars record the patients they show you; this dialog shows none.
- **Evidence:**
  - `RecordPastAppointmentDialog.tsx:163`: `calendarQuery({staffUserId: staffId, from, to: today})`, where `from` is 30 days back, without `WithoutAppointments`.
  - `GetCalendarHandler.cs:34`: records `PatientListed` for every appointment's patient.
  - `ActivityLog.cs:54-74`: one entry per distinct patient, deduplicated only within 5 minutes.
  - The dialog's own tests expect that request: `RecordPastAppointmentDialog.test.tsx:87` and `:180`.
  - The precedent: Apply Template reads days with `calendarDaysQuery` (`withoutAppointments`) for exactly this reason (review of #108).
- **Fix:** read the days with `calendarDaysQuery`, which records nothing. For the taken times, either let the server decide (offer every started time; a clash is the 409 the dialog already shows, naming the time but not the patient), or add a calendar option that returns appointment times without patients and records nothing. Add a test that the dialog asks `WithoutAppointments=true`. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. The walk-in dialog reads the person's days with `calendarDaysQuery` (`withoutAppointments=true`), so it names no patients and records no reads. It offers every start time that has begun by now inside the working day, and an overlap is the server's 409, which keeps the dialog open. A test checks the dialog asks with `WithoutAppointments=true` and never without.

### 3. Medium: a walk-in recorded on the wrong patient, or that never happened, can't be withdrawn

- **Severity:** medium. **Confidence:** high.
- **What goes wrong:**
  - The only correction is flipping the outcome between Attended and Did not attend. Cancelling is for Requested and Booked only, and nothing deletes an appointment.
  - **Scenario:** a walk-in is recorded on "SMITH, John" instead of "SMITH, Jon" (same name, H-01). It's noticed the next day. Patient A's record must now say either "Attended" (false) or "Did not attend" (also false, and a missed appointment can count against a patient under a clinic's DNA policy). A's timeline shows staff "Appointment recorded after the event: attended", with no sign of the error, because staff never see notes there. A can never be deleted (a patient with appointments can't be). If it was recorded against A's stage, it also **used up** A's real Requested visit, which drops out of Requires Appointment for good.
  - The hazard log counts the outcome correction as H-39's corrective control, and the rights procedure says an appointment can be rectified by its outcome. Neither is enough for "this never happened".
  - It's like writing in a paper record in pen with no "struck through, entered in error" convention. H-37 already plans one for notes.
- **Evidence:**
  - `AppointmentWorkflow.cs:49`: `CanCancel => IsOpen(status)` (Requested or Booked).
  - `RecordPastAppointmentHandler.cs:89-104`: the Requested visit becomes the walk-in.
  - `docs/clinical-safety/hazard-log.md:1477`: "The outcome can be corrected ... Corrective; built".
  - `docs/governance/data-subject-rights.md:38`: rectification of "an appointment's notes and outcome".
- **Fix:** an "entered in error" step for appointments recorded after the event, by whoever recorded it or an admin, with a reason: a history entry (a new `AppointmentEventAction`, never renumbering), off the calendar, not counted by `FinishesWithoutVisit`, and, if it took over a Requested visit, the stage's visit requested again (`StageVisits`). If the stage was already finished on its strength, flag that for an admin. Until it's built, say plainly in H-39 and the rights procedure that a wrong walk-in can't be withdrawn, and put it to the clinical safety officer (CSO). **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. `POST api/appointments/{id}/entered-in-error`, with a reason (500 characters), by whoever recorded the walk-in or an admin (403 otherwise), while it still has its outcome and within 30 days of it. It becomes `EnteredInError` (a new status, 5): off the calendar, not open, its notes fixed, no longer a stage's visit. The history gets a new action, `EnteredInError` (8), with the reason and the outcome it had; staff see the entry on the timeline and admins also see the reason. It's allowed while the record is restricted (it's rectification), but not once it's anonymised. If it was a stage's visit, `StageVisits.AfterVisitWithdrawn` asks for the visit again by the same rules as when a stage becomes current, in the same save, and the response says so or why not (for example, the stage was finished on it and an admin should unlock it). `AppointmentDialog` offers Entered in Error to the recorder or an admin, with the reason and the stage's message. H-39, `data-subject-rights.md`, the plan (decision 74) and `AGENTS.md` describe it. The migration's `Down` turns it into a cancellation.

### 4. Medium: closing a day tells nobody what's already booked on it, and only the calendar grid marks it

- **Severity:** medium. **Confidence:** medium.
- **What goes wrong:**
  - Closing a day leaves its appointments, blocks and meetings in place (decision 75). That's right, but the only sign is on the calendar grid, for whoever opens that week. Nobody is notified, the Add Closed Day dialog doesn't say how many are booked, and appointments elsewhere carry no mark.
  - **Scenario:** on Monday an admin adds a staff training day for Friday. 14 patients are booked that day with four clinicians. The procedure pages still say "Booked: Fri 27 Nov, 10:00 with k.ashford", as do the appointment dialog, a patient's appointments and Home's day list. Unless someone opens that week on the Calendar page, patients turn up to a closed clinic, or a time-critical visit slips (H-30, H-35).
  - The hazard log counts "shown, flagged" as an existing control for H-35. It's only half there.
- **Evidence:**
  - `CreateClosedDayHandler.cs:23-35` and `AddBankHolidaysHandler.cs:18-53`: no look at what's on the day, and no notification.
  - `CalendarMapper.cs:32-36`, `AppointmentResponse`: no closed-day flag on an appointment.
  - `CalendarGrid.tsx:156` and `:331`: the only place items are flagged.
  - `docs/clinical-safety/hazard-log.md:1362`: "Appointments on a day that later closed are shown, flagged" (control); `:1369`: listing them "proposed"; `docs/calendar-plan.md:141`.
  - Related: a meeting or block on a closed day can't be edited without moving it, as the validators refuse its unchanged time (`UpdateMeetingValidator`, `UpdateCalendarBlockValidator`); cancelling or deleting still works.
- **Fix:** small, and before first live use as the hazard log already says. Have Add Closed Day (and Add Bank Holidays) say what's booked on each date ("14 appointments and 1 meeting are booked then"), ideally before saving; tell each member of staff with Booked appointments that day (a fixed-title notification in the calendar range); and add `isOnClosedDay` to appointment responses so lists and the stage's Visit show it. Until then, reword the H-35 control to "on the Calendar page's grid". **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. `GET api/calendar/closed-days/bookings?date` (admins) counts the appointments, stored blocks and meetings on a day for each member of staff. The Add Closed Day dialog shows it before saving, and the add responses (one day, and each bank holiday added) carry it. Each member of staff with something on the day, except the admin who closed it, gets a new notification kind, `DayClosed` (8), titled "Day closed": who closed which day, the closed day's name, and what they have then, never a patient. H-35's control now says the grid marks them and that the staff are told. An `isOnClosedDay` flag on appointments outside the calendar is left to `docs/todo.md`.

### 5. Medium: a real visit on a closed day can never be recorded, not even on a day closed part-way through

- **Severity:** medium. **Confidence:** medium.
- **What goes wrong:**
  - A walk-in must be on a working day that isn't closed, and a closed day can't be removed once it's past. So a visit that did happen on a closed day can never go into MedApp.
  - **Scenario:** snow at 11:00. The admin closes today so nothing more is booked (today can be closed). The morning's two walk-ins weren't entered yet. From now on they can't be: today is closed, and after midnight the closed day can't be removed. For a Required stage, the only way on is the admin bypass, which records "finished without a visit": false. The same happens if the clinic decides to open on a bank holiday after the year's were added in one click, and forgets to remove that one.
  - The PR allows walk-ins over blocked time and meetings because "what happened, happened" (decision 76). The same reasoning applies to closed days.
- **Evidence:**
  - `BookingTimes.cs:41`: walk-ins go through `WorkingDays.WhyOutsideAsync`, which refuses closed days.
  - `DeleteClosedDayHandler.cs:17`: a past closed day can't be removed.
  - `RecordPastAppointmentDialog.tsx:169-170`: the dialog offers only working days.
  - `docs/calendar-plan.md:140` (decision 74): "on a working day that wasn't closed".
- **Fix:** let walk-ins ignore closed days (keep the weekday and working-hours rules), and flag them on the grid like anything else on a closed day. Or let an admin remove a past closed day, audited. Update decision 74 and H-39. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. A walk-in may be on a closed day (a weekday, inside the working day): its validator doesn't look closed days up, the one exception in the new convention test. The dialog offers closed weekdays, labelled "closed: Christmas Day", and the visit shows flagged on the day. Decision 74 and H-39 updated. An integration test records a walk-in on a day closed part-way through.

### 6. Medium: the patient's export doesn't say a visit was recorded after the event, and calls it requested and booked

- **Severity:** medium. **Confidence:** high.
- **What goes wrong:**
  - The subject access export (what a patient gets when they ask for their record) has no idea walk-ins exist. The appointment carries no flag, and the page names the history action by its raw code.
  - **Scenario:** after a complaint about a visit on Thursday 12 November, the patient asks for their record. It reads: "Attended: Thu 12 Nov, 10:00 to 10:30 with k.ashford; requested 18 Nov by j.smith, booked 18 Nov by j.smith, outcome recorded 18 Nov by j.smith", and in the history "18 Nov: RecordedAfterEvent by j.smith", with no outcome. It looks as if the visit was booked six days after it happened, which reads like an error or tampering.
  - The DPIA's version 0.11 says "the export and anonymisation already cover appointments and their history". The data is there; the words aren't.
- **Evidence:**
  - `PatientExportMapper.cs:121-141` and `PatientExportResponse.cs:216-246`: no `IsRecordedAfterEvent`.
  - `PatientExportPage.tsx:63-74`: no label for `RecordedAfterEvent`, so the code shows; `:92-111`: no details for it (the outcome isn't said); `:434-435`: "requested … by …, booked … by …".
  - `docs/governance/dpia.md:7` (version 0.11).
- **Fix:** add `IsRecordedAfterEvent` to `ExportedAppointmentResponse`. On the page, say "recorded after the event <when> by <who>" instead of "booked" (keeping "requested" only for a visit that was waiting in Requires Appointment, as `AppointmentDialog` does with `wasRequested`). Label the history action "Recorded after the event", with its outcome and "was in Requires Appointment". Tests in `PatientExportMapperTests` and `PatientExportPage.test.tsx`. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. `ExportedAppointmentResponse.IsRecordedAfterEvent`. The export page says "recorded after the event … by …" instead of "booked", and keeps "requested" only for a visit that was waiting in Requires Appointment. The history labels "Recorded after the event" (with its outcome and "was in Requires Appointment") and "Marked entered in error" (with its reason). Tests in `PatientExportMapperTests` and `PatientExportPage.test.tsx`.

### 7. Low: three races the checks don't close

- **Severity:** low. **Confidence:** medium.
- **What goes wrong:** each window is milliseconds and none loses data, but each breaks a stated rule.
  - **Closing a day and booking it at the same moment:** the closed-day check runs in the validator, before the calendar lock, and closing a day takes no lock. A booking validated just before the day closes is saved after, on the closed day.
  - **A walk-in and a booking for the same stage:** the "visit already Booked" check (`RecordPastAppointmentHandler.cs:64`) runs outside any per-stage lock. The walk-in is inserted as Attended, which the unique index (Requested or Booked only) doesn't see. So both succeed, and the stage has a booked visit and a recorded one, which decision 77 says can't happen.
  - **A walk-in for a stage that's just becoming current:** `StageVisits.cs:27-31` sees no visit yet and requests one, beside the walk-in. A needless item appears in Requires Appointment.
- **Evidence:** `WorkingDays.cs:16-23`; `CreateClosedDayHandler.cs:23-35`; `RecordPastAppointmentHandler.cs:64, 82-110`; `StageVisits.cs:27-31`.
- **Fix:** accept and document them. For the second, re-check `GetOpenForStageAsync` inside the lock before inserting (narrows the window; doesn't close it, as the booking may take another person's lock). **Left to the PR** (see below).
- **What happened to it:** fixed where it could be, the rest documented. Handlers check closed days again under the calendar lock (`CalendarClashes.Find`, `MeetingStaff.EnsureEveryoneFreeAsync`: a 409), so a day closed after the validator ran is caught; closing a day still takes no calendar lock, so a booking whose locked read came first can land. A walk-in re-checks the stage's open visit under its lock; a booking for the same stage under another person's lock can still slip past. A stage becoming current at that moment can still add a needless Requested visit. Decision 77 and H-39 name all three.

### 8. Low: nothing makes the next calendar command use the closed-day rule

- **Severity:** low. **Confidence:** high.
- **What goes wrong:** `WorkingHours.WhyOutside(start, end, closedDays = null)` treats no set as "no closed days", so the closed-day-blind call is the shortest one to write. `AGENTS.md` says validators must use `WorkingDays`, but no test checks it. Today all nine do; the tenth may not.
- **Evidence:** `MedApp.Application/Calendar/Common/WorkingHours.cs:55`; `AGENTS.md` (Closed days: "validators use `WorkingDays`, never `WorkingHours.IsWorkingDay` alone").
- **Fix:** make `closedDays` required (pass `ClosedDaySet.None` where that's meant), and add a convention test: every validator of a command with `StartUtc` and `EndUtc`, or a calendar `Date`, takes `WorkingDays`. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. `WorkingHours.WhyOutside` now requires the closed days (`ClosedDaySet.None` only where they deliberately don't count). `WorkingDaysConventionTests` checks that every command validator with a `StartUtc` and `EndUtc`, and Apply Template's, takes `WorkingDays`. The walk-in is the one listed exception, with its reason.

### 9. Low: a walk-in's time isn't checked against its procedure or stage

- **Severity:** low. **Confidence:** medium.
- **What goes wrong:** nothing compares the visit's time with when the procedure was created or the stage was reached, and the dialog offers every unfinished stage, future ones included. A visit from three weeks ago can be recorded against a Required stage reached yesterday, or a procedure created today, and satisfies it. Some are legitimate (a procedure entered late), so a hard refusal would be wrong.
- **Evidence:** `RecordPastAppointmentHandler.cs:48-73` (no time check); `RecordPastAppointmentDialog.tsx:158, 272-276` (every unfinished stage); `ProcedureWorkflow.cs:138-141` (any Attended appointment on the stage counts).
- **Fix:** a warning in the dialog ("This visit was before the procedure was created" or "before this stage was reached"), and name the cause in H-39. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR, as a refusal rather than a warning. A walk-in from before its procedure was created, or for a stage after the current one, is a 409 saying why and what to do instead (`AppointmentWorkflow.BeforeProcedureMessage`, `WhyStageNotReached`). The dialog offers only the current stage and earlier ones. A procedure entered late can still have its visit recorded without the procedure.

### 10. Low: recording a stage's Requested visit replaces its notes without showing them

- **Severity:** low. **Confidence:** high.
- **What goes wrong:** the dialog's Notes start empty. If notes are typed, they replace the visit's own. The old text survives only in the history, which staff don't see (the timeline gives staff no notes; admins see both).
- **Scenario:** the Requested visit says "Needs a Polish interpreter". The walk-in's notes, "Seen, bloods taken", replace it; the interpreter note is gone from everything staff can see.
- **Evidence:** `RecordPastAppointmentHandler.cs:99-100`; `RecordPastAppointmentDialog.tsx:297-306`.
- **Fix:** start the Notes field with the visit's notes, or append the new text. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. When a walk-in takes over a stage's waiting visit, the visit's notes are kept and the new ones are added after them, with a blank line between (a 409 if together they're over 2,000 characters). The dialog shows the waiting visit's notes first ("Its notes, which are kept").

### 11. Low: documents behind the code

- **Severity:** low. **Confidence:** high.
- **What's out of date:**
  - **The governance pack** still lists three appointment notification titles: `docs/governance/dpia.md:192` (the measure) and `docs/governance/data-inventory.md:79` (Valkey). The staff privacy notice template doesn't mention recording after the event or its notification (`privacy-notice-staff-template.md:22, 28`).
  - **`ClosedDaysPage.tsx:86`** says "decisions 61 to 69"; they're 72 to 75.
  - **`NotificationKind.cs`** says 7 and 8 are left "for the meeting kinds being added alongside it"; the meeting kinds are 4 to 6.
  - **`schema.d.ts` was edited by hand,** not generated with `npm run gen:api` as `AGENTS.md` asks. Its descriptions are missing, and nothing checks it matches the API.
- **Fix:** update them; regenerate the schema with the AppHost running. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR. The DPIA's notifications measure and its version 0.11, the data inventory (Valkey and the appointment statuses), the staff privacy notice, `ClosedDaysPage.tsx` (decisions 72 to 75) and `NotificationKind.cs` are updated. `schema.d.ts` still can't be regenerated without the AppHost running; it's hand-edited to match the API and listed in `docs/todo.md` to regenerate.

### 12. Low: not yet looked at in a browser, and no screen shows a day that closed with things on it

- **Severity:** low. **Confidence:** high.
- **What's missing:**
  - The PR says the browser check is still to do. `AGENTS.md` asks for a look, the parity captures and the axe audit of the screens a change touches.
  - The new parity screens cover the dialog and the Closed Days page, but not a calendar week with a closed day, where the "Closed: …" label sits at the top of a column that may hold items, or a flagged item on one.
  - The demo data has no walk-ins, so "Recorded after the event" and its timeline entry can't be seen without making one.
- **Evidence:** the PR description ("Browser: before merging"); `tools/ui-parity/screens.ts` (three new screens).
- **Fix:** capture a week with a bank holiday that has an appointment on it, run axe on the three screens, and seed one walk-in. **Left to the PR** (see below).
- **What happened to it:** fixed in the PR, apart from the browser look, which the coordinator did. `tools/ui-parity/screens.ts` has the Closed Days page and its Add dialog, the walk-in dialog, and the Christmas week with its bank holiday greyed (`calendar-closed-day-week`). The demo seed records one walk-in (s.brightwell's, recorded the next morning by the booking coordinator), with its notification. The browser check found both features working and axe clean; its two layout notes are below.

### Browser check (after the review)

- **The bank holiday result squeezed into the page header's message area,** with its list of skipped days.
  - **What happened to it:** fixed. The result is now a dismissible alert in the page body, above the list, with each day added (and what's on it) and each skipped day underneath.
- **In the team view, the "Everyone free" column on a closed day** wasn't greyed or labelled like the people's columns.
  - **What happened to it:** fixed. On a closed day it's greyed and labelled "Closed: <name>" like the others, and nothing in it can be selected. The team response's own day now carries the closed day too (`GetTeamCalendarHandler`), so it's right even with nobody shown.

## Remaining risks

- **During a deploy,** old API copies run against the migrated database for a few minutes. They don't know closed days (a booking through one isn't refused), and show action 7 and kind 9 by number.
- **`Notification.HasFixedTitle` now covers 1 to 9,** including the unused 7 and 8. A future kind there with free text in its title would be skipped by anonymisation.
- **One-off bank holiday changes,** and Scotland's and Northern Ireland's, are by hand.
- **No admin report of walk-ins** (H-39's proposed control), and walk-ins aren't a monitoring event. Recording one's own wrong walk-in tells nobody.
- **H-39's ratings are a draft** for the CSO, and findings 3 and 5 weaken its controls.
- **No end-to-end test** covers a walk-in or a closed day.

## Say it like this

"Closed days work like a 'closed' sign on the clinic's door: one rule refuses anything new on them everywhere, and bank holidays are worked out in code rather than fetched from gov.uk, which trades automatic updates for no outside dependency. Walk-ins let staff record a visit after it happened, with its own history entry, so the record says it was added later rather than pretending it was booked. The review's main point is that the edges need the same care as the rules: a wrong walk-in needs an 'entered in error', closing a day should tell people what's booked on it, and a dialog that only needs free times shouldn't log a month of patient reads."

## Detail

### The coverage gate (finding 1)

- **CI's summary at `83b57f8`:** statements 98.96% (8,051 of 8,135), branches 93.01%, functions 99.18%, lines 99.46%. Only statements fail.
- **The margin:** at 99%, at most 81 of 8,135 statements may go uncovered; there are 84. Covering the six new ones is enough.
- **Uncovered statements in the changed files,** from Vitest with coverage on each file's own tests:

| File | Uncovered lines | New in this PR? |
|---|---|---|
| `ClosedDaysPage.tsx` | 124, 147, 271, 306 | Yes |
| `RecordPastAppointmentDialog.tsx` | 289, 290 | Yes |
| `ApplyTemplateDialog.tsx` | 118, 130, 140, 142 | No |
| `CalendarGrid.tsx` | 214, 551, 563, 605, 655, 725 | 725 changed, uncovered before too |
| `DayCalendar.tsx`, `BookAppointmentDialog.tsx`, `ProcedurePage.tsx` | 53; 641; 733 | No |

### Where the closed-day rule runs (finding 8)

| Command | How it asks | Result on a closed day |
|---|---|---|
| `CreateAppointment` (booking), `BookAppointment` (book, reschedule, drag) | `BookingTimes.WhyNotAsync` → `WorkingDays.WhyOutsideAsync` | 400 |
| `RecordPastAppointment` | `BookingTimes.WhyNotRecordedAsync` → the same | 400 (finding 5) |
| `CreateCalendarBlock`, `UpdateCalendarBlock`, `ChangeTemplateBlockForDay`, `CreateMeeting`, `UpdateMeeting` | `InsideWorkingDay` (`WorkingDayRules`) | 400, even for an unchanged time already there |
| `ApplyDayTemplate` | `WorkingDay` (`WorkingDayRules`) | 400 |
| `ClearDayTemplate`, `RemoveTemplateBlockForDay`, deletes, cancels, outcomes, notes | none (they remove or change nothing's time) | allowed |
| `ProcedureChange.RequestVisit` | none (a Requested visit has no day) | allowed |

### The bank holiday substitute rule

Weekday holidays stay put. Then each weekend one, in date order, moves to the next weekday that isn't already a holiday (`BankHolidays.cs:44-55`):
- Christmas Saturday, Boxing Day Sunday (2027): Christmas → Monday 27, Boxing Day → Tuesday 28.
- Christmas Sunday (2022): Boxing Day is Monday 26 already, so Christmas → Tuesday 27.
- Christmas Friday, Boxing Day Saturday (2026): Boxing Day → Monday 28.
- New Year's Day Saturday (2022, 2028) → Monday 3; Sunday (2023) → Monday 2.

These match gov.uk. Easter's two holidays and the three May and August Mondays never fall at a weekend.

### The migration's `Down`

`UPDATE AppointmentEvents SET Action = CASE WHEN JSON_VALUE(Details, '$.outcome') = 'DidNotAttend' THEN 4 ELSE 3 END WHERE Action = 7` matches how `AppointmentChange.RecordAfterEvent` writes the details (`{"outcome":"Attended"}`, camel case). A converted entry keeps `previousStatus: "Requested"` for a taken-over visit, which the older timeline reads as a "from" status; the older web app only words Attended and Did not attend corrections, so it shows nothing odd. The closed days and the flag are dropped, as they should be.
