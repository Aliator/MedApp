# Data subject rights

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Next review** | When roadmap step 7 ships its rights features; at least every 12 months |

How requests from patients and staff to exercise their rights under UK GDPR (Articles 12 to 21, and 22A to 22D on automated decisions) are handled for data in MedApp: who does what, what the app supports today, and what's planned.

## 1. Who handles a request

- **The customer handles it.** The customer is the controller, so the request is the customer's to answer, and the deadlines are the customer's.
- **The supplier helps.** As processor, it gives the help the data processing agreement sets out ([dpa-template.md](dpa-template.md), clause 9).
- **If a request reaches the supplier,** for example a patient who writes to [Company name]:
  1. The supplier passes it to the customer **within 2 working days**, by email to the customer's data protection contact, marked as a rights request.
  2. It replies to the requester only to say that requests about care records are handled by the clinic or hospital that provides the care, that it has passed the message to [Customer name], and gives the customer's contact details. It doesn't say whether that customer holds a record about them; the customer answers that.
  3. It doesn't act on the request, unless the customer instructs it.
  4. It logs the request: date received, date passed on, customer.
- **If the supplier can't tell which customer** the request is about, it asks the requester which clinic or hospital treats them, without searching customers' data.

## 2. Deadlines (for the customer)

- **One month** from receipt, extendable by **two further months** for complex or numerous requests, with the requester told within the first month (Article 12(3)).
- **The clock can pause** while the controller asks for information it reasonably needs to identify the person or the information sought (Article 12A, from the DUAA).
- **A reasonable and proportionate search** is what's required for subject access (from the DUAA) ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/subject-access-requests/a-guide-to-subject-access/)).
- **Free** in most cases. A reasonable fee, or a refusal, only for requests that are manifestly unfounded or excessive.
- **The supplier's help** with an export, correction or erasure is done **within 5 working days** of the customer's written instruction, or as agreed with the customer, so the customer has time to review.

## 3. Each right, today and planned

| Right | Applies to MedApp's data? | What MedApp supports today | Planned (roadmap step 7 unless stated) |
|---|---|---|---|
| **To be informed** (Articles 13 and 14) | Yes | Nothing in the app; it's the customer's privacy notice | Template notices in this folder ([privacy-notice-patients-template.md](privacy-notice-patients-template.md), [privacy-notice-staff-template.md](privacy-notice-staff-template.md)) |
| **Of access** (Article 15) | Yes | Staff can see a patient's details and procedures on screen, and the patient's timeline without its free text, and can download the patient's attached files one at a time. Admins can now see a patient's whole history on screen: the patient page's timeline shows every change to their procedures, with its free text, deleted procedures included (the audit log page still searches by procedure). **No export:** a full copy needs the supplier's manual procedure (section 4). | `GET api/patients/{id}/export`: everything held about a patient as JSON and a readable PDF (details, procedures, stages, note versions, assignments, and the access log if the customer wants it), with a step for a clinician to review and hold back anything that meets the serious harm test. **It must list the patient's attached files** (name, type, size, where attached, who uploaded it and when, including deleted files not yet purged) **and be able to include the files themselves**; this is required of the privacy features work (PR #68) once file attachments are merged. A test that fills every table, and blob storage, and checks the export includes each. |
| **To rectification** (Article 16) | Yes | Staff can edit a patient's details and a procedure's details. Finished stages are locked; an admin can unlock one so it can be corrected, and the unlock and the change are audited. **Patient edits aren't audited.** An attached file can't be changed or moved: a wrong one is deleted (soft, audited) and the right one uploaded. | Audited patient edits (before and after); amending a finished stage with a reason; a "disputed" flag to record that the patient disagrees with an opinion, which the ICO says is good practice ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-rectification/)). |
| **To erasure** (Article 17) | Often not, for health records (below) | Any staff member can permanently delete a patient who has **no** procedures, with no audit entry. A patient with procedures can't be deleted. An admin can delete a cancelled or archived procedure; its audit entries, including notes text, remain. **Attached files:** staff can delete files they uploaded, and admins any file, but deleting only hides it: its contents, its name and its audit entries stay, and the contents are removed only by a purge job that doesn't exist yet. A patient or procedure with files can't be deleted until its files are, and the deleted files' details and contents still remain afterwards. | Hard deletion replaced by archiving. True erasure or anonymisation only through an approved workflow (records made in error, or past their retention date), covering audit details, notifications, saved searches and backups ([retention-schedule.md](retention-schedule.md)), and **attached files:** the files and their thumbnails in blob storage, their `Attachment` rows, and file names in audit entries. Required of the privacy features work (PR #68) once file attachments are merged. A purge job for deleted files' contents (proposed 30 days after deletion). |
| **To restriction** (Article 18) | Yes | None | A "restricted" flag: hides the patient from lists and searches except for named roles, makes the record read-only (no files added or deleted, and downloads limited to the named roles), and shows a banner. |
| **To data portability** (Article 20) | Only where the customer relies on **consent or contract** (for example a private clinic under 6(1)(b)). Not for NHS bodies relying on public task ([Article 20](https://www.legislation.gov.uk/eur/2016/679/article/20)). | None | The export above, in JSON, with attached files in their original formats. |
| **To object** (Article 21) | Where the customer relies on **public task or legitimate interests** | None in the app; the customer considers the objection | Contact preferences (for example "don't email"). |
| **Automated decisions** (Articles 22A to 22D) | No: MedApp makes none | Not applicable | Must stay so ([roles-and-lawful-bases.md](roles-and-lawful-bases.md)) |
| **To withdraw consent** | Only if a customer relies on consent, which the supplier advises against | Not applicable | |

**Staff users** have the same rights for their own data (account, sessions, settings, notifications, their actions in the audit trail, including the files they uploaded, deleted or downloaded). Today a customer's admin can see a user's account and roles; sessions, settings and the user's audit entries need the manual procedure.

### Health data points

- **Erasure usually doesn't apply** to a health record that's still needed. Article 17(3) exempts processing needed for health purposes (17(3)(c), with Article 9(2)(h)), for legal obligations, and for legal claims ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-erasure/)). The customer decides, case by case.
- **The serious harm test:** a health professional must be consulted before disclosing health data that might cause serious harm to the physical or mental health of the patient or someone else (DPA 2018, Schedule 3) ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/right-of-access/health-information/)).
- **Third parties:** notes and attached files may mention other people (relatives, other patients, the clinicians who wrote a letter). The customer must consider them before disclosing (DPA 2018, Schedule 2, paragraph 16).
- **Files are disclosed as they are:** a photo's metadata (where and when it was taken) goes with it unless the customer removes it; the serious harm test applies to results and letters as to notes.
- **Requests on behalf of others:** parents for children, attorneys, and the representatives of people who have died (whose records fall under the Access to Health Records Act 1990, not UK GDPR) are the customer's to handle.

