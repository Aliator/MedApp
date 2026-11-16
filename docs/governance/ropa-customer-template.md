# Record of processing activities: template for customers

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft template), 16 November 2026: adds appointments and staff calendars |
| **For** | Each customer, as the controller of the personal data it records in MedApp |
| **Prepared by** | [Company name], the supplier of MedApp, as its processor |

**How to use it.** Copy these entries into your own record of processing activities (UK GDPR Article 30(1)), and change anything that differs for your organisation. Text in square brackets is for you to fill in. The supplier has filled in what it knows about MedApp; you must check it against how you use MedApp. The choice of lawful basis is yours; [roles-and-lawful-bases.md](roles-and-lawful-bases.md) gives the supplier's guidance.

## Controller details

| Item | Detail |
|---|---|
| **Organisation** | [Customer name] |
| **Address** | [Address] |
| **Contact for data protection** | [Contact] |
| **Data protection officer** | [DPO name and contact] |
| **Caldicott Guardian** (NHS and social care) | [Name] |
| **Senior information risk owner** | [Name] |
| **ICO registration** | [Number] |

## Entry 1: recording and coordinating patients' procedures in MedApp

| Item | Detail |
|---|---|
| **Business function** | [For example: day surgery unit, outpatient procedures, clinic administration] |
| **Purpose** | Recording patients and coordinating the stages of the procedures carried out for them, for their direct care, including booking their appointments with staff; keeping an accurate record of who did what and when. |
| **Data subjects** | Patients, including children [if applicable] |
| **Personal data** | First name, last name, date of birth, email address, NHS number (if you record it). Procedures: title, notes, due date, priority, status. Stage notes written by staff. Files staff attach to the patient, a procedure or a stage (scans, photos, results, letters), with their names. Appointments: the member of staff, the time, the status (requested, booked, attended, did not attend, cancelled), notes and a cancellation reason. |
| **Special category data** | Health data: the procedures a patient is having, their appointments and whether they attended, clinical notes, and attached files, which can include images of the body and other people's details (for example in a letter). |
| **Article 6 basis** | [NHS bodies: 6(1)(e) public task. Private providers: 6(1)(b) contract with the patient, or 6(1)(f) legitimate interests with an assessment.] |
| **Article 9 condition** | 9(2)(h) health or social care |
| **DPA 2018 Schedule 1 condition** | Part 1, paragraph 2 (health or social care purposes). No appropriate policy document needed. |
| **Common law duty of confidentiality** | Implied consent for direct care |
| **Source of the data** | The patient; referrers; [other services whose letters or results you attach]; [other sources] |
| **Recipients** | Your staff with MedApp accounts. Your processor, [Company name], and its sub-processors ([sub-processors.md](sub-processors.md)). [Others you share with, outside MedApp.] |
| **Processor** | [Company name], under the data processing agreement signed [date] |
| **Transfers outside the UK** | None by your processor: data is stored in the UK. Its sub-processor Microsoft may, in rare cases, access data from outside the UK for support, under Microsoft's data protection terms and the safeguards they contain ([international-transfers.md](international-transfers.md)) |
| **Retention** | [Your schedule.] The supplier's default follows the NHS Records Management Code of Practice: adult records 8 years after the conclusion of treatment; children's records until the 25th birthday, or the 26th if the patient was 17 at the conclusion of treatment; attached files with the record ([retention-schedule.md](retention-schedule.md)). **MedApp doesn't apply retention automatically yet,** and keeps the contents of deleted files until a purge job exists (proposed: 30 days after deletion). |
| **Security measures** | See the processor's description ([ropa.md](ropa.md), A6) and your own: [who can have an account, how accounts are reviewed, device and network controls]. |
| **DPIA** | [Reference to your DPIA, adapted from [dpia.md](dpia.md)] |
| **Privacy information** | [Where your privacy notice says this; see [privacy-notice-patients-template.md](privacy-notice-patients-template.md)] |
| **Rights requests** | [Your procedure; see [data-subject-rights.md](data-subject-rights.md)] |
| **National Data Opt-out** | Doesn't apply: this is direct care. |

## Entry 2: the audit trail of procedures and appointments

| Item | Detail |
|---|---|
| **Purpose** | Accountability and the integrity of care records: knowing who changed what on a procedure or an appointment, and who added, deleted or downloaded a file, and when, including after deletes; investigating incidents and complaints. |
| **Data subjects** | Patients; your staff |
| **Personal data** | Procedure ID and title, patient ID, stage name, the action, the staff member's ID and username, the time, and the change, including old and new notes text and the reason for blocking or cancelling. For attached files: each file added, deleted or **downloaded**, with its name, type and size. For appointments: who requested, booked, moved, cancelled or recorded the outcome, the old and new times and member of staff, old and new notes, and the cancellation reason. |
| **Special category data** | Health data (the notes text) |
| **Article 6 basis** | [As entry 1; also 6(1)(c) where a legal duty to keep the record applies] |
| **Article 9 condition** | 9(2)(h), with Schedule 1, Part 1, paragraph 2 |
| **Recipients** | Your MedApp admins: the full audit trail is admin-only, and they can see a patient's whole history on screen. Your staff see a per-patient timeline without its free text: who changed what on their procedures and appointments and when, with old and new due dates, priorities and statuses, past assignees, appointment times, statuses and staff, and the usernames recorded at the time (deleted accounts' included): structured values, never free text; no notes text, titles, reasons (appointment notes and cancellation reasons included) or file names (only that a file was attached or deleted), no downloads, and nothing about deleted procedures or their appointments. |
| **Retention** | The same as the record it relates to |

## Entry 3: staff accounts, sessions, calendars and notifications

| Item | Detail |
|---|---|
| **Purpose** | Giving your staff secure access; knowing who did what; telling staff when they are assigned work (unless they switch it off) or their appointments change; planning staff time around appointments; supervising work, by telling admins about the staff actions their monitoring rules name. |
| **Data subjects** | Your staff with MedApp accounts |
| **Personal data** | Username, password hash, roles, lockout state; sign-in sessions with IP address and browser; account settings, including saved searches; notification choices; notifications; calendars: time blocked off (its activity, such as annual leave or training, and a short note) and default week templates; admins' monitoring rules (which admin watches which member of staff, for which actions) and the monitoring notifications they send. |
| **Special category data** | None by design. A calendar block's note could reveal a staff member's health if they write it; tell staff not to. |
| **Article 6 basis** | [NHS bodies: 6(1)(e), and 6(1)(c) for security records. Private providers: 6(1)(b) for the account and calendar; 6(1)(f) for sessions, security and monitoring rules.] |
| **Recipients** | Your MedApp admins; your processor. Every member of your staff with an account can see everyone's calendar, including annual leave. |
| **Retention** | Accounts: while the person needs access, then removed [within your joiners, movers and leavers process]. Sessions: 12 months. Notifications: 6 months. Calendar blocks: 12 months after the day. Monitoring rules: until the admin deletes them [review them under your monitoring policy] ([retention-schedule.md](retention-schedule.md)). **Not applied automatically yet.** |
| **Privacy information** | [Your staff privacy notice; see [privacy-notice-staff-template.md](privacy-notice-staff-template.md)] |
