# Data protection and information governance pack

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft), 16 November 2026: staff calendars and appointments added across the pack; 0.3 (draft), 17 November 2026: meetings and personal calendar templates added across the pack (review of #99); 0.4 (draft), the same day: deleting procedure notes and staff's requests to delete them (`feature/note-deletion`) |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Describes** | MedApp on `main` as of 16 November 2026, including file attachments, and its Azure design (`deploy/azure`), which has not been deployed |
| **Brief** | Roadmap step 3 in the [architecture handbook](../architecture.md#step-3-write-the-data-protection-and-information-governance-pack) |

The documents UK GDPR, the NHS Data Security and Protection Toolkit (DSPT) and buyers' information governance (IG) teams ask for, and the decisions (roles, lawful bases, retention, transfers) that roadmap steps 4 to 9 build in.

**Read this first:**
- **Nothing here is approved.** Every document is a draft for the DPO, and for the data processing agreement a solicitor, to review.
- **MedApp has never held real patient data,** and the DPIA concludes it must not until the measures it marks "before go-live" are built ([dpia.md](dpia.md)).
- **Controls are described as they are.** Where a document relies on something MedApp doesn't do yet, it says "planned" and names the roadmap step.
- **Placeholders** in square brackets, such as `[Company name]`, `[DPO contact]` and `[Address]`, are to be filled in. No real names or contacts are used.

## The documents

### Decisions and records

| Document | What it covers | For |
|---|---|---|
| [roles-and-lawful-bases.md](roles-and-lawful-bases.md) | Controller and processor, and why; the supplier's commitments not to use customer data for its own purposes; Article 6 and 9 and DPA 2018 Schedule 1 guidance for customers; the supplier's own bases; the ICO fee and the DPO | Supplier, customers |
| [data-inventory.md](data-inventory.md) | Every field of personal data MedApp stores, from the code, and every place it goes: database, blob storage for attached files, browser, staff computers' downloads, logs, backups, GitHub; the data flows | Supplier, customers, assessors |
| [ropa.md](ropa.md) | The supplier's record of processing: as processor (Article 30(2)) and as controller of its own business data (Article 30(1)) | Supplier, ICO |
| [ropa-customer-template.md](ropa-customer-template.md) | Entries for customers to copy into their own record of processing | Customers |
| [dpia.md](dpia.md) | The product DPIA on the ICO's structure: the processing, necessity and proportionality, 26 risks, including the NHS number, attached files, the monitoring of staff, staff calendars and the notepad, with likelihood and severity, the measures linked to the code and infrastructure, and the residual risk now and after the roadmap | Supplier, customers, DTAC |
| [retention-schedule.md](retention-schedule.md) | How long each kind of data is kept, following the NHS Records Management Code of Practice; review before destruction; backups; exit; **what MedApp must build** to enforce it | Supplier, customers |

### Contracts and suppliers

| Document | What it covers | For |
|---|---|---|
| [dpa-template.md](dpa-template.md) | The data processing agreement with the Article 28(3) terms, and schedules for the processing, security measures, sub-processors, and retention and exit | Customers, solicitor |
| [sub-processors.md](sub-processors.md) | Microsoft Azure (the only sub-processor), GitHub (no customer data), and how changes are notified | Public |
| [international-transfers.md](international-transfers.md) | The decision to host in the UK only, what isn't enforced yet, and each possible transfer | Customers, DTAC |
| [support-access-policy.md](support-access-policy.md) | When and how the supplier's people may access customer data | Supplier, customers |

### Procedures

| Document | What it covers | For |
|---|---|---|
| [breach-response.md](breach-response.md) | Breach response: the customer told within 48 hours (24 once out-of-hours cover exists), the ICO within 72 hours for the supplier's own data; grading; the notice template; the breach log | Supplier |
| [data-subject-rights.md](data-subject-rights.md) | Each right, what MedApp supports today, what's planned, and the interim manual procedure | Supplier, customers |
| [complaints.md](complaints.md) | The DUAA complaints duty for the supplier's own processing | Supplier, public |

### Transparency

| Document | What it covers | For |
|---|---|---|
| [privacy-notice-patients-template.md](privacy-notice-patients-template.md) | Wording for customers to give patients | Customers |
| [privacy-notice-staff-template.md](privacy-notice-staff-template.md) | Wording for customers to give their staff users, with rules for staff | Customers |
| [privacy-notice-supplier.md](privacy-notice-supplier.md) | The supplier's own privacy notice | Public |
| [cookies-and-storage.md](cookies-and-storage.md) | The one session cookie and the browser storage keys, under PECR | Public, customers |

### Assurance

| Document | What it covers | For |
|---|---|---|
| [security-policies.md](security-policies.md) | Information security, access control, acceptable use, secure development, supplier management, vulnerability and patch management, continuity and recovery, training | Supplier, DSPT |
| [nhs-context.md](nhs-context.md) | The Caldicott Principles, the National Data Opt-out, and the registrations and certificates NHS buyers ask for | Supplier, NHS customers |
| [dspt-readiness.md](dspt-readiness.md) | The DSPT's ten standards mapped to evidence, with the gaps marked | Supplier |
| [isms-risk-register.md](isms-risk-register.md) | The business risk register and an outline Statement of Applicability for ISO/IEC 27001 | Supplier |

## The main decisions

- **Roles:** customers are controllers of patient and staff data; the supplier is their processor, and a controller only of its own business data. It makes no use of customer data for its own purposes: no analytics, AI training, benchmarking or research.
- **Lawful bases (guidance to customers):** NHS bodies 6(1)(e); private providers 6(1)(b), or 6(1)(f) where there's no contract with the patient; all with 9(2)(h) and DPA 2018 Schedule 1, Part 1, paragraph 2. Not consent.
- **DPO:** the supplier appoints one (an external service), because hosting health records for many customers is large-scale processing of special category data.
- **Hosting:** UK only, Azure UK South, with backups and recovery in UK West.
- **Retention:** clinical records follow the Records Management Code (adults 8 years, children to the 25th or 26th birthday, mental health 20 years, and longer classes per template), reviewed rather than deleted automatically, and attached files and appointments with them; a deleted file's contents purged after a window (proposed 30 days, for the DPO to decide); sessions, sign-in events and staff calendar blocks 12 months; notifications 6 months; logs 90 days; point-in-time backups 35 days and long-term backups 12 months.
- **Breach notice to customers:** within 48 hours of the supplier becoming aware, aiming for 24, moving to a firm 24 hours once out-of-hours cover exists (step 5).
- **Rights requests reaching the supplier:** passed to the customer within 2 working days; help with exports within 5 working days, or as agreed.
- **Sub-processor changes:** 30 days' notice with a right to object.
- **Access from abroad:** customer data is reached only from the UK, except a logged emergency exception for the supplier's own employees, reported to the customer within 24 hours.
- **Exit:** the customer chooses return or deletion within 30 days; return within 30 days of the request; with no answer, reminders, then an export to the last named contact, and deletion only after confirmation or a longer agreed period; backups expire within 12 months.
- **Go-live gate:** no real patient data until the "before go-live" measures from roadmap steps 2, 4, 5 and 7, **step 6's patient banner** and **step 9's malware scanning of attached files** are in place. The banner is in the gate because the wrong-patient risk (DPIA risk 8) stays high without it; it's built (branch `feature/nhs-integrations`), and counts once the Clinical Safety Officer has verified it. Scanning is in the gate because the malicious-file risk (DPIA risk 22) stays high without it. A deployment without file storage, which turns uploads off, doesn't need scanning.
- **The NHS number** is optional, never written to the activity log, and removed by anonymisation; a read-only FHIR API returns it only to signed-in staff, logged. No connection to NHS England's PDS until its onboarding is done and these documents are updated for NHS England as a source of data.

## What MedApp must build for these documents to be true

Collected from the DPIA, the retention schedule and the rights procedure. Roadmap step 7 covers most of it. The privacy features work on roadmap step 7 should be checked against this list.

**Built by the first part of step 7** (branch `feature/privacy-features`, 16 November 2026): the activity log of reads (patients viewed, in search results and searched, with the filters but not the terms; procedures viewed; exports), patient creates, edits (the fields, not the values) and deletes, sign-ins, refused sign-ins and sign-outs, and account and role changes; the access report on a patient's page; the subject access export (JSON and a printable page, without the clinician review step); restriction and dispute flags; anonymisation; the retention review list with keep and anonymise; clearing browser storage at sign-out. **Built by the first part of step 9** (branch `feature/nhs-integrations`, 16 November 2026): the NHS number (optional, validated, unique), the patient banner, a read-only FHIR API for patients, SNOMED CT codes on stage definitions, and a demographics lookup with only an offline stub; the data inventory, the DPIA (new risk 21) and the documents that list patient data are updated for them. **Built with file attachments** (branch `feature/file-attachments`, 16 November 2026): files on patients, procedures and stages, with their lists and downloads in the activity log, their details (not the files) in the subject access export, anonymisation renaming and deleting them, and the restriction guard refusing new or deleted files; the data inventory, the DPIA (new risks 22 and 23) and the documents that list patient data are updated for them. **Built with staff calendars** (branch `feature/staff-calendars`): the privacy features cover appointments (anonymisation replaces their notes and cancellation reasons and the copies in their history, the export includes them and their history, a restricted patient's appointments can only be cancelled, and an anonymised patient's not changed, anonymising is refused while appointments are open, and reading them is in the activity log); the documents that list patient and staff data are updated for appointments and calendars, with DPIA risk 25. **Not built:** the rest of each row below. **Decided against, by the product owner:** keeping identifying filters out of the URL, because shareable list links are a feature (DPIA risk 10 says how that risk is carried), and replacing patient deletion with archiving for now.

| Need | Step | Documents that depend on it |
|---|---|---|
| Audit every read and change of patient data (with search terms), sign-ins, and account and role changes; access reports; an `Auditor` role | 4, 7 | DPIA risks 1 and 7; DSPT standard 4; staff notice |
| Note versions in their own table, instead of copies of notes in audit `Details` (built for procedure notes; stage and appointment notes still to do) | 7 | DPIA risk 4; retention |
| A scheduled purge of notepads untouched for 30 days (today they're cleared when next opened or saved), and ageing a notepad's text by when it was written, since a notepad in use is never cleared | 7 | DPIA risk 26; retention |
| A subject access export (JSON and PDF) with a clinician review step, listing a patient's attached files (built) and able to include the files themselves (not yet) | 7 | Rights; DPA clause 9 |
| Restriction and "disputed" flags; audited patient edits; archiving instead of hard deletion; an approved erasure workflow | 7 | Rights; DPIA risk 7 |
| A retention engine: classes per template, the conclusion of treatment date, a due-for-review list, destroy or anonymise, a destruction log, legal hold | 7 | Retention schedule |
| Automatic deletion of sessions (12 months), notifications (6 months), staff calendar blocks (12 months) and deactivated accounts (12 months) | 7 | Retention schedule; DPIA risk 25 |
| A purge job for deleted files' contents (proposed 30 days after deletion), which also finishes anonymising a patient's files; files, thumbnails and file names covered by destruction and erasure (anonymisation already renames and deletes them) | 7 | DPIA risks 5, 6 and 23; retention; rights |
| Malware scanning of uploaded files (Defender for Storage on Azure) | 9, brought forward before go-live | DPIA risk 22; DSPT standard 6 |
| Clear browser storage at sign-out | 7 | DPIA risk 10; cookies statement |
| Keep identifying filters (name, email, date of birth) out of the URL, so they don't land in browser history; page, sort and status stay in it | 7 | DPIA risk 10; data inventory |
| No patient identifiers in notifications | 7 | DPIA risk 3 |
| MFA, SSO, account deactivation, a chosen first admin name | 4 | DPIA risk 2; DSPT standard 4 |
| An idle timeout; hashed session tokens; patient deletion admin-only (done in step 2's first part: security headers, throttling, sessions ended on a password change or reset) | 2 | DPIA risks 2, 10, 11 and 7 |
| A least-privilege, passwordless database identity, and passwordless access to the files' storage; monitoring and alerts; 35-day and long-term backups, geo-redundant in the UK, and backups of the files; restore drills; an Azure Policy allowing only UK regions; a cluster-wide Cilium policy so only the API and the migrations Job reach the database's private endpoint; Customer Lockbox; just-in-time admin access; out-of-hours cover | 5 | DPIA risks 12 to 17; transfers; support access |
| A patient banner (in the go-live gate; built, to be verified by the CSO); optimistic concurrency | 6 | DPIA risks 8 and 9 |
| Always Encrypted for the NHS number; tracing it with PDS; OAuth clients for the FHIR API | 7, 9 | DPIA risk 21; data inventory |
| A deceased flag | 9 | Retention periods after death |
| A customer-managed key for the files' storage | 9 | DPIA risk 12; DPA Schedule 2 |
| Export and deletion of a whole customer at exit | 7, 8 | DPA clause 11 |

## What the supplier must do (not code)

- Pay the ICO fee; appoint the DPO and name the SIRO.
- Have a solicitor review the data processing agreement; record Microsoft's current data protection terms.
- Put confidentiality clauses in every contract; turn on and confirm MFA on every company account; run pre-engagement checks and annual training.
- Write the export queries for the interim rights procedure before the first customer.
- Certify Cyber Essentials; register for the DSPT and start the 2026-27 assessment (due 30 June 2027).
- Run a breach tabletop exercise and write it up.
- Approve, date and publish these documents, and keep their review dates.

## Keeping it current

- **Any change** that adds personal data, a purpose, a recipient, a sub-processor or a region updates [data-inventory.md](data-inventory.md) and [dpia.md](dpia.md) in the same pull request ([security-policies.md](security-policies.md), secure development). `AGENTS.md` makes this a step of every feature, and a check in every merge review.
- **Each document** has a version, an owner and a review date at the top. Approved versions are tagged in git, and superseded ones are kept for 6 years.
