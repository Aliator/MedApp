# Data protection impact assessment: MedApp

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft), 16 November 2026: the measures roadmap step 7's privacy features put in place |
| **Assessed** | MedApp on `main` as of 16 November 2026, with branch `feature/privacy-features` (roadmap step 7), and its Azure design (`deploy/azure`), which has not been deployed |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Approved by** | Not yet approved |
| **Next review** | Before the first real patient data; then before each roadmap step that changes the processing (4, 5, 7, 8 and 9); and at least every 12 months |

This is the **product DPIA**: the supplier's assessment of MedApp as a hosted service. It follows the structure of the ICO's DPIA template ([ICO](https://ico.org.uk/media2/migrated/2553993/dpia-template.docx)). Each customer is a controller and must carry out **its own DPIA** before using MedApp. It can adopt this one as the starting point, then add its own context: its patients, its staff, how it will use MedApp, and its own controls.

## Summary

- **A DPIA is mandatory.** MedApp is designed for processing health data, a special category, and across customers it's large scale (Article 35(3)(b)).
- **Today's high risks:** any staff member can see any patient (reads are now recorded, and admins can see who opened a record, but nothing restricts or alerts on it); sign-in is by password only; any staff member can still delete a patient with no procedures (now recorded in the activity log); nothing is ever deleted on a schedule; there's no monitoring to notice a breach; backups have never been restored; and on Azure the app would use the database administrator's login.
- **Conclusion: MedApp must not hold real patient data until the measures marked "before go-live" in section 6 are in place.** That's roadmap steps 2, 4, 5 and 7, **plus the patient banner from step 6**, which together make it ready for a first pilot. The banner is in the gate because risk 8 (notes written on the wrong patient's record) stays high until it's built; the rest of step 6 isn't needed for the gate. With these measures, every risk in this assessment is reduced to low or medium, so **prior consultation with the ICO (Article 36) isn't expected to be needed.** If any high residual risk remains at go-live, including risk 8 if the banner isn't built, the processing must not start without consulting the ICO.

## Step 1: The need for a DPIA

**What MedApp does.** MedApp is a web application for clinical and administrative staff. It records patients, and tracks the procedures carried out for them through a fixed sequence of stages, each with clinical notes. Every change to a procedure is recorded in an audit trail. Admins manage staff accounts, procedure templates and assignments. The supplier, [Company name], runs it as a hosted service on Microsoft Azure in the UK, for customers such as clinics, private hospitals and NHS trusts.

**Why a DPIA is needed:**
- **Article 35(3)(b):** processing on a large scale of special category data (health). The supplier processes it for many customers.
- **The ICO's list** of processing likely to result in a high risk includes processing of health data, and new technology.
- **NHS buyers** ask for a product DPIA through DTAC's data protection section ([DTAC form 2.0](https://digital.nhs.uk/binaries/content/assets/website-assets/services/dtac/dtac_form_2.0_february_2026.docx)).

## Step 2: The processing

### Nature

- **Collection:** the customer's staff type patients' details and clinical notes into MedApp in a browser. There's no import, integration or patient-facing access.
- **Storage:** in Azure SQL (UK South), reached only through a private network endpoint, with point-in-time backups.
- **Use:** staff find patients, start procedures from templates, work stages in order, write notes, and see their task lists. Staff also see each patient's timeline on the patient page: who changed what on their procedures and when, with old and new due dates, priorities and statuses, past assignees, and the usernames recorded at the time (deleted accounts' included): structured values, never free text (no notes, titles or reasons from the audit trail), and only for procedures that still exist. Admins assign work, unlock finished stages, read the full audit log, see a patient's whole history on screen (the same timeline with its free text, deleted procedures included), and manage accounts.
- **Sharing:** none outside the customer. Within the customer, every staff user can see every patient (section 5, risk 1).
- **Deletion:** staff can delete a patient who has no procedures. Admins can delete cancelled or archived procedures; their audit entries remain. Admins can anonymise a patient's record in place (identifying details and free text replaced, the structure kept). Nothing is deleted on a schedule: records past their retention period are listed for an admin to review.
- **New technology:** server-side rendering (a Node page server inside the API's container), live notifications over WebSockets with a Valkey relay, and Kubernetes. None involves profiling, AI or automated decisions.
- **Sources and flows:** [data-inventory.md](data-inventory.md).

### Scope

- **Data:** name, date of birth and email of patients; procedure titles, notes, due dates, priorities and statuses; stage notes (clinical free text); the audit trail with copies of notes; staff usernames, password hashes, roles, sessions with IP addresses and browsers, settings with saved searches; notifications. Field by field: [data-inventory.md](data-inventory.md).
- **Special category:** health data. Free text can also hold other special category data (for example religion, sexual life, ethnicity) if staff write it.
- **Volume:** per customer, [patients a year], [staff users]. Across customers, large scale.
- **Retention:** proposed in [retention-schedule.md](retention-schedule.md); **not enforced today**.
- **Geography:** patients in the UK; data stored in the UK.

### Context

- **Relationship:** patients are receiving care from the customer, and expect their information to be recorded and kept confidential. They don't use MedApp themselves.
- **Control:** patients have no direct control over what is recorded; they rely on the customer and its staff.
- **Vulnerable people:** patients include people who are ill, children, and possibly people lacking capacity.
- **Expectations:** patients would expect a care record system; they wouldn't expect staff outside their care to browse their record, or their data to be used for anything else.
- **Prior concerns:** the ICO wrote to over 200 health leaders on 12 November 2026 about staff "snooping" on records, after more than 300 reports of unauthorised access in 2026 ([ICO](https://ico.org.uk/about-the-ico/media-centre/news-and-blogs/2026/11/uk-health-sector-warned-to-tackle-medical-record-snooping/)). NHS suppliers have been fined for weak security, including missing MFA ([ICO](https://ico.org.uk/about-the-ico/media-centre/news-and-blogs/2025/03/software-provider-fined-3m-following-2022-ransomware-attack/)).
- **State of the technology:** MedApp is young (built from January 2026), has one maintainer, and has never run in production ([architecture handbook, 1.5](../architecture.md#15-status-and-maturity)). The code is in a public repository.
- **Codes and standards:** the NHS Records Management Code of Practice, the Caldicott Principles, the DSPT, DCB0129 and DCB0160, OWASP ASVS, Cyber Essentials.

### Purposes

- **For patients:** their procedures are coordinated safely, in order, by the right staff, with an accurate record.
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
| **Minimisation** | **The patient record is deliberately small:** name, date of birth and email; no address, phone, NHS number or sex. The patient banner and identity checks that step 9 adds will need more, each justified then. **Free text isn't limited**, and the audit copies it (risks 3 and 4). |
| **Accuracy** | Staff can edit patients and procedures. Finished stages lock, and only an admin can unlock them, which is audited. Patient edits are recorded in the activity log (which fields changed, not their values), and an admin can mark a record as disputed by the patient. |
| **Retention** | Periods in [retention-schedule.md](retention-schedule.md), based on the Records Management Code. Clinical records past their period are listed for review (keep with a reason, or anonymise); operational data isn't purged yet (risk 5). |
| **Transparency** | Template privacy notices for customers to give patients and staff ([privacy-notice-patients-template.md](privacy-notice-patients-template.md), [privacy-notice-staff-template.md](privacy-notice-staff-template.md)). |
| **Rights** | Supported by the product: a subject access export, restriction and dispute flags, anonymisation; the clinician review of an export is by hand ([data-subject-rights.md](data-subject-rights.md)). |
| **Processors** | The supplier, under an Article 28 agreement ([dpa-template.md](dpa-template.md)); Microsoft as a sub-processor ([sub-processors.md](sub-processors.md)). |
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

Likelihood is judged for MedApp **as it was on `main` on 16 November 2026, before roadmap step 7's privacy features**, if it held real patient data. Step 6 gives the residual risk with the measures in place since, including step 7's (marked "In place (step 7)").

| # | Risk to individuals | Source of the risk | Likelihood | Severity | Overall |
|---|---|---|---|---|---|
| 1 | **Inappropriate access ("snooping"):** a staff member looks at the record of a patient they don't care for (a neighbour, a relative, a public figure), and it can't be detected or disproved. | Every staff user sees every patient and procedure; **reads aren't recorded** at all; there are no access reports. | Probable | Severe | **High** |
| 2 | **Account takeover:** an attacker signs in as a staff member or admin with a stolen, guessed or reused password, and reads or changes records. | Password only, no MFA; no idle timeout. Since the security baseline (step 2, first part): sign-in is throttled (5 attempts a minute per username, 30 per address) in front of the 5-minute lockout, the 10,000 most common passwords are refused, and a password change or a reset ends the user's sessions, and an admin can sign a user out everywhere; first admin always called `admin`. Accounts created before PR #53 may still have the published password `DefaultPassword1`. | Probable | Severe | **High** |
| 3 | **Oversharing in free text:** notes, titles and block or cancel reasons hold more than needed (other people's details, unrelated conditions), and spread to the audit, notifications and logs. | Free text is unlimited; procedure titles appear in notifications and travel through Valkey. | Probable | Significant | **High** |
| 4 | **Copies of clinical text that can't be managed:** correcting, erasing or expiring a note leaves its old text in the audit trail. | `StageNotesUpdated` and `DetailsUpdated` audit entries copy the old and new text into `Details` (`MedApp.Application/Procedures/Common/ProcedureChange.cs`, `UpdatePatientProcedureHandler.cs`). | Probable | Significant | **High** |
| 5 | **Kept for ever:** records, sessions with IP addresses, notifications and audit entries are never deleted, so a later breach exposes far more than needed. | No retention is implemented; nothing is purged. | Probable | Significant | **High** |
| 6 | **Rights can't be met fully or on time:** a subject access request is answered late or incompletely. | No export; the data is spread over several tables; the audit log's screen filters by procedure, not by patient. Admins can now see a patient's whole history on screen, on the patient page's timeline (every change to their procedures, with its free text, deleted procedures included), which helps answer a request, but it is not an export. | Probable | Significant | **High** |
| 7 | **A patient's record is lost or changed without trace:** a staff member deletes or edits a patient, by mistake or deliberately, and nothing records it. | Any staff user can permanently delete a patient with no procedures; patient creates, edits and deletes aren't audited; account and role changes aren't audited. | Possible | Severe | **High** |
| 8 | **Wrong patient:** notes are written on the wrong patient's record, leading to a disclosure or a clinical error. | Patients are identified by name, date of birth and email only; no NHS number; no duplicate check; no patient banner. | Possible | Severe | **High** |
| 9 | **A lost update:** two staff save the same notes, and one silently overwrites the other. | No optimistic concurrency. The audit keeps both texts, but nobody is warned. | Possible | Significant | **Medium** |
| 10 | **Exposure on a shared or unattended computer:** someone else sees a patient's record on a ward or clinic workstation, or in browser storage or history. | 8-hour sessions (extendable on request to 24 at most from sign-in) with no idle timeout; saved searches (which can be patient names) stay in `localStorage` after sign-out; list pages keep their filters in the URL, so a patient search (first name, last name, email, date of birth range: `MedApp.Web/src/pages/listSearch.ts`) lands in the browser's history and address bar suggestions, which signing out doesn't clear. Rendered pages aren't cached. | Probable | Significant | **High** |
| 11 | **Web attacks:** a flaw lets an attacker run script in a staff member's browser, or frame the app. | Since the security baseline: a Content Security Policy with a nonce for each page (no inline or third-party scripts), `frame-ancestors 'none'` and `X-Frame-Options: DENY`, `nosniff` and a `Referrer-Policy` (`SecurityHeaders`). No antiforgery tokens (`SameSite=Lax` is the defence). Output is escaped, and rich text never runs raw HTML. | Possible | Severe | **High** |
| 12 | **Infrastructure compromise:** an attacker or a flaw reaches the database or its credentials. | On Azure the app, the migrations Job and anyone who can read an API pod's environment use the SQL server's admin login; the AKS API server, Key Vault and the registry have public endpoints (behind Entra ID); session IDs are stored unhashed; Node runs in the API's container and could read its environment. | Possible | Severe | **High** |
| 13 | **Undetected breach:** a breach goes unnoticed, so it isn't contained or reported in time. | No monitoring, alerting or exported logs in any deployed environment; no read audit. | Probable | Severe | **High** |
| 14 | **Loss of data or of the service:** records are lost, or unavailable during care. | Backups (7 days, locally redundant) never restored; no recovery objectives; no downtime procedure; one maintainer. | Possible | Severe | **High** |
| 15 | **Supply chain:** malicious code reaches production through a dependency or the build pipeline, and exfiltrates data. | Since the security baseline: actions pinned by commit SHA, Dependabot, CodeQL, dependency review, npm and NuGet audits that fail the build, a Trivy image scan, and SBOM and build provenance attestations on the published image. Images are still pinned by tag, not digest; nothing verifies the attestations before deploy; every merge deploys with no approval step; the repository and image are public. | Possible | Severe | **High** |
| 16 | **Supplier staff access:** someone at the supplier views or copies patient data without a need, or data ends up in support tickets, laptops or AI tools. | Cluster admins can reach everything; no just-in-time access or access log; one maintainer. | Possible | Severe | **High** |
| 17 | **Sub-processor and transfer:** Microsoft accesses or discloses the data, or it's processed outside the UK. | Azure hosting; Microsoft's service operations. | Remote | Significant | **Low** |
| 18 | **Mixing customers' data:** one customer sees another's patients. | Not possible today (one deployment per customer). Becomes a risk with multi-tenancy (step 8). | Not possible today | Severe | **Low today** (the matrix's one exception, because it can't happen yet); **to rescore at step 8** |
| 19 | **Lack of transparency:** patients don't know MedApp is used or who runs it. | Depends on the customer's privacy notice. | Possible | Minimal | **Low** |
| 20 | **Real data in development or testing:** patient data copied into a developer's database, a screenshot, a test, or an AI tool. | Convention only (`AGENTS.md`: synthetic data only); `medapp-dev` refuses anything but a local database. | Remote | Severe | **Medium** |

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
| **Record the reads of patient data** in the activity log, once per person every 5 minutes: a patient's record, timeline and its overview opened (viewed); each patient on a page of patient search, procedure search or retention review results (listed); searches (the filters used and how many matched, not the terms); a procedure opened; exports. **Not recorded per patient:** the deprecated list of every patient (`GET api/patients`, used only by the retired Blazor client), which records one "searched: all" entry; the side nav's pin labels, for records the user already opened; and the admin-only audit and activity logs themselves. A test lists every endpoint with what it records | In place (step 7) | `MedApp.Application/Activity/Common/ActivityLog.cs`; the patient, procedure, timeline, retention and export handlers; `MedApp.IntegrationTests/Privacy/EndpointCoverageTests.cs` |
| "Who has accessed this patient": the access report on the patient's page, and the activity log searchable by patient, user, address, action and date (admins) | In place (step 7) | `MedApp.Web/src/components/patients/PatientAccessReport.tsx`; `MedApp.Web/src/pages/activity/ActivityLogPage.tsx` |
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
| Server-side sessions in an HttpOnly, Secure, SameSite=Lax cookie named `__Host-medapp.session` outside development; roles re-read on every request; deleting a user ends their sessions; a password change ends the user's other sessions, and a reset all of them; admins can sign a user out everywhere | In place | `SessionAuthenticationHandler.cs`, `SessionCookieService.cs`, `SessionService.cs`, `UserSessions.cs`, `SignOutEverywhereHandler.cs` |
| Sign-in throttling per address and per username, in front of the lockout; the 10,000 most common passwords refused | In place | `MedApp.API/Common/RateLimiting`, `CommonPasswordValidator.cs` |
| MFA (TOTP and passkeys), compulsory for admins; SSO with the customer's identity provider; first admin with a chosen name | Planned, step 4. **Before go-live** | |
| A breached-password check | Planned, step 2. **Before go-live** | |
| Find and reset accounts that may still have `DefaultPassword1` | Planned (`docs/todo.md`). **Before go-live** (no such accounts exist in a new deployment) | |

**Residual risk:** now **high**; after steps 2 and 4 **low**.

### Risk 3: oversharing in free text

| Measure | Status | Where |
|---|---|---|
| Staff guidance: write only what's needed for the patient's care; no other people's details | Customer and supplier guidance; planned in-app guidance in step 7 | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| No patient identifiers in notifications; procedure titles reviewed as not identifying | Planned, step 7 | `AssignPatientProcedureHandler.cs` writes the title today |
| Encrypt Valkey traffic, or send only IDs through it | Planned, step 2 or 5 | `docs/reviews/signalr-backplane.md` |

**Residual risk:** now **high**; after **medium**. Free text can't be made risk-free; it relies on training.

### Risk 4: copies of clinical text in the audit

| Measure | Status | Where |
|---|---|---|
| The full audit trail is admin-only and kept with the record; staff see a per-patient timeline without its free text | In place | As risk 1 |
| Anonymising a patient replaces the copies of notes, titles and reasons in their audit entries (and so on the timeline) with placeholders | In place (step 7) | `MedApp.Application/Patients/Common/PatientAnonymiser.cs` |
| Keep note versions in their own table, and have the audit refer to versions, so correction, retention and erasure apply in one place | Planned, step 7. **Before go-live** | |

**Residual risk:** now **high**; after **low**.

### Risk 5: kept for ever

| Measure | Status | Where |
|---|---|---|
| A retention schedule | Draft | [retention-schedule.md](retention-schedule.md) |
| A due-for-review list from configurable periods (adults 8 years, children to 25 or 26), where an admin keeps a record with a reason and a review date, or anonymises it; nothing is deleted automatically | In place (step 7) | `MedApp.Application/Patients/Common/RetentionSchedule.cs`; `MedApp.Web/src/pages/retention/RetentionReviewPage.tsx` |
| The rest of the retention engine: retention classes per template, destruction, a destruction log, legal hold; short fixed periods for sessions, sign-in events and notifications | Planned, step 7. **Before go-live** (sessions and notifications at least; clinical records won't reach their minimum period for years) | |
| Backups expire on a fixed cycle | Planned, step 5 | |

**Residual risk:** now **high**; after **low**.

### Risk 6: rights

| Measure | Status | Where |
|---|---|---|
| Staff can view and correct patients and procedures | In place | |
| An interim, documented manual procedure for exports, run by the supplier on the customer's written instruction | Procedure written | [data-subject-rights.md](data-subject-rights.md) |
| A subject access export of everything held (details, procedures, stages, notes, assignments, the procedure history, the access log, notifications) as JSON and a printable page with the same content, itself recorded; staff appear by username only, without their account IDs, network addresses or read times | In place (step 7) | `GET api/patients/{id}/export`; `MedApp.Web/src/pages/patients/PatientExportPage.tsx` |
| Restricted (no changes, with a banner) and disputed (a banner) flags, set by admins with a reason; anonymisation with a typed confirmation and a reason, refused while procedures are under way | In place (step 7) | `SetPatientFlagHandler`, `PatientRules`, `AnonymisePatientHandler` |
| A clinician review step in the export for the serious harm test; archiving in place of deletion; restriction hiding the patient from lists | Planned, step 7 | |

**Residual risk:** now **low** (the clinician review is done by hand before an export is given out); after **low**.

### Risk 7: lost or untraced changes to patients and accounts

| Measure | Status | Where |
|---|---|---|
| A patient with procedures can't be deleted | In place | `MedApp.Application/Patients/Commands/DeletePatient/DeletePatientHandler.cs` |
| Deleting a user needs the admin's own password | In place | `AuthController.cs` |
| Make patient deletion admin-only | Planned, step 2. **Before go-live** | |
| Record patient creates, edits (the fields changed; not their values, so the log holds no patient details) and deletes, and account, password and role changes, in the activity log, with who and from which address; a row version on each patient, so a save from an older copy is refused rather than silently undoing a later change (such as a restriction or an anonymisation) | In place (step 7) | `CreatePatientHandler`, `UpdatePatientHandler`, `DeletePatientHandler`; the account and role handlers in `MedApp.Application/Authentication` |
| Keep the old values of an edit in a version table; replace deletion with archiving | Planned, step 7 | |
| Backups to recover from a mistaken delete | In place in design (7 days); 35 days planned, step 5 | `deploy/azure/main.bicep` |

**Residual risk:** now **medium** (every change is recorded with who and when, though an old value can't be recovered from the log); after **low**.

### Risk 8: wrong patient

| Measure | Status | Where |
|---|---|---|
| Patient name and date of birth shown on procedure pages | In place | |
| A patient banner (name and date of birth) on every patient and procedure page, so staff can check the patient before writing notes | Planned, step 6. **Before go-live** | |
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
| Rendered pages sent with `Cache-Control: no-store`; the password responses too | In place | `MedApp.API/ServerRendering/SsrPages.cs`; `AuthController.cs` |
| Signing out clears the query cache; the session cookie is HttpOnly and expires with the session (8 hours, extendable to 24 at most from sign-in) | In place | `MedApp.Web/src/auth/auth.ts` |
| An idle timeout (30 minutes to start) with a warning | Planned, step 2. **Before go-live** | |
| Clear the app's `localStorage` and `sessionStorage` at sign-out, but for the purely visual settings; the account keeps the rest for the next sign-in | In place (step 7) | `MedApp.Web/src/app/settingsStore.ts` (`clearStorageAtSignOut`) |
| Keep identifying filters (name, email, date of birth) out of the URL | **Not planned:** the product owner decided on 16 November 2026 to keep list filters in the URL, because shareable list links are a feature. The customer measures below carry this part of the risk | `MedApp.Web/src/pages/listSearch.ts` |
| Staff rules: sign out and lock the screen; don't search by patient name on a shared computer where others use the same browser profile | Template | [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Customer measures: screen locks, no shared logins, separate browser profiles or history cleared on shared computers | The customer's | |

**Residual risk:** now **high** (no idle timeout yet); after **medium**, because patient searches stay in the browser's history unless the customer's measures (separate profiles, cleared history) are followed.

### Risk 11: web attacks

| Measure | Status | Where |
|---|---|---|
| React escapes output; rich text is shown with a Markdown renderer that never runs raw HTML and neutralises unsafe links | In place | `MedApp.Web/src/components/shared/RichText.tsx` |
| One origin, no CORS; `SameSite=Lax` cookie; JSON bodies | In place | `SessionCookieService.cs` |
| HSTS and https redirection outside development | In place | `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs` |
| No open proxy in server rendering; blocked paths; Node's environment limited to an allowlist | In place (PR #51) | `MedApp.API/ServerRendering/` |
| CSP with nonces, `frame-ancestors 'none'`, `nosniff`, `Referrer-Policy`, `no-store` on every `/api` response, a `__Host-` cookie name | In place | `MedApp.API/Middleware/SecurityHeaders.cs`, `SessionCookieService.cs`; `docs/security/asvs.md` |
| An ASVS Level 2 review, a ZAP baseline scan, and a penetration test | Planned, steps 2, 7 and 10. **Before go-live** (ASVS and a first test, if an NHS pilot) | |

**Residual risk:** now **high**; after **low**.

### Risk 12: infrastructure compromise

| Measure | Status | Where |
|---|---|---|
| Azure SQL with public access off, a private endpoint, TLS 1.2 minimum, encryption at rest (TDE with Microsoft's keys), and a delete lock | In place in design | `deploy/azure/main.bicep` |
| Default-deny network policies for MedApp's own pods: only the ingress reaches the API, and MedApp's pods reach only what they need. On kind, only the API and the migrations Job reach the in-cluster database. **On Azure, other pods in the cluster (Traefik, Argo CD, cert-manager, External Secrets) aren't restricted yet, and can open a connection to the database's private endpoint** (they'd still need the password) | In place, with the Azure gap | `deploy/helm/medapp/templates/networkpolicy.yaml`; `deploy/azure/values.yaml` |
| A cluster-wide Cilium policy that denies traffic to the private endpoint subnet from every pod except MedApp's API and migrations Job | Planned, step 5. **Before go-live** | |
| The `restricted` Pod Security Standard, non-root containers, read-only filesystems, no Kubernetes API tokens | In place | `deploy/helm/medapp/templates/_helpers.tpl` |
| Secrets from Key Vault through External Secrets, never in git; Entra ID-only cluster access; OIDC and workload identity, no stored cloud secrets | In place in design | `deploy/helm/medapp/templates/externalsecrets.yaml`; `deploy/azure/` |
| A passwordless, least-privilege database identity for the app, a separate one for migrations, then Entra-only authentication | Planned, step 5. **Before go-live** | `deploy/azure/README.md`, "Not yet" |
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

## Step 7: Sign-off and outcomes

| Item | Name and date | Notes |
|---|---|---|
| **Measures approved by** | [Name, role, date] | Integrate the "before go-live" measures into the delivery plan (roadmap steps 2, 4, 5 and 7, and step 6's patient banner). |
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
- **Your controls:** joiners, movers and leavers; device security; training; who your admins are; how you'll review access reports once they exist.
- **Your retention periods** if they differ from the defaults.
- **Your sign-off:** your DPO, and your Caldicott Guardian for NHS and social care.
- **DCB0160:** your clinical safety assessment of deploying MedApp, separate from this DPIA (roadmap step 6).
