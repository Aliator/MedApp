# The demo data: a guided tour

`medapp-dev`'s `reset_demo_data` fills the local database with a synthetic clinic after about 18 months of using MedApp. This page is a script for showing the app with it: what's in the data, where each feature shows, and what to click.

Everything is invented: names, `@example.com` patient emails, `@clinic.example` staff emails, NHS numbers from the 999 test range, no real people, places or organisations. The clinic has no name.

## Loading it

1. Start the AppHost once (`dotnet run --project MedApp.AppHost`), so the SQL Server and Azurite containers exist.
2. Build the tool: `dotnet build tools/MedApp.DevMcp`.
3. Ask for `reset_demo_data` with `confirm: true` and `keepUsers: ["dev-parity"]`. It saves a `pre-reset-<timestamp>` snapshot first, and takes about 30 seconds.
4. **Keep its summary.** It holds:
   - the generated passwords (`accounts`), and the two-factor account's key and recovery codes (`twoFactor`)
   - `tour`: the records named in this page (blocked procedures, the unlocked stage, the diary), which depend on the day it runs
   - `privacy`: the restricted, disputed, kept and due records
   - the counts

**Times are relative to now,** so the data always looks current. "The diary day" is today, or Monday when it's run at a weekend.

**Files** are attached only when Azurite answers on `127.0.0.1:14331` (the AppHost's, from this branch on). Otherwise the summary says files were skipped, and everything else is the same. A snapshot loaded into kind (`deploy/kind/README.md`) has the files' records but not their bytes, which stay in the AppHost's Azurite.

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
- **Notifications:** a dozen or so unread from the last few days: assignments by the service manager, bookings made by the coordinator, and monitoring alerts.
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

- **The lead's diary day** (`tour.leadsAppointmentsThatDay`): five appointments, lunch, an MDT meeting at 13:30, and free time to book into by clicking.
- **Week templates by role:** clinical lead, surgeon (clinic and theatre days), endoscopy, nursing, part-time mornings, physiotherapy and office. Past weeks show their blocks too.
- **Day templates:** clinic day, theatre day, endoscopy list, admin day, half day (morning) and day off, each with lunch and blocks for the role.
- **Activity types** the clinic added: MDT meeting, ward round, teaching, supervision and off duty.
- **Exceptions:**
  - `k.ellison` has a week's annual leave next month, and next Tuesday uses the theatre day template (covering recovery)
  - `a.pemberton` has a day's leave this week
  - `d.ashworth` has a training day next Wednesday
  - `r.calloway`'s lunch is moved on the day after the diary day
  - `s.brightwell`'s admin time was removed on the day before, and an appointment booked in its place
- **Needs booking:** visits recently asked for, and a few asked for long ago and never booked.
- **Appointment history:** booked, moved (rescheduled), cancelled by the patient or because the clinician couldn't make it, did not attend, notes added and edited.

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
- **Sign out everywhere:** most staff signed in today, so they have live sessions.
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

- **Size:** about 280 patients rather than 250, so that the 650 or so procedures the weekly volumes call for come to two or three a patient. A patient with one procedure is likelier to get another than one with three.
- **History before the last 26 weeks** is at a fifth of the usual volume.
- **The new day-case pathway** launched 10 weeks ago; its 2024 predecessor ran at 5 a week until then. Its procedures were replayed until they all finished more than four weeks ago (but for three stragglers, made deliberately slow).
- **The leaver** led only procedures that closed before they left; a procedure that wouldn't have was replayed with another lead.
- **Imported records** (the retention review's) have no audit history and no visits: they predate the app, whose history starts when it was used. Their stages' visit requirement is None, as the migration left existing stages.
- **Notifications** are made for the last 30 days only; older ones would long have been read.
- **Files on stages** go only on stages still under way, since the app allows files on a stage only while it can be edited; documents from finished stages (consent copies, bloods reports) are on the procedure.
- **Today's sessions** come from the app's own sign-in command, so they start when the seed runs and last eight hours.
- **The locked account's lockout** is the app's own (after five wrong passwords), stretched to tomorrow evening: the app's five minutes would pass before anyone opened the Users page.
- **Email** goes nowhere: the seed's email sender drops every message, while the app's commands still record the address requests and confirmations.
- **SNOMED CT codes** are on Referral received, Triage, Consultation, Follow-up, Physiotherapy session and Discharge, the concepts certain enough to use; the others are blank.
- **An "Off duty" activity type and a "Day off" day template** were added, so part-time afternoons and days off can't be booked.
- **At a weekend** the diary day is the next Monday, since nothing can be booked on a weekend.
