# The demo data: a guided tour

The demo data is a synthetic **community specialist centre** after about 18 months of using MedApp: outpatient clinics that take referrals from GPs, with a small lab of their own. This page is a script for showing the app with it: what's in the data, where each feature shows, and what to click.

Everything is invented: names, `@example.com` patient emails, `@centre.example` staff emails, NHS numbers from the 999 test range, no real people, places or organisations. The centre has no name, and isn't modelled on a real one.

**Nothing clinical is decided by the seed.** Priorities, triage outcomes, results and plans are text and values that a member of staff "entered" in the story. The app only records and moves them, as it does for real users.

## Loading it

**One script loads the demo data, and it's the only way demo data gets in.** Never add demo or dummy records by hand, with SQL or through the app: change the seeder (`tools/MedApp.DevMcp/DemoData`) and run the script again.

1. Start the AppHost once (`dotnet run --project MedApp.AppHost`), so the SQL Server and Azurite containers exist.
2. Run the script from the repo root:

   ```bash
   tools/seed-demo-data.sh --confirm --keep-users dev-parity --out demo-summary.json     # Git Bash, macOS, Linux
   tools\seed-demo-data.ps1 --confirm --keep-users dev-parity --out demo-summary.json    # PowerShell
   ```

   It builds the seeder, checks the database is a local one, applies any pending migrations, saves a `pre-reset-<timestamp>` snapshot, then **wipes the data** and replays the centre's history. It takes about a minute. Without `--confirm` it changes nothing.
