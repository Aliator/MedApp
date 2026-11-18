# MedApp: intended purpose and the medical device decision

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-04, intended purpose and qualification as a medical device |
| **Version** | 0.9, draft |
| **Date** | 18 November 2026 |
| **System** | MedApp, `main` at `88dc1fa` (the merge of #56), reassessed for branch `feature/nhs-integrations` (the NHS number, the patient banner, a read-only FHIR API, SNOMED CT codes on stage definitions and a demographics lookup), for admins' monitoring rules, and for staff calendars and appointments (`feature/staff-calendars`, designed in `docs/calendar-plan.md`); reassessed, from their design, for roles and permissions, specialties, referrals and triage, and the waiting-time clock (`docs/centre-phase2-plan.md`) |
| **Manufacturer** | [Manufacturer name] |
| **Drafted by** | An AI coding agent, for the manufacturer, from the code and `docs/architecture.md`. Not yet reviewed by a person. |
| **Decision owner** | [Manufacturer's accountable owner] |

This document sets out what MedApp is for, and whether that makes it a medical device. It is part of the clinical risk management file (`README.md`).

## Summary

- **The decision:** as it stands, and used for the intended purpose below, MedApp is **an administrative workflow and scheduling tool and not a medical device** under the UK Medical Devices Regulations 2002 (UK MDR 2002).
- **Why:** it records and coordinates the steps of a pathway that staff have chosen, and books staff time for patients' appointments. It doesn't diagnose, monitor, calculate, score, prioritise, triage or recommend anything from patient data. In the MHRA's words, it "just follows the path of a pathway/treatment - there are no decisions", and booking an appointment is unlikely to be a device "if it only has an administrative function".
- **Checked again for staff calendars and appointments** (version 0.6): the decision stands ([scheduling and the visit rule](#scheduling-and-the-visit-rule)).
- **Checked again for meetings, the team view and suggested meeting times** (version 0.7): administrative scheduling of staff time; the decision stands ([scheduling and the visit rule](#scheduling-and-the-visit-rule)).
- **It is still health IT that supports direct care.** Not being a device doesn't take it out of clinical risk management. DCB0129 applies to the manufacturer, and DCB0160 to each organisation that deploys it.
- **It can change.** The decision holds only while the product keeps to its intended purpose. The [conditions that would change it](#what-would-change-the-decision) are listed below, and any feature that meets one triggers a new assessment before it's merged.
- **Reassessed for branch `feature/nhs-integrations`:** the NHS number and its check, the patient banner, the read-only FHIR API, SNOMED CT codes on stage definitions and the demographics (PDS) lookup add no calculation, decision support or clinical logic, so **the decision is unchanged** ([below](#reassessment-the-nhs-number-fhir-snomed-ct-and-pds)).
- **Reassessed for referrals, triage, the waiting-time clock, specialties and roles** (version 0.9, from their design in `docs/centre-phase2-plan.md`, before they're built): a clinician makes every triage decision and MedApp records it; the clock is date arithmetic against the organisation's own targets; a breach is an administrative measure, shown and never sent. **The decision is unchanged**, on the conditions in [the reassessment](#reassessment-referrals-triage-waiting-times-specialties-and-roles).
- **Status:** provisional. The accountable owner signs it, and the Clinical Safety Officer (CSO) and, ideally, a regulatory adviser confirm it ([sign-off](#sign-off)).

## Intended purpose

The MHRA's guidance on intended purpose asks for four things: the product's structure and function (as a process with inputs and outputs), the intended population, the intended users and the use environment ([MHRA](https://www.gov.uk/government/publications/crafting-an-intended-purpose-in-the-context-of-software-as-a-medical-device-samd/crafting-an-intended-purpose-in-the-context-of-software-as-a-medical-device-samd)). Each is below.

### The statement

> MedApp is software for recording and coordinating the steps of pathways that a health or care organisation carries out for its patients, and for the administrative scheduling of its staff's time and its patients' appointments.
>
> Staff record a patient's identifying details, and start a pathway for that patient from a template of stages that the organisation's administrators have written. They work through the stages in a fixed order, record free-text notes at each stage, attach documents such as letters, results and images to the patient, the pathway or a stage, and mark each stage finished. Administrators assign pathways to staff, who see them on their task lists, with the target dates that staff have set and whether a pathway is past its target date. MedApp keeps an audit trail of every change to a pathway.
>
> Each member of staff has a calendar of working hours. Staff block off time on it for activities, and book appointments for patients with a member of staff at a free time they choose, and record whether the patient attended. Where the organisation's template says a stage involves a visit, MedApp asks for an appointment when that stage is reached, and, where the template says the visit is required, doesn't let the stage be finished until an appointment is recorded as attended, unless an administrator finishes it with a reason. MedApp keeps a history of every change to an appointment.
>
> Staff register the referrals the organisation receives: who referred the patient and when, the specialty and the priority the referrer asked for, the reason, and the referral letter. A clinician then records a triage decision on each: to accept it into a pathway the clinician chooses, at a priority the clinician chooses, or to reject or redirect it, with a reason. MedApp counts the days each referral has waited, less pauses staff record for the patient's own reasons, against the organisation's own target for its specialty and that priority, and shows when the target has passed. Administrators give staff roles, which decide what each person may do in MedApp.
>
> MedApp does not interpret clinical data. It calculates nothing from clinical data, and does not score, prioritise, triage, diagnose or recommend: triage decisions are made by clinicians, and MedApp records them. What a pathway involves, its stages, which stages involve a visit, its priority and its target date are all decided by the organisation's staff, using their own knowledge and the organisation's clinical systems. Whether, when and with whom a patient is seen, and how soon, are decided by staff; MedApp only books the time they choose and refuses times that clash. MedApp is not a clinical alerting or patient monitoring system, though it may remind staff about workflow dates and appointments they have set, and may tell administrators when staff take workflow actions they have asked to hear about. It is not the organisation's record of who the patient is, and it doesn't contact patients.

### Its parts

| Element | What it is for MedApp |
|---|---|
| **Function: inputs** | Patient details typed by staff (first and last name, date of birth, email, and optionally the NHS number), or filled from an NHS number by a demographics lookup when one is set up. Templates and stage definitions written by administrators (names, descriptions, instructions, optionally a SNOMED CT code with its display term, and whether a stage involves a visit: none, suggested or required). For each pathway: a template, a title, notes, a target date (a whole day) and a priority, all chosen by staff; then stage notes, and the actions start, finish, assign, unassign, block, cancel, complete, archive and unlock. Calendar setup by administrators: activity types, and day and week templates of blocked time. For each appointment: the patient (and pathway and stage), the member of staff, the length, the day and the time, and notes, all chosen by staff; then the actions book, reschedule, cancel (with a reason), and attended or did not attend. Files staff attach (PDF, images, Word documents, plain text). |
| **Function: processing** | It copies the template's stages onto the pathway; enforces the order of stages and the locks; moves the status as stages are started and finished; compares the target date with today's date; checks an NHS number's check digit and a SNOMED CT code's format; and records each change in the audit log. For calendars: it works out each day's blocks from the templates, lists the free slots in a day, and refuses an appointment or block that falls outside working hours, is in the past, or overlaps another. For appointments: it moves the status as staff act, adds a requested appointment when a stage that involves a visit becomes current, refuses to finish a stage whose visit is required until an appointment for it is attended (admins can override with a reason), cancels requested ones that are no longer needed, and records each change in the appointment's history. For attached files, it checks the type and size, and makes a small thumbnail of each photo by resizing it. None of this uses the content of clinical data: a file is stored and given back exactly as uploaded, and never read or interpreted. |
| **Function: outputs** | Lists and pages showing patients, pathways, stages, notes, attached files (names, thumbnails of photos, and the files themselves as downloads), who did what and when, status, target date and an "overdue" badge; task lists; staff calendars (day and week) with appointments and blocked time; the Requires Appointment list of requested appointments; in-app notifications about the workflow (when someone is assigned a pathway, which each person can turn off; when someone else books, moves or cancels your appointment; and, for administrators, when a staff action matches one of their monitoring rules; reminders about target dates staff set are planned); the audit log and the patient timeline; a patient banner on the pages about one patient, with the patient's name, date of birth and NHS number in the booking dialog too; and a read-only FHIR API that returns a patient's details unchanged. |
| **Where it sits in the care pathway** | Alongside the organisation's own clinical systems, as a shared checklist and task list for the administrative and clinical steps of a pathway, and a diary of staff time for the visits those steps need. It doesn't replace the electronic patient record, the patient administration system, clinical decision-making, or the organisation's own contact with patients about their appointments. |
| **Intended users** | Trained staff of health and care organisations: clinical and administrative staff (role `User`), and administrators (role `Admin`) who manage accounts, templates and assignments. From Phase 2 (`docs/centre-phase2-plan.md`), roles that administrators define (starting with Clinician, Nurse, Booking, Lab and Manager), each with only the permissions it needs; only a clinician with the triage permission, in the referral's specialty, records a triage decision. Not patients or the public. |
| **Intended population** | The organisation's patients, of any age. MedApp makes no decision about any patient, so it has no clinical population restriction. |
| **Use environment** | A desktop web browser on an organisation's network or a managed device, in clinics, hospitals and offices. Not intended for phones (it's desktop-first: `docs/architecture.md`, section 2.1), for emergency or time-critical care, or for tracking anything that must happen within a day at a set time (target dates are whole days). Appointments have times, but MedApp books them only within the working day (08:00 to 18:00 UK time, Monday to Friday), and reminds no one of them. |

### What it's not for

These go in the instructions for use and the contract, so the intended purpose can't be read more widely:
- **Not for decisions:** it must not be used to decide whether, when or how to treat a patient. Stage instructions are the organisation's own text, displayed as written.
- **Not the identity record:** patients must be identified at the point of care by the organisation's own positive identification process, not by MedApp alone. MedApp can hold a patient's NHS number, but doesn't trace it with the Personal Demographics Service (PDS), so it never confirms the number is the patient's.
- **Not for time-critical work:** a pathway becomes overdue only after the end of its due day, UK time, and any reminder about it is best effort.
- **Not a clinical alerting or monitoring system:** notifications are about the workflow (who is assigned, who did what, and dates staff set), never about clinical values. Today they're in-app only, best effort, and of three kinds: being assigned a pathway, someone else booking, moving or cancelling your appointment, and administrators' monitoring rules. Monitoring rules watch **staff actions** (a pathway created, a stage finished, a status chosen, an assignment, an unlock, a target date changed), optionally only for a staff-chosen priority, a template or a stage; they're supervision of work, not monitoring of patients, and must not be relied on to make sure anything clinical happens.
- **Not for medicines:** no doses, prescriptions or administration records.
- **Not for deciding who is seen, when or how soon:** appointments are booked at times staff choose. MedApp doesn't decide urgency, order patients by need, or tell patients about their appointments; the organisation contacts patients by its own process. Referrals are triaged by a clinician: MedApp records the decision, offers no outcome, template or priority, and prefills none. Waiting lists sort by days waited, a count from dates staff entered, and a breached target is the organisation's administrative target, not a measure of the patient's condition.

### Claims the manufacturer must not make

The MHRA judges intended purpose from labelling, instructions for use and promotional material, and says general disclaimers don't help if medical claims are made elsewhere ([MHRA guidance](https://assets.publishing.service.gov.uk/media/6abf6c8c943e890cb55e5e52/Medical_device_stand-alone_software_including_apps__including_IVDMDs.pdf), "Intended purpose"). So the website, sales material, framework listings (such as G-Cloud), the README and the app's own text must not say or imply that MedApp:
- improves clinical outcomes, prevents complications or makes care safer by itself;
- detects, flags or predicts clinical risk, deterioration or urgency;
- tells staff what to do next for a patient, or which patient to see first;
- decides which patients need a visit, how urgently, or when they should be seen;
- checks or calculates anything clinical;
- triages referrals, or tells staff which referrals are most urgent;
- shows, through a breached waiting-time target, that a patient's condition is urgent or getting worse.

They can say that it records and coordinates pathway workflow, keeps an audit trail, shows staff their tasks and target dates, and books staff time for appointments, asking for one where the organisation's template says a stage involves a visit.

## The assessment

### The law and the guidance

- **UK MDR 2002** (SI 2002/618, as it applies in Great Britain) defines a medical device as an article, including software, intended by its manufacturer to be used for human beings for a medical purpose: diagnosis, prevention, monitoring, treatment or alleviation of disease, injury or handicap; investigation, replacement or modification of the anatomy or of a physiological process; or control of conception.
- **The MHRA's guidance on stand-alone software** (version 1.10, last updated October 2026) gives a device determination flow chart and a medical purpose flow chart, and examples ([PDF](https://assets.publishing.service.gov.uk/media/6abf6c8c943e890cb55e5e52/Medical_device_stand-alone_software_including_apps__including_IVDMDs.pdf)). The parts that matter here:
  - Software that only "stores or transmits medical data without change" has no medical purpose. "An electronic patient record that simply replaces a patient's paper file does not meet the definition of a medical device", and software used to book an appointment is unlikely to be a device "if it only has an administrative function".
  - Decision support software is unlikely to be a device if "It just reproduces a paper document in digital format", "It just follows the path of a pathway/treatment - there are no decisions - may provide information", or it has decision points where "the health care professional decides which path to take".
  - It is most likely to be a device if "It is intended to influence the actual treatment - dose, size of implant, time of treatment etc." or "It results in a diagnosis or prognosis".
  - Software that "applies automated reasoning such as a simple calculation, an algorithm or a more complex series of calculations" (dose calculations, symptom tracking, clinical decision guides) is usually a device.
  - In complex systems, only the modules that are devices need to be marked, for example a module "intended to indicate the risk that a specific patient has of developing a disease".
- **Function creep:** the MHRA's intended purpose guidance warns that updates must keep the product "compatible and consistent with the original intended purpose", and that added functionality needs its own evidence and risk assessment.

### Through the MHRA's flow chart

| Step | Question | Answer for MedApp | Why |
|---|---|---|---|
| 1 | Is it a computer program or a functional document? | Yes | Stand-alone web software. |
| 2 | Is it part of a system, or does it work with a medical device? | No | No device integration, and no imports from devices or laboratory systems (`docs/architecture.md`, section 3.3). Its only interfaces are a read-only FHIR API that returns patients' details as stored, and a demographics lookup with only an offline stub; neither involves a medical device. |
| 3 | Does it have a medical purpose? | No | Every function is administrative or records data without change. See the table below. |
| 4 | Does it work with data obtained in vitro? | No | No laboratory data. |
| Result | | **Not a medical device** | |

### Function by function

| Function | Where in the code | Could it be a medical purpose? | Assessment |
|---|---|---|---|
| **Patient register** (name, date of birth, email; list, search, edit) | `MedApp.Domain/Patients/Patient.cs`, `MedApp.Application/Patients` | No | Administrative identification. Stores data without change. |
| **Templates and stage definitions** | `MedApp.Application/Pathways/PatientPathwayStages` | No | An ordered checklist written by the organisation. Reproduces a paper document in digital form. MedApp never chooses a template; staff do (`CreatePatientPathwayHandler`). |
| **Stages in a fixed order, locked when finished** | `PathwayWorkflow.WhyStageCantBeEdited`, `PathwayChange.FinishStage` | No | Follows the path of a pathway, with no decisions. The order is the template's, not derived from patient data. |
| **Stage and pathway notes** | `UpdatePathwayStageNotesHandler`, `AddPathwayNoteHandler`, `EditPathwayNoteHandler` | No | Free text, stored and displayed as typed (Markdown). No parsing or interpretation. |
| **Status workflow** (unassigned, assigned, in progress, in review, completed, blocked, cancelled, archived; on screen, in review is "Awaiting review", blocked is "On hold" and completed is "Discharged") | `PathwayWorkflow.AllowedStatusChanges`, `PathwayChange.SetStatus` | No | Follows which stages are finished and what staff choose. Not a clinical state: "Discharged" is the label staff give a pathway whose stages are all finished, and MedApp neither decides it nor acts on it. |
| **Target date and the overdue badge** | `DueDates`, `PathwayWorkflow.IsOverdue` | No, with care | A date set by staff compared with today. It's a reminder, like a calendar, not a red flag derived from clinical data. It would become borderline if MedApp set or changed target dates from clinical data (see below). |
| **Priority** (low, medium, high, urgent) | `PatientPathwayPriority`, `UpdatePatientPathwayHandler` | No, with care | A label chosen by staff. MedApp never sets it. Lists sort by target date unless the user picks another column (`PathwaySearch.Sort`). |
| **Assignment, task lists and notifications** | `AssignPatientPathwayHandler`, `MyTasksPage.tsx`, `NotificationSender` | No | Work allocation by administrators, and a message that it happened. |
| **Monitoring rules** (an admin is told when a staff member, or anyone, takes a workflow action, optionally only for a priority, template or stage) | `PathwayMonitor`, `MonitoringRuleMatcher`, `MonitoredActivity` | No | Administrative supervision. It matches who did which audited action against the admin's choices; the priority is the label staff chose, compared by its order only, and the template and stage by identity. It never reads notes or any clinical value, and decides nothing about a patient. |
| **Audit log** | `PathwayChange.Audit`, `PatientPathwayAuditEntry` | No | Record keeping. |
| **Attached files** (upload, list, download, delete; thumbnails of photos) | `MedApp.Application/Attachments`, `SkiaThumbnailGenerator` | No | Stores documents and images and gives them back unchanged, like a document management system. The thumbnail is the same picture made smaller, with no measurement, enhancement or analysis. It would become a device question if MedApp read, measured or classified what's in a file (see below). |
| **Search, sort, filters and saved searches** | `PathwaySearch`, `PatientSearch` | No | Simple search on fields staff entered, with user-chosen order. |
| **NHS number and its check** (branch `feature/nhs-integrations`) | `NhsNumbers`, `NhsNumberRules` | No | An identifier. The Modulus 11 check is a format check on the number itself, like a bank account's check digit: it says nothing about the patient's health. |
| **Patient banner** | `PatientBanner.tsx` | No | Shows who the patient is (name, date of birth and age, NHS number). Age is worked out from the date of birth for display, and drives nothing. |
| **Read-only FHIR API** | `FhirController`, `FhirPatientMapper` | No | Returns a patient's identifying details, as stored, in the UK Core Patient shape: stores and transmits data without change. No clinical data, and nothing is inferred. |
| **SNOMED CT codes on stage definitions** | `SnomedCt`, `SnomedRules` | No | A label an admin types for a stage, with its display term; only its format is checked. MedApp never chooses, interprets or acts on a code. |
| **Demographics lookup (PDS)** | `IPersonalDemographicsService`, `LookUpDemographicsHandler` | No | Fills a patient's name and date of birth from an NHS number, for staff to check and save. Administrative identification; only an offline stub exists. |
| **Staff calendars** (working hours, blocked time, day and week templates) | `MedApp.Application/Calendar/Common` (`WorkingHours`, `CalendarProjection`, `TemplateHistory`) | No | A diary of staff time. Blocks and templates are set by staff and administrators; MedApp only lays them out on days. |
| **Meetings, the team view and suggested meeting times** (personal templates too) | `MedApp.Application/Calendar/Common` (`Availability`, `MeetingStaff`, `MeetingNotifications`), `MedApp.Application/Calendar/Meetings` | No | Administrative scheduling of staff time among staff: an admin arranges a meeting; MedApp finds the times when everyone chosen is free (arithmetic on their calendars' times, earliest first) and refuses clashes. No patient is involved, and nothing is computed from clinical data. |
| **Appointments** (booking, rescheduling, cancelling, attended or did not attend; free slots; clash checks) | `MedApp.Application/Appointments/Common` (`AppointmentWorkflow`, `AppointmentChange`), `CalendarClashes` | No | Booking staff time for a patient at a time staff choose: "software used to book an appointment ... if it only has an administrative function". Finding free slots and refusing clashes is arithmetic on times, not on clinical data. |
| **Stage visits** (the requested appointment, the required-visit rule, the admin bypass, suggested visits) | `PathwayChange.RequestVisit`, `PathwayWorkflow.WhyStageNeedsVisit` | No, with care | Enforces the organisation's own template setting, like the order of stages: it follows the path of the pathway. MedApp doesn't decide whether a stage needs a visit. It would become a device question if the software decided that from clinical data (see below). |
| **Requires Appointment list** | `GET api/appointments/search`, the Requires Appointment page | No, with care | Lists requested appointments, soonest pathway target date first: dates staff set, like the default pathway sort. It would become a device question if it were ordered by anything clinical. |
| **Specialties** (designed: `docs/centre-phase2-plan.md`) | `Specialty`, `StaffSpecialty` | No | Organisational labels for templates, pathways and staff, and each specialty's waiting-time targets, set by administrators. |
| **Roles and permissions** (designed) | `Permission`, `RolePermissions` | No | Who may do what in the software: access control, not care. |
| **Referrals** (designed) | `Referral`, `RegisterReferral` | No | An administrative record of an incoming request, stored and shown as typed; the letter is a file, stored and given back unchanged, never read. Duplicates are found by patient and specialty only. |
| **Triage** (designed) | `AcceptReferral`, `RejectReferral`, `RedirectReferral` | No, with care | The clinician chooses the outcome, the template (from the specialty's, listed by name) and the priority (never prefilled); MedApp records who decided, when and why, and creates the pathway the clinician chose. It would become a device function if MedApp suggested an outcome, a template or a priority, or ordered the inbox by anything clinical. |
| **The waiting-time clock and breaches** (designed) | `WaitingClock` | No, with care | Arithmetic on dates: days since the referral arrived, less pauses staff record for patient-initiated reasons, against the organisation's target for the specialty and the priority a clinician chose. A breach is that count passing the target, shown like Overdue and never sent. It would become a device question if a target or priority were set from clinical data. |
| **Sorting by days waited, and the filter by stage reached** (designed) | `ReferralSearch`, `PathwaySearch` | No | Order and filters by dates staff entered and the stage staff reached, not clinical values. |

### Northern Ireland and the EU

In Northern Ireland, and in the EU, the EU Medical Device Regulation (2017/745) applies instead, with its software guidance (MDCG 2019-11). That guidance also treats software that only stores, archives, communicates or simply searches data as not a device. The outcome is expected to be the same for the same reasons, but it hasn't been assessed here. **Do it before selling into Northern Ireland or the EU.**

### Things that might be read as clinical, and why they aren't

- **"Overdue"** is shown as an error-coloured badge. It is still a comparison of two dates, one of them typed by staff, with no clinical input. The badge text says "Overdue", not anything about the patient's condition.
- **The default sort** puts the soonest target date first. That order comes from dates staff entered, not from a clinical judgement made by the software.
- **"Not assigned to you"** is a confirmation about who is doing the work, not about the patient.
- **Stage instructions** can contain clinical guidance, but it's the organisation's own text, shown unchanged. MedApp is the means of display, like a document management system. The organisation is responsible for the content (`dcb0160-deploying-organisations.md`).
- **"Needs a visit before it can be finished"** is the organisation's own template setting, enforced like the order of stages, not a judgement MedApp makes about the patient.
- **Did not attend** is a status staff record. MedApp doesn't act on it, beyond offering to book another appointment.

### Scheduling and the visit rule

Checked for staff calendars and appointments (`docs/calendar-plan.md`, section 5), version 0.6:
- **Booking staff time for a patient is administrative scheduling.** The MHRA's guidance says software used to book an appointment is unlikely to be a medical device "if it only has an administrative function". MedApp's booking is that: staff choose the patient, the member of staff, the length, the day and the free slot; MedApp records the choice, refuses a time outside working hours, in the past or overlapping something else, and shows the calendars. Nothing about the time comes from clinical data.
- **The visit rule only enforces the organisation's own setting.** An administrator marks a stage definition as involving a visit (none, suggested or required). MedApp then requests an appointment when the stage becomes current, and refuses to finish a required one without an attended appointment, unless an administrator finishes it with a reason. That is the same kind of rule as stages in a fixed order: it "just follows the path of a pathway/treatment - there are no decisions".
- **MedApp doesn't decide whether, when or with whom a patient is seen,** or how soon. It computes nothing from clinical data: not the need for a visit, its urgency, its time, or the order of the Requires Appointment list, which is by the pathway's target date, a date staff set.
- **So the decision stands.** The flow chart's answers don't change: no device integration, no in vitro data, and no medical purpose. MedApp remains outside the device definition.
- **Meetings, the team view and suggested times (version 0.7)** are administrative scheduling of staff time among staff. Suggested times are the earliest times everyone chosen is free, from their calendars: arithmetic on times, not a decision about care, and an admin chooses. A meeting can't be put over a booked appointment of anyone going to it (the clash check refuses it), so it never moves or hides a patient's visit. **No new hazard:** the one considered, a meeting arranged over a clinic so nobody sees its patients, can't happen through MedApp, because a meeting is refused wherever someone going to it has an appointment, and booking is refused over a meeting someone is going to; a later template change or day override never moves a meeting or an appointment either.
- **Replies to meetings (version 0.8)** are staff saying whether they'll go: still administrative scheduling. A meeting someone declined isn't busy time on their calendar, so a patient may be booked with them at that time, and a change to the meeting (or a move) doesn't check their calendar; but they aren't going to it, so it can't take them away from that patient. The rule above holds for everyone going: accepting again is refused while something else is at that time, and a move that keeps someone's decline (they're busy at the new time) leaves them not going. Nothing is calculated from clinical data, so the decision stands, and no hazard changes (`docs/calendar-plan.md`, decision 71).

## The decision

> **MedApp, at `main` `88dc1fa` with branch `feature/nhs-integrations`, admins' monitoring rules, and staff calendars and appointments, used for the intended purpose in this document, is not a medical device under UK MDR 2002.** It records and coordinates the administrative and clinical workflow steps of a pathway, and books staff time for patients' appointments as an administrative function, and doesn't interpret clinical data, prioritise patients or recommend treatment. It is health IT that supports direct care, so the manufacturer applies DCB0129 to it, and each deploying organisation applies DCB0160.

### Reassessment: the NHS number, FHIR, SNOMED CT and PDS

Branch `feature/nhs-integrations` (16 November 2026) adds five things from roadmap step 9. Each was checked against [the conditions below](#what-would-change-the-decision):
- **The NHS number and its validation:** the check digit is a format check, not a clinical one. A wrong number is a wrong-patient hazard (`hazard-log.md`, H-01), not a device function.
- **The patient banner:** displays identity only.
- **The FHIR API:** read-only, patients only, and the data goes out as stored. It moves data "without change" in the MHRA's terms, and MedApp doesn't transform or interpret it.
- **SNOMED CT codes:** labels typed by admins on stage definitions, copied onto pathways. The software never chooses a code from clinical data, and nothing acts on one.
- **The PDS lookup:** fills identifying details for staff to confirm, from an offline stub today.

**None adds a calculation, a score, decision support, prioritising, an alert from clinical values, or a medical claim, so the decision is unchanged.** The rows in [Roadmap features to watch](#roadmap-features-to-watch) for these features still apply to what comes next: codes chosen by the software, or the FHIR API transforming or interpreting data, would need a new assessment.

### Reassessment: referrals, triage, waiting times, specialties and roles

Phase 2 of the community specialist centre (`docs/centre-phase2-plan.md`, 18 November 2026) was assessed **from its design, before it's built**, so the build keeps to it. Each feature was checked against [the conditions below](#what-would-change-the-decision):
- **Specialties:** organisational labels for templates, pathways and staff, with targets set by administrators. No clinical content.
- **Roles and permissions:** access control. They decide who may use a function, never anything about a patient.
- **Referrals:** an administrative record of a request from another clinician, stored and shown as typed. The letter is a file, stored and returned unchanged; MedApp never reads it. The duplicate check compares the patient and the specialty, nothing clinical.
- **Triage:** the decision is the clinician's. MedApp offers no suggested outcome, template or priority, prefills none (the referrer's requested priority is shown beside an empty choice), lists templates by name, and orders the inbox by days waited. It records who decided, when and why, and creates the pathway the clinician chose. The MHRA's flow chart asks whether the software decides or supports a decision from patient data; here it only records one.
- **The waiting-time clock:** arithmetic on dates staff entered (when the referral arrived, pauses for the patient's own reasons) against the organisation's own target for the specialty and the priority a clinician chose. It's a count of days, like the overdue badge, and the organisation's targets are administrative, not national clinical rules.
- **Breaches:** shown on lists, as Overdue is, and never sent as an alert. A breach says a target passed, not that the patient is at risk, and the [claims](#claims-the-manufacturer-must-not-make) now forbid saying otherwise.
- **Sorting by days waited, and the filter by stage reached:** order and filters by dates and stages staff recorded.

**None adds a calculation on clinical data, a score, decision support, prioritising by clinical content, an alert from clinical values, or a medical claim, so the decision is unchanged.** It holds only while these stay true: no suggested triage outcome, template or priority; no prefilled priority; targets and priority never set from clinical data; breaches never escalated by clinical values. A change to any of these needs a new assessment before it merges. Hazards H-40 to H-44 (`hazard-log.md`) cover what could go wrong.

It is a decision about this product and this intended purpose. A customer who uses MedApp outside its intended purpose takes on that use; the MHRA notes that using a product for a medical purpose doesn't by itself make it a device, but the hazard log assumes the intended use.

## What would change the decision

### Features that make it likely to be a device

Any feature that does one of these needs a new assessment, and is likely to be a device, or a device module, in its own right:
- **Calculations on patient data:** doses, body mass index, fluid balance, early warning scores (such as NEWS2), risk scores (such as ASA grade calculated rather than recorded), or any clinical calculator.
- **Dose fields with checks:** recording a dose is record keeping; checking it against a range, a weight, or another medicine is decision support.
- **Clinical decision support:** suggesting the next step, the template to use, or whether to proceed, from what's been recorded about the patient.
- **Prioritising or triaging patients:** ordering, flagging or escalating patients by anything derived from their clinical data. The roadmap already warns that waiting-list tracking must keep priorities clinician-assigned (`docs/architecture.md`, "Features worth adding", item 5).
- **Red flags or alerts from clinical values:** for example, highlighting a result, a reading or a questionnaire answer as abnormal.
- **Target dates set by the software from clinical data:** for example, "due 6 weeks after the operation date" is still arithmetic on a date staff chose, and is borderline; "due sooner because the score is high" is a device function.
- **Interpreting free text or images:** summaries, extraction, classification or any AI that reads notes or attachments and says something about the patient.
- **Patient-facing questionnaires that are scored or that trigger actions.**
- **Scheduling from clinical data:** the software choosing an appointment's urgency, time, length or member of staff from clinical data; deciding from what's recorded about the patient whether a stage needs a visit; or ordering, flagging or escalating the Requires Appointment list or a calendar by clinical values (a "most urgent first" list worked out by the software). Ordering by dates and times staff set, and refusing clashes, stay administrative.
- **Acting on attendance clinically:** for example, a missed appointment that changes the patient's clinical priority or pathway by itself. Recording "did not attend" and offering another booking is administrative.
- **Integration that controls or takes data from a medical device.**
- **Claims:** any of the [claims the manufacturer must not make](#claims-the-manufacturer-must-not-make), in any material.

### Roadmap features to watch

From `docs/architecture.md`, step 9 and "Features worth adding":

| Feature | Stays outside the device definition if | Becomes a device question if |
|---|---|---|
| **Structured stage fields** (checkboxes, choices, dates, numbers, signatures) | They only record what staff enter, and show it back unchanged. | A field is validated against a clinical range, a value is calculated or scored, a value changes what happens next, or a value is flagged as abnormal. |
| **NHS number and PDS lookup** (the number and an offline lookup built on branch `feature/nhs-integrations`) | It identifies the patient. | Never, on its own. |
| **SNOMED CT and OPCS-4 codes on templates** (SNOMED CT codes on stage definitions built on branch `feature/nhs-integrations`) | They label the pathway. | Codes are chosen by the software from clinical data. |
| **Reporting and dashboards** | They count workflow (open work, time in stage, overdue work). | They predict or rank patients by clinical risk. |
| **Overdue, blocked and unlocked notifications, email and escalation** | They're about the workflow and dates staff set. | They're triggered by clinical values. |
| **Monitoring rules** (built) | They match staff actions and labels staff chose (priority, template, stage). | A rule can match on a clinical value (a structured stage field, a result), or the software sets the priority it matches on. |
| **Waiting-list tracking (RTT clocks, breaches)** (designed for Phase 2: `docs/centre-phase2-plan.md`) | Clocks count days from dates staff entered, against the organisation's own targets for a specialty and a clinician-chosen priority; breaches are shown, not sent. | The software sets or suggests clinical priority, chooses a target from clinical data, or orders a list by clinical values. |
| **Referral triage** (designed for Phase 2) | A clinician chooses the outcome, template and priority, and MedApp records them. | The software suggests an outcome, a template or a priority, scores or reads the referral letter or reason, or ranks the inbox by clinical content. |
| **Digital consent and documents** (a first version of attached files exists) | They store and display forms and files unchanged. | They assess capacity, risk or suitability, or analyse an image or a document's content. |
| **Patient questionnaires and a portal** | Answers are stored and shown to staff. | Answers are scored, triaged or trigger clinical advice. |
| **Calendar follow-ups: patient reminders, self-booking, overdue requested appointments** (`docs/todo.md`) | Reminders say when an appointment is; patients choose from free slots; requests waiting too long are flagged by how long they've waited. | Reminders give clinical advice; self-booking offers slots by clinical urgency; requests are flagged or ordered by clinical values. |
| **FHIR API and EPR integration** (a read-only Patient API built on branch `feature/nhs-integrations`) | Data moves without change. | MedApp transforms or interprets the data clinically. |

### The change-control rule

- **Who checks:** the author of any change that adds or changes a function in the lists above. The rule is in the merge flow of `AGENTS.md` now, so it applies before a CSO is appointed; once one is, it's also a question in each pull request's clinical safety section (`clinical-risk-management-plan.md`, section 8).
- **What happens:** the change isn't merged until this document is reassessed and the CSO and the accountable owner have signed the new version. If it is a device function, the options are to drop it, redesign it to stay outside the definition, or take that module through UK conformity assessment (UKCA marking) before release.
- **Wording too:** changes to the intended purpose, the instructions for use or the marketing text are reviewed against [the claims](#claims-the-manufacturer-must-not-make) in the same way.
- **Regulation changes:** the government plans to consult by spring 2027 on a new device definition, with possible carve-outs for administrative software ([GOV.UK](https://www.gov.uk/government/publications/government-response-to-the-national-commissions-recommendations-on-the-regulation-of-ai-in-healthcare/government-response-to-the-national-commissions-recommendations-on-the-regulation-of-ai-in-healthcare)). Reassess when it's published.

## Sign-off

| Role | Name | Registration (CSO) | Decision | Signature | Date |
|---|---|---|---|---|---|
| Accountable owner | [Name] | n/a | [Agree / disagree] | | |
| Clinical Safety Officer | [Name] | [Professional body and number] | [Agree / disagree] | | |
| Regulatory adviser (recommended) | [Name, organisation] | n/a | [Agree / disagree] | | |

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | First draft, for `main` at `88dc1fa` | AI coding agent, for review |
| 0.2 | 16 November 2026 | After the review of #60: the intended purpose allows workflow reminders about dates staff set, so planned overdue notifications don't contradict it; all eight statuses listed; the change-control rule is in `AGENTS.md` | AI coding agent, for review |
| 0.3 | 16 November 2026 | Reassessed for branch `feature/nhs-integrations` (the NHS number, the patient banner, a read-only FHIR API, SNOMED CT codes, a demographics lookup): no calculation, decision support or clinical logic, so the decision is unchanged; the intended purpose's parts and the function table updated | AI coding agent, for review |
| 0.4 | 16 November 2026 | Admins' monitoring rules (notifications of staff actions) and each person's switch for assignment notifications: assessed as administrative supervision, not a device function; the intended purpose and outputs say so | AI coding agent, for review |
| 0.5 | 16 November 2026 | File attachments: storing documents added to the statement, its inputs, processing and outputs, and the function table. The decision doesn't change: files are stored and returned unchanged, never interpreted | AI coding agent, for review |
| 0.6 | 16 November 2026 | Staff calendars and appointments (`docs/calendar-plan.md`): the intended purpose now includes administrative scheduling of staff time and appointments; the parts and function tables updated; the device decision checked for booking and the visit rule, and it stands; scheduling-specific conditions added to what would change it (drafted as this branch's 0.3, renumbered on merging `main`'s 0.3 to 0.5) | AI coding agent, for review |
| 0.7 | 16 November 2026 | Meetings, the team view, suggested meeting times and personal calendar templates (`docs/calendar-plan.md`, decisions 50 on): administrative scheduling of staff time; the function table and the scheduling section updated; no new hazard (why, in that section); the decision stands | AI coding agent, for review |
| 0.8 | 18 November 2026 | Replies to meetings (`docs/calendar-plan.md`, decision 71): a declined meeting isn't busy time for the person who declined, so a booking may go into it; the scheduling section says why that hides no visit (they aren't going); no new hazard, and the decision stands (review of #117, finding 8) | AI coding agent, for review |
| 0.9 | 18 November 2026 | Referrals and triage, the waiting-time clock and breaches, specialties, roles and permissions, assessed from their design (`docs/centre-phase2-plan.md`), before they're built: the statement, its users, what it's not for, the claims, the function table and the roadmap rows updated; the decision is unchanged, on the conditions in the new reassessment; hazards H-40 to H-44 drafted in `hazard-log.md` | AI coding agent, for review |
