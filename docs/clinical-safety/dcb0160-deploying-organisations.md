# MedApp: clinical safety for deploying organisations (DCB0160)

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment, nor DCB0160 guidance yet. The manufacturer's CSO must sign it off before it is given to any deploying organisation.**

| | |
|---|---|
| **Document** | CS-05, guidance for organisations that deploy MedApp |
| **Version** | 0.9, draft |
| **Date** | 18 November 2026 |
| **System** | MedApp, `main` at `88dc1fa` |
| **Manufacturer** | [Manufacturer name] |
| **Drafted by** | An AI coding agent, for the manufacturer. Not yet reviewed by a person. |
| **Approved by** | [Manufacturer's Clinical Safety Officer] (see [sign-off](#sign-off)) |

For the clinical safety lead of a health or care organisation that is deploying MedApp. It says what DCB0160 asks of you, what the manufacturer gives you, and which controls in the manufacturer's hazard log rely on you.

## What DCB0160 is

- **DCB0129** applies to the manufacturer of health IT. **DCB0160** applies to the organisation that deploys and uses it. Both are information standards under section 250 of the Health and Social Care Act 2012.
- **Who it applies to:** in England, NHS bodies and organisations providing NHS-funded care must apply it. Independent providers aren't bound by it in the same way, but it's the accepted standard for showing that a clinical system was deployed safely, and the Care Quality Commission expects records that are accurate and changed only by authorised people (Regulation 17).
- **It's changing:** NHS England reviewed both standards in 2026, and future versions are expected to use the new powers in section 121 of the Data (Use and Access) Act 2025. Check for a new version before you start ([NHS England](https://www.england.nhs.uk/long-read/national-review-of-clinical-risk-management-standardsdcb0129-and-dcb0160-supporting-information/)).
- **MedApp isn't a medical device** (`intended-purpose-and-device-decision.md`), so no UKCA marking is involved. DCB0160 still applies, because it supports direct care.

## What you need to do

1. **Appoint a Clinical Safety Officer** for the deployment: a registered clinician, trained in clinical risk management. They own your clinical risk management, not the manufacturer's CSO.
2. **Write a clinical risk management plan** for the deployment, or add MedApp to your existing one.
3. **Read the manufacturer's safety case and hazard log** (`clinical-safety-case-report.md`, `hazard-log.md`), and check that their assumptions hold for you ([below](#the-manufacturers-assumptions-about-you)).
4. **Write your own hazard log** for the deployment, starting from the [starter list](#a-starter-hazard-log-for-your-deployment) and the [controls transferred to you](#controls-the-manufacturers-hazard-log-transfers-to-you).
5. **Configure MedApp safely:** templates (including which stages involve a visit), calendars, roles and accounts ([below](#configuring-medapp-safely)).
6. **Train staff** before they use it ([below](#training)).
7. **Write a downtime procedure,** and test it before go-live ([below](#downtime)).
8. **Write a clinical safety case report** for the deployment, and have your CSO sign it off before go-live.
9. **After go-live:** report incidents to the manufacturer as well as through your own system, review the safety case when MedApp or your use of it changes, and read each release's safety notes.

## What the manufacturer gives you

| Item | State |
|---|---|
| The intended purpose and the medical device decision | Draft: `intended-purpose-and-device-decision.md` |
| The clinical risk management plan | Draft: `clinical-risk-management-plan.md` |
| The hazard log | Draft, 64 hazards, of which H-30 to H-64 are drafts still to be rated by the CSO; H-40 to H-64 (the community specialist centre's Phases 2 to 5) were written from the design, before the features are built: `hazard-log.md` |
| A clinical safety case report for each release | Draft template: `clinical-safety-case-report.md`. **No signed report yet.** |
| This guide, with a starter hazard log for your deployment | Draft: this document |
| A named CSO, and a route for reporting safety incidents | **Not yet:** [CSO name], [incident reporting address] |
| Release notes that say what changed in clinical workflow, and its safety impact | **Planned** (the release gate in the plan, section 8) |
| Instructions for use, and training material | **Planned** |
| A printable task list and pathway summary, for downtime | **Planned** (roadmap step 6) |
| Service levels, monitoring, backups and restore tests, for the hosted service | **Planned** (roadmap step 5) |
| Notice of known issues and field safety information when something goes wrong | **Planned**, through the incident process (plan, section 7) |

## The manufacturer's assumptions about you

The manufacturer's safety case holds only if you:
- **identify patients** at the point of care by your own positive identification process. MedApp has names, dates of birth, email and an optional NHS number, whose check digit it checks but which it doesn't trace with PDS, so check a number against your own records (for example your PAS) when you enter it;
- **govern templates:** clinical sign-off of every template and stage definition before use, and when one changes, including which stages involve a visit and whether it's suggested or required;
- **set up calendars:** activity types, day and week templates and each person's default week, and record staff absence in MedApp;
- **book and follow up visits:** someone reviews the Requires Appointment list and Did not attend appointments on a set schedule;
- **tell patients about their appointments,** and about every cancellation and change, by your own process. MedApp texts and emails patients only if you turn it on, and only those who chose it: treat those messages as a courtesy on top of your process, never instead of it;
- **follow up missed appointments:** someone owns the Did not attend view on Requires Appointment and works it on a set schedule;
- **train staff,** including what finishing a stage means, what a target date means, that notifications aren't to be relied on, and that My Worklist opens on work not yet started;
- **handle referrals safely,** once you use them: give the permission to register referrals (`RegisterReferrals`), and to triage them (`TriageReferrals`, with membership of the specialty), only to the right people, with at least one triager for every specialty; check the referral inbox every working day; keep a paper or downtime process for referrals; and, once the waiting-time clock exists, set its targets and review your waiting list;
- **have at least two admins** who are available while staff work;
- **reassign a leaver's open pathways** before removing their account;
- **review overdue, unassigned and blocked pathways** on a set schedule;
- **have a downtime procedure;**
- **keep the clinical record** of what was done in your clinical systems; MedApp coordinates the steps;
- **keep the master copy of every document** attached in MedApp (results, letters, images) in your own systems, check a document's own identifiers against the patient before acting on it, and protect every computer that opens attached files with up-to-date anti-malware and software;
- **use MedApp within its intended purpose:** not for emergency or time-critical care, not for decisions, not for medicines.

If one of these doesn't hold for you, tell the manufacturer: it changes the hazard ratings.

## Controls the manufacturer's hazard log transfers to you

| Hazard | What you provide |
|---|---|
| H-01 The wrong patient's pathway is worked | Positive patient identification at the point of care; train staff to check the patient's details on the pathway page |
| H-02 Work is recorded on the wrong record | A local rule: correct a wrong entry with a note saying so, never by deleting it |
| H-04 An overdue pathway is missed | Who reviews the overdue and unassigned lists, and how often; at least two admins |
| H-05 A blocked pathway is forgotten | A review of blocked pathways, with who and how often |
| H-06 A stage is finished in error | Admins available to unlock stages while staff work |
| H-07 A stage is recorded as finished when the work wasn't done | A policy that finishing a stage states its work was done; the clinical record of the work in your clinical systems |
| H-09 A member of staff doesn't learn of work assigned to them | Tell staff to work from My Worklist with its filter set to all open work (it opens on "Not started"), not from notifications |
| H-10 MedApp is unavailable | A downtime procedure |
| H-13 A template or stage definition has wrong content | Clinical sign-off of templates and definitions |
| H-14 A correction to a template doesn't reach pathways already under way | When a template is corrected for safety, find its open pathways and review each |
| H-17 Stage instructions aren't read | Write anything essential as its own stage, not only in instructions |
| H-18 A change is recorded under the wrong person's name | No shared accounts; lock screens when away |
| H-21 Work assigned to a deleted account drops off every task list | A leaver process: reassign a leaver's open pathways, then remove the account |
| H-22 Care under way is delayed because the patient's record is restricted | Before restricting a record, the admin checks with the clinician responsible and agrees how care continues; block or cancel pathways with a reason meanwhile |
| H-23 A file is attached to the wrong patient, pathway or stage | Positive patient identification; train staff to check the patient before attaching a file, to check a document's own identifiers before acting on it, and to delete and report a file attached in error |
| H-24 A document or result is missing, and nobody knows | Master copies of documents in your own systems; train staff to check a file is listed after attaching it, and to look in the source system before concluding a result doesn't exist |
| H-25 An attached file carries malware | Up-to-date anti-malware, PDF readers and Office, with macros off, on every computer that opens attached files; tell staff what "Not scanned" means and how to report a suspicious file |
| H-26 A file is deleted by mistake | A local rule: delete a file only if it was attached in error |
| H-27 An out-of-date or duplicate document is relied on | A local rule for naming files (what it is and the document's date), and deleting superseded copies attached in error |
| H-28 File storage is unavailable | Your downtime procedure says where to find documents when MedApp's files can't be opened |
| H-30 A visit the patient needs doesn't happen | Who books from the Requires Appointment list, how often it's reviewed, and who follows up Did not attend |
| H-31 A member of staff is double-booked | MedApp's calendar is the only diary for the appointments it holds |
| H-32 A stage is finished without the visit it needed | Clinical sign-off of which stages need a visit; a review of stages finished without one; admins bypass a required visit only with a recorded reason |
| H-34 The wrong patient or pathway is booked | Positive patient identification; check the patient's name, date of birth and NHS number, which the booking dialog shows, when booking |
| H-35 An appointment is left without its member of staff | Record leave and training in MedApp before booking around it; when someone is away unexpectedly, move or cancel their appointments and tell the patients |
| H-36 A cancelled or moved appointment isn't passed on to the patient | A process for telling patients about every booking, change and cancellation, including the Booked appointments of a cancelled pathway |
| H-40 A referral is lost or delayed before triage | **A daily check of the referral inbox** (Referrals, Awaiting triage, oldest first; the count is in the side navigation), with a named owner and cover; a process for referrals received while MedApp is unavailable (below); a rule that a referral MedApp refuses for a restricted patient goes to an admin at once, with its letter; at least one member of each specialty with the triage permission (the Specialties page warns about any specialty without one: act on it). **Until your CSO has verified the triage controls, don't use referrals with patients** |
| H-41 A referral is triaged into the wrong pathway, priority or patient | Positive patient identification when registering a referral and when triaging it (check the banner in the confirmation against the letter); clinical review of triage decisions; and the triage permission only for clinicians who triage that specialty, as members of it |
| H-42 A paused waiting-time clock is forgotten | Once the clock is built: a weekly look at the Paused clocks view, and pauses only for patient-initiated delays |
| H-43 A breach of a waiting-time target goes unnoticed, or the target is wrong | Once the clock is built: set each specialty's targets under your clinical governance, review them, and review your waiting list on a set schedule |
| H-44 A duplicate referral leads to two pathways for one need | Read MedApp's warning of an open referral to the same specialty before confirming; mark a duplicate registered by mistake as entered in error |
| H-53 A reminder reaches the wrong person or says too much | If you turn texts and emails on: confirm the patient's mobile, email and reminder choice with them whenever they're given or change, and record an opt-out at once; choose a sender name (`PatientMessages:SenderName`) and site names that don't reveal the kind of care where that matters; check a message's stored copy on the appointment when a patient asks what they were sent |
| H-54 A patient relies on a reminder that isn't sent | **Reminders are a courtesy:** your appointment letters or calls stay the way patients are told. Tell patients that, and look at an appointment's messages ("No reminder", failed) when it matters that they come |
| H-55 A missed appointment isn't followed up | **A named owner for the Did not attend view** (Requires Appointment), with cover, who works it on a set schedule, including the Rebook and Clinician to review items still listed; your policy for patients who don't attend, including who may decide "No further appointment". MedApp never discharges anyone because of a missed appointment: discharge is a clinician's action |

### Planned: what you'll provide for the centre's Phases 3 to 5

Specimens, results, letters, clinics, the MDT list, consent and the NHS adapters are designed (`docs/centre-phases-3-5-plan.md`) but **not built**. When each is, its controls transfer to you as below; until then, none applies. Hazards H-45 to H-64 are drafts for the CSO. **Reminders and the Did not attend view are built** (PR 26): their controls (H-53 to H-55) are in the table above.

| Hazard | What you'll provide |
|---|---|
| H-45 A specimen is lost, or its report never comes back | **A daily check of the lab worklist** (Expected and Overdue), and a process for chasing the lab |
| H-46 A result is never seen or acted on | **A daily check of results** awaiting sign-off and Needs a clinician, and cover for clinicians who are away |
| H-47 A report is attached to the wrong patient or specimen | Positive patient identification when labelling; the lab checks each label against its request |
| H-48 A letter goes to the wrong practice or person | Check the patient's address and GP practice with them at each visit |
| H-49 A letter isn't sent, or goes late | A daily check of letters to send and awaiting approval, and **a downtime process for letters** (below) |
| H-50 A letter shows another patient's details, or an unapproved version is sent | Check the identifiers on every page before posting or handing a letter over |
| H-51 A branch is chosen, or a stage skipped, in error | Clinical sign-off of each template's branches and optional stages |
| H-52 A patient is booked when nobody, or no room, is available | Keep sites' and staff's hours and closed days up to date; work the call list when a clinic is cancelled (MedApp can also text or email the patients who chose it, if you've turned messages on and tick "Tell the patient") |
| H-56 A scheduled job stops and nobody notices | Alerting on MedApp's health check, and a weekly look at Scheduled Jobs (for a hosted service, the manufacturer's) |
| H-57 A record is anonymised too early because its retention class is wrong | Your DPO checks the retention class of each template and the years against the Records Management Code of Practice |
| H-58 An MDT outcome is recorded on the wrong patient, or not acted on | The MDT's own process for confirming outcomes and owning actions |
| H-59 A procedure goes ahead without valid consent recorded | Your consent policy, including capacity, and clinical sign-off of which stages need consent |
| H-60 Questionnaire answers are read as an assessment | The instrument's own scoring rules, and a licence for each instrument you use |
| H-61 A video link reaches the wrong patient | The video platform's waiting room, with each patient admitted by the clinician |
| H-62 Data from an NHS system is matched to the wrong patient | Positive patient identification when matching an incoming referral |
| H-64 A file is held as "being scanned" when it's needed | Master copies of documents in your own systems |

## Configuring MedApp safely

- **Templates and stage definitions** are your clinical content:
  - Have each one written or checked by a clinician, and signed off under your clinical governance, before it's used.
  - Keep a record of who approved each version: MedApp doesn't keep template history yet (H-13).
  - Put each step that must not be missed in its own stage, in the right order. MedApp enforces the order you give it.
  - Archive a template rather than editing it heavily; create a new one for a new way of working.
- **Roles:** admins can manage users, templates, assignments and the audit log. Give the role to few people, but at least two. Remove it when someone changes job.
- **Referrals:** give the permission to register referrals to the staff who receive referral letters (the starting roles Clinician, Nurse and Booking have it, and so do Admin and "All staff (before roles)"), and the triage permission only to clinicians, each a member of the specialties they triage. Check every specialty has at least one (the Specialties page warns when one has none). Being an admin doesn't let anyone triage: the built-in Admin role doesn't grant it. Decide how the booking team tells the referrer of each outcome: MedApp tells whoever registered a referral, in the app, but sends nothing outside it. Decide who checks the referral inbox each working day, who covers them, and what happens to a letter MedApp refuses because the patient's record is restricted (it goes to an admin).
- **Accounts:** one account per person; no shared accounts. Reassign a leaver's open pathways, then remove the account. Deleting an account doesn't reassign its work, which then shows as assigned to "Deleted user" and is on nobody's task list (H-21).
- **Target dates:** decide locally what a target date means for each kind of pathway, and tell staff. In MedApp, a pathway is due by the end of its day, UK time.
- **Attached files:** decide what may be attached (for example referral letters, results, consent forms, wound photos), where the master copy lives, and how files are named. Files aren't scanned for malware yet: each shows "Not scanned" (H-25). Any member of staff can open any patient's files, and each download is recorded.
- **Visits:** for each stage definition, decide clinically whether it involves a visit, and whether the visit is **suggested** (finishing without one asks for confirmation) or **required** (it can't be finished until an appointment for it is Attended, except by an admin with a reason). Existing definitions start as none. A change reaches only pathways created after it.
- **Calendars:**
  - The working day is fixed at 08:00 to 18:00 UK time, Monday to Friday, and MedApp doesn't know bank holidays. Block off closed days and shorter hours by hand.
  - Set up the activity types (names should give no private reason: "Annual leave", not why) and the day and week templates, and give each member of staff a default week.
  - A change to a template applies from today, and can land on appointments already booked; MedApp shows both but moves nothing, so check each calendar it affects.
  - Decide how patients are told of bookings, changes and cancellations, and who does it. MedApp texts and emails patients only if you turn it on (below).
- **Texts and emails to patients** (off by default):
  - Decide whether to use them at all. They need **your own GOV.UK Notify service** (or another provider), its two templates and its API key in your deployment's secret store, and the `Live` mode (`PatientMessages:Mode`); your DPO checks the provider first ([`docs/governance/sub-processors.md`](../governance/sub-processors.md)).
  - Set your sender name (`PatientMessages:SenderName`), a contact phone for appointments without a site's phone (`PatientMessages:ContactPhone`), how many days before an appointment reminders go (`Reminders:DaysBefore`, 2 by default), and whether patients who miss an appointment are messaged the next working day (`Reminders:MissedMessage`, off by default).
  - Give each room a site with an address and phone, so messages say where to go; an appointment without a room names no site.
  - Never use the `Offline` mode with real patients: it's for development and demonstrations.

## Training

Before anyone uses MedApp with patients, they need to know:
- how to find the right patient and check the patient's details on every pathway;
- that stages are worked in order, and that **finishing a stage locks it and says the work is done;**
- how to ask an admin to unlock a stage, and how to correct a wrong entry;
- what "overdue" means, and that, today, nothing alerts them when work becomes overdue;
- that blocked pathways don't show as overdue, and need reviewing;
- that notifications are a convenience, and My Worklist is the list to work from, with its filter set to all open work: it opens on "Not started", which hides work already under way, including overdue and blocked work;
- how to attach a file to the right patient, pathway or stage, and what to do if it goes to the wrong one (H-23);
- that files are only downloaded, never shown in the page, what "Not scanned" means, and to delete downloaded copies when finished;
- how to book an appointment, check the patient, and find free time; that Requires Appointment (from the Calendar page) lists the visits still to book; that a stage whose visit is required can't be finished until the appointment is marked Attended; and that a cancellation or change must be passed on to the patient: MedApp texts or emails them only if you've turned it on, the patient chose it, and the member of staff ticks "Tell the patient"; how to read an appointment's messages ("No reminder", sent, delivered, failed); how to record a patient's reminder choice; and how to work the Did not attend view and record a follow-up, knowing MedApp never discharges anyone because of a missed appointment;
- what to do when MedApp is unavailable;
- how to report a problem or a safety incident.

Admins also need template governance (including visit requirements), calendar templates, assignment, unlocking, and when it's right to finish a stage without its required visit.

## Downtime

Plan for MedApp being unavailable, whether for minutes or a day:
- **Before:** print the task lists and summaries of open pathways, and each day's appointments, at a set time each day. MedApp can't print a day list of appointments yet (`docs/todo.md`), so keep a copy another way. MedApp can't print them yet: the printable task list is a control the manufacturer needs before first live use (H-10), so don't go live without it.
- **During:** work from paper; record what was done, by whom and when. If only MedApp's files can't be opened (H-28), use the master copies in your own systems.
- **After:** enter the work into MedApp, saying in the notes that it was done during downtime and when; check for work that was done twice or missed.
- **Letters (planned, Phase 3):** once letters are built, keep a way to write and send an urgent letter outside MedApp (your own template and secure mailbox); afterwards, record it in MedApp as sent outside MedApp, with the date, so the To send list and the letter gates stay right.
- **Referrals:** keep a paper log of referrals received while MedApp is unavailable (patient, specialty, date received, referrer, priority asked for), with the letters; register them afterwards with the date they were received, not the date entered; urgent ones go straight to a clinician of the specialty.
- **Lab work (planned, Phase 3):** during downtime, keep a paper log of specimens taken and sent (with their numbers, if labels were printed), and enter them afterwards; reports that arrive meanwhile go to the responsible clinician on paper.
- **After a restore** from backup: the manufacturer will tell you the restore point. Re-enter work done since then from your downtime records.

## A starter hazard log for your deployment

Copy these into your own hazard log, rate them for your setting, and add your own.

| ID | Hazard | Example causes | Example controls |
|---|---|---|---|
| D-01 | A template doesn't match local practice | Copied from elsewhere; not reviewed | Clinical sign-off of each template; review after go-live |
| D-02 | Staff use MedApp without training | New starters, agency staff | Training before accounts are given; a quick reference card |
| D-03 | Work isn't assigned | No admin available; unclear who assigns | Named assigners, with cover; daily review of unassigned pathways |
| D-04 | Overdue or blocked work isn't reviewed | No owner | A named owner and schedule for the overdue and blocked lists |
| D-05 | Records split between MedApp and other systems | Some steps recorded only in one place | A clear local rule on what goes where |
| D-06 | Downtime procedure not followed | Untested; staff unaware | Test before go-live, and once a year |
| D-07 | Existing work moved in from spreadsheets or paper incorrectly | Manual re-entry at go-live | Checked by a second person; a cut-over date |
| D-08 | MedApp used outside its intended purpose | Used for time-critical or emergency work | Communicate the intended purpose; audit use |
| D-09 | Accounts shared, or leavers not removed | Busy wards; no leaver process | One account per person; a leaver checklist that reassigns the leaver's open pathways, then removes the account |
| D-10 | Unsuitable devices or browsers | Phones, shared or old devices | Supported devices and browsers listed; desktop use |
| D-11 | Attached documents taken or kept outside managed systems | Photos taken on personal phones; downloaded files left on shared computers | Only organisation devices for clinical photos; downloads cleared on shared computers |
| D-12 | Patients aren't told of bookings, changes or cancellations | No owner for patient contact; texts and emails off, or relied on for patients who didn't choose them | A named owner and process; a check that every change has been passed on; messages treated as a courtesy |
| D-13 | Staff absence isn't in MedApp, so appointments are booked when nobody is there | Leave and sickness recorded only elsewhere | Record absence in MedApp before booking; a daily check of the next day's calendars |
| D-14 | Visit requirements don't match local practice | Set without clinical review; left at none | Clinical sign-off of each stage's visit requirement |
| D-15 | A referral letter is never registered, or the inbox isn't read | Letters arriving by post, email and fax to different people; no owner for the inbox | One route for referral letters; a named owner and cover for the daily inbox check; a paper log during downtime |

## Reporting incidents

- **To the manufacturer:** [incident reporting address], marked "clinical safety". The manufacturer acknowledges within one working day, the same day if a patient was harmed (`clinical-risk-management-plan.md`, section 7).
- **In your organisation:** through your own incident system, and, in the NHS, Learn from Patient Safety Events.
- **Include:** what happened, when, which pathway and stage (by ID, not patient details, in the first message), what the screen showed, and what you've done.

## Sign-off

| Role | Name | Registration | Signature | Date |
|---|---|---|---|---|
| Manufacturer's Clinical Safety Officer | [Name] | [Professional body and number] | | |

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | First draft | AI coding agent, for review |
| 0.2 | 16 November 2026 | After the review of #60: a sign-off, My Worklist's default view, the leaver process (H-21) | AI coding agent, for review |
| 0.3 | 16 November 2026 | File attachments: the controls H-23 to H-28 transfer to you, an assumption, configuration, training, downtime and a starter hazard (D-11) | AI coding agent, for review |
| 0.4 | 16 November 2026 | The privacy features and NHS integrations (PRs #68 and #69): the optional NHS number in identifying patients, and the transferred control for H-22. Made in `main` without a new version; recorded here when staff calendars merged it | AI coding agent, for review |
| 0.6 | 18 November 2026 | The community specialist centre's Phases 3 to 5, from their design (`docs/centre-phases-3-5-plan.md`), before they're built: the controls planned to transfer to you for H-45 to H-64 (among them a daily check of the lab worklist and of results awaiting sign-off), and downtime for letters and lab work. The hazard count brought up to date (64) | AI coding agent, for review |
| 0.5 | 16 November 2026 | Staff calendars and appointments: setting up calendars and visit requirements, telling patients, the controls transferred for H-30 to H-36, training, the downtime day list, and starter hazards D-12 to D-14 (drafted as D-11 to D-13, renumbered after file attachments took D-11) | AI coding agent, for review |
| 0.7 | 18 November 2026 | Referrals (branch `feature/referrals`, Phase 2's PR 5): your duties for referrals among the assumptions (the register and triage permissions to the right people, a triager for every specialty, a daily inbox check, a downtime process for referrals, and later the clock's targets), the controls that transfer to you for H-40 to H-44, configuring referral permissions and the inbox's owner, downtime for referrals, and starter hazard D-15; and a warning not to use referrals with patients until triage is built | AI coding agent, for review |
| 0.8 | 18 November 2026 | Triage (branch `feature/triage`, Phase 2's PR 6): the triage permission is no longer "later"; act on the Specialties page's warning; identify the patient again when triaging; decide how the referrer is told of each outcome; the warning not to use referrals with patients now waits on the CSO's verification of the triage controls | AI coding agent, for review |
| 0.9 | 18 November 2026 | Patient reminders and the Did not attend view (branch `feature/patient-reminders`, Phase 4's PR 26): the assumptions now say MedApp texts and emails patients only if you turn it on, as a courtesy, and add following up missed appointments; the controls for H-53 to H-55 move from planned to the table of controls you provide, with what was built (a sender name that doesn't reveal the service, the stored copy, the Rebook and Clinician to review items, who may decide no further appointment); configuring texts and emails (your own Notify service, the settings, rooms with sites, never `Offline` with real patients); training; H-52's call list and D-12 updated | AI coding agent, for review |
