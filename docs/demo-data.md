# The demo data: a guided tour

`medapp-dev`'s `reset_demo_data` fills the local database with a synthetic clinic after about 18 months of using MedApp. This page is a script for showing the app with it: what's in the data, where each feature shows, and what to click.

Everything is invented: names, `@example.com` patient emails, `@clinic.example` staff emails, NHS numbers from the 999 test range, no real people, places or organisations. The clinic has no name.

## Loading it

1. Start the AppHost once (`dotnet run --project MedApp.AppHost`), so the SQL Server and Azurite containers exist.
2. Build the tool: `dotnet build tools/MedApp.DevMcp`.
3. Ask for `reset_demo_data` with `confirm: true` and `keepUsers: ["dev-parity"]`. It saves a `pre-reset-<timestamp>` snapshot first, and takes 30 seconds to a minute.
4. **Keep its summary.** It holds:
   - the generated passwords (`accounts`), and the two-factor account's key and recovery codes (`twoFactor`)
   - `tour`: the records named in this page (blocked procedures, the unlocked stage, the diary, and in `tour.calendar` the meetings, the team view's day and the personal templates), which depend on the day it runs
   - `privacy`: the restricted, disputed, kept and due records
   - `today`: who signed in today, and the locked account
   - the counts

**Every patient's name is unique,** so searching the patients list for a name from the summary finds that one record.

**Times are relative to now,** so the data always looks current, and a few things depend on when it's run:
- **The diary day** is today, or Monday when it's run at a weekend.
- **Today's sign-ins** happen on a working day from 07:30. Run at a weekend, or before 07:30, nobody has signed in today, so there are no live sessions.
- **The locked account** (`c.dunmore`) is locked out whenever it's run: wrong passwords can come on any day.

**Files** are attached only when Azurite answers on `127.0.0.1:14331`, where the AppHost publishes it. Otherwise the summary says files were skipped, and everything else is the same. A snapshot loaded into kind (`deploy/kind/README.md`) has the files' records but not their bytes, which stay in the AppHost's Azurite.

## The people

| Who | Username | What to show with them |
|---|---|---|
| **The clinical lead** (Admin and User) | `admin` / `admin` | The main demo account: an admin who also works clinically |
| **The service manager** (Admin and User) | `j.whitlock` | A second admin, with two-factor on |
| **Two consultant surgeons** | `r.calloway`, `d.ashworth` | Surgical and day-case work; a theatre and clinic week |
| **A consultant gastroenterologist** | `m.fairbanks` | Diagnostic work; has turned off assignment notifications |
| **Three nurses** | `k.ellison`, `s.brightwell`, `l.northcott` | Pre-assessment and recovery; the minor procedures clinic; wound care and follow-up (part-time, mornings) |
| **A physiotherapist** | `a.pemberton` | Physiotherapy courses; had a password reset three weeks ago |
| **The booking coordinator** | `h.marsh` | Referrals, scheduling, and most bookings |
| **A healthcare assistant** | `c.dunmore` | Locked out after wrong passwords; no email address |
| **A new starter** | `e.rowntree` | Must change the generated password at first sign-in; email not confirmed yet |
| **A leaver** | `g.harrow` (deleted) | Appears only on closed procedures, as "Deleted user" |

**Signing in as someone:** use the password from the summary. For `j.whitlock`, type the summary's `authenticatorKey` into an authenticator app, or use one of the `recoveryCodes`. The passwords are new each run, so they're not written here.

## Where to see each feature

### Home and My Tasks (as `admin`)

- **Assigned work:** the lead leads about a third of the surgical procedures, some day-case ones and a few skin lesions.
- **Notifications:** a dozen or so unread from the last few days: assignments by the service manager, bookings made by the coordinator, monitoring alerts, and meetings the service manager arranged, moved or cancelled.
- **The side nav's pins:** two patients and one procedure, from the lead's saved settings.

### The procedures list

- **Saved searches** (the star by the search): "My open work" (the list starts on it), "Overdue surgical", "Blocked" and "Urgent diagnostics".
- **Overdue work** comes from the slow parts, not at random: the surgical pathway has the most (long waits at pre-assessment and scheduling), then diagnostics, after the summer dip.
- **Every status shows:** Unassigned and Assigned (the newest), In progress, In review, Blocked, Completed, Cancelled and Archived.
- **Rows per page** is 25 for `admin` and 50 for `r.calloway`, from their settings.

### A procedure's page

Open the ones named in the summary's `tour`:
- **`blocked`:** blocked with a reason when a stage was reached. **`blockedThenUnblocked`:** blocked, then carried on days later.
- **`stageUnlockedAndCorrected`:** an admin unlocked a finished stage; it was corrected with a bold **Correction:** and finished again.
- **`finishedWithoutItsVisit`:** the two admin bypasses: a Required pre-assessment finished without a visit, with the reason in the audit.
- **`cancelledWithItsBookingCancelled`:** cancelled after its visit was booked; the coordinator then cancelled the booking by hand ("Procedure cancelled; patient informed by phone").
- **`reassigned`:** the lead changed part-way: one assigned, the other taken off.
- **`dueDateOrPriorityChanged`:** due dates moved at scheduling, or a diagnostic referral raised to Urgent at triage.
- **`visitNeverBooked`:** a visit asked for weeks ago and still in Needs booking.
- **`oldPathwayStillFinishing`:** the archived day-case pathway's last three.
- **`missedAndRebooked`:** a visit the patient didn't attend, then a new one.
- **`inReview`:** every stage finished, waiting to be marked completed.

**On any procedure:**
- **Stage notes** are specific to the procedure and stage (observations, measurements, results in words, medicine changes, safety-netting), sometimes with a list or a bold point.
- **The audit log** has every change, by whoever made it.
- **Each stage's visit** shows its appointment: requested by the app when the stage became current, booked (usually by `h.marsh`) with whoever is working on the stage, attended before the stage was finished.
- **Suggested visits** (Consent, Results review, Follow-up) are often finished without one: the app's request is cancelled automatically, and the audit says "without visit".

### A patient's page

- **The timeline:** every procedure change and appointment entry, filterable by kind.
- **The access report:** who viewed, listed or searched for the patient in the last 60 days.
- **Restricted** (the summary's `privacy.restricted`): two records. The first has a procedure under way: staff can still block or cancel it, nothing else.
- **Disputed:** two, with reasons; a banner only. The first was also exported for a subject access request.
- **Children:** a few on the minor procedures and physiotherapy pathways ("Children" saved search on the patients list).
- **Registered, no procedures yet:** about 14 patients from the last four weeks.
- **Deleted procedures:** two, on the patients in `tour.deletedProceduresOf`. Admins see them on the timeline, labelled deleted.

### Files

On a dozen procedures and patients: referral letters, pre-op bloods reports, ECG-like traces (with thumbnails), consent form copies and discharge summaries, plus an exercise diagram and a phone note on stages under way. Each says it's synthetic. Two are deleted ("Show deleted", as an admin), and some have been downloaded (the audit and the access report show it).

### The calendar

- **The lead's diary day** (`tour.leadsAppointmentsThatDay`): five appointments, lunch, theatre list planning (a meeting, usually in the afternoon), any of the day's regular meetings, and free time to book into by clicking.
- **Personal templates** (Calendar, then Calendar templates; admins choose whose with the staff picker): everyone has their own day templates, week templates and default week. Most follow their role (clinic, theatre, endoscopy, office), with the differences listed in `tour.calendar.personalTemplates`:
  - `l.northcott` worked full time until 20 weeks ago, then mornings only (two dated default-week rows: past weeks still show full days)
  - `m.fairbanks` moved the Endoscopy list's lunch later 10 weeks ago (an edit to the template, so earlier list days keep the earlier lunch)
  - `h.marsh` has a late lunch (13:30), covering the desk
  - `d.ashworth` operates on Friday mornings (a "Friday list"), where `r.calloway` has an admin day
  - `j.whitlock` finishes early on Fridays, and `admin` has a management afternoon on Wednesdays
- **Day templates** have lunch and blocks for the role: clinic day, theatre day, endoscopy list, admin day, half day (morning), day off and office day. They leave the regular meeting times free.
- **Activity types** the clinic added: ward round, teaching, supervision and off duty.
- **Exceptions:**
  - **Leave and training days** have no day template (as "This day's template: none" saves), then one block for the day: the app refuses a whole-day block over a template's lunch
  - `k.ellison` has a week's annual leave next month, and next Tuesday uses her theatre day template (covering recovery)
  - `a.pemberton` has a day's leave this week
  - `d.ashworth` has a training day next Wednesday
  - `r.calloway`'s lunch is moved on the day after the diary day, round the meetings, into free time
  - `s.brightwell`'s admin time was removed on the day before, and an appointment booked in its place
- **Needs booking:** visits recently asked for, and a few asked for long ago and never booked.
- **Appointment history:** booked, moved (rescheduled), cancelled by the patient or because the clinician couldn't make it, did not attend, notes added and edited.

### Meetings and the team view (as `admin` or `j.whitlock`)

- **Regular meetings** across the whole 18 months and the coming weeks, each arranged by an admin some weeks ahead:
  - **Monday huddle**, 08:30 to 08:45: everyone working that morning
  - **MDT**, Friday 13:30 to 14:30: the lead, the surgeons, the endoscopist and two nurses
  - **Clinical governance**, the second Wednesday of the month, 16:00 to 17:00
  - **Mandatory training** every six weeks (Basic life support update, Fire safety, Safeguarding update and others), for the nurses, the healthcare assistant and the physiotherapist, at a time they're all free
  - **1:1 catch-ups** each month: `j.whitlock` with `h.marsh` and `c.dunmore`, the lead with `k.ellison` and `s.brightwell`
  - **Welcome and induction** for the new starter, and **theatre list planning** on the diary day
- **Who goes:** only people free at the time, as the app requires. The part-time nurse misses afternoon meetings; someone on leave isn't invited; someone who booked leave after a meeting was arranged was taken off it first ("took you off" in their notifications); newcomers were added to meetings already arranged.
- **Changes** (`tour.calendar.movedMeetings` and `tour.calendar.cancelledMeeting`): theatre list planning moved three days ago, with `h.marsh` added; the next governance meeting and a 1:1 moved when there was time to; away day planning was cancelled four days ago. Their notifications ("Meeting invitation", "Meeting moved", "Meeting cancelled") are in the bells of everyone told, never the admin who made the change.
- **The team view** (Calendar, Team, admins only): open it on `tour.calendar.teamViewDay` to see everyone's day side by side, with the day's meetings (`tour.calendar.meetingsThatDay`) across several columns and the **Everyone free** column. Select free time there to arrange a **New meeting**; its suggested times come from the same calendars. `tour.calendar.upcomingMeetings` lists what's next.

### Notifications and monitoring (as `admin`)

- **Monitoring rules** (`/admin/monitoring`):
  - when the new starter creates a procedure
  - when `r.calloway` finishes a stage on a High or Urgent procedure
  - when `r.calloway` or `d.ashworth` blocks a procedure, and when anyone blocks a diagnostic one
  - when anyone changes a due date
  - one switched off two weeks ago: unlocks by the service manager
- **The service manager's own rules:** urgent diagnostic referrals, cancellations, completed day-case procedures, and the lead being assigned.
- **Monitoring notifications** in the last 30 days match those rules, as the app would have sent them.
- **Notification choices:** `m.fairbanks` turned off assignment notifications 45 days ago, so has none since.

### Insights (`/admin/insights`)

Each combination of period and template looks different. Roughly, for a run in November 2026:

| Template | Last 4 weeks | Last 12 weeks | Last 26 weeks |
|---|---|---|---|
| **All** | About 90 created; the diagnostic spike and the day-case ramp | About 230 created | About 460 created |
| **Day-case pathway** | Ramping up: 7 to 9 a week; few completed yet | Launched 10 weeks ago: nothing before | The same as 12 weeks |
| **Day-case pathway (2024)** (archived) | Almost nothing: only its three stragglers' stages | Its last weeks before archiving, and most of its completions | About 5 a week, steady, then stops |
| **Standard surgical pathway** | 2 to 3 a week; completions lag; the most overdue | A growing backlog | Steady creations; long waits at pre-assessment and scheduling |
| **Diagnostic procedure** | A spike to about 12 a week (the waiting-list initiative); scheduling waits short | The summer dip, 8 to 11 weeks ago | About 6 a week; long scheduling waits before the initiative; more urgent work |
| **Minor procedure (clinic)** | A low week last week (a clinic was cancelled) | Quick: completions keep up | About 3.5 a week |
| **Physiotherapy course** | About 2 a week | Weekly sessions; short stage times | Started 16 weeks ago: nothing before |

**"This time last week"** comparisons, the overdue charts (by assignee and priority), throughput and the time at each stage all differ with them. `DemoDataSeedTests` checks that every combination gives a different result.

### Users and security (as `admin`)

- **Users:** `c.dunmore` is locked out until tomorrow evening (Unlock); `e.rowntree` was created this week; `j.whitlock` has two-factor; most have a confirmed `@clinic.example` address, `e.rowntree`'s isn't confirmed, and `c.dunmore` has none.
- **Sign out everywhere:** on a working day from 07:30, the staff who've worked today have signed in, so they have live sessions. At a weekend, or earlier in the morning, nobody has.
- **Security** (Admin settings): the admins' sign-in rules are back to a new database's (none), whatever they were before the reset.
- **The activity log** (60 days):
  - sign-ins and sign-outs on working days, some refused, and a few for names no account has
  - the locked account's run of wrong passwords
  - patient and procedure views by the staff working on them, and lists and searches (filter names and counts, never the terms)
  - two exports, the restrictions, disputes and anonymisation
  - the new starter's creation, the service manager made an admin, the password reset, two-factor turned on
  - file views and downloads, and the retention "keep" decisions

### Retention review

Six imported records are due: five adults treated 9 to 12 years ago, and one child treated at 11, now over 25. Two of them are kept until next year, with reasons (shown with "Include kept"). A seventh was anonymised three weeks ago (`privacy.anonymisedPatientId`), so it's no longer listed.

## Choices made

The plan left these open; this is what the seed does.

- **Size:** the weekly volumes decide it: about 650 to 670 procedures, each going to someone already registered six times in ten (a patient with one procedure is likelier to get another than one with three), so 230 or so patients have procedures. With 14 waiting for a referral and 12 imported records, that's about 260 patients in all (259 to 268 in four runs from late October to late November 2026); it varies a little with the day it's run.
- **History before the last 26 weeks** is at a fifth of the usual volume.
- **The new day-case pathway** launched 10 weeks ago; its 2024 predecessor ran at 5 a week until then. Its procedures were replayed until they all finished more than four weeks ago (but for three stragglers, made deliberately slow).
- **The leaver** led only procedures that closed before they left; a procedure that wouldn't have was replayed with another lead.
- **Imported records** (the retention review's) have no audit history and no visits: they predate the app, whose history starts when it was used. Their stages' visit requirement is None, as the migration left existing stages.
- **Notifications** are made for the last 30 days only; older ones would long have been read.
- **Files on stages** go only on stages still under way, since the app allows files on a stage only while it can be edited; documents from finished stages (consent copies, bloods reports) are on the procedure.
- **Today's sessions** come from the app's own sign-in command, at the moment each person started work today, and last eight hours. Only on working days from 07:30, as in the history.
- **The locked account's lockout** is the app's own (after five wrong passwords, 20 minutes before the run), stretched to tomorrow evening: the app's five minutes would pass before anyone opened the Users page. It happens whatever the day.
- **Email** goes nowhere: the seed's email sender drops every message, while the app's commands still record the address requests and confirmations.
- **SNOMED CT codes** are on Referral received, Triage, Consultation, Follow-up, Physiotherapy session and Discharge, the concepts certain enough to use; the others are blank.
- **An "Off duty" activity type and a "Day off" day template** were added, so part-time afternoons and days off can't be booked.
- **At a weekend** the diary day is the next Monday, since nothing can be booked on a weekend.
- **Leave** is stored as the app would let someone record it: the day set to no day template, then the leave as one block.
- **Templates are personal:** each person has their own copies of the day templates their work needs, named the same way, as the app keeps them, rather than one set the clinic shares.
- **Meetings** are arranged one at a time, as the app has no repeating meetings: the regular ones in batches of eight weeks, four weeks ahead; governance three weeks ahead; training four; 1:1s two. Everyone invited is free then, so a meeting never overlaps anything on an attendee's calendar, and visits are booked round them. Their notifications, like the rest, are made for the last 30 days only.
- **Patients' names** are unique, so the summary can name a record by name alone. Real clinics have namesakes; a demo can't afford to open the wrong one.
- **History respects when each patient existed:** nobody views, lists, books or changes a patient before they were registered, and nobody sees the anonymised record afterwards.
