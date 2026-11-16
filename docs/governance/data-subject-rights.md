# Data subject rights

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft), 16 November 2026: the rights features of roadmap step 7 |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Next review** | When the rest of roadmap step 7 ships (archiving, the clinician review step, contact preferences); at least every 12 months |

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
| **Of access** (Article 15) | Yes | Staff can see a patient's details and procedures on screen, and the patient's timeline without its free text. Admins can see a patient's whole history on screen: the patient page's timeline shows every change to their procedures, with its free text, deleted procedures included. **An admin exports everything held about a patient** from the patient's page (Privacy, Export Record): details, procedures with stages, notes and assignments, the history of changes to their procedures (including deleted ones), the access log, and notifications about their procedures. It shows as a printable page and downloads as JSON (`GET api/patients/{id}/export`), and the export is itself recorded in the activity log. The page reminds the admin to have a clinician check it first; **that review is done by hand** (section 3, health data points). The patient's page also shows admins who has accessed the record (the access report). | A step in the app for the clinician's review, holding back what meets the serious harm test; note versions once they exist; a test that fills every table and checks the export includes each (the export's tests check every part of the record it reads). |
| **To rectification** (Article 16) | Yes | Staff can edit a patient's details and a procedure's details. Finished stages are locked; an admin can unlock one so it can be corrected, and the unlock and the change are audited. **Patient edits are recorded** in the activity log (which fields changed, by whom and when; not the old values). **An admin can mark a record as disputed** with what the patient disputes, which every member of staff sees on a banner; the ICO says noting the challenge is good practice ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-rectification/)). | The old values of patient edits (a version table); amending a finished stage with a reason. |
| **To erasure** (Article 17) | Often not, for health records (below) | Any staff member can permanently delete a patient who has **no** procedures; the deletion is recorded in the activity log. A patient with procedures can't be deleted. An admin can delete a cancelled or archived procedure; its audit entries remain. **An admin can anonymise a record** (Privacy, Anonymise Record, or from the retention review): the name, email and every note, title and reason, in the record and its audit trail (so the timeline too) and notifications, are replaced with placeholders, and the date of birth keeps only its year; the structure and the audit trail's who and when stay. It needs the patient's full name typed to confirm and a reason, which the activity log keeps, and it's refused while a procedure is under way or the record is restricted. It can't be undone. | Hard deletion replaced by archiving; an approval step (a second person) for anonymisation; saved searches that name the patient, and backups ([retention-schedule.md](retention-schedule.md)). |
| **To restriction** (Article 18) | Yes | **An admin can restrict a record** with a reason (Privacy, Restrict Record). While it's restricted, nothing about the patient or their procedures can be changed (the API refuses with a 409, and the patient's and procedures' pages offer no changes), and everyone who opens it sees a banner with the reason. Lifting it is recorded too. | Hiding a restricted patient from lists and searches except for named roles. |
| **To data portability** (Article 20) | Only where the customer relies on **consent or contract** (for example a private clinic under 6(1)(b)). Not for NHS bodies relying on public task ([Article 20](https://www.legislation.gov.uk/eur/2016/679/article/20)). | The export above, downloaded as JSON. | |
| **To object** (Article 21) | Where the customer relies on **public task or legitimate interests** | None in the app; the customer considers the objection | Contact preferences (for example "don't email"). |
| **Automated decisions** (Articles 22A to 22D) | No: MedApp makes none | Not applicable | Must stay so ([roles-and-lawful-bases.md](roles-and-lawful-bases.md)) |
| **To withdraw consent** | Only if a customer relies on consent, which the supplier advises against | Not applicable | |

**Staff users** have the same rights for their own data (account, sessions, settings, notifications, their actions in the audit trail). Today a customer's admin can see a user's account and roles; sessions, settings and the user's audit entries need the manual procedure.

### Health data points

- **Erasure usually doesn't apply** to a health record that's still needed. Article 17(3) exempts processing needed for health purposes (17(3)(c), with Article 9(2)(h)), for legal obligations, and for legal claims ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-erasure/)). The customer decides, case by case.
- **The serious harm test:** a health professional must be consulted before disclosing health data that might cause serious harm to the physical or mental health of the patient or someone else (DPA 2018, Schedule 3) ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/right-of-access/health-information/)).
- **Third parties:** notes may mention other people (relatives, other patients). The customer must consider them before disclosing (DPA 2018, Schedule 2, paragraph 16).
- **Requests on behalf of others:** parents for children, attorneys, and the representatives of people who have died (whose records fall under the Access to Health Records Act 1990, not UK GDPR) are the customer's to handle.

## 4. The manual procedure (what the app doesn't do yet)

For patients, the customer's admins now answer access, rectification, restriction and erasure requests in the app (section 3). The supplier carries out the rest on the customer's written instruction: requests about **staff users** (their account, sessions, settings and activity), anything the export doesn't cover (saved searches, logs, backups), and help when a customer's admin can't do it themselves.

1. **Instruction.** A named contact at the customer sends a written instruction through the support route: which patient (by MedApp's patient ID, not by name in the ticket), which right, and what's needed.
2. **Check.** The supplier confirms the instruction came from a contact named in the agreement.
3. **Extract.** An authorised person ([support-access-policy.md](support-access-policy.md)) runs the **documented export queries** [to be written, and kept in `docs/runbooks/`, before the first customer goes live], which collect, for the patient's ID:
   - the `Patient` row
   - their procedures, stages and assignments
   - every audit entry with their `PatientId`, including those of deleted procedures
   - notifications about their procedures
   - staff usernames for the IDs in all of these

   For a **staff user**, the same for their account, roles, sessions, settings, notifications and audit entries by `UserId`.
4. **Deliver.** The result is put in a file encrypted with a password, sent to the customer by a secure route agreed with them, and the password sent separately. Nothing is kept on the supplier's devices; the file is deleted from wherever it was made once the customer confirms receipt, and in any case within 30 days.
5. **Corrections and erasures** are made through the app where it can do them, and otherwise by a reviewed database change, with the customer's instruction kept in the ticket.
6. **Log** the request, the access, and the delivery ([support-access-policy.md](support-access-policy.md), access log).

**Limits today:**
- **Reads are recorded only from step 7 on.** Who viewed a record before then can't be said. A view is recorded once per person every 5 minutes, and a record opened through a page the server renders shows the server's address rather than the person's.
- **Saved searches** in staff settings may contain a patient's name; they can't be searched reliably by patient.
- **Logs** may hold record IDs in URLs; they aren't included in an export, and are kept 90 days once step 5 sets that.

## 5. The supplier's own processing

For requests about the supplier's own data (customer contacts, marketing, support correspondence, its staff), the supplier is the controller. The DPO handles them within the deadlines in section 2, and logs them. Complaints follow [complaints.md](complaints.md).
