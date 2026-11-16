# MedApp: staff calendars and appointments

Status: **built on `feature/staff-calendars`** · 2026-11-16

Staff get a **calendar**: an 08:00 to 18:00 working day, UK time, Monday to Friday, with weekends shown greyed as non-working. On it they **book appointments** with patients and **block off time** for activities from a preset list (Lunch, Training, ...). **Day and week templates** fill each week with its usual blocks automatically, and can be changed for one day. Stage definitions say whether a stage **involves a visit** (none, suggested or required); the app then asks for an appointment before the stage can be finished. Appointments go through **Requested** (needs booking), **Booked**, then **Attended** or **Did not attend**, or **Cancelled**.

The owner was away and asked for every choice to be made for them. This document records each one, with the reason, so they can be reviewed and changed. It replaces the to-do item "Appointments and booking".

## 1. Decisions

### The working day
1. **08:00 to 18:00, UK time (Europe/London), Monday to Friday.** Weekends are shown, greyed, as non-working: nothing can be booked or blocked on them. The grid is in 15-minute rows. `WorkingHours` (`MedApp.Application/Calendar/Common`) holds the rules.
2. **Everything on a calendar sits inside the working day.** An appointment or a block must start and end on the same weekday, between 08:00 and 18:00 UK time, on a 5-minute boundary, and start before it ends. The server refuses anything else (400). Bank holidays aren't known yet (section 8).
3. **Times are stored and sent as UTC, with a `Z`** (the existing `UtcDateTimeConverter`). Template times are UK clock times (`TimeOnly`), turned into UTC for each date, so a 12:00 lunch is at 12:00 UK time on both sides of a clock change. The calendar response gives each day's `workStartUtc` and `workEndUtc`, so the browser steps through 15-minute slots in UTC without doing time zone arithmetic. A UK working day never spans a clock change (they happen at 01:00 and 02:00).

