# MedApp: intended purpose and the medical device decision

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-04, intended purpose and qualification as a medical device |
| **Version** | 0.3, draft |
| **Date** | 16 November 2026 |
| **System** | MedApp, `main` at `88dc1fa` (the merge of #56) |
| **Manufacturer** | [Manufacturer name] |
| **Drafted by** | An AI coding agent, for the manufacturer, from the code and `docs/architecture.md`. Not yet reviewed by a person. |
| **Decision owner** | [Manufacturer's accountable owner] |

This document sets out what MedApp is for, and whether that makes it a medical device. It is part of the clinical risk management file (`README.md`).

## Summary

- **The decision:** as it stands, and used for the intended purpose below, MedApp is **an administrative workflow tool and not a medical device** under the UK Medical Devices Regulations 2002 (UK MDR 2002).
- **Why:** it records and coordinates the steps of a procedure that staff have chosen. It doesn't diagnose, monitor, calculate, score, prioritise, triage or recommend anything from patient data. In the MHRA's words, it "just follows the path of a procedure/treatment - there are no decisions".
- **It is still health IT that supports direct care.** Not being a device doesn't take it out of clinical risk management. DCB0129 applies to the manufacturer, and DCB0160 to each organisation that deploys it.
- **It can change.** The decision holds only while the product keeps to its intended purpose. The [conditions that would change it](#what-would-change-the-decision) are listed below, and any feature that meets one triggers a new assessment before it's merged.
- **Status:** provisional. The accountable owner signs it, and the Clinical Safety Officer (CSO) and, ideally, a regulatory adviser confirm it ([sign-off](#sign-off)).

## Intended purpose

The MHRA's guidance on intended purpose asks for four things: the product's structure and function (as a process with inputs and outputs), the intended population, the intended users and the use environment ([MHRA](https://www.gov.uk/government/publications/crafting-an-intended-purpose-in-the-context-of-software-as-a-medical-device-samd/crafting-an-intended-purpose-in-the-context-of-software-as-a-medical-device-samd)). Each is below.

### The statement

> MedApp is software for recording and coordinating the steps of procedures that a health or care organisation carries out for its patients.
>
> Staff record a patient's identifying details, and start a procedure for that patient from a template of stages that the organisation's administrators have written. They work through the stages in a fixed order, record free-text notes at each stage, and mark each stage finished. Administrators assign procedures to staff, who see them on their task lists, with the due dates that staff have set and whether a procedure is past its due date. MedApp keeps an audit trail of every change to a procedure.
>
> MedApp does not interpret clinical data. It does not calculate, score, prioritise, triage, diagnose or recommend. What a procedure involves, its stages, its priority and its due date are all decided by the organisation's staff, using their own knowledge and the organisation's clinical systems. MedApp is not a scheduling, clinical alerting or patient monitoring system, though it may remind staff about workflow dates they have set, and may tell administrators when staff take workflow actions they have asked to hear about. It is not the organisation's record of who the patient is.

### Its parts

| Element | What it is for MedApp |
|---|---|
| **Function: inputs** | Patient details typed by staff (first and last name, date of birth, email). Templates and stage definitions written by administrators (names, descriptions, instructions). For each procedure: a template, a title, notes, a due date (a whole day) and a priority, all chosen by staff; then stage notes, and the actions start, finish, assign, unassign, block, cancel, complete, archive and unlock. |
| **Function: processing** | It copies the template's stages onto the procedure; enforces the order of stages and the locks; moves the status as stages are started and finished; compares the due date with today's date; and records each change in the audit log. None of this uses the content of clinical data. |
| **Function: outputs** | Lists and pages showing patients, procedures, stages, notes, who did what and when, status, due date and an "overdue" badge; task lists; in-app notifications about the workflow (when someone is assigned a procedure, which each person can turn off; and, for administrators, when a staff action matches one of their monitoring rules; reminders about due dates staff set are planned); the audit log. |
| **Where it sits in the care pathway** | Alongside the organisation's own clinical systems, as a shared checklist and task list for the administrative and clinical steps of a procedure. It doesn't replace the electronic patient record, the patient administration system, scheduling or clinical decision-making. |
| **Intended users** | Trained staff of health and care organisations: clinical and administrative staff (role `User`), and administrators (role `Admin`) who manage accounts, templates and assignments. Not patients or the public. |
| **Intended population** | The organisation's patients, of any age. MedApp makes no decision about any patient, so it has no clinical population restriction. |
| **Use environment** | A desktop web browser on an organisation's network or a managed device, in clinics, hospitals and offices. Not intended for phones (it's desktop-first: `docs/architecture.md`, section 2.1), for emergency or time-critical care, or for tracking anything that must happen within a day at a set time (due dates are whole days). |

### What it's not for

These go in the instructions for use and the contract, so the intended purpose can't be read more widely:
- **Not for decisions:** it must not be used to decide whether, when or how to treat a patient. Stage instructions are the organisation's own text, displayed as written.
- **Not the identity record:** patients must be identified at the point of care by the organisation's own positive identification process, not by MedApp alone. MedApp has no NHS number yet.
- **Not for time-critical work:** a procedure becomes overdue only after the end of its due day, UK time, and any reminder about it is best effort.
- **Not a clinical alerting or monitoring system:** notifications are about the workflow (who is assigned, who did what, and dates staff set), never about clinical values. Today they're in-app only, best effort, and of two kinds: being assigned a procedure, and administrators' monitoring rules. Monitoring rules watch **staff actions** (a procedure created, a stage finished, a status chosen, an assignment, an unlock, a due date changed), optionally only for a staff-chosen priority, a template or a stage; they're supervision of work, not monitoring of patients, and must not be relied on to make sure anything clinical happens.
- **Not for medicines:** no doses, prescriptions or administration records.

### Claims the manufacturer must not make

The MHRA judges intended purpose from labelling, instructions for use and promotional material, and says general disclaimers don't help if medical claims are made elsewhere ([MHRA guidance](https://assets.publishing.service.gov.uk/media/6abf6c8c943e890cb55e5e52/Medical_device_stand-alone_software_including_apps__including_IVDMDs.pdf), "Intended purpose"). So the website, sales material, framework listings (such as G-Cloud), the README and the app's own text must not say or imply that MedApp:
- improves clinical outcomes, prevents complications or makes care safer by itself;
- detects, flags or predicts clinical risk, deterioration or urgency;
- tells staff what to do next for a patient, or which patient to see first;
- checks or calculates anything clinical.

They can say that it records and coordinates procedure workflow, keeps an audit trail, and shows staff their tasks and due dates.

## The assessment

### The law and the guidance

- **UK MDR 2002** (SI 2002/618, as it applies in Great Britain) defines a medical device as an article, including software, intended by its manufacturer to be used for human beings for a medical purpose: diagnosis, prevention, monitoring, treatment or alleviation of disease, injury or handicap; investigation, replacement or modification of the anatomy or of a physiological process; or control of conception.
- **The MHRA's guidance on stand-alone software** (version 1.10, last updated October 2026) gives a device determination flow chart and a medical purpose flow chart, and examples ([PDF](https://assets.publishing.service.gov.uk/media/6abf6c8c943e890cb55e5e52/Medical_device_stand-alone_software_including_apps__including_IVDMDs.pdf)). The parts that matter here:
  - Software that only "stores or transmits medical data without change" has no medical purpose. "An electronic patient record that simply replaces a patient's paper file does not meet the definition of a medical device", and software used to book an appointment is unlikely to be a device "if it only has an administrative function".
  - Decision support software is unlikely to be a device if "It just reproduces a paper document in digital format", "It just follows the path of a procedure/treatment - there are no decisions - may provide information", or it has decision points where "the health care professional decides which path to take".
  - It is most likely to be a device if "It is intended to influence the actual treatment - dose, size of implant, time of treatment etc." or "It results in a diagnosis or prognosis".
  - Software that "applies automated reasoning such as a simple calculation, an algorithm or a more complex series of calculations" (dose calculations, symptom tracking, clinical decision guides) is usually a device.
  - In complex systems, only the modules that are devices need to be marked, for example a module "intended to indicate the risk that a specific patient has of developing a disease".
- **Function creep:** the MHRA's intended purpose guidance warns that updates must keep the product "compatible and consistent with the original intended purpose", and that added functionality needs its own evidence and risk assessment.

### Through the MHRA's flow chart

| Step | Question | Answer for MedApp | Why |
|---|---|---|---|
| 1 | Is it a computer program or a functional document? | Yes | Stand-alone web software. |
| 2 | Is it part of a system, or does it work with a medical device? | No | No device integration, no imports from devices or laboratory systems, no integrations at all (`docs/architecture.md`, section 3.3). |
| 3 | Does it have a medical purpose? | No | Every function is administrative or records data without change. See the table below. |
| 4 | Does it work with data obtained in vitro? | No | No laboratory data. |
| Result | | **Not a medical device** | |

### Function by function

| Function | Where in the code | Could it be a medical purpose? | Assessment |
|---|---|---|---|
| **Patient register** (name, date of birth, email; list, search, edit) | `MedApp.Domain/Patients/Patient.cs`, `MedApp.Application/Patients` | No | Administrative identification. Stores data without change. |
| **Templates and stage definitions** | `MedApp.Application/Procedures/PatientProcedureStages` | No | An ordered checklist written by the organisation. Reproduces a paper document in digital form. MedApp never chooses a template; staff do (`CreatePatientProcedureHandler`). |
| **Stages in a fixed order, locked when finished** | `ProcedureWorkflow.WhyStageCantBeEdited`, `ProcedureChange.FinishStage` | No | Follows the path of a procedure, with no decisions. The order is the template's, not derived from patient data. |
| **Stage and procedure notes** | `UpdateProcedureStageNotesHandler`, `UpdatePatientProcedureHandler` | No | Free text, stored and displayed as typed (Markdown). No parsing or interpretation. |
| **Status workflow** (unassigned, assigned, in progress, in review, completed, blocked, cancelled, archived) | `ProcedureWorkflow.AllowedStatusChanges`, `ProcedureChange.SetStatus` | No | Follows which stages are finished and what staff choose. Not a clinical state. |
| **Due date and the overdue badge** | `DueDates`, `ProcedureWorkflow.IsOverdue` | No, with care | A date set by staff compared with today. It's a reminder, like a calendar, not a red flag derived from clinical data. It would become borderline if MedApp set or changed due dates from clinical data (see below). |
| **Priority** (low, medium, high, urgent) | `PatientProcedurePriority`, `UpdatePatientProcedureHandler` | No, with care | A label chosen by staff. MedApp never sets it. Lists sort by due date unless the user picks another column (`ProcedureSearch.Sort`). |
| **Assignment, task lists and notifications** | `AssignPatientProcedureHandler`, `MyTasksPage.tsx`, `NotificationSender` | No | Work allocation by administrators, and a message that it happened. |
| **Monitoring rules** (an admin is told when a staff member, or anyone, takes a workflow action, optionally only for a priority, template or stage) | `ProcedureMonitor`, `MonitoringRuleMatcher`, `MonitoredActivity` | No | Administrative supervision. It matches who did which audited action against the admin's choices; the priority is the label staff chose, compared by its order only, and the template and stage by identity. It never reads notes or any clinical value, and decides nothing about a patient. |
| **Audit log** | `ProcedureChange.Audit`, `PatientProcedureAuditEntry` | No | Record keeping. |
| **Search, sort, filters and saved searches** | `ProcedureSearch`, `PatientSearch` | No | Simple search on fields staff entered, with user-chosen order. |

### Northern Ireland and the EU

In Northern Ireland, and in the EU, the EU Medical Device Regulation (2017/745) applies instead, with its software guidance (MDCG 2019-11). That guidance also treats software that only stores, archives, communicates or simply searches data as not a device. The outcome is expected to be the same for the same reasons, but it hasn't been assessed here. **Do it before selling into Northern Ireland or the EU.**

### Things that might be read as clinical, and why they aren't

- **"Overdue"** is shown as an error-coloured badge. It is still a comparison of two dates, one of them typed by staff, with no clinical input. The badge text says "Overdue", not anything about the patient's condition.
- **The default sort** puts the soonest due date first. That order comes from dates staff entered, not from a clinical judgement made by the software.
- **"Not assigned to you"** is a confirmation about who is doing the work, not about the patient.
- **Stage instructions** can contain clinical guidance, but it's the organisation's own text, shown unchanged. MedApp is the means of display, like a document management system. The organisation is responsible for the content (`dcb0160-deploying-organisations.md`).

## The decision

> **MedApp, at `main` `88dc1fa`, used for the intended purpose in this document, is not a medical device under UK MDR 2002.** It records and coordinates the administrative and clinical workflow steps of a procedure, and doesn't interpret clinical data, prioritise patients or recommend treatment. It is health IT that supports direct care, so the manufacturer applies DCB0129 to it, and each deploying organisation applies DCB0160.

It is a decision about this product and this intended purpose. A customer who uses MedApp outside its intended purpose takes on that use; the MHRA notes that using a product for a medical purpose doesn't by itself make it a device, but the hazard log assumes the intended use.

## What would change the decision

### Features that make it likely to be a device

Any feature that does one of these needs a new assessment, and is likely to be a device, or a device module, in its own right:
- **Calculations on patient data:** doses, body mass index, fluid balance, early warning scores (such as NEWS2), risk scores (such as ASA grade calculated rather than recorded), or any clinical calculator.
- **Dose fields with checks:** recording a dose is record keeping; checking it against a range, a weight, or another medicine is decision support.
- **Clinical decision support:** suggesting the next step, the template to use, or whether to proceed, from what's been recorded about the patient.
- **Prioritising or triaging patients:** ordering, flagging or escalating patients by anything derived from their clinical data. The roadmap already warns that waiting-list tracking must keep priorities clinician-assigned (`docs/architecture.md`, "Features worth adding", item 5).
- **Red flags or alerts from clinical values:** for example, highlighting a result, a reading or a questionnaire answer as abnormal.
- **Due dates set by the software from clinical data:** for example, "due 6 weeks after the operation date" is still arithmetic on a date staff chose, and is borderline; "due sooner because the score is high" is a device function.
- **Interpreting free text or images:** summaries, extraction, classification or any AI that reads notes or attachments and says something about the patient.
- **Patient-facing questionnaires that are scored or that trigger actions.**
- **Integration that controls or takes data from a medical device.**
- **Claims:** any of the [claims the manufacturer must not make](#claims-the-manufacturer-must-not-make), in any material.

### Roadmap features to watch

From `docs/architecture.md`, step 9 and "Features worth adding":

| Feature | Stays outside the device definition if | Becomes a device question if |
|---|---|---|
| **Structured stage fields** (checkboxes, choices, dates, numbers, signatures) | They only record what staff enter, and show it back unchanged. | A field is validated against a clinical range, a value is calculated or scored, a value changes what happens next, or a value is flagged as abnormal. |
| **NHS number and PDS lookup** | It identifies the patient. | Never, on its own. |
| **SNOMED CT and OPCS-4 codes on templates** | They label the procedure. | Codes are chosen by the software from clinical data. |
| **Reporting and dashboards** | They count workflow (open work, time in stage, overdue work). | They predict or rank patients by clinical risk. |
| **Overdue, blocked and unlocked notifications, email and escalation** | They're about the workflow and dates staff set. | They're triggered by clinical values. |
| **Monitoring rules** (built) | They match staff actions and labels staff chose (priority, template, stage). | A rule can match on a clinical value (a structured stage field, a result), or the software sets the priority it matches on. |
| **Waiting-list tracking (RTT clocks, breaches)** | Clocks and breach dates follow the national rules, and priority stays clinician-assigned. | The software sets or suggests clinical priority. |
| **Digital consent and documents** | They store and display forms. | They assess capacity, risk or suitability. |
| **Patient questionnaires and a portal** | Answers are stored and shown to staff. | Answers are scored, triaged or trigger clinical advice. |
| **FHIR API and EPR integration** | Data moves without change. | MedApp transforms or interprets the data clinically. |

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
| 0.3 | 16 November 2026 | Admins' monitoring rules (notifications of staff actions) and each person's switch for assignment notifications: assessed as administrative supervision, not a device function; the intended purpose and outputs say so | AI coding agent, for review |
| 0.2 | 16 November 2026 | After the review of #60: the intended purpose allows workflow reminders about dates staff set, so planned overdue notifications don't contradict it; all eight statuses listed; the change-control rule is in `AGENTS.md` | AI coding agent, for review |
