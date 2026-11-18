# Data protection impact assessment: MedApp

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.3 (draft), 16 November 2026: the NHS number, the patient banner, the FHIR API, SNOMED CT codes and the demographics lookup (roadmap step 9's first part), with a new risk 21; 0.4 (draft), the same day: admins' monitoring rules, with a new risk 24 (monitoring of staff); 0.5 (draft), the same day: file attachments (scans, photos, results and letters attached to patients, pathways and stages), with new risks 22 and 23; 0.6 (draft), the same day: staff calendars and appointments (risks 1, 3 to 6, and a new risk 25; 25 is held back for another branch), merged with staff email addresses and two-factor sign-in (#86, which changed risk 2's measures and the sharing without a version of its own or a new risk); 0.7 (draft), the same day: meetings, the team view, suggested meeting times and personal calendar templates (`docs/calendar-plan.md`, decisions 50 on); 0.8 (draft), 17 November 2026: meeting titles in notifications, under risk 25 (review of #99); 0.9 (draft), 17 November 2026: several notes per pathway (risk 4's measures) and each member of staff's notepad, with a new risk 26, re-rated High (health data, kept while the notepad is used) after the review of #110; 0.10 (draft), 17 November 2026: the notepad's explanation and prompt removed by the product owner's decision, and the notepad in a half view, with no separate window (risk 26's measures; rating unchanged); 0.11 (draft), 18 November 2026: deleting pathway notes (soft, by admins, with a reason) and staff's requests to delete them (risks 3, 4, 6 and 8's measures; no new risk); 0.12 (draft), 18 November 2026: two-factor codes by email, a weaker second option (risk 2's measures and the sharing); 0.13 (draft), 18 November 2026: closed days (organisation-wide configuration, no personal data but the admin who added one) and appointments recorded after the event (walk-ins: the same appointment data as a booking, with a flag and who recorded it, and a notification with a fixed title, "Appointment recorded", saying who, when and whether the patient attended). No new risk and no rating changed: risk 3's and 4's measures cover the walk-in's notes as appointment notes, and the export and anonymisation already cover appointments and their history; after the review of #119: a walk-in can be withdrawn as entered in error (its reason is free text, admins-only on the timeline and replaced by anonymisation, like a cancellation reason), the export says which appointments were recorded after the event, the walk-in dialog reads a person's days without their appointments (recording no reads of patients it never shows), and closing a day tells the staff with something on it (counts only); after its follow-up review: withdrawing a finished stage's only visit tells the pathway's assignees and the admins (a fixed title, "Stage visit withdrawn", and no patient or free text), and admins can withdraw a walk-in at any time (whoever recorded it, for 30 days); 0.14 (draft), 18 November 2026: replies to meetings (who accepted or declined, seen by everyone who can see the meeting) and the organiser told of a decline, under risk 25 (`docs/calendar-plan.md`, decision 71); 0.15 (draft), 18 November 2026: **planned** risks 29 to 40 for the community specialist centre's Phases 3 to 5 (`docs/centre-phases-3-5-plan.md`), from their design, before anything is built: draft scores and planned measures, each marked with the PR that makes it apply and completes it; no processing or data described here is held yet, and no existing risk's rating changed (27 and 28 are reserved for Phase 2's referrals); 0.16 (draft), 18 November 2026: specialties (`docs/centre-phase2-plan.md`, PR 2): an organisation-wide list of services, each pathway template's and pathway's specialty (a label saying which service a patient is under, as the template's name already did), and which specialties each member of staff belongs to, set by admins and recorded in the activity log. Staff data of the same kind as roles; it changes nothing about who can see what. No new risk and no rating changed |
| **Assessed** | MedApp on `main` as of 16 November 2026, with branch `feature/privacy-features` (roadmap step 7), branch `feature/nhs-integrations` (step 9's first part), branch `feature/file-attachments` (file attachments) and branch `feature/staff-calendars` (staff calendars and appointments, designed in `docs/calendar-plan.md`), and its Azure design (`deploy/azure`), which has not been deployed |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Approved by** | Not yet approved |
| **Next review** | Before the first real patient data; then before each roadmap step that changes the processing (4, 5, 7, 8 and 9); and at least every 12 months |

This is the **product DPIA**: the supplier's assessment of MedApp as a hosted service. It follows the structure of the ICO's DPIA template ([ICO](https://ico.org.uk/media2/migrated/2553993/dpia-template.docx)). Each customer is a controller and must carry out **its own DPIA** before using MedApp. It can adopt this one as the starting point, then add its own context: its patients, its staff, how it will use MedApp, and its own controls.

## Summary

- **A DPIA is mandatory.** MedApp is designed for processing health data, a special category, and across customers it's large scale (Article 35(3)(b)).
- **Today's high risks:** any staff member can see any patient (reads are now recorded, and admins can see who opened a record, but nothing restricts or alerts on it); two-factor sign-in is optional, so an account without it still signs in with a password alone; any staff member can still delete a patient with no pathways (now recorded in the activity log); nothing is ever deleted on a schedule, and deleted files' contents are kept indefinitely; there's no monitoring to notice a breach; backups have never been restored; on Azure the app would use the database administrator's login; and **uploaded files aren't scanned for malware** before staff open them (risk 22).
- **Planned:** risks 29 to 40, for the community specialist centre's Phases 3 to 5 (specimens and lab reports, letters, contact details, messages to patients, scheduled jobs, MDT lists, consent and questionnaires, insurers, data-request packs, several organisations, incoming referrals, video consultations), are drafted from the design in [Step 5](#planned-risks-the-community-specialist-centres-phases-3-to-5). None applies until its feature's PR merges, which completes it.
- **Conclusion: MedApp must not hold real patient data until the measures marked "before go-live" in section 6 are in place.** That's roadmap steps 2, 4, 5 and 7, **plus the patient banner from step 6** and **malware scanning of uploaded files, brought forward from step 9** (or a deployment run without file storage, which turns uploads off), which together make it ready for a first pilot. The banner is in the gate because risk 8 (notes or files put on the wrong patient's record) stays high until it's built; the rest of step 6 isn't needed for the gate. **The banner is now built** (branch `feature/nhs-integrations`), and counts for the gate once the Clinical Safety Officer has verified it. Malware scanning is in the gate because risk 22 (a malicious file opened on a clinical workstation) stays high without it. With these measures, every risk in this assessment is reduced to low or medium, so **prior consultation with the ICO (Article 36) isn't expected to be needed.** If any high residual risk remains at go-live, including risk 8 if the banner isn't built or risk 22 if files are accepted unscanned, the processing must not start without consulting the ICO.

## Step 1: The need for a DPIA

**What MedApp does.** MedApp is a web application for clinical and administrative staff. It records patients, and tracks the pathways carried out for them through a fixed sequence of stages, each with clinical notes. Staff can attach files (scans, photos, results, letters) to a patient, a pathway or a stage. Every change to a pathway, and every file added, deleted or downloaded, is recorded in an audit trail. Each member of staff has a calendar, on which staff book appointments with patients and block off time (lunch, training, annual leave). Admins manage staff accounts, pathway templates, calendar templates and assignments. The supplier, [Company name], runs it as a hosted service on Microsoft Azure in the UK, for customers such as clinics, private hospitals and NHS trusts.

**Why a DPIA is needed:**
- **Article 35(3)(b):** processing on a large scale of special category data (health). The supplier processes it for many customers.
- **The ICO's list** of processing likely to result in a high risk includes processing of health data, and new technology.
- **NHS buyers** ask for a product DPIA through DTAC's data protection section ([DTAC form 2.0](https://digital.nhs.uk/binaries/content/assets/website-assets/services/dtac/dtac_form_2.0_february_2026.docx)).

## Step 2: The processing

### Nature

- **Collection:** the customer's staff type patients' details and clinical notes into MedApp in a browser, and upload files they already hold (a scanned referral letter, a photo of a wound, a result from another system). There's no import or patient-facing access. A demographics lookup can fill a patient's name and date of birth from their NHS number, but only an offline stub of synthetic people exists; the real Personal Demographics Service (PDS) isn't connected, and connecting it would make NHS England a source of patient data and need this DPIA updated first.
- **Storage:** in Azure SQL (UK South), reached only through a private network endpoint, with point-in-time backups. Attached files are in Azure Blob Storage in the same region, also reached only through a private endpoint, with 7 days' soft delete and no other backup; their details (name, type, size, who uploaded them) are in Azure SQL.
- **Use:** staff find patients, start pathways from templates, work stages in order, write notes, and see their task lists. Staff also see each patient's timeline on the patient page: who changed what on their pathways and when, with old and new target dates, priorities and statuses, past assignees, and the usernames recorded at the time (deleted accounts' included): structured values, never free text (no notes, titles or reasons from the audit trail), and only for pathways that still exist. Admins assign work, unlock finished stages, read the full audit log, see a patient's whole history on screen (the same timeline with its free text, deleted pathways included), and manage accounts. Admins can also set **monitoring rules** (Administration, Monitoring): to be told in the bell when a particular staff member, or anyone, does something to a pathway (creates one, finishes a stage, completes, blocks or cancels one, is assigned, unlocks a stage, changes a target date), optionally only for a priority, template or stage. Each person chooses whether they're told when they're assigned a pathway. Staff upload, list and download any patient's files; listing and downloading are recorded in the activity log, and every download in the pathway audit log too. The timeline shows that a file was attached or deleted, with its name for admins only. Files are only ever downloaded, never shown in the page; the only images shown are small thumbnails the server makes from PNG and JPEG photos.
- **Calendars and appointments:** staff book a patient with a member of staff at a time, optionally for a pathway's stage, with optional notes. An appointment goes from requested to booked, then attended or did not attend, or is cancelled with a reason; every change, including old and new notes and the reason, is kept in its history. When a stage that involves a visit becomes current, MedApp requests an appointment for it. Staff block off their own time for activities from an admin-managed list (Lunch, Training, Annual leave, ...), with an optional short note; each person's own day and week templates fill their week with its usual blocks. Admins arrange **meetings** for several members of staff (a title, optional notes about staff, a time and the attendees); a meeting is busy time on each attendee's calendar, and every member of staff can see it. Admins also see a **team view** (several people's day side by side) and **suggested meeting times** (when everyone chosen is free), computed from the same calendars; neither names a patient. The patient timeline shows appointment history: to staff, structured values only (status, time, member of staff, stage), never notes or cancellation reasons; to admins, everything.
- **Sharing:** none outside the customer, except emails to staff (password reset links, notices of changes to their own sign-in, and, for those who choose them, short-lived two-factor codes), sent through Azure Communication Services Email; they carry a staff member's username and email address and never patient data (a code email carries only the code). Within the customer, every staff user can see every patient, and every staff member's calendar and every appointment (section 5, risks 1 and 25). A read-only FHIR API returns a patient's details, by ID or NHS number, in the NHS's standard format, but only to a caller signed in as one of the customer's staff, and each use is logged (risks 1 and 21); there are no system-to-system clients yet.
- **Deletion:** staff can delete a patient who has no pathways, no appointments and no files. Admins can delete cancelled or archived pathways that have no files; their audit entries remain, and their appointments are kept, detached from the pathway. Admins can anonymise a patient's record in place (identifying details and free text replaced, appointments' notes and cancellation reasons included, the structure kept); anonymising also renames the patient's files and deletes them, with an audit entry for each. Staff can delete files they uploaded, and admins any file, except on a restricted or anonymised record, where nobody can add or delete files. A deleted file is hidden from staff and can't be downloaded, but its details and its contents are kept until a purge job removes the contents after a retention window. **No purge job exists yet**, so deleted files, anonymised patients' included, are kept indefinitely. Nothing is deleted on a schedule: records past their retention period are listed for an admin to review.
- **New technology:** server-side rendering (a Node page server: in Kubernetes a container of its own in the API's pod, without the API's secrets; run on its own, inside the API's container), live notifications over WebSockets with a Valkey relay, Kubernetes, and cloud blob storage for files, with thumbnails made on the server (SkiaSharp). None involves profiling, AI or automated decisions: no file is read, classified or interpreted. Monitoring rules tell an admin about single actions as they happen; they don't score, rank or judge staff, and decide nothing (risk 24).
- **Sources and flows:** [data-inventory.md](data-inventory.md).

### Scope

- **Data:** name, date of birth, email and (optionally) NHS number of patients; pathway titles, notes (several per pathway, each with every version kept), target dates, priorities and statuses; stage notes (clinical free text); **attached files** (scans, photos, results, letters, up to 20 MB each) and their names (free text); the audit trail with copies of notes and file names, and a record of each download; appointments (patient, member of staff, time, status, notes and cancellation reasons) and their history with copies of the notes and reasons; staff usernames, password hashes, roles, the specialties each belongs to, optional work email addresses, two-factor keys and recovery codes, sessions with IP addresses and browsers, settings with saved searches; each member of staff's own notepad (free text, which may hold patient details; the whole notepad is cleared only once it has gone 30 days without any change, so what's in it stays while it's used; risk 26); staff calendars (blocked time with its activity, such as annual leave or training, and a short note); notifications, and each person's notification choices; admins' monitoring rules, which name the staff they watch. SNOMED CT codes on stage definitions label kinds of step and aren't personal data. Field by field: [data-inventory.md](data-inventory.md).
- **Special category:** health data about patients, including that a patient is being seen and whether they attended. Free text can also hold other special category data (for example religion, sexual life, ethnicity) if staff write it. **Files** are health data by nature, can be **images of the body**, keep the **metadata** they were uploaded with (for a photo, possibly where and when it was taken), and can hold **third parties'** data (a letter naming relatives or other clinicians). MedApp is not meant to hold health data about staff, but a calendar block's note could ("hospital appointment"; risk 25).
- **Volume:** per customer, [patients a year], [staff users]. Across customers, large scale.
- **Retention:** proposed in [retention-schedule.md](retention-schedule.md); **not enforced today**.
- **Geography:** patients in the UK; data stored in the UK.

### Context

- **Relationship:** patients are receiving care from the customer, and expect their information to be recorded and kept confidential. They don't use MedApp themselves. **Staff** are the customer's employees (or contractors): there's an imbalance of power, so monitoring them must be necessary, proportionate and open (risk 24).
- **Control:** patients have no direct control over what is recorded; they rely on the customer and its staff.
- **Vulnerable people:** patients include people who are ill, children, and possibly people lacking capacity.
- **Expectations:** patients would expect a care record system, including copies of their letters and results; they wouldn't expect staff outside their care to browse their record or their photos, or their data to be used for anything else.
- **Prior concerns:** the ICO wrote to over 200 health leaders on 12 November 2026 about staff "snooping" on records, after more than 300 reports of unauthorised access in 2026 ([ICO](https://ico.org.uk/about-the-ico/media-centre/news-and-blogs/2026/11/uk-health-sector-warned-to-tackle-medical-record-snooping/)). NHS suppliers have been fined for weak security, including missing MFA ([ICO](https://ico.org.uk/about-the-ico/media-centre/news-and-blogs/2025/03/software-provider-fined-3m-following-2022-ransomware-attack/)).
- **State of the technology:** MedApp is young (built from January 2026), has one maintainer, and has never run in production ([architecture handbook, 1.5](../architecture.md#15-status-and-maturity)). The code is in a public repository.
- **Codes and standards:** the NHS Records Management Code of Practice, the Caldicott Principles, the DSPT, DCB0129 and DCB0160, OWASP ASVS, Cyber Essentials.

### Purposes

- **For patients:** their pathways are coordinated safely, in order, by the right staff, with an accurate record, and the documents their care needs (letters, results, images) are with it; the visits their pathways need are booked, and visits that are needed aren't skipped by mistake.
- **For the customer:** coordinating work and staff time, accountability through the audit trail, meeting record-keeping duties, and supervising work as it happens (admins' monitoring rules), for example a team lead keeping an eye on a new member of staff's work. Monitoring is supervision, not clinical follow-up, which doesn't rely on it (hazard H-29).
- **For the supplier:** providing the service under contract. No purpose of its own ([roles-and-lawful-bases.md](roles-and-lawful-bases.md)).

## Step 3: Consultation

| Who | How | Status |
|---|---|---|
| **The supplier's DPO** | Review of this DPIA | To do |
| **Pilot customers' DPOs, Caldicott Guardians and IG leads** | Review of the product DPIA as the basis for their own | To do |
| **A Clinical Safety Officer** | Overlap between privacy and clinical hazards (wrong patient, lost updates) | To do (roadmap step 6) |
| **Patients** | Through the customer, for example its patient panel, if the customer judges it proportionate | The customer's decision |
| **Security** | A penetration test and the ASVS review (roadmap steps 2 and 10) | To do |
| **A solicitor** | Review of the data processing agreement | To do |

## Step 4: Necessity and proportionality

| Question | Answer |
|---|---|
| **Lawful basis** | The customer's: typically 6(1)(e) (NHS) or 6(1)(b)/(f) (private), with 9(2)(h) and DPA 2018 Schedule 1, Part 1, paragraph 2 ([roles-and-lawful-bases.md](roles-and-lawful-bases.md)). The supplier acts on instructions under Article 28. |
| **Does the processing achieve the purpose?** | Yes. Coordinating a pathway's stages needs the patient's identity, the pathway, and notes on each stage. Booking a visit needs the patient, the member of staff and the time, and booking around staff time needs to know when staff are unavailable, but not why. |
| **Is there a less intrusive way?** | Paper or spreadsheets hold the same data with weaker access control and no audit. A system without patient identity can't coordinate care for a person. |
| **Minimisation** | **The patient record is deliberately small:** name, date of birth, email and the NHS number; no address, phone or sex. **The NHS number is optional,** and justified by safe identification: NHS England mandates it as the patient's identifier in health and care, it's what the patient banner shows to tell patients apart (risk 8), and it's how other NHS systems find the same patient. It's never written to the activity log. Further identity details step 9 adds will each be justified then. **Free text isn't limited**, and the audit copies it (risks 3 and 4). **Files aren't limited in content** either, only in type (six kinds, found from their contents) and size (20 MB); their names are free text, and originals keep their metadata (risk 23). Appointments hold only what booking needs; their notes are optional and limited to 2,000 characters, cancellation reasons to 500, and calendar block notes to 200. Blocked time records an activity from a fixed list, not a reason. Appointment notifications carry no patient name or notes. |
| **Accuracy** | Staff can edit patients and pathways. An NHS number must pass its check digit, and can't be on two patients; it isn't traced against PDS, so it isn't confirmed to be the patient's. Finished stages lock, and only an admin can unlock them, which is audited. Patient edits are recorded in the activity log (which fields changed, not their values), and an admin can mark a record as disputed by the patient. A file put on the wrong record can't be moved, only deleted (soft, audited) and uploaded again to the right one (risk 8). |
| **Retention** | Periods in [retention-schedule.md](retention-schedule.md), based on the Records Management Code; attached files are part of the clinical record. Clinical records past their period are listed for review (keep with a reason, or anonymise); operational data isn't purged yet, and deleted files are kept until a purge job exists (risk 5). |
| **Transparency** | Template privacy notices for customers to give patients and staff ([privacy-notice-patients-template.md](privacy-notice-patients-template.md), [privacy-notice-staff-template.md](privacy-notice-staff-template.md)). |
| **Rights** | Supported by the product: a subject access export, restriction and dispute flags, anonymisation; the clinician review of an export is by hand ([data-subject-rights.md](data-subject-rights.md)). The export lists a patient's attached files, but doesn't include the files themselves yet. |
| **Processors** | The supplier, under an Article 28 agreement ([dpa-template.md](dpa-template.md)); Microsoft as a sub-processor ([sub-processors.md](sub-processors.md)), whose Azure Blob Storage holds the files. |
| **International transfers** | None by the supplier; UK hosting ([international-transfers.md](international-transfers.md)). |
| **Monitoring staff** | Admins' monitoring rules process **staff** personal data: what a named member of staff does, as it happens. **Necessary:** supervising clinical and administrative work (supporting a new member of staff, knowing when work is blocked or cancelled) is part of running a service, and the same facts are already in the audit trail admins can read; the rules only bring them sooner. **Proportionate:** only admins can have rules, and only while they're admins; each admin sees only their own; the events are workflow actions already audited (no reads, searches, sign-ins, locations or timings beyond the action's own); a notification carries no free text and no patient identifiers; nobody is told about their own actions; at most 50 rules each. **Open:** staff are told in the staff privacy notice ([privacy-notice-staff-template.md](privacy-notice-staff-template.md)), and the customer should have a monitoring policy that says why and how admins use it, following the ICO's guidance on monitoring workers. Risk 24. |
| **Function creep** | The supplier commits to no use of its own. Any new purpose needs a DPIA update first. |

## Step 5: Risks

**Scoring,** as in the ICO's template:
- **Likelihood of harm:** remote, possible or probable.
- **Severity of harm:** minimal, significant or severe.
- **Overall risk:** low, medium or high, from the two together, by this matrix:

| Likelihood | Minimal | Significant | Severe |
|---|---|---|---|
| **Remote** | Low | Low | Medium |
| **Possible** | Low | Medium | High |
| **Probable** | Medium | High | High |

The one exception is risk 18, scored low today because it can't happen while each customer has its own deployment; it's rescored at step 8.

Likelihood is judged for MedApp **as it was on `main` on 16 November 2026, before roadmap step 7's privacy features**, if it held real patient data. Step 6 gives the residual risk with the measures in place since, including step 7's (marked "In place (step 7)").

| # | Risk to individuals | Source of the risk | Likelihood | Severity | Overall |
|---|---|---|---|---|---|
| 1 | **Inappropriate access ("snooping"):** a staff member looks at the record of a patient they don't care for (a neighbour, a relative, a public figure), and it can't be detected or disproved. | Every staff user sees every patient and pathway, every patient's files, and every appointment on every calendar (who is being seen, when and by whom); **reads aren't recorded** at all; there are no access reports. | Probable | Severe | **High** |
| 2 | **Account takeover:** an attacker signs in as a staff member or admin with a stolen, guessed or reused password, and reads or changes records. | Password only, no MFA; no idle timeout. Since the security baseline (step 2, first part): sign-in is throttled (5 attempts a minute per username, 30 per address) in front of the 5-minute lockout, the 10,000 most common passwords are refused, and a password change or a reset ends the user's sessions, and an admin can sign a user out everywhere; first admin always called `admin`. Accounts created before PR #53 may still have the published password `DefaultPassword1`. | Probable | Severe | **High** |
| 3 | **Oversharing in free text:** notes, titles, a pathway's block or cancel reasons, appointment notes and cancellation reasons, and file names hold more than needed (other people's details, unrelated conditions, a patient's name in a file's name), and spread to the audit, the appointment history, notifications and logs. | Free text is unlimited in pathway notes, and long in appointment notes (2,000 characters); pathway titles appear in notifications and travel through Valkey; file names are copied into the audit log; every staff user can open any appointment and read its notes. | Probable | Significant | **High** |
| 4 | **Copies of clinical text that can't be managed:** correcting, erasing or expiring a note leaves its old text in the audit trail or the appointment history. | `StageNotesUpdated` audit entries copy the old and new stage notes into `Details` (`MedApp.Application/Pathways/Common/PathwayChange.cs`), as `DetailsUpdated` entries written before pathways had several notes do for pathway notes; an appointment's history (`AppointmentEvent`) copies its old and new notes and its cancellation reason. **Pathway notes no longer are copied:** their versions are in a table of their own (`PatientPathwayNoteVersion`), and the audit names only the note and version. | Probable | Significant | **High** |
| 5 | **Kept for ever:** records, files, appointments, staff calendars, sessions with IP addresses, notifications and audit entries are never deleted, so a later breach exposes far more than needed. | Clinical records are reviewed, never deleted automatically. The operational purge (sessions, sign-in events, notifications, notepads) is built but ships as a dry run until the DPO confirms its periods. A deleted file is only hidden: its contents and details stay, because the purge job that would remove them doesn't exist yet. | Probable | Significant | **High** |
| 6 | **Rights can't be met fully or on time:** a subject access request is answered late or incompletely. | No export; the data is spread over several tables, now including appointments and their history, and attached files are in blob storage, outside the database; the audit log's screen filters by pathway, not by patient. Admins can now see a patient's whole history on screen, on the patient page's timeline (every change to their pathways, with its free text, deleted pathways included), which helps answer a request, but it is not an export. | Probable | Significant | **High** |
| 7 | **A patient's record is lost or changed without trace:** a staff member deletes or edits a patient, by mistake or deliberately, and nothing records it. | Any staff user can permanently delete a patient with no pathways and no files; patient creates, edits and deletes aren't audited; account and role changes aren't audited. | Possible | Severe | **High** |
| 8 | **Wrong patient:** notes are written on, or a file (a letter, a result, a photo) attached to, the wrong patient's record, leading to a disclosure or a clinical error. | Patients are identified by name, date of birth and email only; no NHS number; no duplicate check; no patient banner. A file can't be moved once attached, only deleted and uploaded again. | Possible | Severe | **High** |
| 9 | **A lost update:** two staff save the same notes, and one silently overwrites the other. | No optimistic concurrency. The audit keeps both texts, but nobody is warned. | Possible | Significant | **Medium** |
| 10 | **Exposure on a shared or unattended computer:** someone else sees a patient's record on a ward or clinic workstation, in browser storage or history, or in a file downloaded to it. | 8-hour sessions (extendable on request to 24 at most from sign-in) with no idle timeout; saved searches (which can be patient names) stay in `localStorage` after sign-out; list pages keep their filters in the URL, so a patient search (first name, last name, email, date of birth range: `MedApp.Web/src/pages/listSearch.ts`) lands in the browser's history and address bar suggestions, which signing out doesn't clear. Every file opened is first downloaded, and stays in the computer's downloads folder after sign-out. Rendered pages and files aren't cached. | Probable | Significant | **High** |
| 11 | **Web attacks:** a flaw lets an attacker run script in a staff member's browser, or frame the app. | Since the security baseline: a Content Security Policy with a nonce for each page (no inline or third-party scripts), `frame-ancestors 'none'` and `X-Frame-Options: DENY`, `nosniff` and a `Referrer-Policy` (`SecurityHeaders`). No antiforgery tokens (`SameSite=Lax` is the defence). Output is escaped, and rich text never runs raw HTML. Files uploaded by staff are served from the app's own origin, though only as downloads, with their own sandboxing CSP (risk 22). | Possible | Severe | **High** |
| 12 | **Infrastructure compromise:** an attacker or a flaw reaches the database or its credentials. | On Azure the app, the migrations Job and anyone who can read an API pod's environment use the SQL server's admin login, and the storage account's key, which can read and delete every file; the AKS API server, Key Vault and the registry have public endpoints (behind Entra ID); session IDs are stored unhashed; Node sees every visitor's session while rendering their pages, and, only when the image runs on its own outside Kubernetes, could read the API's environment (in Kubernetes it's a container of its own, without the API's secrets). | Possible | Severe | **High** |
| 13 | **Undetected breach:** a breach goes unnoticed, so it isn't contained or reported in time. | No monitoring, alerting or exported logs in any deployed environment; no read audit. | Probable | Severe | **High** |
| 14 | **Loss of data or of the service:** records are lost, or unavailable during care. | Backups (7 days, locally redundant) never restored; no recovery objectives; no downtime procedure; one maintainer. Files have only 7 days' soft delete, with no backup, and a database restore doesn't bring back files, or their details, with it. | Possible | Severe | **High** |
| 15 | **Supply chain:** malicious code reaches production through a dependency or the build pipeline, and exfiltrates data. | Since the security baseline: actions pinned by commit SHA, Dependabot, CodeQL, dependency review, npm and NuGet audits that fail the build, a Trivy image scan, and SBOM and build provenance attestations on the published image. Images are still pinned by tag, not digest; nothing verifies the attestations before deploy; every merge deploys with no approval step; the repository and image are public. | Possible | Severe | **High** |
| 16 | **Supplier staff access:** someone at the supplier views or copies patient data without a need, or data ends up in support tickets, laptops or AI tools. | Cluster admins can reach everything; no just-in-time access or access log; one maintainer. | Possible | Severe | **High** |
| 17 | **Sub-processor and transfer:** Microsoft accesses or discloses the data, or it's processed outside the UK. | Azure hosting; Microsoft's service operations. | Remote | Significant | **Low** |
| 18 | **Mixing customers' data:** one customer sees another's patients. | Not possible today (one deployment per customer). Becomes a risk with multi-tenancy (step 8). | Not possible today | Severe | **Low today** (the matrix's one exception, because it can't happen yet); **to rescore at step 8** |
| 19 | **Lack of transparency:** patients don't know MedApp is used or who runs it. | Depends on the customer's privacy notice. | Possible | Minimal | **Low** |
| 20 | **Real data in development or testing:** patient data copied into a developer's database, a screenshot, a test, or an AI tool. | Convention only (`AGENTS.md`: synthetic data only); `medapp-dev` refuses anything but a local database. | Remote | Severe | **Medium** |
| 21 | **The NHS number, a national identifier:** a disclosed NHS number lets anyone match the patient across every NHS system; a wrong number ties the patient to someone else's record in the systems that read it from MedApp. | New with branch `feature/nhs-integrations`, and scored for it: an optional NHS number on every patient, returned by a FHIR API that any signed-in staff member (or a script using their session) can call; not traced against PDS; not encrypted at field level. | Possible | Significant | **Medium** |
| 22 | **Malicious files:** a file carrying malware (for example a PDF or Word document with an exploit, received from outside and uploaded) is downloaded and opened on a clinical workstation, which is compromised, exposing the records it can reach and the staff member's session. | **Files aren't scanned:** every file is stored as "Not scanned" (`NoMalwareScanner`). Staff upload files that came from outside (patients, referrers, other systems), and any staff member can download them; they open in desktop apps (PDF readers, Word, image viewers). The type check refuses the commonest active content (macros, external links and embedded objects in Word, scripts and launch actions in PDFs), not exploits in the formats themselves. | Possible | Severe | **High** |
| 23 | **Files hold more than staff see:** a photo's metadata (where and when it was taken, the phone), a document's author or tracked changes, a third party named in a letter, or a patient's name in a file's name, is kept and passed on with every download, or shown in the audit log. | Files are stored exactly as uploaded; only thumbnails are re-encoded without metadata; file names are free text; a download gives the whole original. | Possible | Significant | **Medium** |
| 24 | **Excessive or covert monitoring of staff:** an admin (or, since roles, a holder of the `ManageMonitoring` permission: the starting Manager role) uses monitoring rules to follow one member of staff's every action, without a legitimate reason or their knowledge (to build a case, or to harass), and staff feel watched. | Rules can name any account and any of eight events, with no reason recorded; staff can't see the rules about them; rule changes aren't audited. | Possible | Significant | **Medium** |
| 25 | **Staff calendars reveal more than needed about staff:** colleagues see when someone is on annual leave or away, and a block's note, a meeting's title or notes, or a personal template's name gives a reason that reveals their health or private life (for example "hospital appointment", "return-to-work meeting"), or names a patient. | Every staff user can see every calendar, its meetings (with their attendees, each one's reply, titles and notes) and everyone's templates; block notes (200 characters) and meeting titles and notes (100 and 500) are free text; MedApp can't check what they say. Admins' team view and suggested times show several people's busy time at once, but name no patient. | Possible | Significant | **Medium** |
| 26 | **Patient details in a notepad, outside the record:** staff jot patient details (a name, a phone call's content, which can be health data: "rang, worried about the biopsy result") in their own notepad, where they're kept but can't be found by the patient's export (Article 15) or removed by anonymisation (Article 17), and aren't part of the record others see. | New with branch `feature/procedure-notes`: one notepad per member of staff, free text up to 20,000 characters, kept on the server, not linked to any patient; nobody else can read it, and reading it isn't in the activity log. It's cleared only once the whole notepad has gone 30 days without any change, so a notepad used every day keeps a jotting from months ago until its owner takes it out. | Probable | Significant | **High** |

### Planned risks: the community specialist centre's Phases 3 to 5

Risks 29 to 40 come from the design of Phases 3 to 5 (`docs/centre-phases-3-5-plan.md`), written **before any of the features exists**, so each feature's PR can build its measures with it. **None of this processing happens today, and MedApp holds none of this data yet.** Each risk applies from the PR named (the plan's section 6 numbers), and that PR completes it: it confirms the source and the scores against what it built, moves the risk into the table above, fills in its measures in Step 6, and updates the data inventory, ROPA, retention schedule and privacy notices in the same PR (the plan's decision 76). Risks 27 and 28 are reserved for Phase 2's referrals and staff attribution of triage decisions (`docs/centre-phase2-plan.md`), added by the PRs that build them.

Scores are **drafts**, judged on the design without its measures, as the table above judges `main` before step 7.

| # | Risk to individuals | Planned source of the risk | Applies | Likelihood | Severity | Overall |
|---|---|---|---|---|---|---|
| 29 | **Specimens and lab reports reach a new recipient:** the request sheet and the specimen (with the patient's name, date of birth, NHS number and request notes) go to an external lab, and its report comes back; a request goes to the wrong lab, or holds more than the lab needs. | Specimens tracked to an external lab (a new recipient); request notes and the specimen's site are free text; reports stored as files. | **Planned: applies when PR 13 merges** (specimens; results with PR 14) | Possible | Significant | **Medium** (draft) |
| 30 | **A letter to the wrong recipient, or sent by email:** a clinic letter or discharge summary (clinical detail, special category) goes to the wrong practice, an old address or a mistyped "other" recipient, or travels by unsecured email. | Letters with clinical free text, sent by post, by hand or by secure email to GP practices, patients, referrers and others; addresses typed or copied from the record. | **Planned: applies when PR 16 merges** (letters; templates with PR 15) | Probable | Significant | **High** (draft) |
| 31 | **More identifying data about patients:** a postal address, mobile number and registered GP practice make the record more identifying, and a breach more harmful; a wrong address or number sends post or messages to someone else. | Optional contact details and a practice on every patient; a directory of GP practices. Changes the minimisation statement in Step 4 ("no address, phone or sex"). | **Planned: applies when PR 12 merges** | Possible | Significant | **Medium** (draft) |
| 32 | **Messages to patients:** a reminder read by someone else on a shared phone or inbox, or saying more than when and where; GOV.UK Notify (or another provider) as a new sub-processor. | Text and email reminders, moved, cancelled and missed-appointment messages, each stored as sent, through a provider. | **Planned: applies when PR 26 merges**; the live sender can't send anything until `sub-processors.md` and `international-transfers.md` are updated in the same PR | Possible | Significant | **Medium** (draft) |
| 33 | **Automated processing nobody watches:** scheduled jobs purge data, send messages and raise alerts without a person; a fault deletes the wrong operational data or sends the wrong thing, and nobody notices. | A job runner inside the API (purges of sessions, sign-in events, notifications and notepads; reminders; service alerts). | The job runner shows each job, its failures and Run now; one copy runs each job (a lease); counts logged, never content; admins told after three failures; `/health` Degraded when a job is overdue; the purge never touches patient records and ships as a dry run (Risk 33 below) | Unlikely | Significant | **Low** |
| 34 | **MDT lists shown and printed in meetings:** a list of patients with their clinical questions is shown on a screen or printed, seen by people outside the patient's care, or left in a room. | The MDT list, its print view, and the outcome (free text). | **Planned: applies when PR 30 merges** | Possible | Significant | **Medium** (draft) |
| 35 | **More special category detail:** consent records (who signed, a best-interests decision, a withdrawal) and questionnaire answers (pain, mood, function) add sensitive detail, some of it about capacity or mental health. | Consent records with scanned forms; outcome questionnaire answers and totals as written. | **Planned: applies when PR 31 merges** (consent) **and PR 32** (questionnaires) | Possible | Significant | **Medium** (draft) |
| 36 | **Insurers as a new recipient:** for insured private patients, the policy number, authorisation code and invoice (which shows the care given) go to the insurer; a patient who didn't expect it, or the wrong insurer. | Funding per pathway, insurers, invoices and payments. | **Planned: applies when PR 35 merges** | Possible | Significant | **Medium** (draft) |
| 37 | **A data-request pack sent to the wrong person:** a subject access pack (the whole record and its files) goes to someone who isn't the patient or entitled to act for them, or includes what should have been withheld. | Data requests with requesters on the patient's behalf, the clinician review, and a pack built on request. | **Planned: applies when PR 36 merges** | Possible | Severe | **High** (draft) |
| 38 | **One organisation sees another's patients:** with several organisations in one deployment, a query that misses the organisation's filter shows, or changes, another customer's records. | Multi-tenancy: shared tables, separated by the organisation. **Replaces risk 18's "rescore at step 8"** when it applies. | **Planned: applies when PR 40a merges**, before a second organisation shares a deployment | Possible | Severe | **High** (draft) |
| 39 | **Patient data without a patient:** referrals fetched from the e-Referral Service wait unmatched, with a name, date of birth, NHS number and letter, outside any patient's record, so a patient's export and anonymisation can't find them, and they're kept until someone acts. | Incoming referrals from e-RS, matched by a person. | **Planned: applies when PR 38a merges** (live only after NHS onboarding) | Possible | Significant | **Medium** (draft) |
| 40 | **The video platform as a processor:** a video consultation's link and the call itself go through a platform the customer chooses, which processes health data. | Video appointments with a link to another tool. | **Planned: applies when PR 34 merges** | Possible | Significant | **Medium** (draft) |

Two existing risks change with these phases too, in the PRs named: risk 22 (malicious files) gains a real scanner (PR 37), and risk 5 (kept for ever) gains the operational purge (PR 19, built: a dry run until the DPO confirms its periods) and retention classes (PR 27).

## Step 6: Measures to reduce the risks

**Status:**
- **In place:** built, in the code or the deployment design. Azure is written but **not yet deployed**, so its controls are "in place in design".
- **Planned:** in the roadmap ([architecture handbook](../architecture.md#roadmap-the-next-10-steps)), with the step.
- **Before go-live:** must be done before MedApp holds real patient data.

**Residual risk** is given twice: **now** (with the measures in place) and **after** the planned measures.

### Risk 1: inappropriate access

| Measure | Status | Where |
|---|---|---|
| Every API endpoint that reaches patient or account data requires a signed-in session and a permission its roles grant (`[RequirePermission]`); only sign-in, sign-out, `whoami` and the session's own endpoints don't; the API is the only enforcement; a convention test fails any endpoint without one | In place | `MedApp.API/Common/Authentication/RequirePermissionAttribute.cs`, `PermissionPolicies.cs`, the controllers in `MedApp.API/Controllers/`, `AuthorisationConventionTests` |
| Least privilege: roles that admins manage grant only what each job needs (starting roles Clinician, Nurse, Booking, Lab, Manager), reducing the risk that any member of staff can change any record; administration permissions belong to Admin alone; the last active admin can't be removed, and `grant-admin` recovers a locked-out deployment | In place (roles page and the web app following permissions to come in Phase 2) | `MedApp.Application/Common/Identity/Permissions.cs`, `StandardRoles.cs`, `LastAdminGuard.cs`, `AdminGrant.cs` |
| The full audit trail is admin-only; staff see a per-patient timeline without its free text (no notes, titles or reasons; old and new target dates, priorities and statuses, past assignees and recorded usernames are shown). For appointments, staff see the status, time, member of staff and stage, never the notes or a cancellation reason, and nothing about appointments of a deleted pathway | In place (appointments: on `feature/staff-calendars`) | `MedApp.API/Controllers/PathwayAuditController.cs`, `MedApp.Application/Pathways/Timeline/Common/TimelineMapper.cs`, `TimelineStaffCanaryTests` |
| Every change to a pathway is recorded with who and when; so is every change to an appointment (`AppointmentEvent`) | In place (appointments: on `feature/staff-calendars`) | `MedApp.Application/Pathways/Common/PathwayChange.cs`; `MedApp.Application/Appointments/Common/AppointmentChange.cs` |
| **Record the reads of patient data** in the activity log, once per person every 5 minutes: a patient's record, timeline and its overview opened (viewed); each patient on a page of patient search, pathway search or retention review results (listed); searches (the filters used and how many matched, not the terms); a pathway opened; appointments read (the calendar, the Requires Appointment list, the booking dialog and an appointment opened), as reads of the patients they show; exports. **Not recorded per patient:** a calendar read without its appointments (Apply Template's days: `withoutAppointments`, which shows no patients); the deprecated list of every patient (`GET api/patients`, used only by the retired Blazor client), which records one "searched: all" entry; the side nav's pin labels, for records the user already opened; and the admin-only audit and activity logs themselves. A test lists every endpoint with what it records | In place (step 7; appointments on `feature/staff-calendars`) | `MedApp.Application/Activity/Common/ActivityLog.cs`; the patient, pathway, timeline, retention, export and appointment handlers; `MedApp.IntegrationTests/Privacy/EndpointCoverageTests.cs` |
| **Attached files in the activity log:** listing a patient's, pathway's or stage's files (`AttachmentsViewed`) and downloading one (`AttachmentDownloaded`), against the patient (and the pathway, for a pathway's or stage's files), within the same 5-minute window. Every download is also in the pathway audit log, each one on its own. Thumbnails aren't recorded: they're shown with a list, which is | In place | `ActivityLog`; `MedApp.Application/Attachments`; `AttachmentAudit.cs` |
| "Who has accessed this patient": the access report on the patient's page, and the activity log searchable by patient, user, address, action and date (admins) | In place (step 7) | `MedApp.Web/src/components/patients/PatientAccessReport.tsx`; `MedApp.Web/src/pages/activity/ActivityLogPage.tsx` |
| The FHIR API is held to the same rules as the pages: staff only, `no-store`, and every read and search recorded (`PatientViewed`, `PatientsSearched` and `PatientListed`, marked `via: "fhir"`) | In place (branch `feature/nhs-integrations`) | `MedApp.API/Controllers/FhirController.cs`; `GetFhirPatientHandler`, `SearchFhirPatientsHandler` |
| "What has this person seen" as a report (today a search of the activity log by username), an `Auditor` role, unusual-access alerts, periodic access reviews, a tamper-evident (ledger) log | Planned, steps 4 and 7. **Before go-live** (periodic review) | |
| Restrict access by site, team or care relationship | Planned, step 8 (sites); customers decide who gets accounts until then | |
| Customer measures: accounts only for staff who need them; staff told that access is monitored; disciplinary policy | The customer's | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |

**Residual risk:** now **medium** (every read through the current app's pages is recorded and can be reported, so snooping is detectable and deterred, though still possible, and the log isn't yet tamper-evident); after step 8 **low to medium**.

### Risk 2: account takeover

| Measure | Status | Where |
|---|---|---|
| Passwords stored as salted hashes by ASP.NET Core Identity | In place | `MedApp.Infrastructure/Common/Identity` |
| Server-generated first passwords and resets (at least 16 characters, from a cryptographic random generator), shown once, never logged or cached | In place | `IdentityUserService.cs`; `MedApp.API/Controllers/AuthController.cs` (`no-store`) |
| Lockout after 5 failures for 5 minutes | In place | `MedApp.Infrastructure/Common/Authentication/IdentityAuthenticationService.cs`; `MedApp.API/appsettings.json` (`Identity`) |
| Server-side sessions in an HttpOnly, Secure, SameSite=Lax cookie named `__Host-medapp.session` outside development; roles and their permissions re-read on every request (no cache); deleting a user ends their sessions; a password change ends the user's other sessions, and a reset all of them; admins can sign a user out everywhere | In place | `SessionAuthenticationHandler.cs`, `SessionCookieService.cs`, `SessionService.cs`, `UserSessions.cs`, `SignOutEverywhereHandler.cs` |
| Sign-in throttling per address and per username, in front of the lockout; the 10,000 most common passwords refused | In place | `MedApp.API/Common/RateLimiting`, `CommonPasswordValidator.cs` |
| Two-factor authentication with an authenticator app (TOTP) and recovery codes, optional for everyone; admins can require it for admin accounts; failed codes count towards the lockout; admins can reset someone's two-factor (recorded, and the owner is emailed) | In place (`feature/email-2fa`) | `TwoFactorService.cs`, `LoginTwoFactorHandler.cs`, `TwoFactorSetupRequired.cs`, `SecuritySettings.cs` |
| Two-factor codes by email as a second, weaker option, for someone with a confirmed address: 6 digits from a cryptographic generator, valid 10 minutes, single use, stored only as a hash, five tries a sign-in, sent again at most once a minute and three times, ten emails per account in 15 minutes; wrong ones count towards the lockout as wrong authenticator codes do. Turning them on proves the mailbox (an emailed code), needs the password, and ends other sessions. The admin requirement needs the authenticator app: anyone who gets into the mailbox gets the code, so an admin with codes by email is held to set the app up. The address can't be removed while it's used for codes. Weaker than the app (NIST SP 800-63B doesn't accept email as an out-of-band authenticator), and **for these accounts a reset link and a sign-in code go to the same mailbox, so the mailbox alone is enough to take the account over** | In place (`feature/email-2fa-codes`) | `EmailCodeService.cs`, `EmailCode.cs`, `LoginTwoFactorHandler.cs`, `SessionService.cs` |
| "Forgot your password?" by a single-use, 30-minute link to the staff member's confirmed work address, the same answer whether or not an account matched, every session ended, the owner told by email; it doesn't bypass an authenticator app (it does bypass codes by email, above) | In place (`feature/email-2fa`) | `ForgotPasswordHandler.cs`, `ResetPasswordHandler.cs`, `PasswordResetService.cs` |
| Two-factor compulsory for admins from day one (today an admin turns the requirement on), passkeys; SSO with the customer's identity provider; first admin with a chosen name | Planned, step 4. **Before go-live** | |
| A breached-password check | Planned, step 2. **Before go-live** | |
| Find and reset accounts that may still have `DefaultPassword1` | Planned (`docs/todo.md`). **Before go-live** (no such accounts exist in a new deployment) | |

**Residual risk:** now **high** while two-factor is optional (**medium** once a customer requires it for admins); after steps 2 and 4 **low**.

**Staff email addresses** add a little personal data of staff (a work address, given by them or their admin) and a new recipient (the email service). They're used only to reset a forgotten password and to tell the person about changes to their own sign-in, so a compromised account is noticed. Emails never carry patient data, clinical free text (a notification's title is a pathway's title, so notifications are *not* emailed) or a password, and a welcome email is only a link to confirm the address. Customers should tell staff in their staff privacy notice ([privacy-notice-staff-template.md](privacy-notice-staff-template.md)).

### Risk 3: oversharing in free text

| Measure | Status | Where |
|---|---|---|
| Staff guidance: write only what's needed for the patient's care; no other people's details | Customer and supplier guidance; planned in-app guidance in step 7 | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| No free text or patient identifiers in **monitoring** notifications: a fixed title, and a message of usernames, priority, template and stage names and dates | In place | `MedApp.Application/Monitoring/Common/MonitoringNotifications.cs` |
| No free text or patient identifiers in **appointment** notifications: a fixed title ("Appointment booked", "Appointment moved", "Appointment cancelled" or "Appointment recorded"), and a message of who and when (and, for one recorded after the event, whether the patient attended). **Day closed** notifications (to staff with something on a day an admin closes): a fixed title, the day, the closed day's admin-written name, and how many appointments, blocks and meetings they have then, never which patients. **Stage visit withdrawn** notifications (to a pathway's assignees and the admins when a finished stage's only visit is withdrawn as entered in error): a fixed title, and a message of who, the visit's time, the stage's name and when it was finished, never the patient, the pathway's title or the reason | In place (on `feature/staff-calendars`) | `MedApp.Application/Appointments/Common/AppointmentNotifications.cs`; `docs/calendar-plan.md`, decision 20 |
| No patient identifiers in **assignment** notifications; pathway titles reviewed as not identifying | Planned, step 7 | `AssignPatientPathwayHandler.cs` writes the title today |
| **Meeting** notifications: a fixed title ("Meeting invitation", "Meeting moved", "Meeting cancelled" or, to the organiser, "Meeting declined"), and a message of who, the meeting's title and when; never its notes. A reply records only accepted or declined, never a reason, so it holds no free text. The title is free text an admin writes, so it's treated like a template's name: the dialog says not to put patient details in it (risk 25) | In place | `MedApp.Application/Calendar/Common/MeetingNotifications.cs`; `MedApp.Web/src/components/calendar/MeetingDialog.tsx` |
| Appointment notes and cancellation reasons have length limits (2,000 and 500 characters), and are never shown to staff on the patient timeline (only to admins) | In place on `feature/staff-calendars` | `docs/calendar-plan.md`, decisions 15, 16 and 27 |
| Staff guidance for appointment notes: write only what the visit needs; nothing about other people; a cancellation reason says why, not more | Customer guidance | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Encrypt Valkey traffic, or send only IDs through it | Planned, step 2 or 5 | `docs/reviews/signalr-backplane.md` |
| File names hidden from staff on the patient's timeline ("a file was attached"); only admins see them, in the timeline and the audit log | In place | `MedApp.Application/Pathways/Timeline/Common/TimelineMapper.cs` |
| **Deleting notes:** a deletion (an admin's) and a request to delete (a member of staff's) each give a reason, a fixed choice (wrong patient, written in error, duplicate, other), and an optional detail, free text of 500 characters at most (required for "other"), which can say why a note was wrong, naming another patient. Only admins read them: in the request's dialog, the audit log, the admins' timeline and the export. Staff see only that a note has a request waiting ("Deletion requested"), or on the timeline that a note was deleted, by whom and when. The notifications have fixed titles ("Note deletion requested" to admins, "Note deleted" or "Note kept" to the requester, and "Your note was deleted" to a deleted note's author) and messages of usernames only: never the note, the reason or the patient | In place (`feature/note-deletion`) | `NoteDeletionNotifications`, `NoteDeletionRequestMapper`, `TimelineMapper`, `TimelineStaffCanaryTests` |
| Staff guidance: name a file for what it is, not who it's about | Template | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |

**Residual risk:** now **high**; after **medium**. Free text can't be made risk-free; it relies on training.

### Risk 4: copies of clinical text in the audit

| Measure | Status | Where |
|---|---|---|
| The full audit trail is admin-only and kept with the record; staff see a per-patient timeline without its free text, including appointments' | In place | As risk 1 |
| Anonymising a patient replaces the copies of notes, titles and reasons in their audit entries (and so on the timeline) with placeholders, and does the same for their appointments' notes and cancellation reasons and the copies in the appointment history | In place (step 7; appointments on `feature/staff-calendars`) | `MedApp.Application/Patients/Common/PatientAnonymiser.cs` |
| **Pathway notes:** several per pathway, each with every version in its own table (`PatientPathwayNoteVersion`: text, who, when); the audit's `NoteAdded` and `NoteEdited` entries hold the note's ID and version, never its text, so correction, retention and erasure of pathway notes apply in one place. Staff see on the timeline that a note was added or edited, by whom and when; admins see the text, read from the versions. The export lists every version; anonymising replaces each one's text. The old single notes field's text was moved into each pathway's first note (its old column is kept, unused, for one release, and anonymising clears it too) | In place (`feature/procedure-notes`) | `PathwayChange.AddNote`/`EditNote`, `PatientRecordRepository`, `PatientAnonymiser`, `TimelineMapper`, `TimelineStaffCanaryTests`, migration `AddedProcedureNotes` |
| **Deleting a pathway note is soft, so its text is still held:** an admin deletes a note with a reason (or approves a member of staff's request), and it's hidden from the pathway page for everyone, but its row and every version stay, with who deleted it, when and why, so the record still accounts for it (clinical record integrity). The reason is a fixed choice (wrong patient, written in error, duplicate, other) with an optional free-text detail. The audit's `NoteDeleted` entry holds the note's ID, version, the reason and the detail, never its text. The export lists deleted notes as deleted, and every request with its reason and outcome; anonymising replaces the note text, the deletion details and the requests' details, and closes waiting requests. **A note deleted as "wrong patient" is the other patient's data:** its text stays on this record, visible to admins, until this record is anonymised; it's in this patient's export, flagged for the clinician reviewing the export to hold back under the third-party and serious harm tests; and the right patient's export, rectification and anonymisation never find it, as everything is found by the patient it's filed under (a "move to the right patient" action isn't built). The author is told their note was deleted, so they can add it to the right record. Purging deleted text after a retention window isn't built | In place (`feature/note-deletion`); purging planned (`docs/todo.md`) | `PathwayChange.DeleteNote`, `PathwayNoteDeletionRequest`, `PatientRecordRepository`, `PatientExportMapper`, `PatientAnonymiser` |
| Do the same for stage notes, and for appointment notes and their history's copies | Planned, step 7. **Before go-live** | |

**Residual risk:** now **high** (pathway notes are done; stage and appointment notes are still copied); after **low**.

### Risk 5: kept for ever

| Measure | Status | Where |
|---|---|---|
| A retention schedule | Draft | [retention-schedule.md](retention-schedule.md) |
| A due-for-review list from configurable periods (adults 8 years, children to 25 or 26), where an admin keeps a record with a reason and a review date, or anonymises it; nothing is deleted automatically | In place (step 7) | `MedApp.Application/Patients/Common/RetentionSchedule.cs`; `MedApp.Web/src/pages/retention/RetentionReviewPage.tsx` |
| The operational purge (`operational-purge`, a scheduled job run once across the API copies under a lease in SQL Server): sessions and sign-in and security events after 12 months, notifications after 6, notepads unchanged for 30 days, expired sign-in codes after a day; counts logged, never content; patient records never touched | **Built, as a dry run** (`Retention:Operational:DryRun`): it counts and deletes nothing until the DPO confirms the periods and the deploying organisation turns it off. **Before go-live** | `MedApp.Application/Jobs/OperationalPurge`; [retention-schedule.md](retention-schedule.md) |
| The rest of the retention engine: retention classes per template, destruction, a destruction log, legal hold; short fixed periods for staff calendar blocks and meetings; appointments kept and destroyed with the patient's record | Planned, step 7. **Before go-live** (sessions and notifications at least; clinical records won't reach their minimum period for years) | [retention-schedule.md](retention-schedule.md) |
| A purge job that deletes a deleted file's contents (and thumbnail) after a window, proposed as 30 days after deletion for the DPO to decide; files destroyed with the rest of the record by the retention engine. Until it exists, the contents of deleted files, and of an anonymised patient's files, stay in storage | Planned (`docs/todo.md`), with step 7's retention engine. **Before go-live** | [retention-schedule.md](retention-schedule.md) |
| Backups expire on a fixed cycle | Planned, step 5 | |

**Residual risk:** now **high**; after **low**.

### Risk 6: rights

| Measure | Status | Where |
|---|---|---|
| Staff can view and correct patients and pathways | In place | |
| An interim, documented manual procedure for exports, run by the supplier on the customer's written instruction | Procedure written | [data-subject-rights.md](data-subject-rights.md) |
| A subject access export of everything held (details, pathways, stages, notes, assignments, the pathway history, appointments and their history, the access log, notifications) as JSON and a printable page with the same content, itself recorded; staff appear by username only, without their account IDs, network addresses or read times | In place (step 7; appointments on `feature/staff-calendars`) | `GET api/patients/{id}/export`; `MedApp.Web/src/pages/patients/PatientExportPage.tsx` |
| The export lists every note on the patient's pathways, deleted ones included (when, by whom and why), and every request from staff to delete one, with its reason, status and who decided it, by username | In place (`feature/note-deletion`) | `PatientExportMapper`; `ExportedNoteDeletionRequestResponse` |
| The export lists the patient's attached files, deleted ones included: name, type, size, SHA-256, scan state, what each is attached to, and who uploaded and deleted it, by username | In place | `PatientExportMapper` |
| The files themselves in the export | Planned (`docs/todo.md`). **Before go-live** | |
| Restricted (no changes, with a banner) and disputed (a banner) flags, set by admins with a reason; anonymisation with a typed confirmation and a reason, refused while pathways are under way or appointments are open. A restricted patient's appointments can't be booked, rescheduled or given an outcome either, only cancelled; an anonymised patient's can't be changed | In place (step 7; appointments on `feature/staff-calendars`) | `SetPatientFlagHandler`, `PatientRules`, `AnonymisePatientHandler` |
| On a restricted or anonymised record, files can't be added or deleted, on the patient or on their pathways and stages, by anyone, admins included (409, the restriction guard). An upload is checked again once it's staged, just before it's stored: the target still exists, the record isn't restricted or anonymised, and the stage or pathway still takes files. That narrows the window in which a record that changed during a slow upload could still take the file to milliseconds; closing it fully would need the save to re-read the record in the same transaction | In place | `PatientRules`; `UploadAttachmentHandler`, `DeleteAttachmentHandler` |
| Anonymisation covers files: each file's name becomes "Anonymised file" with its type's usual extension, file names in the pathway audit log's details are replaced with the removed-text placeholder, and every live file is deleted (soft, by the anonymising admin). **The files' contents stay in storage until the purge job removes them** (risk 5), so until then an anonymised record still has its documents, out of the app's reach but not erased | In place; the contents' removal planned with the purge job | `PatientAnonymiser` |
| A clinician review step in the export for the serious harm test; archiving in place of deletion; restriction hiding the patient from lists | Planned, step 7 | |

**Residual risk:** now **low** (the clinician review is done by hand before an export is given out); after **low**.

### Risk 7: lost or untraced changes to patients and accounts

| Measure | Status | Where |
|---|---|---|
| A patient with pathways can't be deleted | In place | `MedApp.Application/Patients/Commands/DeletePatient/DeletePatientHandler.cs` |
| Deleting a file is soft and audited (who and when, kept on the file for admins); a patient or pathway with files can't be deleted until the files are | In place | `DeleteAttachmentHandler.cs`; `DeletePatientHandler.cs`; `DeletePatientPathwayHandler.cs` |
| Deleting a user needs the admin's own password | In place | `AuthController.cs` |
| Make patient deletion admin-only | Planned, step 2. **Before go-live** | |
| Record patient creates, edits (the fields changed; not their values, so the log holds no patient details) and deletes, and account, password and role changes, in the activity log, with who and from which address; a row version on each patient, so a save from an older copy is refused rather than silently undoing a later change (such as a restriction or an anonymisation) | In place (step 7) | `CreatePatientHandler`, `UpdatePatientHandler`, `DeletePatientHandler`; the account and role handlers in `MedApp.Application/Authentication` |
| Keep the old values of an edit in a version table; replace deletion with archiving | Planned, step 7 | |
| Backups to recover from a mistaken delete | In place in design (7 days); 35 days planned, step 5 | `deploy/azure/main.bicep` |

**Residual risk:** now **medium** (every change is recorded with who and when, though an old value can't be recovered from the log); after **low**.

### Risk 8: wrong patient

| Measure | Status | Where |
|---|---|---|
| Patient name and date of birth shown on pathway pages | In place | |
| Each file's list says where it's attached (the patient, a pathway or a stage), who uploaded it and when; the upload is audited and shown on the patient's timeline | In place | `MedApp.Web/src/components/attachments/Attachments.tsx`; `AttachmentAudit.cs` |
| A file put on the wrong record can be deleted at once by whoever uploaded it, or by an admin, so it stops being shown and can't be downloaded | In place | `DeleteAttachmentHandler.cs` |
| A note put on the wrong record can be deleted by an admin, with a reason, after a confirmation that shows the patient (as the banner does), the pathway and the note; staff ask an admin to, and every admin is told. Deleting is soft: the note stops being shown on the pathway page, and is kept for the record (risk 4). Not while the record is restricted or anonymised. Hazard H-38 covers deleting the wrong note | In place (`feature/note-deletion`) | `DeletePathwayNoteHandler`, `DecideNoteDeletionRequestHandler`; `docs/clinical-safety/hazard-log.md` |
| A patient banner (`SURNAME, Forename`, date of birth and age, NHS number) under the header of every page about one patient (the patient, Edit Patient, export, pathways, pathway, Edit Pathway and New Pathway pages), in view on all but the printable export, so staff can check the patient before writing notes or attaching files | Built (branch `feature/nhs-integrations`); to be verified by the Clinical Safety Officer. **Before go-live** | `MedApp.Web/src/components/patients/PatientBanner.tsx` |
| The patient and the target named before files are sent ("Attach 3 files to … for {patient}?") | Proposed by the hazard log (H-23). **Before go-live** | `docs/clinical-safety/hazard-log.md` |
| An optional NHS number, checked with its check digit; two patients can't have the same one | In place (branch `feature/nhs-integrations`) | `NhsNumbers`, `NhsNumberRules`; `IX_Patients_NhsNumber` |
| The date of birth in task and pathway lists; the patient named in the pathway page's title and in the Save Notes and Finish Stage confirmations | Proposed by the hazard log (H-01, H-02). **Before go-live** | `docs/clinical-safety/hazard-log.md` |
| Duplicate checks beyond an exact NHS number (same name and date of birth, near matches), and tracing NHS numbers with PDS | Planned, step 9 | |
| Clinical safety hazard log | Drafted (step 6) | `docs/clinical-safety/hazard-log.md` |

**Residual risk:** now **high**, until the banner is merged and verified; then **medium**; after step 9's duplicate checks and tracing **low**.

### Risk 9: lost updates

| Measure | Status | Where |
|---|---|---|
| Both texts kept in the audit (stage notes), and every version of a pathway note in its own table | In place | `PathwayChange.cs` |
| Optimistic concurrency with a 409 on conflict: **pathway notes** (each edit names the version it started from, and the note has a row version), patients and appointments | In place for those; stage notes planned, step 6 | `EditPathwayNoteHandler`, `PatientPathwayNote.RowVersion` |
| Only a note's author can edit it, so two people never edit one note | In place | `EditPathwayNoteHandler` |

**Residual risk:** now **medium**; after **low**.

### Risk 10: shared and unattended computers

| Measure | Status | Where |
|---|---|---|
| Rendered pages sent with `Cache-Control: no-store`; the password responses and attached files too | In place | `MedApp.API/ServerRendering/SsrPages.cs`; `AuthController.cs`; `MedApp.API/Attachments/AttachmentResponseHeaders.cs` |
| Signing out clears the query cache; the session cookie is HttpOnly and expires with the session (8 hours, extendable to 24 at most from sign-in) | In place | `MedApp.Web/src/auth/auth.ts` |
| An idle timeout (30 minutes to start) with a warning | Planned, step 2. **Before go-live** | |
| Clear the app's `localStorage` and `sessionStorage` at sign-out, but for the purely visual settings; the account keeps the rest for the next sign-in | In place (step 7) | `MedApp.Web/src/app/settingsStore.ts` (`clearStorageAtSignOut`) |
| Keep identifying filters (name, email, date of birth, and now the NHS number) out of the URL | **Not planned:** the product owner decided on 16 November 2026 to keep list filters in the URL, because shareable list links are a feature. The customer measures below carry this part of the risk | `MedApp.Web/src/pages/listSearch.ts` |
| Staff rules: sign out and lock the screen; don't search by patient name on a shared computer where others use the same browser profile; delete downloaded files once read, and don't keep them on shared computers | Template | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Customer measures: screen locks, no shared logins, separate browser profiles or history cleared on shared computers; downloads folders cleared at sign-out on shared computers, by device policy | The customer's | |

**Residual risk:** now **high** (no idle timeout yet); after **medium**, because patient searches stay in the browser's history unless the customer's measures (separate profiles, cleared history) are followed.

### Risk 11: web attacks

| Measure | Status | Where |
|---|---|---|
| React escapes output; rich text is shown with a Markdown renderer that never runs raw HTML and neutralises unsafe links | In place | `MedApp.Web/src/components/shared/RichText.tsx` |
| One origin, no CORS; `SameSite=Lax` cookie; JSON bodies | In place | `SessionCookieService.cs` |
| HSTS and https redirection outside development | In place | `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs` |
| No open proxy in server rendering; blocked paths; Node's environment limited to an allowlist | In place (PR #51) | `MedApp.API/ServerRendering/` |
| Uploaded files never shown or run on the app's origin: always downloads, with a sandboxing CSP; SVG and HTML refused (risk 22) | In place | `MedApp.API/Attachments/AttachmentResponseHeaders.cs`; `FileTypeSniffer.cs` |
| CSP with nonces, `frame-ancestors 'none'`, `nosniff`, `Referrer-Policy`, `no-store` on every `/api` response, a `__Host-` cookie name | In place | `MedApp.API/Middleware/SecurityHeaders.cs`, `SessionCookieService.cs`; `docs/security/asvs.md` |
| An ASVS Level 2 review, a ZAP baseline scan, and a penetration test | Planned, steps 2, 7 and 10. **Before go-live** (ASVS and a first test, if an NHS pilot) | |

**Residual risk:** now **high**; after **low**.

### Risk 12: infrastructure compromise

| Measure | Status | Where |
|---|---|---|
| Azure SQL with public access off, a private endpoint, TLS 1.2 minimum, encryption at rest (TDE with Microsoft's keys), and a delete lock | In place in design | `deploy/azure/main.bicep` |
| The files' storage account with public network access off, a private endpoint, https only and TLS 1.2 minimum, no anonymous access, encryption at rest with Microsoft's keys, and a delete lock; files reached only through the API, never by a direct link. On kind, Azurite reachable only from the API | In place in design (Azure); in place (kind) | `deploy/azure/main.bicep`; `deploy/helm/medapp/templates/networkpolicy.yaml` |
| Default-deny network policies for MedApp's own pods: only the ingress reaches the API, and MedApp's pods reach only what they need. On kind, only the API and the migrations Job reach the in-cluster database. **On Azure, other pods in the cluster (Traefik, Argo CD, cert-manager, External Secrets) aren't restricted yet, and can open a connection to the database's and the storage account's private endpoints** (they'd still need the password or the account key) | In place, with the Azure gap | `deploy/helm/medapp/templates/networkpolicy.yaml`; `deploy/azure/values.yaml` |
| A cluster-wide Cilium policy that denies traffic to the private endpoint subnet from every pod except MedApp's API and migrations Job | Planned, step 5. **Before go-live** | |
| The `restricted` Pod Security Standard, non-root containers, read-only filesystems, no Kubernetes API tokens | In place | `deploy/helm/medapp/templates/_helpers.tpl` |
| Secrets from Key Vault through External Secrets, never in git; Entra ID-only cluster access; OIDC and workload identity, no stored cloud secrets | In place in design | `deploy/helm/medapp/templates/externalsecrets.yaml`; `deploy/azure/` |
| A passwordless, least-privilege database identity for the app, a separate one for migrations, then Entra-only authentication | Planned, step 5. **Before go-live** | `deploy/azure/README.md`, "Not yet" |
| Passwordless storage access: workload identity with a role on the files' container only, then shared key access turned off | Planned, step 5. **Before go-live** | `deploy/azure/README.md`, "Not yet" |
| A customer-managed key for the files' storage account | Planned, step 9 | |
| Private endpoints for Key Vault and the registry; a restricted AKS API server; Defender for Cloud; a customer-managed TDE key | Planned, step 5 | |
| Move the page server into a container of its own, without the API's secrets | Done in Kubernetes (PR #83) | `deploy/helm/medapp/templates/api.yaml` (`pages`) |
| Hash session tokens | Planned, step 2 | |

**Residual risk:** now **high**; after **low**.

### Risk 13: undetected breach

| Measure | Status | Where |
|---|---|---|
| OpenTelemetry built in, ready to export | In place | `MedApp.ServiceDefaults` |
| Application Insights and Log Analytics in the UK; alerts on errors, sign-in failures, lockouts, database health and backups; a tamper-evident audit (ledger tables) streamed to the log workspace | Planned, steps 5 and 7. **Before go-live** | |
| A breach procedure and a tabletop exercise | Draft procedure | [breach-response.md](breach-response.md) |

**Residual risk:** now **high**; after **low to medium** (one maintainer limits round-the-clock response unless a monitoring service is contracted; step 5).

### Risk 14: loss of data or service

| Measure | Status | Where |
|---|---|---|
| Two API copies with a disruption budget; migrations that stop a bad rollout | In place | `deploy/helm/medapp/templates/api.yaml`, `migrations-job.yaml` |
| Azure SQL point-in-time restore (7 days) | In place in design | `deploy/azure/main.bicep` |
| Files: zone-redundant storage, 7 days' soft delete for files and the container, and a delete lock on the account; no versioning or other backup | In place in design | `deploy/azure/main.bicep` |
| A hostile file can't exhaust an API copy's memory: no thumbnail (the upload still succeeds, without a preview) when the source image is over 24 megapixels or 10,000 pixels on a side, read from its header before decoding; at most two thumbnails or Word inspections at a time per API copy; a `.docx` whose table of contents lists more than 5,000 entries refused before the archive is opened; 20 MiB per request | In place | `SkiaThumbnailGenerator`; `FileTypeSniffer` |
| When file storage fails, uploads and downloads fail within bounds (2 retries, 30 seconds a try, 10 seconds to clean up a failed upload) with a 503, "File storage isn't available", while the rest of MedApp works | In place | `BlobFileStore`; `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs` |
| Vaulted backups of the files (Azure Backup), restored with the database in drills, so files and their details come back together | Planned, step 5. **Before go-live** | `deploy/azure/README.md`, "Not yet" |
| 35-day point-in-time restore, geo-redundant backups within the UK, long-term retention, quarterly restore drills, recovery objectives (RPO 15 minutes, RTO 4 hours), runbooks | Planned, step 5. **Before go-live** | |
| A downtime procedure for customers | Planned, step 6 | |

**Residual risk:** now **high**; after **low**.

### Risk 15: supply chain

| Measure | Status | Where |
|---|---|---|
| Required CI checks on `main`; a written review of every merge; the GitOps deploy limited by an Argo CD project | In place | `.github/workflows/main.yml`; `deploy/argocd/project.yaml` |
| Actions pinned by commit SHA; Dependabot; CodeQL, dependency review and image scanning; SBOM and provenance attestations on the published image | In place | `.github/workflows`, `.github/dependabot.yml` |
| Images pinned by digest; attestations verified before deploy; a required reviewer before production | Planned, steps 2 and 5. **Before go-live** | |
| Make the repository and image private | Planned, step 1 | |

**Residual risk:** now **high**; after **low to medium**.

### Risk 16: supplier staff access

| Measure | Status | Where |
|---|---|---|
| A support access policy: access only on the customer's instruction, named people, MFA, confidentiality clauses, no patient data in tickets, laptops or AI tools, an access log, the customer told | Policy written | [support-access-policy.md](support-access-policy.md) |
| Just-in-time admin access (Entra ID Privileged Identity Management), break-glass accounts, access logs streamed to the workspace | Planned, step 5 | |
| Annual data security training and vetting | Planned, step 3 | [security-policies.md](security-policies.md) |

**Residual risk:** now **high** (policy without technical enforcement); after **low**.

### Risk 17: sub-processor and transfer

| Measure | Status | Where |
|---|---|---|
| UK South region; backups in the UK | In place in design (`deploy.sh` defaults to `uksouth`) | `deploy/azure/deploy.sh` |
| Microsoft's data protection terms; an Azure Policy that allows only UK regions; Customer Lockbox where available | Terms to be checked; policy and Lockbox planned, step 5 | [international-transfers.md](international-transfers.md) |

**Residual risk:** **low**.

### Risk 18: mixing customers' data

| Measure | Status | Where |
|---|---|---|
| One deployment and database per customer | In place (the chart deploys one organisation per namespace) | `deploy/helm/medapp` |
| Query filters, row-level security and a cross-tenant test suite | Planned, step 8. **Before any shared deployment** | |

**Residual risk:** **low** today; to reassess at step 8.

### Risk 19: transparency

| Measure | Status | Where |
|---|---|---|
| Template privacy notices for patients and staff; a cookies and storage statement | Draft | [privacy-notice-patients-template.md](privacy-notice-patients-template.md); [cookies-and-storage.md](cookies-and-storage.md) |

**Residual risk:** **low**.

### Risk 20: real data in development

| Measure | Status | Where |
|---|---|---|
| Synthetic data only in code, tests, seeds and screenshots; `medapp-dev` runs only against a local database | In place (convention and tool) | `AGENTS.md`; `tools/MedApp.DevMcp` |
| No production data copied to any other environment; a restored backup for a drill stays inside Azure UK, is deleted after the drill, and never reaches a developer or AI tool | Policy | [support-access-policy.md](support-access-policy.md) |

**Residual risk:** **low**.

### Risk 21: the NHS number

| Measure | Status | Where |
|---|---|---|
| Optional: a customer that doesn't need it leaves it empty | In place (branch `feature/nhs-integrations`) | `MedApp.Domain/Patients/Patient.cs` |
| Checked with its Modulus 11 check digit on the server and in the form, and unique across patients, so a mistyped or duplicated number is refused | In place | `NhsNumberRules`; the filtered unique index `IX_Patients_NhsNumber` |
| Never in the activity log (field and filter names only) or in notifications; included in the subject access export; removed by anonymisation | In place | `UpdatePatientHandler`, `SearchPatientsHandler`; `PatientExportMapper`; `PatientAnonymiser` |
| Placeholders that pass the check digit (ten of one digit, `0123456789`, `9876543210`) refused on the server and in the form | In place | `NhsNumbers.IsPlaceholder`, `NhsNumberRules` |
| Kept out of the error log: EF Core's failed-save and failed-command events, whose SQL Server message can hold a duplicate number, are logged at Debug, which isn't written | In place | `LogFailedSavesAtDebug` (`MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs`) |
| **Saved searches can hold the number** (the patient list's NHS number filter), in the account's settings and the browser, until the user deletes them; anonymisation doesn't remove them, as it doesn't remove saved names | Gap: planned (`docs/todo.md`, saved searches removed on anonymisation) | `UserSettings.Json`, `medapp.favourites` |
| The FHIR API: read-only, staff only, `no-store`, every use logged; it reports the number as "not traced", so a receiving system doesn't treat it as verified; an anonymised record goes out as `active: false` with no name, email or full date of birth, and a restricted or disputed one with a `meta.tag` | In place | `FhirController`, `FhirPatientMapper` |
| Synthetic 999 numbers only in tests, demo data and screenshots | In place (convention and tool) | `AGENTS.md`; `medapp-dev` |
| Always Encrypted (deterministic) for the number, so database administrators and backups don't see it in clear | Planned, step 7 (`docs/todo.md`) | |
| Tracing numbers with PDS, which needs NHS England's onboarding; OAuth clients with their own scopes for systems that call the FHIR API | Planned, step 9 | |
| Keep the number out of URLs, which request logs and traces can record: today it's in the patient list's filter and the FHIR search's query string (the demographics lookup is already a POST, with the number in its body). FHIR's `POST Patient/_search`; and checking what the ingress and telemetry record before logs are exported | Planned (`docs/todo.md`); step 5 for the logs | |
| Customer measures: check the number against the patient's own documents or the organisation's PAS when it's entered | The customer's | |

**Residual risk:** now **medium**; after **low**.

### Risk 22: malicious files

| Measure | Status | Where |
|---|---|---|
| A type allow-list found from each file's contents, never its name or the browser's type, and the name's extension must match: PDF, PNG, JPEG, HEIC/HEIF, Word `.docx` and plain UTF-8 text. Macro-enabled Word, SVG and HTML refused; anything else refused; at most 20 MB | In place | `MedApp.Application/Attachments/Common/FileTypeSniffer.cs`, `UploadAttachmentValidator.cs` |
| **Active content refused in the allowed types:** a `.docx` with any external relationship (`TargetMode="External"` in a `.rels` part: a remote template, an external link), embedded objects (`word/embeddings/`) or ActiveX (`word/activeX/`); a PDF containing `/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile` or `/RichMedia` (a byte search, so a harmless file that mentions one is refused too). Everything else, such as an exploit in an image or a PDF's own format, is left to the malware scanner | In place | `FileTypeSniffer.cs` |
| Files never shown or run in the app: always sent as a download (`Content-Disposition: attachment`), with the sniffed type, `nosniff`, `Content-Security-Policy: sandbox; default-src 'none'`, `Cross-Origin-Resource-Policy: same-origin` and `no-store`; only thumbnails the server re-encodes are shown in pages | In place | `MedApp.API/Attachments/AttachmentResponseHeaders.cs`; `MedApp.Infrastructure/Attachments/SkiaThumbnailGenerator.cs` |
| "Not scanned" shown on every file, so staff know | In place | `MedApp.Web/src/components/attachments/Attachments.tsx` |
| Malware scanning on upload, refusing infected files: Microsoft Defender for Storage on Azure, behind the existing `IMalwareScanner` | Planned, step 9. **Before go-live**, brought forward; or the deployment runs without file storage, which turns uploads off | `deploy/azure/README.md`, "Not yet" |
| Customer measures: up-to-date anti-malware, PDF readers and Office (with Protected View for downloaded files) on every computer that opens files; macros off by policy | The customer's | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |

**Residual risk:** now **high**; after scanning **medium** (scanning finds known malware, not all of it); **low to medium** with the customer's endpoint protection.

### Risk 23: files hold more than staff see

| Measure | Status | Where |
|---|---|---|
| Thumbnails re-encoded, which strips their metadata | In place | `SkiaThumbnailGenerator.cs` |
| File names cleaned (no folders or control characters, at most 255 characters) and hidden from staff on the timeline | In place | `AttachmentFileName.cs`; `TimelineMapper.cs` |
| Staff guidance: upload only what the care needs; name a file for what it is, not who it's about | Template | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Strip location metadata from photos at upload. A decision for the DPO and the CSO, because the original may be evidence | Proposed, step 7 | |
| The export lists files with their names; anonymisation replaces file names (in the audit's details too), deletes the files and records an audit entry for each, with the name already scrubbed (their contents wait for the purge job, risk 5); the files themselves aren't in the export yet (risk 6) | In place, with the gaps in risks 5 and 6 | `PatientExportMapper`; `PatientAnonymiser` |

**Residual risk:** now **medium**; after **low**.

### Risk 24: monitoring of staff

| Measure | Status | Where |
|---|---|---|
| Rules only for holders of `ManageMonitoring` (Admin and the starting Manager role), and only while they hold it: a rule whose owner has lost it sends nothing | In place | `MedApp.API/Controllers/MonitoringController.cs`; `PathwayMonitor` checks the permission on every change |
| Each admin sees and changes only their own rules (another admin's is a 404) | In place | `MonitoringRuleRepository.GetAsync`, the handlers in `MedApp.Application/Monitoring` |
| Only workflow actions already in the audit trail can be watched; no reads, searches or sign-ins | In place | `MonitoredActivity` |
| Notifications carry no free text and no patient identifiers; nobody is told about their own action; one notification per admin per change | In place | `MonitoringNotifications`, `MonitoringRuleMatcher` |
| A patient's export leaves monitoring notifications out (they're the staff's data); a staff member's subject access request includes the rules and notifications that name them | In place (export); manual procedure (staff) | `PatientExportMapper`; [data-subject-rights.md](data-subject-rights.md) |
| At most 50 rules per admin | In place | `MonitoringRuleInput.MaxRulesPerAdmin` |
| Staff told that admins can be notified of their actions | Draft | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| The customer's monitoring policy: why admins use it, who may, for how long, and how staff can ask | The customer's | [privacy-notice-staff-template.md](privacy-notice-staff-template.md), notes for customers |
| Audit who adds, changes and deletes rules, and let a staff member (or the DPO) see the rules that name them | Proposed (`docs/todo.md`) | |
| Remove an account's rules, and its notification choices, when the account is deleted | Proposed (`docs/todo.md`) | |

**Residual risk:** now **medium** (it relies on the customer's policy); after the proposed audit and visibility **low**.

### Risk 25: staff calendars reveal more than needed about staff

| Measure | Status | Where |
|---|---|---|
| Blocked time records an activity from an admin-managed list (Lunch, Admin time, Training, Meeting, Annual leave, Travel), not a reason; customers can name activities generically ("Away") | In place on `feature/staff-calendars` | `docs/calendar-plan.md`, decision 4 |
| A block's note is optional and short (200 characters), and the UI tells staff not to put patient details in it | In place on `feature/staff-calendars` | `docs/calendar-plan.md`, decision 5 |
| Meetings are arranged and changed only by admins; their title (100 characters) and notes (500) are free text, and the dialog says not to put patient details in either | In place | `MeetingsController`; `MeetingDialog.tsx` |
| **A meeting's title is copied** into its attendees' notifications ("invited you to 'Team huddle', ..."), into the 409 anyone gets when booking over it, and into the busy times admins see when choosing a time. The notification copies stay after the meeting is cancelled, until notifications are purged (not built yet), and a title that named a patient wouldn't be found by that patient's export or anonymisation, since it isn't linked to them | Accepted, relying on the warning and admins' care | `MeetingNotifications.cs`, `CalendarClashes.cs`; [retention-schedule.md](retention-schedule.md) |
| Only a calendar's owner or an admin can add, change or remove its blocks | In place on `feature/staff-calendars` | `docs/calendar-plan.md`, decision 7 |
| Staff guidance: give no reason for time away, or a generic one; never health or family details, and never a patient | Customer guidance | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Staff told that colleagues can see their calendar, including annual leave | Customer's staff notice | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Blocks deleted after a fixed period | Planned, step 7 | [retention-schedule.md](retention-schedule.md), section 3 |

**Residual risk:** **medium** now; **low** after guidance and retention. Free text can't be made risk-free.

### Risk 26: patient details in a notepad

| Measure | Status | Where |
|---|---|---|
| **The notepad's text can't be found by a patient's subject access export or removed by their anonymisation:** it isn't linked to any patient. This is accepted, with the controls below, because the notepad is for passing jottings, not records | Accepted (product owner's decision; for the DPO to confirm) | `PatientRecordRepository` (doesn't read notepads) |
| **The whole notepad is cleared once it has gone 30 days without any change** (`Notepad:KeepDays`), when next read or saved; deleted with the account; the owner can clear it at any time. **This doesn't age what's in it:** each change restarts the 30 days for all of it, so a notepad used every day is never cleared, and a jotting stays until its owner takes it out | In place | `GetNotepadHandler`, `SaveNotepadHandler`, `NotepadConfiguration` (cascade); [retention-schedule.md](retention-schedule.md) |
| **Add note to pathway** moves what matters into the record: the text, or the part selected, becomes a note on the pathway (so it's in the patient's export and anonymisation from then on), after a confirmation showing the patient; "Remove it from the notepad" starts ticked and takes out exactly what was added, so it doesn't stay in the notepad too (or says it couldn't) | In place | `AddToPathwayDialog.tsx`, `NotepadPane.tsx`; hazard H-37 |
| ~~The notepad says what it's for, and how long it keeps text~~ (its explanation, "For jottings. Anything about a patient belongs in their record: ...", and its prompt, "Jot something down") | **Removed** on 17 November 2026, by the product owner's decision (branch `feature/notepad-window`). The staff privacy notice still tells staff what the notepad is for and how long it keeps text, and Add note to pathway still moves what matters into the record | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Kept on the server only, never in the browser (only whether it's open is, in `localStorage`), so a shared computer holds none of it after sign-out | In place | [cookies-and-storage.md](cookies-and-storage.md) |
| **The notepad in a half view** (`/notepad` beside a page, from the side nav) is the same notepad, read and saved on the server, inside the signed-in page, so it goes with it on sign-out. Its Add note to pathway offers only the pathway page beside it, with the same confirmation. **There's no separate notepad window:** one was tried on this branch and removed before merging (product owner's decision), because it outlived signing out, keeping the notepad's text and patients' details on screen | In place | `NotepadPage.tsx`, `NotepadContext.tsx` |
| Only its owner can read or change it (`api/me/notepad`, the signed-in user's own); not shown to admins | In place | `NotepadController` |
| Staff guidance: don't keep patient details in the notepad; add them to the record, or clear them | Customer guidance | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| A scheduled purge of notepads untouched for 30 days, so one nobody opens again doesn't wait for its account to be deleted. It doesn't reach a notepad in use (each batch's delete checks the date in the same statement) | **Built, as a dry run** until the DPO confirms the operational periods (`Retention:Operational:DryRun`) | `OperationalPurgeJob` |
| **Age the text, not the notepad:** record when each part was written, and after 30 days ask its owner to add it to a record or clear it (or clear it, after warning), so a notepad in daily use doesn't keep a jotting indefinitely | Planned (`docs/todo.md`) | |
| A step in the rights procedure: for a subject access or erasure request, an admin asks staff (or searches notepads) for the patient's name and NHS number | For the DPO to decide | [rights-procedure.md](rights-procedure.md) |

**Residual risk:** **medium** now, and still **medium** with the scheduled purge, which only clears notepads nobody uses: in daily use, a jotting stays until its owner takes it out. **Low** once text is aged by when it was written (planned). Removing the notepad's explanation (17 November 2026) leaves this unchanged: it was information only, the weakest kind of control, and the staff privacy notice still says the same; but staff who haven't read the notice are no longer reminded in the notepad itself. For the DPO to confirm.

### Risk 33: automated processing nobody watches

Built in PR 19 (the job runner and the operational purge). Scheduled jobs (today only the operational purge) delete data with nobody at the keyboard. A job that deletes too much, or stops and leaves data kept longer than promised, could go unnoticed.

| Measure | Status | Where |
|---|---|---|
| Each job is named, scheduled and shown on Administration, Scheduled Jobs (last start, finish, outcome, count, failures in a row, the error's type), with Run now | In place | `/admin/jobs`; `GET api/jobs` |
| Exactly one copy of the API runs a job at a time: a lease in SQL Server, taken in one statement | In place | `ScheduledJobRunner`, `ScheduledJobRepository` |
| Jobs log counts only, never patient details; a failure stores and logs the exception's type, never its message | In place | `ScheduledJobRunner` |
| Holders of `ManageSetup` are told after three failures in a row (once, until it succeeds); `/health` is Degraded while a job is two intervals overdue | In place | `ScheduledJobNotifications`; `ScheduledJobsHealthCheck` |
| Patient records are never deleted by a job; the purge's list of what it deletes is fixed in code and tested | In place | `OperationalPurgeJob` and its tests |
| The purge ships as a dry run, and each job can be switched off (`Jobs:{Name}:Enabled`) | In place | `appsettings.json` |

**Residual risk:** **low**.

### Planned risks 29 to 40: the planned measures

From `docs/centre-phases-3-5-plan.md` ("decision n" is its numbering). **All are planned, none built:** each applies when its PR merges, and that PR replaces this list with the measures it built, their status and where they are in the code, and a residual risk.

| Risk | Planned measures | Applies when |
|---|---|---|
| **29: specimens and lab reports** | The request sheet holds only what the lab needs (type, site, request notes, the requesting clinician, the patient's identifiers), and the label's QR code holds the specimen number only (decision 87); reads of a specimen recorded (`SpecimenViewed`); specimens and results in the export, and their free text replaced on anonymisation; the patient privacy notice names labs as recipients; the lab in the ROPA as a recipient; reports kept with the record | PR 13 (PR 14 for results) |
| **30: letters** | Recipients shown with addresses before approval and sending; the practice from the directory; addresses copied at approval; secure email only through a relay the organisation declares meets DCB1596, only to allowed domains, and **never to patients** (decisions 101 to 107); the email itself holds no patient or clinical text; withdraw and correct, listing who it went to; reads recorded (`LetterViewed`); letters in the export; the body and comments replaced on anonymisation | PR 16 |
| **31: contact details** | All optional; edits logged by field name only (decision 78); in the export and replaced on anonymisation; the minimisation statement in Step 4 updated to justify each (letters need the address, reminders the mobile, letters to the GP the practice) | PR 12 |
| **32: messages to patients** | Fixed wording that never says why (no specialty, clinician, pathway or note; decision 131); only on the channels the patient chose, recorded on the patient form (decision 133); each message stored as sent and in the export, replaced on anonymisation; the provider listed in `sub-processors.md` and `international-transfers.md` **before the live sender can send anything** (decision 128) | PR 26 |
| **34: MDT lists** | Opening or printing the list records `PatientListed` for each patient (decision 143); a meeting's title and notes still never name patients; customer guidance on showing and disposing of printed lists | PR 30 |
| **35: consent and questionnaires** | Reads through the pathway page (`PathwayViewed`); in the export; free text (withdrawal detail, text answers) replaced on anonymisation, scans deleted with the files; no totals, bands or charts calculated (decision 154) | PRs 31 and 32 |
| **36: insurers** | Funding set only by `ManageBilling` holders; the patient privacy notice tells insured patients; reads recorded (`InvoiceViewed`); invoices in the export; the policy number, authorisation code and payer replaced on anonymisation, keeping what tax records need (decision 158) | PR 35 |
| **37: data-request packs** | Identity checked and recorded before the pack can be made (decisions 160 and 163); a clinician's review with items withheld and why (decision 161); the pack built on request and never stored, its download recorded (`DataRequestPackDownloaded`) (decision 162) | PR 36 |
| **38: several organisations** | EF Core query filters and SQL Server row-level security (decision 176); a cross-organisation test for every endpoint (decision 178); offboarding exports then deletes an organisation's data (decision 177); risk 18 rescored here | PR 40a to 40c |
| **39: unmatched incoming referrals** | Matched by a person, never automatically (decision 169); listed for staff to match or reject; deleted once registered or rejected (section 4.11); the e-Referral Service added to the data inventory, DPIA and ROPA as a source **before** the live adapter is turned on (decision 173) | PR 38a |
| **40: the video platform** | Chosen and contracted by the customer as its own processor; links only `https` to allowed hosts (decision 164), only in that patient's message; cleared on anonymisation; the customer's DPIA covers the platform | PR 34 |

## Step 7: Sign-off and outcomes

| Item | Name and date | Notes |
|---|---|---|
| **Measures approved by** | [Name, role, date] | Integrate the "before go-live" measures into the delivery plan (roadmap steps 2, 4, 5 and 7, step 6's patient banner, and step 9's malware scanning of files). |
| **Residual risks approved by** | [SIRO name, date] | If any high residual risk remains at go-live, consult the ICO before processing. |
| **DPO advice provided** | [DPO name, date] | |
| **Summary of DPO advice** | [To complete] | |
| **DPO advice accepted or overruled by** | [Name, date] | If overruled, give reasons. |
| **Consultation responses reviewed by** | [Name, date] | |
| **This DPIA will be kept under review by** | [DPO name] | At the events listed at the top. |

## For customers adapting this DPIA

Add to your copy:
- **Your context:** which services and patients, how many staff, which sites, whether children or people lacking capacity are involved.
- **Your lawful bases** and your privacy notice.
- **Your controls:** joiners, movers and leavers; device security, including anti-malware on computers that open attached files, and clearing downloads on shared computers; training; who your admins are; how you'll review access reports once they exist; what your staff may write in appointment notes and calendar block notes.
- **Whether you'll use file attachments,** and for what kinds of documents.
- **Your retention periods** if they differ from the defaults.
- **Your sign-off:** your DPO, and your Caldicott Guardian for NHS and social care.
- **DCB0160:** your clinical safety assessment of deploying MedApp, separate from this DPIA (roadmap step 6).