## 4. The interim manual procedure (until step 7)

Until MedApp has its own export and rights features, the supplier carries out these on the customer's written instruction.

1. **Instruction.** A named contact at the customer sends a written instruction through the support route: which patient (by MedApp's patient ID, not by name in the ticket), which right, and what's needed.
2. **Check.** The supplier confirms the instruction came from a contact named in the agreement.
3. **Extract.** An authorised person ([support-access-policy.md](support-access-policy.md)) runs the **documented export queries** [to be written, and kept in `docs/runbooks/`, before the first customer goes live], which collect, for the patient's ID:
   - the `Patient` row
   - their procedures, stages and assignments
   - every audit entry with their `PatientId`, including those of deleted procedures, and those about their files
   - their `Attachment` rows (by `PatientId`), including deleted files not yet purged, and, if the customer asks for them, the files themselves from blob storage
   - notifications about their procedures
   - staff usernames for the IDs in all of these

   For a **staff user**, the same for their account, roles, sessions, settings, notifications and audit entries by `UserId`, and the `Attachment` rows they uploaded or deleted (without the files, which are about patients).
4. **Deliver.** The result, with any files, is put in an archive encrypted with a password, sent to the customer by a secure route agreed with them, and the password sent separately. Nothing is kept on the supplier's devices; the file is deleted from wherever it was made once the customer confirms receipt, and in any case within 30 days.
5. **Corrections and erasures** are made through the app where it can do them, and otherwise by a reviewed database change, with the customer's instruction kept in the ticket.
6. **Log** the request, the access, and the delivery ([support-access-policy.md](support-access-policy.md), access log).

**Limits today:**
- **Reads aren't recorded,** so the supplier can't say who has viewed a patient's record before step 7 is in place.
- **Saved searches** in staff settings may contain a patient's name; they can't be searched reliably by patient.
- **Logs** may hold record IDs in URLs; they aren't included in an export, and are kept 90 days once step 5 sets that.
- **Downloaded copies** of files on staff computers are outside MedApp. The audit log says who downloaded which file and when, so the customer can find them.
- **Deleted files** are kept, with their contents, until a purge job exists. They're still held, so they're part of a full answer to a request.

## 5. The supplier's own processing

For requests about the supplier's own data (customer contacts, marketing, support correspondence, its staff), the supplier is the controller. The DPO handles them within the deadlines in section 2, and logs them. Complaints follow [complaints.md](complaints.md).