3. **Keep its summary** (`--out` writes it to a file; without it, it's printed). It holds:
   - the generated passwords (`accounts`), and the two-factor account's key and recovery codes (`twoFactor`)
   - `tour`: the records named in this page (blocked pathways, the unlocked stage, the diary, in `tour.calendar` the meetings, the team view's day and the personal templates, in `tour.referrals` the inbox, triage outcomes and waiting-time clocks, and in `tour.centre` the lab, results, consent, MDT, branches, letters and clinic sessions), which depend on the day it runs
   - `privacy`: the restricted, disputed, kept and due records
   - `today`: who signed in today, and the locked account
   - the counts

   The summary holds passwords for local accounts: don't commit it.

**Other ways in, all the same seeder:**
- **From Claude Code:** `medapp-dev`'s `reset_demo_data` with `confirm: true` and `keepUsers: ["dev-parity"]` runs the same code as the script (build the tool first: `dotnet build tools/MedApp.DevMcp`). It doesn't apply migrations: it refuses, and names the script, if any are pending.
- **The seeder alone:** `dotnet run --project tools/MedApp.DevMcp -c Release -- seed-demo --confirm`, when the database is already migrated. `seed-demo --help` lists its options (`--keep-users`, `--seed`, `--out`, `--no-snapshot`, `--save-snapshot`, `--check`).
- **On kind:** `deploy/kind/up.sh --demo-data kind-demo` restores a snapshot of a database the script loaded: add `--save-snapshot kind-demo` to the script's options to save one (`deploy/kind/README.md`).

**It only runs against a local database** (LocalDB, or SQL Server on localhost): by default the AppHost's SQL container. `admin` is always kept. To load another local database, set `ConnectionStrings__DefaultConnection` first.

**Every patient's name is unique,** so searching the patients list for a name from the summary finds that one record.

**Times are relative to now,** so the data always looks current, and a few things depend on when it's run:
- **The diary day** is today, or the next working day when it's run at a weekend or on a bank holiday.
- **Today's sign-ins** happen on a working day from 07:30. Run at a weekend, or before 07:30, nobody has signed in today, so there are no live sessions.
- **The locked account** (`c.dunmore`) is locked out whenever it's run: wrong passwords can come on any day.
- **The seasons** are the real ones: skin lesion referrals rise from May to September and inflammatory skin referrals from November to March, so Insights looks different in July from January.

**Files** are attached only when Azurite answers on `127.0.0.1:14331`, where the AppHost publishes it. Otherwise the summary says files were skipped, and everything else is the same. A snapshot loaded into kind (`deploy/kind/README.md`) has the files' records but not their bytes, which stay in the AppHost's Azurite.

## The centre

A referral-led outpatient centre: GPs refer, the booking coordinator registers the referral, a clinician of its specialty triages it (accepting it into a pathway, rejecting it, or redirecting it), the patient is seen in clinic, and anything taken (a biopsy, bloods) goes to the centre's own lab. Results come back to a team meeting, then to the patient, and a discharge letter goes to the GP. Two consultants visit from the hospital for part of each week.

### The people

| Who | Username | What to show with them |
|---|---|---|
| **The clinical lead** (Admin and Clinician) | `admin` / `admin` | The main demo account: an admin who also works clinically (a consultant physician who leads some diabetes reviews) |
| **The service manager** (Manager, then Admin too) | `j.whitlock` | A second admin, with two-factor on |
| **A consultant dermatologist** | `n.thornbury` | Skin lesions and inflammatory skin; two minor surgery lists a week; 50 rows per page and a saved search |
| **A GP with a special interest in skin** | `t.hallworth` | The same work; operates on Friday mornings |
| **A visiting consultant gynaecologist** | `f.linton` | The post-menopausal bleeding pathway; here on Tuesdays and Thursday afternoons; has turned off assignment notifications |
| **A visiting consultant urologist** | `o.penhale` | Prostate assessments; here on Wednesdays and Friday mornings |
| **Two clinical nurse specialists** | `b.sandiford`, `m.carrow` | Skin cancer: follow-up, samples and the walk-in. Diabetes: leads most diabetes reviews (part-time, mornings) |
| **A biomedical scientist** | `r.aldous` | The lab: works the Histology (lab) and Lab tests (lab) stages; no appointments |
| **A podiatrist** | `s.winterburn` | Biomechanics courses; had a password reset three weeks ago |
| **A pharmacist** | `d.quince` | Medicines reviews |
| **The booking coordinator** | `h.marsh` | Registers every referral (and is told of each triage decision), and makes most bookings |
| **A healthcare assistant** | `c.dunmore` | Takes bloods and samples. Locked out after wrong passwords; no email address |
| **A new starter** | `e.rowntree` | Must change the generated password at first sign-in; email not confirmed yet |
| **A leaver** | `g.harrow` (deleted) | A GP with a special interest in skin who left 16 weeks ago. Appears only on closed pathways, as "Deleted user" |

**Roles:** everyone has the starting role for their job, never "All staff (before roles)": the consultants, the GPs with a special interest, the podiatrist and the pharmacist **Clinician**; the nurse specialists, the healthcare assistant and the new starter **Nurse**; the booking coordinator **Booking**; the biomedical scientist **Lab**; the service manager **Manager** (and Admin, later). Each run puts the standard roles' permissions back as they start.

**Roles shaped to the centre** (Roles and Permissions, `/admin/roles`, made through the role commands, so each step is in the activity log as `RoleCreated` and `RoleChanged`): eight weeks ago the lead made **Locum cover** (patients, pathways, appointments and files) and four weeks ago archived it, so it shows Restore. The same afternoon the lead made **Visiting consultant** (Clinician without overriding stages) and gave it to the visiting consultants, `f.linton` and `o.penhale`, in place of Clinician (two `RolesChanged` entries). Each run deletes the two from the last run first; a kept account holding one stops the reset, saying so.

**Signing in as someone:** use the password from the summary. For `j.whitlock`, type the summary's `authenticatorKey` into an authenticator app, or use one of the `recoveryCodes`. The passwords are new each run, so they're not written here.

### Specialties

Five, as the centre's services, added by the clinical lead before it opened (Administration, then Specialties): **Dermatology** (the three skin templates, the archived one too), **Gynaecology**, **Urology**, **Diabetes** and **Biomechanics**. A sixth, **Respiratory**, is a new service the lead added about three weeks ago: its clinician hasn't started, so it has no templates and no members, and **nobody can triage its referrals** (the Specialties page warns, and its three referrals wait in the inbox). General, which every database starts with, is there too, with nothing in it.

**Waiting-time targets** (days, by priority: the centre's own, not national rules):

| Specialty | Urgent | High | Medium | Low |
|---|---|---|---|---|
| Dermatology | 14 | 28 | 126 | 126 |
| Gynaecology | 14 | 28 | 63 | 126 |
| Urology | 28 | 56 | 63 | 126 |
| Diabetes | 14 | 42 | 84 | 126 |
| Biomechanics | 14 | 28 | 84 | 84 |
| Respiratory (new) | 14 | 28 | 126 | 126 (the app's defaults) |

**Who belongs to which** (Users, then a person's Specialties; your own in Account Settings): `n.thornbury`, `t.hallworth` and `b.sandiford` Dermatology; `f.linton` Gynaecology; `o.penhale` Urology; `m.carrow` Diabetes; `s.winterburn` Biomechanics; `d.quince` Dermatology, Urology and Diabetes; `admin` the five original ones (not Respiratory). The manager, the booking coordinator, the lab and the healthcare assistant are in none. Each change is in the activity log (`StaffSpecialtiesChanged`).

**Who triages** (the `TriageReferrals` permission, from Clinician or Visiting consultant, and membership of the specialty): `n.thornbury`, `t.hallworth` and `d.quince` Dermatology; `f.linton` Gynaecology; `o.penhale` and `d.quince` Urology; `admin` (mostly) and `d.quince` Diabetes, whose nurse specialist's role (Nurse) doesn't triage; `s.winterburn` Biomechanics; and `admin` any of the five. Usually the clinician who then leads the pathway. (Running `medapp-dev`'s `ensure_test_admin` afterwards adds `admin` to every specialty, Respiratory too, which ends that warning.)

### The pathway templates

Seven, one archived. Each is an ordered list of stages from the stage library, with one **clock-stop stage** (in bold): finishing it stops the waiting-time clock of a referral accepted into the pathway (the template editor's "The waiting-time clock stops when this stage is finished"). Referrals are records of their own now, so the "Referral received" and "Triage" stages have gone from every template (decision 26).

| Pathway template | Stages | Led by |
|---|---|---|
| **Skin lesion pathway** | **First clinic visit**, Biopsy or sample, Histology (lab), Results and team review (a branch point), then **Benign**: Discharge letter to GP, or **Treatment**: Treatment, Follow-up, Discharge letter to GP | `n.thornbury` or `t.hallworth` |
| **Skin lesion pathway (2024)** (archived) | **First clinic visit**, Biopsy or sample, Histology (lab), Follow-up | The same two, and the leaver |
| **Inflammatory skin review** | **First clinic visit**, Medicines review, Follow-up, Discharge letter to GP | `t.hallworth` mostly |
| **Post-menopausal bleeding pathway** | **First clinic visit**, Biopsy or sample, Histology (lab), Results and team review, Discharge letter to GP | `f.linton` |
| **Prostate assessment** | Blood and urine tests, Lab tests (lab), **First clinic visit**, Results and team review, Follow-up, Discharge letter to GP | `o.penhale` |
| **Diabetes review** | Blood and urine tests, Lab tests (lab), **Clinic review**, Medicines review, Follow-up, Discharge letter to GP | `m.carrow` or `admin` |
| **Biomechanics course** | **First clinic visit**, Biomechanics session (three times; the third optional), Discharge letter to GP | `s.winterburn` |

**Who works each stage:** the lead clinician sees the patient and writes the letter; the healthcare assistant (or `b.sandiford`) takes bloods and samples; `r.aldous` does the lab's stages; `d.quince` the medicines reviews; a nurse specialist the follow-up on the skin lesion and diabetes pathways. Before any of that, the booking coordinator registers the referral and a clinician of its specialty (usually the lead) accepts it, which creates the pathway.

### The stage library

Twelve stage definitions in use, and three retired (archived): "Referral received" and "Triage", which referrals replaced, and the much older "Paper triage form (retired)".

| Visit | Stages |
|---|---|
| **Required** (the patient must be seen) | First clinic visit, Biopsy or sample, Treatment, Blood and urine tests, Clinic review, Biomechanics session |
| **Suggested** (a phone call often does) | Results and team review, Medicines review, Follow-up |
| **None** (the lab's work, and letters) | Histology (lab), Lab tests (lab), Discharge letter to GP (and the retired Referral received and Triage) |

**What a stage needs before it's finished** (Phases 3 to 5; copied onto pathways created from ten weeks ago, when the centre started tracking them, so older pathways don't have them): Histology (lab) and Lab tests (lab) **wait for results** (signed off); Biopsy or sample **needs consent** recorded; Discharge letter to GP **needs a discharge summary**, approved and sent. And always: a pathway's last stage waits for its specimens and results.

**SNOMED CT codes** are on Referral received, Triage, First clinic visit, Biopsy or sample, Follow-up, Discharge letter to GP, Lab tests (lab) and Medicines review: the concepts certain enough to use. The others are blank, so both states show.

## Where to see each feature

### Home and My Worklist (as `admin`)

- **Assigned work:** the lead leads about four in ten diabetes reviews.
- **Notifications:** a dozen or so unread from the last few days: assignments by the service manager, bookings made by the coordinator, monitoring alerts (a clock pausing moves a target date, which a rule watches), and meetings the service manager arranged, moved or cancelled.
- **The side nav's pins:** two patients and one pathway record, from the lead's saved settings.

### The pathways list

- **Saved searches** (the star by the search): "My open work" (the list starts on it), "Urgent skin referrals", "Overdue prostate assessments", "Blocked", "Waiting for histology" and "Breached waits" (Breached only). `h.marsh` has "Skin lesions in progress", "Urgent bleeding referrals" and "Breached urology waits" (Breached, with the Urology specialty); `n.thornbury` has "My overdue".
- **The Wait column** ("Day 84 of 126", Breached, Paused) shows on every pathway from a referral; sort by it for the longest waits. Pathways made without a referral (`tour.referrals.clocks.createdWithoutAReferral`: diabetes recalls and biomechanics courses asked for in clinic) have no clock and a target date set by hand.
- **Waiting for histology** is the **Stage reached** filter on Histology (lab): every open record whose current stage is the lab's, on both skin lesion pathways and the post-menopausal bleeding pathway alike, on hold ones included. Add **Specialty** to narrow it to Dermatology.
- **Overdue work** comes from the slow parts, not at random: the waits for the visiting consultants' clinics (urgent post-menopausal bleeding referrals, due in three weeks, and prostate assessments), and skin lesions waiting for histology and treatment.
- **Every status shows:** Unassigned and Assigned (the newest), In progress, Awaiting review, On hold, Discharged, Cancelled and Archived.
- **Rows per page** is 25 for `admin` and 50 for `n.thornbury`, from their settings.

### A pathway record's page

Open the ones named in the summary's `tour`:
- **`blocked`:** put on hold with a reason when a stage was reached. **`blockedThenUnblocked`:** put on hold, then carried on days later.
- **`stageUnlockedAndCorrected`:** an admin unlocked a finished stage; it was corrected with a bold **Correction:** and finished again.
- **`finishedWithoutItsVisit`:** the two admin bypasses: a Required "Blood and urine tests" stage finished without a visit, because the samples were taken elsewhere, with the reason in the audit.
- **`cancelledWithItsBookingCancelled`:** cancelled after its visit was booked; the coordinator then cancelled the booking by hand ("Pathway cancelled; patient informed by phone").
- **`reassigned`:** a skin lesion that changed hands part-way: one skin clinician assigned, the other taken off.
- **`dueDateChangedByHand`:** target dates moved by hand: on a pathway made without a referral, or once its clock has stopped (when the lead sets the target date for the rest of the pathway). While a clock runs or is paused, it sets the target date, and the Edit Pathway page shows it read-only.
- **`visitNeverBooked`:** a visit asked for weeks ago and still in Requires Appointment.
- **`oldPathwayStillFinishing`:** the archived skin lesion pathway's last three, whose histology took months.
- **`missedAndRebooked`:** a visit the patient didn't attend, then a new one.
- **`inReview`:** every stage finished, awaiting review: waiting to be marked discharged.

**On any pathway record:**
- **Stage notes** are specific to the referral and the stage, and agree from stage to stage: a skin lesion's notes name the same site throughout (the first visit describes the lesion, the biopsy says where it was taken from, the lab's note gives the histology in words, the team review gives the plan, the treatment note says what was done). Sometimes with a list or a bold point.
- **The audit log** has every change, by whoever made it.
- **Each stage's visit** shows its appointment: requested by the app when the stage became current, booked (usually by `h.marsh`) with whoever is working on the stage, attended before the stage was finished.
- **Suggested visits** (Results and team review, Medicines review, Follow-up) are often finished without one: the app's request is cancelled automatically, and the audit says "without visit".
- **Notes on the record:** often a first note from the clinician who accepted the referral, and more added after stages; some edited by their authors.
- **Referral and wait** (a card on every pathway from a referral): when it was received, the specialty, "Day N of target", Breached or Paused, **Pause clock** and **Resume clock**, and past pauses. Its target date shows "from the waiting-time clock" until the clock stops.

### A patient's page

- **The timeline:** every pathway change and appointment entry, filterable by kind.
- **The access report:** who viewed, listed or searched for the patient in the last 60 days.
- **Restricted** (the summary's `privacy.restricted`): two records. The first has a pathway under way: staff can still block or cancel it, nothing else.
- **Disputed:** two, with reasons; a banner only. The first was also exported for a subject access request.
- **Children:** a few, on the inflammatory skin and biomechanics pathways only ("Children" saved search on the patients list). Most patients are older adults, as a centre like this sees: about half are 70 or over.
- **Women only, or men only, where it matters:** the post-menopausal bleeding and prostate pathways take patients of 50 and over, with women's or men's first names.
- **Referrals** (a card on the patient's page, with **Register referral**): every patient with a pathway from a referral has one; about 15 registered in the last four weeks have only a referral awaiting triage, and about 45 more only referrals that were rejected, redirected elsewhere, withdrawn or entered in error.
- **Deleted pathways:** two, on the patients in `tour.deletedPathwaysOf`. Admins see them on the timeline, labelled deleted.

### Referrals, triage and the waiting-time clock

Every referral is registered by `h.marsh` from the letter, usually the day it arrived, and triaged by a clinician of its specialty: urgent ones within a day or so, routine ones within two weeks. About 700 in all over the 18 months; the summary's `counts.referrals` has the numbers and `tour.referrals` the records.

- **The inbox** (Referrals, the side nav's count; as `admin`, or as a triager such as `n.thornbury`, who starts on their own specialties): about 20 awaiting triage now, in every specialty, longest wait first.
  - **Past their target** (`tour.referrals.awaitingTriageBreached`): an urgent skin referral at 19 days (target 14), a high-priority bleeding one at 30 (target 28), and an urgent urology one at 31 (target 28): Breached, in red.
  - **Nobody can triage** (`tour.referrals.nobodyCanTriage`): the new Respiratory service's three referrals. Open one: it says only members of Respiratory can triage it. Administration, Specialties warns about Respiratory.
  - **Moved here** (`tour.referrals.redirectedHereAwaitingTriage`): registered under Gynaecology, moved to Dermatology by the gynaecologist three days ago (its history shows Specialty changed and the target re-read).
  - **A confirmed duplicate** (`tour.referrals.duplicateAwaitingTriage`): registered yesterday on a patient with an open dermatology pathway. To see the warning, **Register referral** for that patient in Dermatology (`tour.referrals.registerAgainToSeeTheDuplicateWarning`): the form lists the open ones and asks to confirm.
- **Triage** (as `n.thornbury`, on a dermatology referral in the inbox): the banner, the letter, "The referrer asked for: High" beside an empty priority, the specialty's templates by name; Accept, Reject or Redirect, each confirmed with the patient shown.
- **Triaged before** (each a referral's page, with its history):
  - **Accepted:** every pathway from a referral; `tour.referrals.acceptedAfterBeingRedirected` were moved first (bleeding referrals sent to urology, foot pain sent to diabetes), and `tour.referrals.raisedToUrgentAtTriage` were raised to Urgent by the clinician who read them.
  - **Rejected** (about one in ten), one of each reason in `tour.referrals.rejected`; the duplicate (`tour.referrals.duplicateRejected`) was registered after its warning was confirmed, then rejected as a duplicate.
  - **Redirected elsewhere** (`tour.referrals.redirectedElsewhere`), **withdrawn** (`tour.referrals.withdrawn`), and one **entered in error** (`tour.referrals.enteredInError`): registered on the wrong patient, marked within half an hour, then registered on the right one, whose pathway is under way.
- **The coordinator's bell:** "Referral triaged" for each decision in the last 30 days ("n.thornbury accepted a Dermatology referral."), never naming the patient.
- **Waiting-time clocks** (the Referrals page's views, and each pathway's Referral and wait card):
  - **Waiting list** (accepted, clock running or paused), longest wait first. Every pathway's clock stops when its clock-stop stage (usually the first clinic visit) is finished.
  - **Paused clocks** (`tour.referrals.clocks.paused`): about ten, each paused when the patient asked to wait (chose to wait, unavailable, or another reason with a detail), with a review date as the pathway's target date. One (`tour.referrals.clocks.pauseReviewOverdue`) is past its review date, so it's flagged and its pathway shows Overdue (hazard H-42).
  - **Resumed** (`tour.referrals.clocks.resumed`): paused for some weeks, then resumed; the days paused moved the target on.
  - **Breached** (`tour.referrals.clocks.breachedOpen`, open; `tour.referrals.clocks.breachedStopped`, stopped late): mostly urology, where the visiting consultant's clinic is the slowest to reach, and now and then an urgent skin or bleeding referral.
- **Saved searches on the Referrals page:** `admin` has "Longest waits" (the waiting list, whose order is the longest wait first) and "Paused clocks"; `h.marsh` has "Paused clocks" and "Closed referrals"; `n.thornbury` has "Dermatology inbox".
- **On the patient's timeline:** Referrals and Waiting time entries (registered, triaged, paused, resumed, stopped, target changed); staff never see the reason, the details or the referrer.

### The lab, results and consent (from ten weeks ago: `tour.centre`)

The centre started tracking specimens in MedApp ten weeks ago (`tour.centre.labAndLettersFrom`); older biopsies have their reports as files only.
- **Specimens** (Lab, as `r.aldous`): every biopsy (a punch, an excision or an endometrial biopsy) and every blood and urine visit since then takes one or two, labelled with a number (`MA26-000123`). Sent within the hour, received when the lab's stage starts, reported when its work is done; a fifth of the skin biopsies go to the invented partner lab, which takes longer (a long tail), so a few are **overdue** now (`specimensOverdueAtLab`). The archived pathway's stragglers' biopsies have been at the lab for weeks.
  - **Rejected and taken again** (`specimenRejectedAndTakenAgain`): the urine pot came without a label; the lab rejected it and the sample was taken again before the patient left.
  - **Lost** (`specimenLostAndPathwayOnHold`): on its way to the partner lab; the clinician marked it lost and put the pathway on hold for a repeat biopsy.
- **Results** (Results, as the clinician who asked): each report makes a result for the requesting clinician to sign off with what they did (every action shows: no further action, patient informed, appointment arranged, for MDT discussion, referred on, other with a detail); some marked seen first. About 15 await sign-off now (`resultsAwaitingSignOff`), a few for over two weeks (`resultsAwaitingOverTwoWeeks`); a lab stage waits for them. One was **entered in error** by the lab (filed against the wrong specimen) and reported again (`resultEnteredInErrorAndReportedAgain`). One, a report from another service on a patient's record, was recorded by the leaver before they left and never signed off: it **needs a clinician** (`resultNeedingAClinician`).
- **Consent** (the pathway's Consent card): a signed consent form ("Consent for a skin procedure (demo)" or "Consent for an endometrial biopsy (demo)") recorded on each biopsy stage. One patient **withdrew** before their biopsy; the pathway is on hold (`consentWithdrawnAndPathwayOnHold`).
- **Nothing here is MedApp's advice:** the actions and outcomes are what the clinician "recorded" from the referral's planned outcome; MedApp never reads a report.

### The MDT list (Friday's skin cancer team meeting)

Since the MDT list went live, the weekly skin cancer team meeting is the Dermatology **MDT**. Each skin lesion's results and team review lists it for the next one with the lead's question; the lead, who chairs it, records the outcome the team agreed, and who was there (its attendees who hadn't declined). The pathway's assignees are told ("MDT outcome recorded"). `tour.centre.mdt` names the last meeting (with how many were discussed) and the next one, with the pathways listed for it.

### Branches and skipped stages

- **Branches** (the skin lesion pathway): after the results and team review the clinician chooses **Benign** (straight to the discharge letter) or **Treatment** (treatment, follow-up, then the letter), from the referral's outcome; the other branch's stages show as skipped. `benignBranchChosen` and `treatmentBranchChosen` name some.
- **Skipped when the patient declines:** the third biomechanics session is optional; about one in five patients since then declined it (`stageSkippedPatientDeclined`), and one changed their mind: the stage was **reinstated** and the session held (`stageSkippedThenReinstated`).
- **Did not attend:** since then, the booking coordinator records what's next after each (another appointment, or a clinician to review); a few from the last week still wait for a decision (`didNotAttendAwaitingDecision`).

### Letters

Letter templates for each specialty (clinic letter, discharge summary, referral response, request for information; Administration, Letter templates), with merge fields for the patient, the practice, the author and the approver. Staff have full names and job titles (invented), as letters print them.
- **Discharge summaries** for every discharge letter stage that needs one: written by the lead from the template, approved by them (a clinician approves their own) or, for the diabetes nurse, submitted to the clinical lead; its PDF made; then recorded as posted to the GP practice and the patient by the booking coordinator, before the stage was finished.
- **In other states now** (`tour.centre.letters`): a `draft`, one `awaitingApproval`, one `returned` with a comment, one `approvedToSend`, and one `withdrawnAndCorrected` (withdrawn after it was sent; its correction approved and posted). A request for information is drafted on a dermatology referral awaiting triage (`informationRequestDraftedOn`).
- **Practices and contact details:** eight invented GP practices (the referrals' names) on invented streets, postcodes in the `ZZ` area (none exists), phones from Ofcom's drama range, no ODS codes. Every patient has an address there, most a mobile from 07700 900000 to 900999, a registered practice, and how they agreed to be reminded (a quarter gave no consent).

### Sites, hours, clinic sessions and reminders

- **Sites and rooms** (Administration, Sites): the main site (open until 20:00 on Tuesdays) with clinic rooms, a minor surgery room and a phlebotomy room; the invented Wrenhurst outreach clinic (Saturday mornings too) with a consulting room and a gait lab.
- **Working hours** (Users, a person's hours) from ten weeks ago: `f.linton` Tuesdays and Thursdays, `o.penhale` Wednesdays and Fridays, `m.carrow` mornings; everyone else the default.
- **Clinic sessions** (Calendar, Clinics): a weekly pattern for each specialty's clinic, set up three weeks ago; its sessions made for eight weeks through Create sessions (dates already taken on the clinician's or the room's calendar were skipped, as the app skips them). Reviews are booked into some of the coming slots, and one session was **cancelled** with its call list (`tour.centre.clinics`). Appointments booked by the pathways before then stay outside sessions.
- **Reminders** (an appointment's Messages): a reminder two days before each appointment over the last four weeks, made by the app's own factory as the reminders job makes them and sent by the offline transport (nothing leaves): most delivered, a few failed, and a Not sent record for patients who gave no consent. The setting that sends them (`PatientMessages:Mode`) stays as the deployment has it (Off unless turned on).

### Files

On about 60 records: referral letters on most referrals registered in the last two months (the rest show "no letter", as when a request is phoned in and the letter follows); histology reports, blood and urine results, clinic letters and discharge letters on pathway records; a lesion's site map (the site its notes name) and a urine flow test's trace, both pictures with thumbnails; and a home programme and a phone note on stages under way. Each says it's synthetic. Two are deleted ("Show deleted", as an admin): a second scan of a referral letter, by its uploader, and a letter on the wrong patient, by an admin. Some have been downloaded (the audit and the access report show it).

### The calendar

- **The lead's diary day** (`tour.leadsAppointmentsThatDay`): five appointments, lunch, minor surgery list planning (a meeting), any of the day's regular meetings, and free time to book into by clicking.
- **Personal templates** (Calendar, then Calendar templates; admins choose whose with the staff picker): everyone has their own day templates, week templates and default week, with the differences listed in `tour.calendar.personalTemplates`:
  - `f.linton` and `o.penhale` have a "Visiting week": "At the hospital" on the days they aren't here, so nothing can be booked with them then
  - `r.aldous` has "Lab reporting" every day
  - `m.carrow` worked full time until 20 weeks ago, then mornings only (two dated default-week rows: past weeks still show full days)
  - `n.thornbury` moved the Minor surgery list's lunch later 10 weeks ago (an edit to the template, so earlier list days keep the earlier lunch)
  - `h.marsh` has a late lunch (13:30), covering the desk
  - `t.hallworth` operates on Friday mornings (a "Friday list"), where `n.thornbury` has an admin day
  - `j.whitlock` finishes early on Fridays, and `admin` has a management afternoon on Wednesdays and an outreach clinic on Thursdays
- **Day templates** have lunch and blocks for the role: Clinic day, Morning clinic, Afternoon clinic, Minor surgery list, Friday list, Lab reporting, Outreach clinic, Triage session, Admin day, At the hospital, Day off, Office day. They leave the regular meeting times free for the people who go to them.
- **Activity types** the centre added: Lab reporting, Outreach, Triage, Teaching, Supervision, Off site and Off duty.
- **Closed days** (Administration, Closed Days): England and Wales bank holidays for every year the story covers and the next, added by the lead. Nothing is booked, blocked or arranged on them, and they show greyed on the calendar ("Closed: Christmas Day"); a weekly meeting that would fall on one is skipped that week.
- **Exceptions:**
  - **Leave and training days** have no day template (as "This day's template: none" saves), then one block for the day: the app refuses a whole-day block over a template's lunch
  - `b.sandiford` has a week's annual leave next month, and next Friday uses her Minor surgery list template (assisting the Friday list)
  - `s.winterburn` has a day's leave this week
  - `t.hallworth` has a training day next Wednesday
  - `n.thornbury` has teaching on the next two Thursdays, and her lunch is moved on the day after the diary day, into free time
  - `d.quince`'s admin time was removed on the day before, and an appointment booked in its place
- **A walk-in:** `b.sandiford` saw a patient without a booking on the last working day; `h.marsh` recorded it the next morning ("Recorded after the event").
- **Requires Appointment:** visits recently asked for, and a few asked for long ago and never booked.
- **Appointment history:** booked, moved (rescheduled), cancelled by the patient or because the clinician couldn't make it, did not attend, notes added and edited.

### Meetings and the team view (as `admin` or `j.whitlock`)

- **Regular meetings** across the whole 18 months and the coming weeks, each arranged by an admin some weeks ahead:
  - **Monday huddle**, 08:30 to 08:45: everyone here that morning (not the visiting consultants)
  - **Skin cancer team meeting**, Friday 13:30 to 14:30: the lead (who chairs it), the two skin clinicians, the skin cancer nurse specialist and the lab
  - **Clinical governance**, the second Wednesday of the month, 16:00 to 17:00
  - **Mandatory training** every six weeks (Basic life support update, Fire safety, Safeguarding update and others), for the nurse specialists, the healthcare assistant, the podiatrist and the pharmacist, at a time they're all free
  - **1:1 catch-ups** each month: `j.whitlock` with `h.marsh` and `c.dunmore`, the lead with `d.quince` and `b.sandiford`
  - **Welcome and induction** for the new starter, and **minor surgery list planning** on the diary day
- **Who goes:** only people free at the time, as the app requires. The part-time nurse specialist and the visiting consultants miss what falls outside their days; someone on leave isn't invited; someone who booked leave after a meeting was arranged was taken off it first ("took you off" in their notifications); newcomers were added to meetings already arranged.
- **Changes** (`tour.calendar.movedMeetings` and `tour.calendar.cancelledMeeting`): minor surgery list planning moved three days ago, with `h.marsh` added; the next governance meeting and a 1:1 moved when there was time to; away day planning was cancelled four days ago. Their notifications ("Meeting invitation", "Meeting moved", "Meeting cancelled") are in the bells of everyone told, never the admin who made the change.
- **Replies:** open any meeting to see who accepted, who declined and who hasn't replied, with a summary. Past meetings were mostly accepted, with a few declines and some never answered; upcoming ones are a mix, so signing in as an attendee shows Accept and Decline. A meeting someone declined is faded on their calendar and says Declined (its time is free for them), and each decline told the organiser ("Meeting declined" in `admin`'s or `j.whitlock`'s bell, for the last 30 days). (Moving a meeting clears its replies; the moved meetings above were answered after their move.) The summary's `calendar` counts `meetingsAccepted` and `meetingsDeclined`.
- **The team view** (Calendar, Team, admins only): open it on `tour.calendar.teamViewDay` to see everyone's day side by side, with the day's meetings (`tour.calendar.meetingsThatDay`) across several columns and the **Everyone free** column. Select free time there to arrange a **New meeting**; its suggested times come from the same calendars. `tour.calendar.upcomingMeetings` lists what's next.

### Notifications and monitoring (as `admin`)

- **Monitoring rules** (`/admin/monitoring`), eight of the lead's:
  - when the new starter creates a pathway record
  - when `n.thornbury` finishes **Treatment** on a High or Urgent one, and when anyone finishes **Histology (lab)** on an Urgent one (rules that name a stage)
  - when `n.thornbury` or `t.hallworth` blocks one, and when anyone blocks a post-menopausal bleeding one
  - when anyone changes a target date
  - one switched off two weeks ago: unlocks by the service manager
- **The service manager's own four rules:** urgent post-menopausal bleeding referrals, cancellations, completed biomechanics courses, and the lead being assigned.
- **Monitoring notifications** in the last 30 days match those rules, as the app would have sent them.
- **Notification choices:** `f.linton` turned off assignment notifications 45 days ago, so has none since.

### Insights (`/admin/insights`)

Each combination of period and template looks different. The **Specialty** choice narrows everything to one specialty's pathways (and the Template choice to its templates): Dermatology is the two skin lesion pathways and inflammatory skin, Gynaecology the bleeding pathway, Urology prostate assessment, Diabetes and Biomechanics one each; General is empty. Roughly, for a run in November 2026:

| Template | Last 4 weeks | Last 12 weeks | Last 26 weeks |
|---|---|---|---|
| **All** | About 70 created | About 235 created | About 480 created |
| **Skin lesion pathway** | A steady 8 a week; completions catching up; the wait at Histology (lab) shows | 10 a week in September (summer volumes), 8 since October | Launched 18 weeks ago, in July: nothing before, then a six-week ramp-up |
| **Skin lesion pathway (2024)** (archived) | Almost nothing: only its three stragglers' stages | The stragglers, and its last completions | About 6 a week until the redesign, then it stops |
| **Inflammatory skin review** | A low week last week (a clinic was cancelled) | Rising into the winter | Quiet over the summer, about 3 a week; 4 to 5 from November |
| **Post-menopausal bleeding pathway** | A spike to 4 a week two and three weeks ago (extra visiting clinics); first visits booked quickly | The dip while the consultant was away, 8 to 11 weeks ago | About 2 a week; many urgent |
| **Prostate assessment** | 2 a week; completions lag; overdue for its size | A growing backlog | Steady referrals; the long wait for the first clinic visit |
| **Diabetes review** | The recall week's jump (6 that week, 2 otherwise) | Three recall weeks | A jump at the start of each month |
| **Biomechanics course** | About 2 a week | Sessions a week or two apart | Started 20 weeks ago: nothing before |

**"This time last week"** comparisons, the overdue charts (by assignee and priority), throughput and the time at each stage all differ with them. `DemoDataSeedTests` checks that every combination gives a different result.

### Users and security (as `admin`)

- **Users:** `c.dunmore` is locked out until tomorrow evening (Unlock); `e.rowntree` was created this week; `j.whitlock` has two-factor; most have a confirmed `@centre.example` address, `e.rowntree`'s isn't confirmed, and `c.dunmore` has none.
- **Sign out everywhere:** on a working day from 07:30, the staff who've worked today have signed in, so they have live sessions. At a weekend, or earlier in the morning, nobody has.
- **Security** (Admin settings): the admins' sign-in rules are back to a new database's (none), whatever they were before the reset.
- **The activity log** (60 days):
  - sign-ins and sign-outs on working days, some refused, and a few for names no account has
  - the locked account's run of wrong passwords
  - patient and pathway views by the staff working on them, referral views (`ReferralViewed`) by whoever registered or triaged each, and lists and searches (the referral inbox each morning for the coordinator and the triagers; filter names and counts, never the terms)
  - two exports, the restrictions, disputes and anonymisation
  - the new starter's creation, the service manager made an admin, the password reset, two-factor turned on
  - file views and downloads, and the retention "keep" decisions

### Data requests (Administration, Data requests)

One open subject access request (the summary's `openDataRequestFor`): received nine days ago from the patient's daughter (an invented name) with their written consent, logged by the clinical lead, her identity checked from documents two days later; its one-month deadline is ahead.

### Retention review

Six imported records are due: five adults last seen 9 to 12 years ago, and one child seen at 11, now over 25. Two of them are kept until next year, with reasons (shown with "Include kept"). A seventh was anonymised three weeks ago (`privacy.anonymisedPatientId`), so it's no longer listed.

## Choices made

- **Seven templates:** the six pathways the centre runs, and one archived older template (the 2024 skin lesion pathway), kept because some of its records are still finishing.
- **Size:** the weekly volumes decide it: about 680 pathway records, each going to someone already registered about two times in three (a patient with one is likelier to get another than one with three), with women only on one pathway and men only on another. With about 20 referrals awaiting triage, the patients of referrals that weren't accepted (about half of them new), and 12 imported records, that's about 300 patients in all; it varies a little with the day it's run.
- **Referrals:** every pathway from a template starts from a referral, but half the diabetes reviews (recalls the service sends itself) and half the biomechanics courses (asked for by a colleague in clinic), which are made directly, with no clock. The referral is registered before the patient's pathway exists, so a new patient is added minutes before their referral (decision 11). The accepting clinician is usually the pathway's lead; the pathway's target date is the clock's until it stops, then the lead's. Referrals that weren't accepted come at the pathways' own volumes (about one rejected for every ten accepted).
- **Clock-stop stages** are where the patient is first seen (the first clinic visit, or the diabetes clinic review), so the wait ends with being seen. The skin lesion pathway's Treatment would have made nearly every urgent skin referral breach.
- **Pauses** are asked for when an early stage is reached, by the lead (who holds Work pathways), as the app allows only after acceptance. No clock is paused before triage.
- **Referrers** are invented practices and GPs (none is a real organisation in the NHS Organisation Data Service's directory), with no ODS codes and no link to the GP practice directory.
- **History before the last 26 weeks** is at a fifth of the usual volume.
- **The redesigned skin lesion pathway** launched 18 weeks ago and took six weeks to reach 8 a week (10 in summer); its 2024 predecessor ran at 5 to 6 a week until then. The old pathway's records were replayed until they all finished more than four weeks ago (but for three stragglers, made deliberately slow).
- **The biomechanics service** started 20 weeks ago, when its podiatrist joined.
- **The leaver** led only pathways that closed before they left; one that wouldn't have was replayed with another lead.
- **Imported records** (the retention review's) have no audit history and no visits: they predate the app, whose history starts when it was used. Their stages' visit requirement is None, as the migration left existing stages.
- **Notifications** are made for the last 30 days only; older ones would long have been read.
- **Files on stages** go only on stages still under way, since the app allows files on a stage only while it can be edited; documents from finished stages (reports, letters) are on the pathway record.
- **One outcome and one site for each referral,** chosen when it's planned, so its notes agree from stage to stage. The outcomes are a few fixed texts for each kind of referral: the seed picks one, it doesn't work one out.
- **Today's sessions** come from the app's own sign-in command, at the moment each person started work today, and last eight hours. Only on working days from 07:30, as in the history. The visiting consultants sign in on their hospital days too (they triage from there).
- **The locked account's lockout** is the app's own (after five wrong passwords, 20 minutes before the run), stretched to tomorrow evening: the app's five minutes would pass before anyone opened the Users page. It happens whatever the day.
- **Email** goes nowhere: the seed's email sender drops every message, while the app's commands still record the address requests and confirmations.
- **SNOMED CT codes** are only on the stages whose concept is certain; each passes the app's format check. Nothing checks them against a terminology server, as the app doesn't.
- **"Off duty", "Off site", "Day off" and "At the hospital"** block the time someone isn't at the centre, so part-time afternoons, days off and the visiting consultants' hospital days can't be booked.
- **At a weekend** the diary day is the next Monday, since nothing can be booked on a weekend.
- **Leave** is stored as the app would let someone record it: the day set to no day template, then the leave as one block.
- **Templates are personal:** each person has their own copies of the day templates their work needs, named the same way, as the app keeps them, rather than one set the centre shares.
- **Meetings** are arranged one at a time, as the app has no repeating meetings: the regular ones in batches of eight weeks, four weeks ahead; governance three weeks ahead; training four; 1:1s two. Everyone invited is free then, so a meeting never overlaps anything on an attendee's calendar, and visits are booked round them. Their notifications, like the rest, are made for the last 30 days only.
- **Patients' names** are unique, so the summary can name a record by name alone. Real centres have namesakes; a demo can't afford to open the wrong one.
- **Phases 3 to 5 start ten weeks ago** (`LabAndLettersFromUtc`): specimens, results, consent, the MDT list, letters, Did not attend decisions and skipped stages come from then, and only pathways created from then carry the stage rules that came with them (a stage's copy is fixed when the pathway is made). Through the app's shared code (`SpecimenChange`, `ResultChange`, `ConsentChange`, `MdtAudit`, `StageFinishing`, `StageGates`, `AppointmentChange`) in the replay, and the letter and clinic commands afterwards.
- **The lab reports whatever happened to the pathway since** (on hold, cancelled): a report is never stuck; one on a pathway that no longer takes files goes on the patient's record, as the app files it.
- **The reports' and letters' files** are stored only when Azurite answers; otherwise their records are kept without the bytes (as on kind).
- **Specimen numbers** restart the sequence after the last used, so one recorded now gets the next.
- **Clinic sessions don't cover the history:** the pathways' appointments were booked before sessions existed, so sessions are made only from three weeks ago, where the calendars are free.
- **History respects when each patient existed:** nobody views, lists, books or changes a patient before they were registered, and nobody sees the anonymised record afterwards.
