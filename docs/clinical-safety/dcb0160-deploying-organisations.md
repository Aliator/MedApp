# MedApp: clinical safety for deploying organisations (DCB0160)

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment, nor DCB0160 guidance yet. The manufacturer's CSO must sign it off before it is given to any deploying organisation.**

| | |
|---|---|
| **Document** | CS-05, guidance for organisations that deploy MedApp |
| **Version** | 0.5, draft |
| **Date** | 16 November 2026 |
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
| The hazard log | Draft, 36 hazards, of which H-30 to H-36 (staff calendars and appointments) are drafts still to be rated by the CSO: `hazard-log.md` |
| A clinical safety case report for each release | Draft template: `clinical-safety-case-report.md`. **No signed report yet.** |
| This guide, with a starter hazard log for your deployment | Draft: this document |
| A named CSO, and a route for reporting safety incidents | **Not yet:** [CSO name], [incident reporting address] |
| Release notes that say what changed in clinical workflow, and its safety impact | **Planned** (the release gate in the plan, section 8) |
| Instructions for use, and training material | **Planned** |
| A printable task list and procedure summary, for downtime | **Planned** (roadmap step 6) |
| Service levels, monitoring, backups and restore tests, for the hosted service | **Planned** (roadmap step 5) |
| Notice of known issues and field safety information when something goes wrong | **Planned**, through the incident process (plan, section 7) |

## The manufacturer's assumptions about you

The manufacturer's safety case holds only if you:
- **identify patients** at the point of care by your own positive identification process. MedApp has names, dates of birth, email and an optional NHS number, whose check digit it checks but which it doesn't trace with PDS, so check a number against your own records (for example your PAS) when you enter it;
- **govern templates:** clinical sign-off of every template and stage definition before use, and when one changes, including which stages involve a visit and whether it's suggested or required;
- **set up calendars:** activity types, day and week templates and each person's default week, and record staff absence in MedApp;
- **book and follow up visits:** someone reviews the Requires Appointment list and Did not attend appointments on a set schedule;
- **tell patients about their appointments,** and about every cancellation and change, by your own process. MedApp doesn't contact patients;
- **train staff,** including what finishing a stage means, what a due date means, that notifications aren't to be relied on, and that My Tasks opens on work not yet started;
- **have at least two admins** who are available while staff work;
- **reassign a leaver's open procedures** before removing their account;
- **review overdue, unassigned and blocked procedures** on a set schedule;
- **have a downtime procedure;**
- **keep the clinical record** of what was done in your clinical systems; MedApp coordinates the steps;
- **keep the master copy of every document** attached in MedApp (results, letters, images) in your own systems, check a document's own identifiers against the patient before acting on it, and protect every computer that opens attached files with up-to-date anti-malware and software;
- **use MedApp within its intended purpose:** not for emergency or time-critical care, not for decisions, not for medicines.

If one of these doesn't hold for you, tell the manufacturer: it changes the hazard ratings.

## Controls the manufacturer's hazard log transfers to you

