# Record of processing activities: template for customers

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft template), 16 November 2026 |
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
| **Purpose** | Recording patients and coordinating the stages of the procedures carried out for them, for their direct care; keeping an accurate record of who did what and when. |
| **Data subjects** | Patients, including children [if applicable] |
| **Personal data** | First name, last name, date of birth, email address. Procedures: title, notes, due date, priority, status. Stage notes written by staff. |
| **Special category data** | Health data: the procedures a patient is having, and clinical notes. |
| **Article 6 basis** | [NHS bodies: 6(1)(e) public task. Private providers: 6(1)(b) contract with the patient, or 6(1)(f) legitimate interests with an assessment.] |
| **Article 9 condition** | 9(2)(h) health or social care |
| **DPA 2018 Schedule 1 condition** | Part 1, paragraph 2 (health or social care purposes). No appropriate policy document needed. |
| **Common law duty of confidentiality** | Implied consent for direct care |
| **Source of the data** | The patient; referrers; [other sources] |
| **Recipients** | Your staff with MedApp accounts. Your processor, [Company name], and its sub-processors ([sub-processors.md](sub-processors.md)). [Others you share with, outside MedApp.] |
| **Processor** | [Company name], under the data processing agreement signed [date] |
| **Transfers outside the UK** | None by your processor: data is stored in the UK. Its sub-processor Microsoft may, in rare cases, access data from outside the UK for support, under Microsoft's data protection terms and the safeguards they contain ([international-transfers.md](international-transfers.md)) |
| **Retention** | [Your schedule.] The supplier's default follows the NHS Records Management Code of Practice: adult records 8 years after the conclusion of treatment; children's records until the 25th birthday, or the 26th if the patient was 17 at the conclusion of treatment ([retention-schedule.md](retention-schedule.md)). **MedApp doesn't apply retention automatically yet.** |
| **Security measures** | See the processor's description ([ropa.md](ropa.md), A6) and your own: [who can have an account, how accounts are reviewed, device and network controls]. |
| **DPIA** | [Reference to your DPIA, adapted from [dpia.md](dpia.md)] |
| **Privacy information** | [Where your privacy notice says this; see [privacy-notice-patients-template.md](privacy-notice-patients-template.md)] |
| **Rights requests** | [Your procedure; see [data-subject-rights.md](data-subject-rights.md)] |
| **National Data Opt-out** | Doesn't apply: this is direct care. |

## Entry 2: the audit trail of procedures

| Item | Detail |
|---|---|
| **Purpose** | Accountability and the integrity of care records: knowing who changed what on a procedure, and when, including after deletes; investigating incidents and complaints. |
| **Data subjects** | Patients; your staff |
| **Personal data** | Procedure ID and title, patient ID, stage name, the action, the staff member's ID and username, the time, and the change, including old and new notes text and the reason for blocking or cancelling. |
| **Special category data** | Health data (the notes text) |
| **Article 6 basis** | [As entry 1; also 6(1)(c) where a legal duty to keep the record applies] |
| **Article 9 condition** | 9(2)(h), with Schedule 1, Part 1, paragraph 2 |
| **Recipients** | Your MedApp admins (only admins can read the audit log) |
| **Retention** | The same as the record it relates to |

## Entry 3: staff accounts, sessions and notifications

| Item | Detail |
|---|---|
| **Purpose** | Giving your staff secure access; knowing who did what; telling staff when they are assigned work. |
| **Data subjects** | Your staff with MedApp accounts |
| **Personal data** | Username, password hash, roles, lockout state; sign-in sessions with IP address and browser; account settings, including saved searches; notifications. |
| **Special category data** | None |
| **Article 6 basis** | [NHS bodies: 6(1)(e), and 6(1)(c) for security records. Private providers: 6(1)(b) for the account; 6(1)(f) for sessions and security.] |
| **Recipients** | Your MedApp admins; your processor |
| **Retention** | Accounts: while the person needs access, then removed [within your joiners, movers and leavers process]. Sessions: 12 months. Notifications: 6 months ([retention-schedule.md](retention-schedule.md)). **Not applied automatically yet.** |
| **Privacy information** | [Your staff privacy notice; see [privacy-notice-staff-template.md](privacy-notice-staff-template.md)] |
