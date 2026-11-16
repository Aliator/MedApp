# Retention schedule

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Approved by** | Not yet approved |
| **Next review** | Every 12 months, and when the NHS Records Management Code of Practice changes |

How long each kind of data in MedApp is kept, and what happens at the end. It sets the **defaults** MedApp will ship with. Each customer, as controller, approves its own schedule and may change the defaults for clinical records (section 2).

**Today MedApp enforces none of this.** Nothing is ever deleted on a schedule ([architecture handbook, 8.2](../architecture.md#82-data-protection-and-privacy-as-they-stand)). Section 6 lists what the app must build; roadmap step 7 builds it.

## 1. Principles

- **Clinical records follow the NHS Records Management Code of Practice** ([NHS England](https://digital.nhs.uk/data-and-information/information-governance/guidance/records-management-code-of-practice/appendix-ii/retention-schedule-care-records)). It applies to NHS bodies and to organisations providing NHS-funded care. MedApp uses it as the default for **every** customer, including private clinics, because it's the accepted standard for health records in England, and private providers are expected to keep records in line with current guidance (CQC Regulation 17). [Check each period against the current version of the Code when approving.]
- **Periods are minimums for clinical records.** At the end, a record is **reviewed**, not deleted automatically. The Code says "In no circumstances should a record or series be automatically destroyed or deleted", and that "A separate record should be kept of what record has been deleted" ([NHS England](https://digital.nhs.uk/data-and-information/information-governance/guidance/records-management-code-of-practice/management-of-records-when-the-minimum-retention-period-is-reached)).
- **Operational data has short fixed periods,** deleted automatically. It isn't part of the health record, so storage limitation (Article 5(1)(e)) argues for keeping it no longer than needed.
- **The audit trail lives as long as the record it's about.** An audit trail is part of the record's evidence.
- **Legal hold overrides everything.** A complaint, claim, investigation, inquiry or instruction not to destroy (for example a public inquiry's) suspends destruction for the records it covers until it's lifted.

## 2. Clinical records

**The trigger, "the conclusion of treatment",** is worked out per patient by MedApp (to be built): the latest of
- the date the patient's last procedure was completed, cancelled or archived, and
- the date the patient's record was last changed,

counted only once the patient has no open procedures. MedApp doesn't record deaths yet (a deceased flag comes in step 9), so the periods after death in the Code can't be applied yet; the longer period applies until then.

**Retention classes.** Each procedure template has a retention class, set by the customer's admin (to be built). A patient's records are kept for the longest period among their procedures' classes.

| Class | Default minimum period (from the Code) | When it applies |
|---|---|---|
| **Adult, general** (the default) | **8 years** after the conclusion of treatment | Patients 18 or over at the conclusion of treatment, unless another class applies |
| **Children and young people** | Until the patient's **25th birthday**, or **26th** if they were 17 at the conclusion of treatment | Worked out automatically from the date of birth: patients under 18 at the conclusion of treatment |
| **Mental health** | **20 years** after the conclusion of treatment, or 10 years after death | Templates the customer marks as mental health |
| **Maternity** | **25 years** after the birth of the last child [check against the Code] | Templates the customer marks as maternity |
| **Oncology** | **30 years** from diagnosis, or 8 years after death [check against the Code] | Templates the customer marks as oncology |
| **Customer-defined** | A longer period the customer sets, with a reason | For example research protocols, or a specialty with its own rules |

A customer may set a **longer** period for any class. A **shorter** period than the Code's needs the customer's DPO to approve it in writing, and is recorded in the customer's schedule.

### What a clinical record includes

Everything about the patient, kept and destroyed together:
- the `Patient` row
- their procedures, stages, stage notes and assignments
- the audit entries about them (`PatientProcedureAuditEntry`, matched by `PatientId`), including entries for procedures already deleted
- note versions and access audit entries, once step 7 adds them
- notifications about their procedures (already gone under section 3, which is shorter)

### At the end of the period

1. **Due for review.** MedApp lists the patients whose period has passed (to be built).
2. **Decision.** The customer's records manager, or an admin with that duty, decides for each:
   - **destroy:** delete every part of the record listed above
   - **anonymise:** keep the procedure data for statistics, with the patient's identity and free text removed so no one can be identified
   - **keep:** with a reason (for example legal hold, or ongoing care) and a new review date, no more than 2 years ahead
3. **Destruction log.** For each record destroyed or anonymised, MedApp records: the record's internal ID, the retention class, the conclusion of treatment date, the decision, who approved it, and when it was done. **The log holds no clinical content, name or date of birth.**
4. **Backups.** Destroyed records stay in backups until they expire (section 4). After any restore, the destruction log is applied again before the service reopens.

## 3. Operational data

| Data | Period | At the end | Why this period |
|---|---|---|---|
| **Sessions** (`UserSession`: IP address, browser, times) | **12 months** after the session was created | Deleted automatically | Long enough to investigate account misuse reported late; sessions last 8 hours (24 at most, if extended), so nothing operational needs them after that. |
| **Sign-in and security events** (step 7: sign-ins, failures, refused requests, MFA changes) | **12 months** | Deleted automatically | As sessions. |
| **Access audit** (step 7: who viewed or searched which patient) | **As the patient's record** (section 2) | With the record | It's the record's audit trail, and the evidence for "who has seen my record" questions. Search terms that match no single patient are kept **12 months**. |
| **Notifications** | **6 months** after creation, read or not | Deleted automatically | A notification is a pointer to work; the procedure and its audit are the record. Titles can be sensitive. |
| **Staff accounts** | While the customer keeps the account. Once deactivated (step 4), **12 months**, then deleted | Deleted automatically, with the account's settings | Usernames in audit entries are copies, kept with the audit (section 2), so deleting the account doesn't break the trail. |
| **Account settings** (`UserSettings`, including saved searches) | With the account | Deleted with it | Saved searches can hold patient names; see section 6. |
| **Notification choices** (`NotificationPreferences`) | With the account | Deleted with it (to be built: `docs/todo.md`) | A person's own choice; nothing needs it once the account is gone. |
| **Monitoring rules** (`MonitoringRules`: which admin watches which staff member, for what) | Until the admin deletes the rule, or the admin's or the watched person's account is deleted | Deleted then (to be built: `docs/todo.md`; until then they stay, and a rule whose admin is no longer an admin, or whose person is deleted, sends nothing) | A rule is only for supervising current work; the customer's monitoring policy should say when rules about a person are reviewed. |
| **Browser storage** (`localStorage`, `medapp.*`) | Until sign-out | Cleared at sign-out (to be built) | Shared computers. |
| **Stage definitions and templates** | For the life of the customer's contract | Returned or deleted at exit | Configuration, not personal data. Archived ones are kept because procedures refer to them. |
| **Application logs and traces** (Log Analytics, step 5) | **90 days** | Deleted automatically by the workspace's retention setting | Enough for troubleshooting and incident investigation; logs aren't the record. |
| **Azure activity and infrastructure sign-in logs** (supplier's own) | **12 months** | Deleted automatically | Security investigation. |

## 4. Backups

| Backup | Period | Status |
|---|---|---|
| **Azure SQL point-in-time restore** | **35 days** | 7 days today (Azure's default, `deploy/azure/main.bicep`); 35 planned in step 5 |
| **Azure SQL long-term retention** | **Monthly backups kept 12 months**; no yearly backups | Planned in step 5 |
| **Backups made for a recovery drill** | Deleted when the drill ends | Policy ([support-access-policy.md](support-access-policy.md)) |

Data deleted from the live service stays in backups until they expire: at most 35 days for point-in-time backups, and 12 months for long-term ones. Backups are kept "beyond use": restored only for disaster recovery or on the customer's instruction, and the destruction log is applied again after any restore. Customers' privacy notices say so ([privacy-notice-patients-template.md](privacy-notice-patients-template.md)).

## 5. At the end of a contract

| Step | Period |
|---|---|
| The customer chooses return or deletion | Within 30 days of the end |
| The supplier returns a full export | Within 30 days of the request |
| The supplier deletes the customer's data from the live service | Within 30 days after the return is confirmed |
| If the customer doesn't answer | No deletion. Two reminders, 14 days apart; then an export to the last named contact; deletion only after they confirm receipt, or [6 months] after the export is sent |
| Backups expire | Within 12 months |
| The supplier confirms deletion in writing | When the live data is deleted, with the date the backups expire |

These are the terms of the data processing agreement ([dpa-template.md](dpa-template.md), clause 11, and Schedule 4). The customer remains responsible for keeping the returned records for their remaining retention periods.

## 6. What MedApp must build to enforce this

Roadmap step 7 builds these. The privacy features work on roadmap step 7 must meet these requirements, and anything it doesn't cover stays open here.

| # | Feature | Detail |
|---|---|---|
| 1 | **Retention settings per customer** | The classes and periods in sections 2 and 3, with these defaults, changeable by the customer's admin; a shorter clinical period needs a recorded approval. |
| 2 | **A retention class on each template** | Adult general by default; children worked out from the date of birth. |
| 3 | **The conclusion of treatment date** | Worked out per patient as in section 2, and shown on the patient's page. |
| 4 | **A due-for-review list and decisions** | Destroy, anonymise or keep (with a reason and a review date), approved by a named person. |
| 5 | **Destruction across every table** | Patient, procedures, stages, assignments, audit entries (by `PatientId`, including those of deleted procedures), note versions, access audit, and notifications that refer to the patient's procedures. Saved searches whose terms match the patient's name are removed. |
| 6 | **Anonymisation** | Removes names, date of birth, email and every free-text field, and unlinks the audit, so the remaining data can't identify anyone. |
| 7 | **A destruction log** | As section 2, step 3; kept for as long as the customer uses MedApp, and included in the export at exit. |
| 8 | **Legal hold** | Per patient and for the whole customer; suspends destruction and shows on the due-for-review list. |
| 9 | **Automatic jobs** for operational data | Sessions and security events at 12 months, notifications at 6 months, deactivated accounts at 12 months. A scheduled job that runs once across several API copies (a lease, or a Kubernetes CronJob), and logs what it deleted (counts, not content). |
| 10 | **Note versions instead of copies in the audit** | So destroying or correcting a note doesn't leave its text in `Details` ([dpia.md](dpia.md), risk 4). |
| 11 | **Clear `localStorage` at sign-out** | Already in step 7's scope. |
| 12 | **Archiving in place of deletion** | Staff can't hard-delete a patient; erasure goes through an approved workflow ([data-subject-rights.md](data-subject-rights.md)). |
| 13 | **Backups set to section 4** | Step 5: point-in-time restore of 35 days and long-term retention of 12 months in `deploy/azure/main.bicep`; a runbook step to re-apply the destruction log after a restore. |
| 14 | **Log retention** | Step 5: the Log Analytics workspace set to 90 days. |
| 15 | **A deceased flag** | Step 9, so the periods after death can apply. |

## 7. The supplier's own records

Retention of the supplier's business data (contracts, invoices, support, complaints, breach records) is in its own record of processing ([ropa.md](ropa.md), part B). In short: contracts and accounts **6 years**; support requests **2 years** after closure; complaints **3 years** after closure; breach records and the support access log **6 years**; superseded versions of these governance documents **6 years**.