### What's on a calendar
4. **Activity types** (`CalendarActivityType`): the preset list for blocked time. Admin-managed; each has a name and a colour from a fixed palette (`red`, `orange`, `yellow`, `green`, `teal`, `cyan`, `blue`, `purple`, `pink`, `gray`: Astryx `Token` colours, so contrast comes from the theme). Seeded: Lunch (green), Admin time (blue), Training (purple), Meeting (teal), Annual leave (orange), Travel (gray). Archived, not deleted, when in use (by a block or a template block); archived ones can't be chosen for new blocks, but existing blocks keep them.
5. **Blocks** (`CalendarBlock`): a member of staff, an activity type, a start and end, and an optional note (200 characters). Blocks are about staff, not patients: the note must not hold patient details (the UI says so).
6. **No overlaps.** A new or moved appointment or block may not overlap any appointment (Booked, Attended or Did not attend) or block (stored or from a template) on that person's calendar: the server refuses it with a 409 naming what it clashes with ("That time overlaps Lunch, 12:00 to 13:00."), and the UI only offers free slots. Two requests racing for the same slot are serialised by a database lock per member of staff (`ICalendarLock`, SQL Server `sp_getapplock`), so one wins and the other gets the 409.
   - Something that was already on the calendar can come to overlap a later template change (an admin moves everyone's lunch over a booked appointment). Both still show, the appointment on top, and the block is flagged as overlapping; nothing is moved or removed by the app.
7. **Who changes what:**
   - **Any staff member** can see anyone's calendar, and book, reschedule, cancel and record the outcome of any appointment, for any member of staff (as they can work on any procedure).
   - **Blocks, a day's template and the default week** of a calendar: its owner, or an admin.
   - **Activity types, day templates and week templates:** admins (staff can read them).
8. **Past days don't change.** Blocks and templates on a day before today (UK) can't be added, edited or removed (409), and the templates they came from are versioned (decision 11), so a past day keeps showing what it was. Appointments in the past can still have their outcome recorded.

### Templates, applied automatically
9. **A day template** (`DayTemplate`) is named, with blocks given as activity type, start time, end time and an optional note. **A week template** (`WeekTemplate`) is named, with a day template (or none) for each weekday, Monday to Friday.
10. **Each member of staff may have a default week template**, set by themselves (on the Calendar page) or by an admin (on the Calendar templates page, for anyone).
11. **Applied when the calendar is read, never generated.** The calendar works out each day's template blocks when it's shown (`CalendarProjection`), so editing a template changes every day it applies to from then on. Nothing runs in the background.
    - **History is kept by dating rows, not by copying them.** A day template's blocks, a week template's days and a person's default week each have `ValidFrom` and `ValidTo` (UK dates). Editing one closes the rows that changed the day before today and adds new ones from today; rows that didn't change are kept as they are. A row added today and changed again today is just replaced. So a past day projects exactly what applied then, and a change shows from today.
    - **Why today and not tomorrow:** someone editing a template expects to see the result on today's calendar. Today's blocks that have already passed can change with it; that's accepted.
12. **Changing one day's template block** stores an exception for that date (`CalendarBlockException`), so the template block isn't projected that day, and, for an edit, a stored block with the new values. **Removing** it stores only the exception. A later edit to the template keeps exceptions on blocks it didn't change.
13. **"Apply a day template to this day"** stores a day override (`CalendarDayOverride`): that date uses the chosen day template (or none) instead of the week template's. It can be removed again, back to the week's. Blocks added by hand that day stay.
14. **Templates in use are archived, not deleted,** like stage definitions. A day template is in use once any week template or day override has referred to it; a week template once anyone has had it as their default. Archived ones can't be chosen again, but keep applying where they already do (an archived week template stays someone's default until it's changed).

### Appointments
15. **An appointment** (`Appointment`) is for a patient, optionally for one of their procedures and one of its stages, with one member of staff, a start and end (UTC), a status, optional notes (2000 characters, free text about the patient, treated like procedure notes), who requested or booked it and when, and the outcome's who and when.
16. **Statuses** (`AppointmentStatus`, stored as numbers, never renumbered): `Requested = 0`, `Booked = 1`, `Attended = 2`, `DidNotAttend = 3`, `Cancelled = 4`.
    - **Requested** ("Needs booking") has no member of staff or time yet. **Booked** has all three.
    - **Requested → Booked:** a member of staff, a start and an end, in the future, clash-free.
    - **Booked → Booked:** rescheduling (another time, or another member of staff), in the future, clash-free.
    - **Booked → Attended or Did not attend:** only once its start time has passed. **Attended ↔ Did not attend** can be corrected the same way.
    - **Requested or Booked → Cancelled,** with a reason (500 characters).
    - Cancelled is final. A patient who didn't attend gets a new appointment (the UI offers "Book another").
17. **Booking can't be in the past**, so walk-ins aren't recorded after the event (section 8).
18. **One open appointment per stage.** A stage can have one Requested or Booked appointment at a time; asking for another is a 409 that points to the one there is. Appointments without a stage have no limit.
19. **Every change to an appointment is recorded** in its history (`AppointmentEvent`): requested, booked, rescheduled (with the old time and member of staff), attended, did not attend, cancelled (with the reason) and notes changed (old and new text). Like the procedure audit log, it has no foreign keys, so it outlives what it's about. It feeds the patient timeline (decision 27).
20. **Notifications** (through `INotificationSender`, new kinds, never renumbered): `AppointmentBooked = 1` (an appointment was booked with you, or moved to you), `AppointmentRescheduled = 2` (yours was moved to another time), `AppointmentCancelled = 3` (yours was cancelled, or moved to someone else). Only when someone else did it. Their title is the procedure's title, or "Appointment"; the message says who and when, never the patient's name or notes. A notification links to the appointment (`Notification.AppointmentId`), opening it on the Calendar page.
21. **Deleting** a patient who has appointments is refused (409), as with procedures. Deleting a procedure (only once it's cancelled or archived) detaches its appointments (procedure and stage set to none) in the same save; their history keeps the procedure. Deleting a user account with upcoming Booked appointments is refused until they're moved or cancelled, so no appointment is left with nobody.

### Stage visits
22. **Stage definitions get `VisitRequirement`**: `None = 0` (the default), `Suggested = 1`, `Required = 2`, set by admins when adding or editing a definition. It's copied onto each procedure stage at creation, like the name and instructions. Existing definitions and stages are `None`, so existing templates and procedures behave exactly as before.
23. **The Requested appointment:** when a stage with a visit becomes the current stage (the procedure is created, or the stage before it is finished), `ProcedureChange` creates a Requested appointment linked to it, unless it already has one that's Requested, Booked or Attended (one booked ahead counts). It's recorded in the appointment's history as requested by the app. It isn't made while the procedure is blocked or closed.
24. **Required:** finishing the stage needs an Attended appointment linked to it, checked on the server (`ProcedureWorkflow.WhyStageNeedsVisit`). Staff are refused (409): "The 'X' stage needs a visit before it can be finished: book it, and mark it Attended once it's happened. An admin can finish it without one."
    - **Admin bypass:** an admin may finish it anyway, after a confirmation dialog that asks for a reason (`FinishProcedureStageRequest { withoutVisit: true, reason }`). The stage's `StageFinished` audit entry records `withoutVisit`, the requirement and the reason.
25. **Suggested:** finishing without an Attended appointment asks for confirmation in the UI, for anyone. The server doesn't refuse; the `StageFinished` entry records `withoutVisit` and the requirement.
26. **Tidying up:** finishing a stage cancels its Requested appointments (it doesn't need booking any more), and cancelling a procedure cancels all its Requested ones, each automatically, with a reason saying why. Booked appointments are never cancelled by the app: someone may need to tell the patient, so the procedure page lists them.
    - Finishing a stage again after an admin unlocked it doesn't ask for a visit again.

### Where it shows
27. **The patient timeline** gets an **Appointments** kind: requested, booked, rescheduled, attended, did not attend, cancelled and notes changed, merged into the feed by time. The visibility rules are the timeline's own: staff see the structured values (status, time, member of staff, stage) but **never free text** (no notes, no cancellation reason), and nothing about appointments of a deleted procedure; admins see everything. `TimelineStaffCanaryTests` covers the new entries, and a stage finished without a visit shows that (and, for admins, the reason).
28. **Calendar page** (`/calendar`, side nav, for everyone): day and week views, your own by default, with a staff picker for anyone's.
    - Click an empty slot, or drag across slots, to book an appointment or block time there; the keyboard does the same (arrow keys move between slots, Shift with an arrow extends, Enter opens). A **New** button does it without the grid at all.
    - Click an appointment or block to see and change it in a dialog; drag one to move it, or move it from its dialog.
    - Colours: appointments by status, blocks by activity type, from theme tokens.
    - Its toolbar: Previous, Today, Next, a date picker, Day or Week, whose calendar, **This day's template** (apply one, or back to the week's), and **My default week** (your own default week template).
29. **Needs booking** (`/calendar/needs-booking`, side nav): a list of Requested appointments, soonest procedure due date first, each with Book.
30. **Booking** is one dialog (`BookAppointmentDialog`), opened from the patient page, the procedure page, a stage that needs a visit ("Book visit"), the Needs booking list, an empty calendar slot, or an appointment (to reschedule): choose the patient (and procedure and stage) unless they're known, the member of staff (the calendar's owner, else the procedure's only assignee, else you), the length (15, 30, 45, 60, 90 or 120 minutes; 30 by default), the day (the week's working days with how many slots are free), then a free slot. Notes are optional. Without a time it can **Add to Needs booking** (a Requested appointment) instead.
31. **Calendar templates** (`/admin/calendar-templates`, admins): day templates and week templates (add, edit, archive, restore, delete when unused) and each member of staff's default week. **Activity types** (`/admin/activity-types`, admins): add, edit, archive, restore and delete when unused.
32. **Home** shows your **day calendar**: today, with Previous and Next day, your appointments and blocks. Clicking an appointment opens its procedure (or the patient, when it has none). A customisable widget home screen stays on the to-do list.
33. **Narrow windows and half views:** the week view becomes the day view, the toolbar wraps, and dialogs fill the width.

## 2. The model

```
CalendarActivityType  Id, Name (60, unique), Colour, IsArchived                               seeded
CalendarBlock         Id, StaffUserId, ActivityTypeId -> ActivityType, StartUtc, EndUtc, Note (200),
                      CreatedAtUtc, CreatedByUserId, LastUpdated
DayTemplate           Id, Name (100, unique), IsArchived
DayTemplateBlock      Id, DayTemplateId -> DayTemplate (cascade), ActivityTypeId -> ActivityType,
                      StartTime, EndTime (TimeOnly, UK), Note (200), ValidFrom, ValidTo? (DateOnly)
WeekTemplate          Id, Name (100, unique), IsArchived
WeekTemplateDay       Id, WeekTemplateId -> WeekTemplate (cascade), DayOfWeek (1 to 5), DayTemplateId -> DayTemplate,
                      ValidFrom, ValidTo?
StaffWeekTemplate     Id, StaffUserId, WeekTemplateId -> WeekTemplate, ValidFrom, ValidTo?      (a person's default)
CalendarDayOverride   Id, StaffUserId, Date, DayTemplateId? -> DayTemplate                      unique (StaffUserId, Date)
CalendarBlockException Id, StaffUserId, Date, DayTemplateBlockId -> DayTemplateBlock (cascade)  unique (all three)
Appointment           Id, PatientId -> Patient (no action), ProcedureId? -> PatientProcedure (no action),
                      StageId? -> PatientProcedureStage (no action), StaffUserId?, StartUtc?, EndUtc?, Status,
                      Notes (2000), CancelReason? (500), IsAutomatic, CreatedAtUtc, CreatedByUserId,
                      BookedAtUtc?, BookedByUserId?, OutcomeAtUtc?, OutcomeByUserId?, LastUpdated
AppointmentEvent      Id, AppointmentId, PatientId, ProcedureId?, ProcedureTitle, StageName?, Action, UserId, Username,
                      AtUtc, StaffUserId?, StaffUsername?, StartUtc?, EndUtc?, Details (JSON)   no foreign keys
PatientProcedureStageDefinition.VisitRequirement, PatientProcedureStage.VisitRequirement   int, default 0 (None)
Notification.AppointmentId?
```

Staff user IDs have no foreign keys, as with procedure assignments. One migration, `AddedStaffCalendars`.

## 3. The rules, and where they live

- `MedApp.Application/Calendar/Common`:
  - `WorkingHours`: the working day, slot length, and `WhyOutside(startUtc, endUtc)` for the validators.
  - `UkTime`: UK dates and clock times to and from UTC, and "Monday 23 November at 10:00" for messages.
  - `TemplateHistory`: replacing a dated row set from today (decision 11), as plain logic with its own tests.
  - `CalendarProjection`: a staff member's days, template blocks and exceptions, from the rows the repository loads (`CalendarData`).
  - `CalendarClashes`: what a time would overlap.
  - `CalendarMapper`, `CalendarSetupMapper`.
- `MedApp.Application/Appointments/Common`: `AppointmentWorkflow` (statuses, what's allowed), `AppointmentChange` (applies a change, writes its history), `AppointmentMapper`.
- `MedApp.Application/Procedures/Common`: `ProcedureWorkflow.WhyStageNeedsVisit`, and `ProcedureChange.RequestVisit` and `CancelRequestedVisit`, which collect the appointments and history to save with the procedure change (`IPatientProcedureRepository.SaveAsync(ProcedureChange)`), in one `SaveChangesAsync`.

## 4. The API

| Method and path | Who | What |
|---|---|---|
| `GET api/calendar?staffUserId&from&to` | Staff | One person's days (at most 31), blocks (stored and from templates) and appointments |
| `GET api/calendar/staff` | Staff | Everyone with a role, with their default week template |
| `POST api/calendar/blocks`, `PUT`/`DELETE api/calendar/blocks/{id}` | Owner or admin | A block |
| `PUT`/`DELETE api/calendar/staff/{userId}/days/{date}/template-blocks/{templateBlockId}` | Owner or admin | Change or remove one day's template block |
| `PUT`/`DELETE api/calendar/staff/{userId}/days/{date}/day-template` | Owner or admin | Apply a day template to a day (or none), or go back to the week's |
| `GET`/`PUT api/calendar/staff/{userId}/week-template` | Staff to read; owner or admin to set | A person's default week |
| `api/calendar/activity-types`, `api/calendar/day-templates`, `api/calendar/week-templates` | Staff read; admins write | Each: list (`includeArchived`), get, add, edit, archive, restore, delete |
| `POST api/appointments` | Staff | Request (no time) or book (staff, start and end) |
| `GET api/appointments/{id}`, `GET api/appointments/search` | Staff | One, or a page (patient, procedure, stage, staff, statuses, from and to; sorted by start, requested, due date or patient) |
| `POST api/appointments/{id}/book` | Staff | Book a Requested one, or reschedule a Booked one |
| `POST api/appointments/{id}/outcome` | Staff | Attended or DidNotAttend |
| `POST api/appointments/{id}/cancel` | Staff | With a reason |
| `PUT api/appointments/{id}/notes` | Staff | Its notes |
| `POST .../stages/{stageId}/finish` | Staff | Takes an optional `{ withoutVisit, reason }` (decision 24); no body works as before |

## 5. Device and safety

- **Not a medical device.** Booking a member of staff's time for a patient is administrative scheduling, which the MHRA's guidance names as unlikely to be a device "if it only has an administrative function". The visit rule asks for an appointment because an organisation's own template says a stage involves one; MedApp doesn't decide whether a patient needs a visit, when, or with whom, and computes nothing from clinical data. Recorded in `docs/clinical-safety/intended-purpose-and-device-decision.md`.
- **New hazards,** drafted for the clinical safety officer in `docs/clinical-safety/hazard-log.md`: a missed appointment (a Requested one never booked, or a Did not attend not followed up), a double booking, a bypassed required visit, a booking at the wrong time (time zones and clock changes), the wrong patient booked, and appointments left with a deleted account.

## 6. Privacy

Appointment notes and cancellation reasons are free text about patients, and block notes free text about staff. They're listed in `docs/governance/data-inventory.md` and assessed in `docs/governance/dpia.md`, with retention in `retention-schedule.md`.

**When PR #68 (privacy features) merges:** anonymising a patient must also clear their appointments' notes and cancellation reasons and their history's notes and reasons, and the subject access export must include their appointments and appointment history.

## 7. Building it

- Tests: unit tests for every handler, validator and mapper, `WorkingHours`, `TemplateHistory`, `CalendarProjection` (including a clock change), `CalendarClashes`, `AppointmentWorkflow` and the visit rule; integration tests for clashes (including two bookings racing), the visit rule and the admin bypass, projection across a clock change, and the status changes; Vitest for every component and page; E2E journeys for booking from the patient page and for the visit rule.
- Not built (section 8) is on `docs/todo.md`.

## 8. Left out, for later

- Bank holidays and other closed days, and working hours per person or per site.
- Recurring appointments, rooms and resources, and booking several staff for one appointment.
- Recording an appointment after the event (a walk-in): booking is only in the future.
- Patient reminders (email or SMS) and patient self-booking.
- Overdue Requested appointments (requested long ago and still not booked) flagged or notified.
- Printing a day list.
