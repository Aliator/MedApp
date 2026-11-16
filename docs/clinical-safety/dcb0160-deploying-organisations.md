# MedApp: clinical safety for deploying organisations (DCB0160)

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-05, guidance for organisations that deploy MedApp |
| **Version** | 0.1, draft |
| **Date** | 16 November 2026 |
| **System** | MedApp, `main` at `88dc1fa` |
| **Manufacturer** | [Manufacturer name] |
| **Drafted by** | An AI coding agent, for the manufacturer. Not yet reviewed by a person. |

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
5. **Configure MedApp safely:** templates, roles and accounts ([below](#configuring-medapp-safely)).
6. **Train staff** before they use it ([below](#training)).
7. **Write a downtime procedure,** and test it before go-live ([below](#downtime)).
8. **Write a clinical safety case report** for the deployment, and have your CSO sign it off before go-live.
9. **After go-live:** report incidents to the manufacturer as well as through your own system, review the safety case when MedApp or your use of it changes, and read each release's safety notes.

## What the manufacturer gives you

| Item | State |
|---|---|
| The intended purpose and the medical device decision | Draft: `intended-purpose-and-device-decision.md` |
| The clinical risk management plan | Draft: `clinical-risk-management-plan.md` |
| The hazard log | Draft, 20 hazards: `hazard-log.md` |
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
- **identify patients** at the point of care by your own positive identification process. MedApp has names, dates of birth and email, and no NHS number yet;
- **govern templates:** clinical sign-off of every template and stage definition before use, and when one changes;
- **train staff,** including what finishing a stage means, what a due date means, and that notifications aren't to be relied on;
- **have at least two admins** who are available while staff work;
- **review overdue, unassigned and blocked procedures** on a set schedule;
- **have a downtime procedure;**
- **keep the clinical record** of what was done in your clinical systems; MedApp coordinates the steps;
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
| H-09 An assignment notification isn't received | Tell staff to work from My Tasks, not from notifications |
| H-10 MedApp is unavailable | A downtime procedure |
| H-13 A template or stage definition has wrong content | Clinical sign-off of templates and definitions |
| H-14 A correction to a template doesn't reach procedures already under way | When a template is corrected for safety, find its open procedures and review each |
| H-17 Stage instructions aren't read | Write anything essential as its own stage, not only in instructions |
| H-18 A change is recorded under the wrong person's name | No shared accounts; lock screens when away |

## Configuring MedApp safely

- **Templates and stage definitions** are your clinical content:
  - Have each one written or checked by a clinician, and signed off under your clinical governance, before it's used.
  - Keep a record of who approved each version: MedApp doesn't keep template history yet (H-13).
  - Put each step that must not be missed in its own stage, in the right order. MedApp enforces the order you give it.
  - Archive a template rather than editing it heavily; create a new one for a new way of working.
- **Roles:** admins can manage users, templates, assignments and the audit log. Give the role to few people, but at least two. Remove it when someone changes job.
- **Accounts:** one account per person; no shared accounts. Remove leavers on their last day.
- **Due dates:** decide locally what a due date means for each kind of procedure, and tell staff. In MedApp, a procedure is due by the end of its day, UK time.

## Training

Before anyone uses MedApp with patients, they need to know:
- how to find the right patient and check the patient's details on every procedure;
- that stages are worked in order, and that **finishing a stage locks it and says the work is done;**
- how to ask an admin to unlock a stage, and how to correct a wrong entry;
- what "overdue" means, and that nothing alerts them when work becomes overdue;
- that blocked procedures don't show as overdue, and need reviewing;
- that notifications are a convenience, and My Tasks is the list to work from;
- what to do when MedApp is unavailable;
- how to report a problem or a safety incident.

Admins also need template governance, assignment and unlocking.

## Downtime

Plan for MedApp being unavailable, whether for minutes or a day:
- **Before:** print the task lists and summaries of open procedures at a set time each day. MedApp can't print them yet: the printable task list is a control the manufacturer needs before first live use (H-10), so don't go live without it.
- **During:** work from paper; record what was done, by whom and when.
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
| D-09 | Accounts shared, or leavers not removed | Busy wards; no leaver process | One account per person; leaver checklist |
| D-10 | Unsuitable devices or browsers | Phones, shared or old devices | Supported devices and browsers listed; desktop use |

## Reporting incidents

- **To the manufacturer:** [incident reporting address], marked "clinical safety". The manufacturer acknowledges within one working day, the same day if a patient was harmed (`clinical-risk-management-plan.md`, section 7).
- **In your organisation:** through your own incident system, and, in the NHS, Learn from Patient Safety Events.
- **Include:** what happened, when, which procedure and stage (by ID, not patient details, in the first message), what the screen showed, and what you've done.

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | First draft | AI coding agent, for review |