| Hazard | What you provide |
|---|---|
| H-01 The wrong patient's procedure is worked | Positive patient identification at the point of care; train staff to check the patient's details on the procedure page |
| H-02 Work is recorded on the wrong record | A local rule: correct a wrong entry with a note saying so, never by deleting it |
| H-04 An overdue procedure is missed | Who reviews the overdue and unassigned lists, and how often; at least two admins |
| H-05 A blocked procedure is forgotten | A review of blocked procedures, with who and how often |
| H-06 A stage is finished in error | Admins available to unlock stages while staff work |
| H-07 A stage is recorded as finished when the work wasn't done | A policy that finishing a stage states its work was done; the clinical record of the work in your clinical systems |
| H-09 A member of staff doesn't learn of work assigned to them | Tell staff to work from My Tasks with its filter set to all open work (it opens on "Not started"), not from notifications |
| H-10 MedApp is unavailable | A downtime procedure |
| H-13 A template or stage definition has wrong content | Clinical sign-off of templates and definitions |
| H-14 A correction to a template doesn't reach procedures already under way | When a template is corrected for safety, find its open procedures and review each |
| H-17 Stage instructions aren't read | Write anything essential as its own stage, not only in instructions |
| H-18 A change is recorded under the wrong person's name | No shared accounts; lock screens when away |
| H-21 Work assigned to a deleted account drops off every task list | A leaver process: reassign a leaver's open procedures, then remove the account |
| H-22 Care under way is delayed because the patient's record is restricted | Before restricting a record, the admin checks with the clinician responsible and agrees how care continues; block or cancel procedures with a reason meanwhile |
| H-23 A file is attached to the wrong patient, procedure or stage | Positive patient identification; train staff to check the patient before attaching a file, to check a document's own identifiers before acting on it, and to delete and report a file attached in error |
| H-24 A document or result is missing, and nobody knows | Master copies of documents in your own systems; train staff to check a file is listed after attaching it, and to look in the source system before concluding a result doesn't exist |
| H-25 An attached file carries malware | Up-to-date anti-malware, PDF readers and Office, with macros off, on every computer that opens attached files; tell staff what "Not scanned" means and how to report a suspicious file |
| H-26 A file is deleted by mistake | A local rule: delete a file only if it was attached in error |
| H-27 An out-of-date or duplicate document is relied on | A local rule for naming files (what it is and the document's date), and deleting superseded copies attached in error |
| H-28 File storage is unavailable | Your downtime procedure says where to find documents when MedApp's files can't be opened |
| H-30 A visit the patient needs doesn't happen | Who books from the Requires Appointment list, how often it's reviewed, and who follows up Did not attend |
| H-31 A member of staff is double-booked | MedApp's calendar is the only diary for the appointments it holds |
| H-32 A stage is finished without the visit it needed | Clinical sign-off of which stages need a visit; a review of stages finished without one; admins bypass a required visit only with a recorded reason |
| H-34 The wrong patient or procedure is booked | Positive patient identification; check the patient's name, date of birth and NHS number, which the booking dialog shows, when booking |
| H-35 An appointment is left without its member of staff | Record leave and training in MedApp before booking around it; when someone is away unexpectedly, move or cancel their appointments and tell the patients |
| H-36 A cancelled or moved appointment isn't passed on to the patient | A process for telling patients about every booking, change and cancellation, including the Booked appointments of a cancelled procedure |

## Configuring MedApp safely

- **Templates and stage definitions** are your clinical content:
  - Have each one written or checked by a clinician, and signed off under your clinical governance, before it's used.
  - Keep a record of who approved each version: MedApp doesn't keep template history yet (H-13).
  - Put each step that must not be missed in its own stage, in the right order. MedApp enforces the order you give it.
  - Archive a template rather than editing it heavily; create a new one for a new way of working.
- **Roles:** admins can manage users, templates, assignments and the audit log. Give the role to few people, but at least two. Remove it when someone changes job.
- **Accounts:** one account per person; no shared accounts. Reassign a leaver's open procedures, then remove the account. Deleting an account doesn't reassign its work, which then shows as assigned to "Deleted user" and is on nobody's task list (H-21).
- **Due dates:** decide locally what a due date means for each kind of procedure, and tell staff. In MedApp, a procedure is due by the end of its day, UK time.
- **Attached files:** decide what may be attached (for example referral letters, results, consent forms, wound photos), where the master copy lives, and how files are named. Files aren't scanned for malware yet: each shows "Not scanned" (H-25). Any member of staff can open any patient's files, and each download is recorded.
- **Visits:** for each stage definition, decide clinically whether it involves a visit, and whether the visit is **suggested** (finishing without one asks for confirmation) or **required** (it can't be finished until an appointment for it is Attended, except by an admin with a reason). Existing definitions start as none. A change reaches only procedures created after it.
- **Calendars:**
  - The working day is fixed at 08:00 to 18:00 UK time, Monday to Friday, and MedApp doesn't know bank holidays. Block off closed days and shorter hours by hand.
  - Set up the activity types (names should give no private reason: "Annual leave", not why) and the day and week templates, and give each member of staff a default week.
  - A change to a template applies from today, and can land on appointments already booked; MedApp shows both but moves nothing, so check each calendar it affects.
  - MedApp doesn't contact patients. Decide how patients are told of bookings, changes and cancellations, and who does it.

## Training

Before anyone uses MedApp with patients, they need to know:
- how to find the right patient and check the patient's details on every procedure;
- that stages are worked in order, and that **finishing a stage locks it and says the work is done;**
- how to ask an admin to unlock a stage, and how to correct a wrong entry;
- what "overdue" means, and that, today, nothing alerts them when work becomes overdue;
- that blocked procedures don't show as overdue, and need reviewing;
- that notifications are a convenience, and My Tasks is the list to work from, with its filter set to all open work: it opens on "Not started", which hides work already under way, including overdue and blocked work;
- how to attach a file to the right patient, procedure or stage, and what to do if it goes to the wrong one (H-23);
- that files are only downloaded, never shown in the page, what "Not scanned" means, and to delete downloaded copies when finished;
- how to book an appointment, check the patient, and find free time; that Requires Appointment (from the Calendar page) lists the visits still to book; that a stage whose visit is required can't be finished until the appointment is marked Attended; and that MedApp doesn't tell patients anything, so a cancellation or change must be passed on;
- what to do when MedApp is unavailable;
- how to report a problem or a safety incident.

Admins also need template governance (including visit requirements), calendar templates, assignment, unlocking, and when it's right to finish a stage without its required visit.

## Downtime

Plan for MedApp being unavailable, whether for minutes or a day:
- **Before:** print the task lists and summaries of open procedures, and each day's appointments, at a set time each day. MedApp can't print a day list of appointments yet (`docs/todo.md`), so keep a copy another way. MedApp can't print them yet: the printable task list is a control the manufacturer needs before first live use (H-10), so don't go live without it.
- **During:** work from paper; record what was done, by whom and when. If only MedApp's files can't be opened (H-28), use the master copies in your own systems.
- **After:** enter the work into MedApp, saying in the notes that it was done during downtime and when; check for work that was done twice or missed.
- **After a restore** from backup: the manufacturer will tell you the restore point. Re-enter work done since then from your downtime records.

## A starter hazard log for your deployment

Copy these into your own hazard log, rate them for your setting, and add your own.

| ID | Hazard | Example causes | Example controls |
|---|---|---|---|
| D-01 | A template doesn't match local practice | Copied from elsewhere; not reviewed | Clinical sign-off of each template; review after go-live |
| D-02 | Staff use MedApp without training | New starters, agency staff | Training before accounts are given; a quick reference card |
| D-03 | Work isn't assigned | No admin available; unclear who assigns | Named assigners, with cover; daily review of unassigned procedures |
| D-04 | Overdue or blocked work isn't reviewed | No owner | A named owner and schedule for the overdue and blocked lists |
| D-05 | Records split between MedApp and other systems | Some steps recorded only in one place | A clear local rule on what goes where |
| D-06 | Downtime procedure not followed | Untested; staff unaware | Test before go-live, and once a year |
| D-07 | Existing work moved in from spreadsheets or paper incorrectly | Manual re-entry at go-live | Checked by a second person; a cut-over date |
| D-08 | MedApp used outside its intended purpose | Used for time-critical or emergency work | Communicate the intended purpose; audit use |
| D-09 | Accounts shared, or leavers not removed | Busy wards; no leaver process | One account per person; a leaver checklist that reassigns the leaver's open procedures, then removes the account |
| D-10 | Unsuitable devices or browsers | Phones, shared or old devices | Supported devices and browsers listed; desktop use |
| D-11 | Attached documents taken or kept outside managed systems | Photos taken on personal phones; downloaded files left on shared computers | Only organisation devices for clinical photos; downloads cleared on shared computers |
| D-12 | Patients aren't told of bookings, changes or cancellations | No owner for patient contact; MedApp sends nothing | A named owner and process; a check that every change has been passed on |
| D-13 | Staff absence isn't in MedApp, so appointments are booked when nobody is there | Leave and sickness recorded only elsewhere | Record absence in MedApp before booking; a daily check of the next day's calendars |
| D-14 | Visit requirements don't match local practice | Set without clinical review; left at none | Clinical sign-off of each stage's visit requirement |

## Reporting incidents

- **To the manufacturer:** [incident reporting address], marked "clinical safety". The manufacturer acknowledges within one working day, the same day if a patient was harmed (`clinical-risk-management-plan.md`, section 7).
- **In your organisation:** through your own incident system, and, in the NHS, Learn from Patient Safety Events.
- **Include:** what happened, when, which procedure and stage (by ID, not patient details, in the first message), what the screen showed, and what you've done.

## Sign-off

| Role | Name | Registration | Signature | Date |
|---|---|---|---|---|
| Manufacturer's Clinical Safety Officer | [Name] | [Professional body and number] | | |

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | First draft | AI coding agent, for review |
| 0.2 | 16 November 2026 | After the review of #60: a sign-off, My Tasks' default view, the leaver process (H-21) | AI coding agent, for review |
| 0.3 | 16 November 2026 | File attachments: the controls H-23 to H-28 transfer to you, an assumption, configuration, training, downtime and a starter hazard (D-11) | AI coding agent, for review |
| 0.4 | 16 November 2026 | The privacy features and NHS integrations (PRs #68 and #69): the optional NHS number in identifying patients, and the transferred control for H-22. Made in `main` without a new version; recorded here when staff calendars merged it | AI coding agent, for review |
| 0.5 | 16 November 2026 | Staff calendars and appointments: setting up calendars and visit requirements, telling patients, the controls transferred for H-30 to H-36, training, the downtime day list, and starter hazards D-12 to D-14 (drafted as D-11 to D-13, renumbered after file attachments took D-11) | AI coding agent, for review |
