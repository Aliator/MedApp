# Data protection impact assessment: MedApp

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Assessed** | MedApp on `main` as of 16 November 2026, including file attachments (scans, photos, results and letters attached to patients, procedures and stages), and its Azure design (`deploy/azure`), which has not been deployed |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Approved by** | Not yet approved |
| **Next review** | Before the first real patient data; then before each roadmap step that changes the processing (4, 5, 7, 8 and 9); and at least every 12 months |

This is the **product DPIA**: the supplier's assessment of MedApp as a hosted service. It follows the structure of the ICO's DPIA template ([ICO](https://ico.org.uk/media2/migrated/2553993/dpia-template.docx)). Each customer is a controller and must carry out **its own DPIA** before using MedApp. It can adopt this one as the starting point, then add its own context: its patients, its staff, how it will use MedApp, and its own controls.

## Summary

- **A DPIA is mandatory.** MedApp is designed for processing health data, a special category, and across customers it's large scale (Article 35(3)(b)).
- **Today's high risks:** any staff member can see any patient, and reads aren't recorded; sign-in is by password only; patient records can be deleted without trace; nothing is ever deleted on a schedule, and deleted files' contents are kept indefinitely; there's no monitoring to notice a breach; backups have never been restored; on Azure the app would use the database administrator's login; and **uploaded files aren't scanned for malware** before staff open them (risk 21).
- **Conclusion: MedApp must not hold real patient data until the measures marked "before go-live" in section 6 are in place.** That's roadmap steps 2, 4, 5 and 7, **plus the patient banner from step 6** and **malware scanning of uploaded files, brought forward from step 9** (or a deployment run without file storage, which turns uploads off), which together make it ready for a first pilot. The banner is in the gate because risk 8 (notes or files put on the wrong patient's record) stays high until it's built; the rest of step 6 isn't needed for the gate. Malware scanning is in it because risk 21 (a malicious file opened on a clinical workstation) stays high without it. With these measures, every risk in this assessment is reduced to low or medium, so **prior consultation with the ICO (Article 36) isn't expected to be needed.** If any high residual risk remains at go-live, including risk 8 if the banner isn't built or risk 21 if files are accepted unscanned, the processing must not start without consulting the ICO.

## Step 1: The need for a DPIA

**What MedApp does.** MedApp is a web application for clinical and administrative staff. It records patients, and tracks the procedures carried out for them through a fixed sequence of stages, each with clinical notes. Staff can attach files (scans, photos, results, letters) to a patient, a procedure or a stage. Every change to a procedure, and every file added, deleted or downloaded, is recorded in an audit trail. Admins manage staff accounts, procedure templates and assignments. The supplier, [Company name], runs it as a hosted service on Microsoft Azure in the UK, for customers such as clinics, private hospitals and NHS trusts.

**Why a DPIA is needed:**
- **Article 35(3)(b):** processing on a large scale of special category data (health). The supplier processes it for many customers.
- **The ICO's list** of processing likely to result in a high risk includes processing of health data, and new technology.
- **NHS buyers** ask for a product DPIA through DTAC's data protection section ([DTAC form 2.0](https://digital.nhs.uk/binaries/content/assets/website-assets/services/dtac/dtac_form_2.0_february_2026.docx)).

## Step 2: The processing

### Nature

- **Collection:** the customer's staff type patients' details and clinical notes into MedApp in a browser, and upload files they already hold (a scanned referral letter, a photo of a wound, a result from another system). There's no import, integration or patient-facing access.
- **Storage:** in Azure SQL (UK South), reached only through a private network endpoint, with point-in-time backups. Attached files are in Azure Blob Storage in the same region, also reached only through a private endpoint, with 7 days' soft delete and no other backup; their details (name, type, size, who uploaded them) are in Azure SQL.
- **Use:** staff find patients, start procedures from templates, work stages in order, write notes, and see their task lists. Staff also see each patient's timeline on the patient page: who changed what on their procedures and when, with old and new due dates, priorities and statuses, past assignees, and the usernames recorded at the time (deleted accounts' included): structured values, never free text (no notes, titles or reasons from the audit trail), and only for procedures that still exist. Admins assign work, unlock finished stages, read the full audit log, see a patient's whole history on screen (the same timeline with its free text, deleted procedures included), and manage accounts. Staff upload, list and download any patient's files. The timeline shows that a file was attached or deleted, with its name for admins only; downloads are only in the admins' audit log. Files are only ever downloaded, never shown in the page; the only images shown are small thumbnails the server makes from PNG and JPEG photos.
- **Sharing:** none outside the customer. Within the customer, every staff user can see every patient (section 5, risk 1).
- **Deletion:** staff can delete a patient who has no procedures and no files. Admins can delete cancelled or archived procedures that have no files; their audit entries remain. Staff can delete files they uploaded, and admins any file: a deleted file is hidden from staff and can't be downloaded, but its details and its contents are kept until a purge job removes the contents after a retention window. **No purge job exists yet**, so deleted files are kept indefinitely. Nothing is deleted on a schedule.
- **New technology:** server-side rendering (a Node page server inside the API's container), live notifications over WebSockets with a Valkey relay, Kubernetes, and cloud blob storage for files, with thumbnails made on the server (SkiaSharp). None involves profiling, AI or automated decisions: no file is read, classified or interpreted.
- **Sources and flows:** [data-inventory.md](data-inventory.md).

### Scope

- **Data:** name, date of birth and email of patients; procedure titles, notes, due dates, priorities and statuses; stage notes (clinical free text); **attached files** (scans, photos, results, letters, up to 20 MB each) and their names (free text); the audit trail with copies of notes and file names, and a record of each download; staff usernames, password hashes, roles, sessions with IP addresses and browsers, settings with saved searches; notifications. Field by field: [data-inventory.md](data-inventory.md).
- **Special category:** health data. Free text can also hold other special category data (for example religion, sexual life, ethnicity) if staff write it. **Files** are health data by nature, can be **images of the body**, keep the **metadata** they were uploaded with (for a photo, possibly where and when it was taken), and can hold **third parties'** data (a letter naming relatives or other clinicians).
- **Volume:** per customer, [patients a year], [staff users]. Across customers, large scale.
- **Retention:** proposed in [retention-schedule.md](retention-schedule.md); **not enforced today**.
- **Geography:** patients in the UK; data stored in the UK.

### Context

- **Relationship:** patients are receiving care from the customer, and expect their information to be recorded and kept confidential. They don't use MedApp themselves.
- **Control:** patients have no direct control over what is recorded; they rely on the customer and its staff.
- **Vulnerable people:** patients include people who are ill, children, and possibly people lacking capacity.
- **Expectations:** patients would expect a care record system, including copies of their letters and results; they wouldn't expect staff outside their care to browse their record or their photos, or their data to be used for anything else.
- **Prior concerns:** the ICO wrote to over 200 health leaders on 12 November 2026 about staff "snooping" on records, after more than 300 reports of unauthorised access in 2026 ([ICO](https://ico.org.uk/about-the-ico/media-centre/news-and-blogs/2026/11/uk-health-sector-warned-to-tackle-medical-record-snooping/)). NHS suppliers have been fined for weak security, including missing MFA ([ICO](https://ico.org.uk/about-the-ico/media-centre/news-and-blogs/2025/03/software-provider-fined-3m-following-2022-ransomware-attack/)).
- **State of the technology:** MedApp is young (built from January 2026), has one maintainer, and has never run in production ([architecture handbook, 1.5](../architecture.md#15-status-and-maturity)). The code is in a public repository.
- **Codes and standards:** the NHS Records Management Code of Practice, the Caldicott Principles, the DSPT, DCB0129 and DCB0160, OWASP ASVS, Cyber Essentials.

### Purposes

- **For patients:** their procedures are coordinated safely, in order, by the right staff, with an accurate record, and the documents their care needs (letters, results, images) are with it.
- **For the customer:** coordinating work, accountability through the audit trail, meeting record-keeping duties.
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
| **Does the processing achieve the purpose?** | Yes. Coordinating a procedure's stages needs the patient's identity, the procedure, and notes on each stage. |
| **Is there a less intrusive way?** | Paper or spreadsheets hold the same data with weaker access control and no audit. A system without patient identity can't coordinate care for a person. |
| **Minimisation** | **The patient record is deliberately small:** name, date of birth and email; no address, phone, NHS number or sex. The patient banner and identity checks that step 9 adds will need more, each justified then. **Free text isn't limited**, and the audit copies it (risks 3 and 4). **Files aren't limited in content** either, only in type (six kinds, found from their contents) and size (20 MB); their names are free text, and originals keep their metadata (risk 22). |
| **Accuracy** | Staff can edit patients and procedures. Finished stages lock, and only an admin can unlock them, which is audited. Patient edits aren't audited (risk 7). A file put on the wrong record can't be moved, only deleted (soft, audited) and uploaded again to the right one (risk 8). |
| **Retention** | Proposed periods in [retention-schedule.md](retention-schedule.md), based on the Records Management Code; attached files are part of the clinical record. Not enforced, and deleted files are kept until a purge job exists (risk 5). |
| **Transparency** | Template privacy notices for customers to give patients and staff ([privacy-notice-patients-template.md](privacy-notice-patients-template.md), [privacy-notice-staff-template.md](privacy-notice-staff-template.md)). |
| **Rights** | Supported partly by the product and partly by hand today; planned in step 7 ([data-subject-rights.md](data-subject-rights.md)). |
| **Processors** | The supplier, under an Article 28 agreement ([dpa-template.md](dpa-template.md)); Microsoft as a sub-processor ([sub-processors.md](sub-processors.md)), whose Azure Blob Storage holds the files. |
| **International transfers** | None by the supplier; UK hosting ([international-transfers.md](international-transfers.md)). |
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

Likelihood is judged for MedApp **as it is today**, if it held real patient data.

| # | Risk to individuals | Source of the risk | Likelihood | Severity | Overall |
|---|---|---|---|---|---|
| 1 | **Inappropriate access ("snooping"):** a staff member looks at the record of a patient they don't care for (a neighbour, a relative, a public figure), and it can't be detected or disproved. | Every staff user sees every patient and procedure, and every patient's files; **reads aren't recorded**, except a file's download (listing a patient's files and seeing their thumbnails aren't); there are no access reports. | Probable | Severe | **High** |
| 2 | **Account takeover:** an attacker signs in as a staff member or admin with a stolen, guessed or reused password, and reads or changes records. | Password only, no MFA; no throttling beyond a 5-minute lockout; sessions not ended by a password change; no idle timeout; first admin always called `admin`. Accounts created before PR #53 may still have the published password `DefaultPassword1`. | Probable | Severe | **High** |
| 3 | **Oversharing in free text:** notes, titles, block or cancel reasons and file names hold more than needed (other people's details, unrelated conditions, a patient's name in a file's name), and spread to the audit, notifications and logs. | Free text is unlimited; procedure titles appear in notifications and travel through Valkey; file names are copied into the audit log. | Probable | Significant | **High** |
| 4 | **Copies of clinical text that can't be managed:** correcting, erasing or expiring a note leaves its old text in the audit trail. | `StageNotesUpdated` and `DetailsUpdated` audit entries copy the old and new text into `Details` (`MedApp.Application/Procedures/Common/ProcedureChange.cs`, `UpdatePatientProcedureHandler.cs`). | Probable | Significant | **High** |
| 5 | **Kept for ever:** records, files, sessions with IP addresses, notifications and audit entries are never deleted, so a later breach exposes far more than needed. | No retention is implemented; nothing is purged. A deleted file is only hidden: its contents and details stay, because the purge job that would remove them doesn't exist yet. | Probable | Significant | **High** |
| 6 | **Rights can't be met fully or on time:** a subject access request is answered late or incompletely. | No export; the data is spread over several tables, and attached files are in blob storage, outside the database; the audit log's screen filters by procedure, not by patient. Admins can now see a patient's whole history on screen, on the patient page's timeline (every change to their procedures, with its free text, deleted procedures included), which helps answer a request, but it is not an export. | Probable | Significant | **High** |
| 7 | **A patient's record is lost or changed without trace:** a staff member deletes or edits a patient, by mistake or deliberately, and nothing records it. | Any staff user can permanently delete a patient with no procedures and no files; patient creates, edits and deletes aren't audited; account and role changes aren't audited. | Possible | Severe | **High** |
| 8 | **Wrong patient:** notes are written on, or a file (a letter, a result, a photo) attached to, the wrong patient's record, leading to a disclosure or a clinical error. | Patients are identified by name, date of birth and email only; no NHS number; no duplicate check; no patient banner. A file can't be moved once attached, only deleted and uploaded again. | Possible | Severe | **High** |
| 9 | **A lost update:** two staff save the same notes, and one silently overwrites the other. | No optimistic concurrency. The audit keeps both texts, but nobody is warned. | Possible | Significant | **Medium** |
| 10 | **Exposure on a shared or unattended computer:** someone else sees a patient's record on a ward or clinic workstation, in browser storage or history, or in a file downloaded to it. | 8-hour sessions (extendable on request to 24 at most from sign-in) with no idle timeout; saved searches (which can be patient names) stay in `localStorage` after sign-out; list pages keep their filters in the URL, so a patient search (first name, last name, email, date of birth range: `MedApp.Web/src/pages/listSearch.ts`) lands in the browser's history and address bar suggestions, which signing out doesn't clear. Every file opened is first downloaded, and stays in the computer's downloads folder after sign-out. Rendered pages and files aren't cached. | Probable | Significant | **High** |
| 11 | **Web attacks:** a flaw lets an attacker run script in a staff member's browser, or frame the app. | No Content Security Policy or other security headers on pages; no antiforgery tokens (`SameSite=Lax` is the defence). Output is escaped, and rich text never runs raw HTML. Files uploaded by staff are served from the app's own origin, though only as downloads, with their own sandboxing CSP (risk 21). | Possible | Severe | **High** |
| 12 | **Infrastructure compromise:** an attacker or a flaw reaches the database or its credentials. | On Azure the app, the migrations Job and anyone who can read an API pod's environment use the SQL server's admin login, and the storage account's key, which can read and delete every file; the AKS API server, Key Vault and the registry have public endpoints (behind Entra ID); session IDs are stored unhashed; Node runs in the API's container and could read its environment. | Possible | Severe | **High** |
| 13 | **Undetected breach:** a breach goes unnoticed, so it isn't contained or reported in time. | No monitoring, alerting or exported logs in any deployed environment; no read audit. | Probable | Severe | **High** |
| 14 | **Loss of data or of the service:** records are lost, or unavailable during care. | Backups (7 days, locally redundant) never restored; no recovery objectives; no downtime procedure; one maintainer. Files have only 7 days' soft delete, with no backup, and a database restore doesn't bring back files, or their details, with it. | Possible | Severe | **High** |
| 15 | **Supply chain:** malicious code reaches production through a dependency or the build pipeline, and exfiltrates data. | Actions and images pinned by tag, not digest; no image scanning, SBOM or signing; every merge deploys with no approval step; the repository and image are public. | Possible | Severe | **High** |
| 16 | **Supplier staff access:** someone at the supplier views or copies patient data without a need, or data ends up in support tickets, laptops or AI tools. | Cluster admins can reach everything; no just-in-time access or access log; one maintainer. | Possible | Severe | **High** |
| 17 | **Sub-processor and transfer:** Microsoft accesses or discloses the data, or it's processed outside the UK. | Azure hosting; Microsoft's service operations. | Remote | Significant | **Low** |
| 18 | **Mixing customers' data:** one customer sees another's patients. | Not possible today (one deployment per customer). Becomes a risk with multi-tenancy (step 8). | Not possible today | Severe | **Low today** (the matrix's one exception, because it can't happen yet); **to rescore at step 8** |
| 19 | **Lack of transparency:** patients don't know MedApp is used or who runs it. | Depends on the customer's privacy notice. | Possible | Minimal | **Low** |
| 20 | **Real data in development or testing:** patient data copied into a developer's database, a screenshot, a test, or an AI tool. | Convention only (`AGENTS.md`: synthetic data only); `medapp-dev` refuses anything but a local database. | Remote | Severe | **Medium** |
| 21 | **Malicious files:** a file carrying malware (for example a PDF or Word document with an exploit, received from outside and uploaded) is downloaded and opened on a clinical workstation, which is compromised, exposing the records it can reach and the staff member's session. | **Files aren't scanned:** every file is stored as "Not scanned" (`NoMalwareScanner`). Staff upload files that came from outside (patients, referrers, other systems), and any staff member can download them; they open in desktop apps (PDF readers, Word, image viewers). | Possible | Severe | **High** |
| 22 | **Files hold more than staff see:** a photo's metadata (where and when it was taken, the phone), a document's author or tracked changes, a third party named in a letter, or a patient's name in a file's name, is kept and passed on with every download, or shown in the audit log. | Files are stored exactly as uploaded; only thumbnails are re-encoded without metadata; file names are free text; a download gives the whole original. | Possible | Significant | **Medium** |

## Step 6: Measures to reduce the risks

**Status:**
- **In place:** built, in the code or the deployment design. Azure is written but **not yet deployed**, so its controls are "in place in design".
- **Planned:** in the roadmap ([architecture handbook](../architecture.md#roadmap-the-next-10-steps)), with the step.
- **Before go-live:** must be done before MedApp holds real patient data.

**Residual risk** is given twice: **now** (with the measures in place) and **after** the planned measures.

### Risk 1: inappropriate access

| Measure | Status | Where |
|---|---|---|
| Every API endpoint that reaches patient or account data requires a signed-in session and a role (`Staff` or `Admin`); only sign-in, sign-out and `whoami` don't; the API is the only enforcement | In place | `MedApp.API/Common/Authentication/AuthPolicies.cs`, the controllers in `MedApp.API/Controllers/` |
| The full audit trail is admin-only; staff see a per-patient timeline without its free text (no notes, titles or reasons; old and new due dates, priorities and statuses, past assignees and recorded usernames are shown) | In place | `MedApp.API/Controllers/ProcedureAuditController.cs`, `MedApp.Application/Procedures/Timeline/Common/TimelineMapper.cs` |
| Every change to a procedure is recorded with who and when | In place | `MedApp.Application/Procedures/Common/ProcedureChange.cs` |
| Downloads of attached files are recorded in the audit log, with who, when and which file; the full log is admin-only, and downloads don't appear on the patient's timeline | In place | `MedApp.Application/Attachments/Common/AttachmentAudit.cs`, `DownloadAttachmentHandler.cs` |
| **Record every read of patient data** (patient viewed, searched with the terms, procedure viewed, a patient's files listed, exported) | Planned, step 7. **Before go-live** | |
| Access reports ("who has seen this patient", "what has this person seen"), an `Auditor` role, unusual-access alerts, periodic access reviews | Planned, steps 4 and 7. **Before go-live** (reports and review) | |
| Restrict access by site, team or care relationship | Planned, step 8 (sites); customers decide who gets accounts until then | |
| Customer measures: accounts only for staff who need them; staff told that access is monitored; disciplinary policy | The customer's | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |

**Residual risk:** now **high**; after step 7 **medium** (detectable and deterred, but still possible); after step 8 **low to medium**.

### Risk 2: account takeover

| Measure | Status | Where |
|---|---|---|
| Passwords stored as salted hashes by ASP.NET Core Identity | In place | `MedApp.Infrastructure/Common/Identity` |
| Server-generated first passwords and resets (at least 16 characters, from a cryptographic random generator), shown once, never logged or cached | In place | `IdentityUserService.cs`; `MedApp.API/Controllers/AuthController.cs` (`no-store`) |
| Lockout after 5 failures for 5 minutes | In place | `MedApp.Infrastructure/Common/Authentication/IdentityAuthenticationService.cs`; `MedApp.API/appsettings.json` (`Identity`) |
| Server-side sessions in an HttpOnly, Secure, SameSite=Lax cookie; roles re-read on every request; deleting a user ends their sessions | In place | `SessionAuthenticationHandler.cs`, `SessionCookieService.cs`, `SessionService.cs` |
| MFA (TOTP and passkeys), compulsory for admins; SSO with the customer's identity provider; first admin with a chosen name | Planned, step 4. **Before go-live** | |
| Sign-in throttling; a breached-password check; a forced change of generated passwords; sessions ended on password change | Planned, step 2. **Before go-live** | |
| Find and reset accounts that may still have `DefaultPassword1` | Planned (`docs/todo.md`). **Before go-live** (no such accounts exist in a new deployment) | |

**Residual risk:** now **high**; after steps 2 and 4 **low**.

### Risk 3: oversharing in free text

| Measure | Status | Where |
|---|---|---|
| Staff guidance: write only what's needed for the patient's care; no other people's details | Customer and supplier guidance; planned in-app guidance in step 7 | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| No patient identifiers in notifications; procedure titles reviewed as not identifying | Planned, step 7 | `AssignPatientProcedureHandler.cs` writes the title today |
| Encrypt Valkey traffic, or send only IDs through it | Planned, step 2 or 5 | `docs/reviews/signalr-backplane.md` |
| File names hidden from staff on the patient's timeline ("a file was attached"); only admins see them, in the timeline and the audit log | In place | `MedApp.Application/Procedures/Timeline/Common/TimelineMapper.cs` |
| Staff guidance: name a file for what it is, not who it's about | Template | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |

**Residual risk:** now **high**; after **medium**. Free text can't be made risk-free; it relies on training.

### Risk 4: copies of clinical text in the audit

| Measure | Status | Where |
|---|---|---|
| The full audit trail is admin-only and kept with the record; staff see a per-patient timeline without its free text | In place | As risk 1 |
| Keep note versions in their own table, and have the audit refer to versions, so correction, retention and erasure apply in one place | Planned, step 7. **Before go-live** | |

**Residual risk:** now **high**; after **low**.

### Risk 5: kept for ever

| Measure | Status | Where |
|---|---|---|
| A retention schedule | Draft | [retention-schedule.md](retention-schedule.md) |
| A retention engine: a due-for-review list, admin approval, destruction or anonymisation, a destruction log; short fixed periods for sessions and notifications | Planned, step 7. **Before go-live** (sessions and notifications at least; clinical records won't reach their minimum period for years) | |
| A purge job that deletes a deleted file's contents (and thumbnail) after a window, proposed as 30 days after deletion for the DPO to decide; files destroyed with the rest of the record by the retention engine | Planned (`docs/todo.md`), with step 7's retention engine. **Before go-live** | [retention-schedule.md](retention-schedule.md) |
| Backups expire on a fixed cycle | Planned, step 5 | |

**Residual risk:** now **high**; after **low**.

### Risk 6: rights

| Measure | Status | Where |
|---|---|---|
| Staff can view and correct patients and procedures | In place | |
| An interim, documented manual procedure for exports, run by the supplier on the customer's written instruction | Procedure written | [data-subject-rights.md](data-subject-rights.md) |
| A subject access export (JSON and PDF), restriction and dispute flags, archiving in place of deletion, an erasure workflow | Planned, step 7. **Before go-live** (export) | |
| The export lists a patient's files and can include the files themselves; anonymisation and erasure cover file names, the files and their thumbnails. Required of the privacy features work (PR #68) once file attachments are merged | Planned, step 7. **Before go-live** | [data-subject-rights.md](data-subject-rights.md) |

**Residual risk:** now **medium** with the manual procedure; after **low**.

### Risk 7: lost or untraced changes to patients and accounts

| Measure | Status | Where |
|---|---|---|
| A patient with procedures can't be deleted | In place | `MedApp.Application/Patients/Commands/DeletePatient/DeletePatientHandler.cs` |
| Deleting a file is soft and audited (who and when, kept on the file for admins); a patient or procedure with files can't be deleted until the files are | In place | `DeleteAttachmentHandler.cs`; `DeletePatientHandler.cs`; `DeletePatientProcedureHandler.cs` |
| Deleting a user needs the admin's own password | In place | `AuthController.cs` |
| Make patient deletion admin-only | Planned, step 2. **Before go-live** | |
| Audit patient creates, edits (before and after) and deletes, and account and role changes; replace deletion with archiving | Planned, step 7. **Before go-live** | |
| Backups to recover from a mistaken delete | In place in design (7 days); 35 days planned, step 5 | `deploy/azure/main.bicep` |

**Residual risk:** now **high**; after **low**.

### Risk 8: wrong patient

| Measure | Status | Where |
|---|---|---|
| Patient name and date of birth shown on procedure pages | In place | |
| Each file's list says where it's attached (the patient, a procedure or a stage), who uploaded it and when; the upload is audited and shown on the patient's timeline | In place | `MedApp.Web/src/components/attachments/Attachments.tsx`; `AttachmentAudit.cs` |
| A file put on the wrong record can be deleted at once by whoever uploaded it, or by an admin, so it stops being shown and can't be downloaded | In place | `DeleteAttachmentHandler.cs` |
| A patient banner (name and date of birth) on every patient and procedure page, so staff can check the patient before writing notes or attaching files | Planned, step 6. **Before go-live** | |
| NHS number and duplicate checks | Planned, step 9 | |
| Clinical safety hazard log | Planned, step 6 | |

**Residual risk:** now **high**; after step 6's banner (in the go-live gate) **medium**; after step 9 **low**.

### Risk 9: lost updates

| Measure | Status | Where |
|---|---|---|
| Both texts kept in the audit | In place | `ProcedureChange.cs` |
| Optimistic concurrency with a 409 on conflict | Planned, step 6 | |

**Residual risk:** now **medium**; after **low**.

### Risk 10: shared and unattended computers

| Measure | Status | Where |
|---|---|---|
| Rendered pages sent with `Cache-Control: no-store`; the password responses and attached files too | In place | `MedApp.API/ServerRendering/SsrPages.cs`; `AuthController.cs`; `MedApp.API/Attachments/AttachmentResponseHeaders.cs` |
| Signing out clears the query cache; the session cookie is HttpOnly and expires with the session (8 hours, extendable to 24 at most from sign-in) | In place | `MedApp.Web/src/auth/auth.ts` |
| An idle timeout (30 minutes to start) with a warning | Planned, step 2. **Before go-live** | |
| Clear `localStorage` at sign-out | Planned, step 7. **Before go-live** | |
| Keep identifying filters (name, email, date of birth) out of the URL, in session storage or the router's state, while page, sort and status stay in it | Planned, step 7. **Before go-live** | `MedApp.Web/src/pages/listSearch.ts` |
| Staff rules: sign out and lock the screen; don't search by patient name on a shared computer where others use the same browser profile; delete downloaded files once read, and don't keep them on shared computers | Template | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Customer measures: screen locks, no shared logins, separate browser profiles or history cleared on shared computers; downloads folders cleared at sign-out on shared computers, by device policy | The customer's | |

**Residual risk:** now **high**; after **low**. History already recorded before step 7 stays in browsers until it's cleared or expires.

### Risk 11: web attacks

| Measure | Status | Where |
|---|---|---|
| React escapes output; rich text is shown with a Markdown renderer that never runs raw HTML and neutralises unsafe links | In place | `MedApp.Web/src/components/shared/RichText.tsx` |
| One origin, no CORS; `SameSite=Lax` cookie; JSON bodies | In place | `SessionCookieService.cs` |
| HSTS and https redirection outside development | In place | `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs` |
| No open proxy in server rendering; blocked paths; Node's environment limited to an allowlist | In place (PR #51) | `MedApp.API/ServerRendering/` |
| Uploaded files never shown or run on the app's origin: always downloads, with a sandboxing CSP; SVG and HTML refused (risk 21) | In place | `MedApp.API/Attachments/AttachmentResponseHeaders.cs`; `FileTypeSniffer.cs` |
| CSP with nonces, `frame-ancestors 'none'`, `nosniff`, `Referrer-Policy`, `no-store` on every `/api` response, a `__Host-` cookie name | Planned, step 2. **Before go-live** | |
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
| Hash session tokens; move the page server into a sidecar | Planned, step 2 | |

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
| Vaulted backups of the files (Azure Backup), restored with the database in drills, so files and their details come back together | Planned, step 5. **Before go-live** | `deploy/azure/README.md`, "Not yet" |
| 35-day point-in-time restore, geo-redundant backups within the UK, long-term retention, quarterly restore drills, recovery objectives (RPO 15 minutes, RTO 4 hours), runbooks | Planned, step 5. **Before go-live** | |
| A downtime procedure for customers | Planned, step 6 | |

**Residual risk:** now **high**; after **low**.

### Risk 15: supply chain

| Measure | Status | Where |
|---|---|---|
| Required CI checks on `main`; a written review of every merge; the GitOps deploy limited by an Argo CD project | In place | `.github/workflows/main.yml`; `deploy/argocd/project.yaml` |
| Pin actions and images by digest; CodeQL, dependency review and image scanning; SBOMs and provenance; a required reviewer before production | Planned, steps 1, 2 and 5. **Before go-live** | |
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

### Risk 21: malicious files

| Measure | Status | Where |
|---|---|---|
| A type allow-list found from each file's contents, never its name or the browser's type, and the name's extension must match: PDF, PNG, JPEG, HEIC/HEIF, Word `.docx` and plain UTF-8 text. Macro-enabled Word, SVG and HTML refused; anything else refused; at most 20 MB | In place | `MedApp.Application/Attachments/Common/FileTypeSniffer.cs`, `UploadAttachmentValidator.cs` |
| Files never shown or run in the app: always sent as a download (`Content-Disposition: attachment`), with the sniffed type, `nosniff`, `Content-Security-Policy: sandbox; default-src 'none'`, `Cross-Origin-Resource-Policy: same-origin` and `no-store`; only thumbnails the server re-encodes are shown in pages | In place | `MedApp.API/Attachments/AttachmentResponseHeaders.cs`; `MedApp.Infrastructure/Attachments/SkiaThumbnailGenerator.cs` |
| "Not scanned" shown on every file, so staff know | In place | `MedApp.Web/src/components/attachments/Attachments.tsx` |
| Malware scanning on upload, refusing infected files: Microsoft Defender for Storage on Azure, behind the existing `IMalwareScanner` | Planned, step 9. **Before go-live**, brought forward; or the deployment runs without file storage, which turns uploads off | `deploy/azure/README.md`, "Not yet" |
| Customer measures: up-to-date anti-malware, PDF readers and Office on every computer that opens files; macros off by policy | The customer's | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |

**Residual risk:** now **high**; after scanning **medium** (scanning finds known malware, not all of it); **low to medium** with the customer's endpoint protection.

### Risk 22: files hold more than staff see

| Measure | Status | Where |
|---|---|---|
| Thumbnails re-encoded, which strips their metadata | In place | `SkiaThumbnailGenerator.cs` |
| File names cleaned (no folders or control characters, at most 255 characters) and hidden from staff on the timeline | In place | `AttachmentFileName.cs`; `TimelineMapper.cs` |
| Staff guidance: upload only what the care needs; name a file for what it is, not who it's about | Template | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Strip location metadata from photos at upload. A decision for the DPO and the CSO, because the original may be evidence | Proposed, step 7 | |
| The export, erasure and anonymisation cover files and their names (risk 6) | Planned, step 7 | |

**Residual risk:** now **medium**; after **low**.

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
- **Your controls:** joiners, movers and leavers; device security, including anti-malware on computers that open attached files, and clearing downloads on shared computers; training; who your admins are; how you'll review access reports once they exist.
- **Whether you'll use file attachments,** and for what kinds of documents.
- **Your retention periods** if they differ from the defaults.
- **Your sign-off:** your DPO, and your Caldicott Guardian for NHS and social care.
- **DCB0160:** your clinical safety assessment of deploying MedApp, separate from this DPIA (roadmap step 6).
