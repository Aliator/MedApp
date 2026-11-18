# Retention schedule

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft), 16 November 2026: what roadmap step 7 built (section 6); 0.3 (draft), the same day: appointments and staff calendars; 0.4 (draft), the same day: meetings, the team view, suggested meeting times and personal calendar templates (`docs/calendar-plan.md`, decisions 50 on); 0.5 (draft), 17 November 2026: what happens to templates when their owner's account goes (review of #99); 0.6 (draft), the same day: deleted procedure notes, and staff's requests to delete them (`feature/note-deletion`) |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Approved by** | Not yet approved |
| **Next review** | Every 12 months, and when the NHS Records Management Code of Practice changes |

How long each kind of data in MedApp is kept, and what happens at the end. It sets the **defaults** MedApp will ship with. Each customer, as controller, approves its own schedule and may change the defaults for clinical records (section 2).

**MedApp enforces part of this.** It lists clinical records past their default period for an admin to review, keep or anonymise, and clears the browser's storage at sign-out. Nothing is ever deleted on a schedule, and operational data isn't purged yet ([architecture handbook, 8.2](../architecture.md#82-data-protection-and-privacy-as-they-stand)). Section 6 lists what's built and what's still to build.

## 1. Principles

- **Clinical records follow the NHS Records Management Code of Practice** ([NHS England](https://digital.nhs.uk/data-and-information/information-governance/guidance/records-management-code-of-practice/appendix-ii/retention-schedule-care-records)). It applies to NHS bodies and to organisations providing NHS-funded care. MedApp uses it as the default for **every** customer, including private clinics, because it's the accepted standard for health records in England, and private providers are expected to keep records in line with current guidance (CQC Regulation 17). [Check each period against the current version of the Code when approving.]
- **Periods are minimums for clinical records.** At the end, a record is **reviewed**, not deleted automatically. The Code says "In no circumstances should a record or series be automatically destroyed or deleted", and that "A separate record should be kept of what record has been deleted" ([NHS England](https://digital.nhs.uk/data-and-information/information-governance/guidance/records-management-code-of-practice/management-of-records-when-the-minimum-retention-period-is-reached)).
- **Operational data has short fixed periods,** deleted automatically. It isn't part of the health record, so storage limitation (Article 5(1)(e)) argues for keeping it no longer than needed.
- **The audit trail lives as long as the record it's about.** An audit trail is part of the record's evidence.
- **Legal hold overrides everything.** A complaint, claim, investigation, inquiry or instruction not to destroy (for example a public inquiry's) suspends destruction for the records it covers until it's lifted.

## 2. Clinical records

**The trigger, "the conclusion of treatment",** is worked out per patient by MedApp (built in step 7, for the review list, except for appointments: section 6, item 3): the latest of
- the date the patient's last procedure was completed, cancelled or archived,
- the date of the patient's last appointment that was attended, not attended or cancelled, and
- the date the patient's record was last changed,

counted only once the patient has no open procedures and no Requested or Booked appointments. **Reading the record doesn't move it:** downloading an attached file, or any other read recorded in the audit (`AttachmentDownloaded`), isn't a change, so an admin who opens a letter to decide on a record due for review doesn't push its review back. Attaching or deleting a file is a change, and counts. MedApp doesn't record deaths yet (a deceased flag comes in step 9), so the periods after death in the Code can't be applied yet; the longer period applies until then.

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
- their **attached files** (on the patient, a procedure or a stage): each file and its thumbnail in blob storage, and its `Attachment` row, including files already deleted but not yet purged (section 3)
- the audit entries about them (`PatientProcedureAuditEntry`, matched by `PatientId`), including entries for procedures already deleted
- their appointments (`Appointment`, by `PatientId`, including those detached from a deleted procedure), with their notes and cancellation reasons
- their appointment history (`AppointmentEvent`, matched by `PatientId`), which keeps copies of old notes and cancellation reasons and outlives the appointment
- the versions of their procedures' notes (`PatientProcedureNoteVersion`, matched by `PatientId`), including those of procedures already deleted, and their procedures' notes themselves, **deleted notes included**: an admin's deletion only hides a note, which is kept, with who deleted it, when and why, as part of the record
- staff's requests to delete their procedures' notes (`ProcedureNoteDeletionRequest`, matched by `PatientId`), with their reasons and outcomes, including those about procedures already deleted
- access audit entries, once step 7 adds them
- notifications about their procedures and appointments (already gone under section 3, which is shorter)

**Attached files follow the record.** Scans, photos, results and letters attached in MedApp are part of the clinical record, so they have no period of their own: they're kept, reviewed and destroyed with the patient's record, never deleted automatically. A customer who keeps the master copy of a document in another system (its EPR or document store) may still treat the MedApp copy as part of the record; its schedule should say which.

### At the end of the period

1. **Due for review.** MedApp lists the patients whose period has passed (Retention Review, admins; built in step 7 for the adult and children classes).
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
| **Deleted attached files** (a file staff or an admin deleted: hidden at once, its contents kept until purged) | **Proposed: the contents purged 30 days after deletion,** for the DPO to decide. The file's `Attachment` row (name, type, size, who uploaded and deleted it) is kept with the record, like its audit entries | Contents and thumbnail deleted automatically by a purge job (to be built); on Azure they then stay in blob soft delete for 7 more days | Most files are deleted because they were attached to the wrong record, or twice; keeping them longer keeps another patient's data on the wrong record. 30 days gives time to notice a mistaken delete and restore it (restoring is planned too) and to investigate an incident. A file that is part of the record shouldn't be deleted this way at all: removing it is destruction, through the review in section 2. **Today no purge job exists, so deleted files are kept indefinitely.** |
| **Uploads being checked** (a temporary copy in the API pod's `/tmp`) | Seconds | Deleted as soon as the file is checked | Only while the API finds its type, hashes it and (once built) scans it. |
| **Notifications** | **6 months** after creation, read or not | Deleted automatically | A notification is a pointer to work; the procedure and its audit are the record. Titles can be sensitive. The same for appointment notifications. |
| **Staff calendar blocks** (`CalendarBlock`: time blocked off, its activity and note), with a person's day overrides and template block exceptions | **12 months** after the day [proposed; for the DPO to confirm] | Deleted automatically | They show when staff were available, which matters while appointments around them are booked and reviewed. They aren't the customer's record of leave or training, which its HR systems keep. Annual leave and notes can reveal private matters ([dpia.md](dpia.md), risk 25). |
| **Meetings** (`Meeting` and its attendees: title, notes, time, who) | **12 months** after their day, like blocks [proposed; for the DPO to confirm] | Deleted automatically (cancelling one deletes it at once) | They show when staff were busy, as blocks do. Notes and titles can reveal private matters ([dpia.md](dpia.md), risk 25). |
| **A person's default week template** (`StaffWeekTemplate` rows) | While the account exists; closed rows **12 months** after their end date | Deleted automatically, and with the account | They say which template applied on past days; a past day older than 12 months no longer needs it. |
| **Staff accounts** | While the customer keeps the account. Once deactivated (step 4), **12 months**, then deleted | Deleted automatically, with the account's settings | Usernames in audit entries are copies, kept with the audit (section 2), so deleting the account doesn't break the trail. |
| **Account settings** (`UserSettings`, including saved searches) | With the account | Deleted with it | Saved searches can hold patient names; see section 6. |
| **Notepads** (`Notepad`: each member of staff's own jottings) | **30 days without any change to the whole notepad** (`Notepad:KeepDays`), and never longer than the account. Each change restarts the 30 days for everything in it, so a notepad in use keeps its text until its owner takes it out | Cleared (deleted) when next read or saved after that, as there's no scheduled job; deleted with the account; its owner can clear it at any time | It's for jottings, not the record: what matters about a patient goes into their record with Add note to procedure. It may hold patient details that a patient's export and anonymisation can't find (it isn't linked to a patient), so it's kept briefly. A notepad nobody opens again stays in the database until its account is deleted or it's next opened; a scheduled purge would close that gap. A notepad used every day is never cleared, so a jotting in it has no limit until text is aged by when it was written (both in `docs/todo.md`; DPIA risk 26). Decided by the product owner; for the DPO to confirm. |
| **Notification choices** (`NotificationPreferences`) | With the account | Deleted with it (to be built: `docs/todo.md`) | A person's own choice; nothing needs it once the account is gone. |
| **Monitoring rules** (`MonitoringRules`: which admin watches which staff member, for what) | Until the admin deletes the rule, or the admin's or the watched person's account is deleted | Deleted then (to be built: `docs/todo.md`; until then they stay, and a rule whose admin is no longer an admin, or whose person is deleted, sends nothing) | A rule is only for supervising current work; the customer's monitoring policy should say when rules about a person are reviewed. |
| **Browser storage** (`localStorage` and `sessionStorage`, `medapp.*`) | Until sign-out | Cleared at sign-out, but for the four visual settings (built in step 7) | Shared computers. |
| **Stage definitions and templates** | For the life of the customer's contract | Returned or deleted at exit | Configuration, not personal data. Archived ones are kept because procedures refer to them. |
| **Activity types** | For the life of the customer's contract | Returned or deleted at exit | Calendar configuration, not personal data. Archived ones are kept because blocks and dated template rows refer to them. |
| **Day and week templates** (each person's own) | Until their owner or an admin deletes them (one that's in use is archived instead, and kept as long as dated rows or blocks refer to it) | Deleted by their owner or an admin; returned or deleted at exit. **Deleting an account doesn't remove its templates yet** (to be built: `docs/todo.md`): they stay, unused, where no list shows them. The migration that made templates personal gave the templates only leavers had used to the longest-standing admin | A person's usual working pattern (staff data, but not sensitive by design). Archived ones are kept because past days project from their dated rows. |
| **Application logs and traces** (Log Analytics, step 5) | **90 days** | Deleted automatically by the workspace's retention setting | Enough for troubleshooting and incident investigation; logs aren't the record. |
| **Azure activity and infrastructure sign-in logs** (supplier's own) | **12 months** | Deleted automatically | Security investigation. |

## 4. Backups

| Backup | Period | Status |
|---|---|---|
| **Azure SQL point-in-time restore** | **35 days** | 7 days today (Azure's default, `deploy/azure/main.bicep`); 35 planned in step 5 |
| **Azure SQL long-term retention** | **Monthly backups kept 12 months**; no yearly backups | Planned in step 5 |
| **Backups made for a recovery drill** | Deleted when the drill ends | Policy ([support-access-policy.md](support-access-policy.md)) |
| **Attached files: blob soft delete** | **7 days** after a file is purged or the container deleted | In place in design (`deploy/azure/main.bicep`) |
| **Attached files: backups** | **35 days,** to match the database, so a restore brings back files and their details together | **None today:** the files have soft delete only (`deploy/azure/README.md`, "Not yet"); planned in step 5 (Azure Backup's vaulted blob backups) |

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

Roadmap step 7 builds these. The privacy features work on roadmap step 7 must meet these requirements, and anything it doesn't cover stays open here. The first part of step 7 (branch `feature/privacy-features`) built what the Status column says.

| # | Feature | Detail | Status |
|---|---|---|---|
| 1 | **Retention settings per customer** | The classes and periods in sections 2 and 3, with these defaults, changeable by the customer's admin; a shorter clinical period needs a recorded approval. | **Partly built.** The adult and children periods are settings of the deployment (`Retention` in `appsettings.json`: 8 years; 25th birthday, or 26th at 17), with the Code's defaults. Not yet changeable by the customer's admin in the app (step 8 makes settings per customer), and no other classes. |
| 2 | **A retention class on each template** | Adult general by default; children worked out from the date of birth. | **Partly built:** adult or child from the date of birth at the conclusion of treatment. No classes on templates yet, so **a specialty record (mental health, maternity, oncology) is listed when the adult period ends, 12 to 22 years early.** Until classes exist, a customer that keeps specialty records sets `Retention:AdultYears` to its longest class, and keeps any specialty record listed early (Keep, with the class as the reason). The Retention Review page and the anonymise dialog say so. |
| 3 | **The conclusion of treatment date** | Worked out per patient as in section 2, and shown on the patient's page. | **Partly built:** worked out as in section 2 (the latest of the last change to any procedure, all closed, to the record, and to the procedure audit, which outlives a deleted procedure; reads such as file downloads don't count), shown on the review list. A 29 February birthday falls on 1 March in other years. Not on the patient's page. **Appointments aren't counted yet:** neither the last appointment's date nor open Requested or Booked appointments hold the date back (`docs/todo.md`). |
| 4 | **A due-for-review list and decisions** | Destroy, anonymise or keep (with a reason and a review date), approved by a named person. | **Built, without destroy:** anonymise, or keep with a reason and a review date up to 2 years ahead, each recorded in the activity log with the admin's name. Review opens the patient's page. |
| 5 | **Destruction across every table** | Patient, procedures, stages, assignments, audit entries (by `PatientId`, including those of deleted procedures), appointments and appointment history (by `PatientId`), note versions, access audit, and notifications that refer to the patient's procedures or appointments; the patient's attached files, live and deleted, with their thumbnails in blob storage and their `Attachment` rows. Saved searches whose terms match the patient's name are removed. | Not built. (The whole record is gathered from every table already, for exports and anonymisation: `PatientRecordRepository`.) |
| 6 | **Anonymisation** | Removes names, date of birth, email, the NHS number and every free-text field, including file names in `Attachment` rows and in audit entries, and appointment notes and cancellation reasons and the copies in the appointment history, deletes the attached files themselves, and unlinks the audit, so the remaining data can't identify anyone. | **Built, differently in two ways:** the date of birth keeps its year (for age statistics), and the audit stays linked by the patient's ID, so the audit trail's integrity survives. **The result is therefore pseudonymised, not anonymous:** with the patient ID, the year of birth, the dates and who did what, someone who knows the patient could recognise the record, so it stays personal data under this schedule. Procedure titles become their template's name, in the appointment history too, and appointments' notes and cancellation reasons, with their history's copies, are replaced. Saved searches aren't covered. **Attached files:** each file's name becomes "Anonymised file" with its type's usual extension, file names in the audit's `Details` become the removed-text placeholder, and every live file is deleted (soft) by the anonymising admin, with an `AttachmentDeleted` audit entry for each, its name already replaced; **their contents stay in storage until the purge job (row 9) removes them,** which isn't built. |
| 7 | **A destruction log** | As section 2, step 3; kept for as long as the customer uses MedApp, and included in the export at exit. | Partly: anonymising and keeping are in the activity log with the reason, the admin and the time. No separate destruction log. |
| 8 | **Legal hold** | Per patient and for the whole customer; suspends destruction and shows on the due-for-review list. | Not built. Keeping a record with a reason ("legal hold") and a review date stands in for it per patient. |
| 9 | **Automatic jobs** for operational data | Sessions and security events at 12 months, notifications at 6 months, staff calendar blocks (with day overrides and exceptions), meetings, and closed default-week rows at 12 months, deactivated accounts at 12 months, deleted files' contents at the purge window (section 3; also in `docs/todo.md`). A scheduled job that runs once across several API copies (a lease, or a Kubernetes CronJob), and logs what it deleted (counts, not content). | Not built. |
| 10 | **Note versions instead of copies in the audit** | So destroying or correcting a note doesn't leave its text in `Details` ([dpia.md](dpia.md), risk 4). | **Built for procedure notes** (`PatientProcedureNoteVersion`; the audit names the note and version only). Adding or editing a note is a change, so it moves the conclusion of treatment. Stage and appointment notes are still copied; anonymising replaces those copies in `Details`. |
| 11 | **Clear `localStorage` at sign-out** | Already in step 7's scope. | **Built:** every `medapp.` key in localStorage and sessionStorage but the four visual settings. |
| 12 | **Archiving in place of deletion** | Staff can't hard-delete a patient; erasure goes through an approved workflow ([data-subject-rights.md](data-subject-rights.md)). | Not built: deleting a patient with no procedures is unchanged, by the product owner's decision, and is now recorded. |
| 13 | **Backups set to section 4** | Step 5: point-in-time restore of 35 days and long-term retention of 12 months in `deploy/azure/main.bicep`, and backups of the attached files to match; a runbook step to re-apply the destruction log, and the purges of deleted files, after a restore. | Step 5. |
| 14 | **Log retention** | Step 5: the Log Analytics workspace set to 90 days. | Step 5. |
| 15 | **A deceased flag** | Step 9, so the periods after death can apply. | Step 9. |

## 7. The supplier's own records

Retention of the supplier's business data (contracts, invoices, support, complaints, breach records) is in its own record of processing ([ropa.md](ropa.md), part B). In short: contracts and accounts **6 years**; support requests **2 years** after closure; complaints **3 years** after closure; breach records and the support access log **6 years**; superseded versions of these governance documents **6 years**.
