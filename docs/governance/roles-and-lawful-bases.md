# Roles and lawful bases

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft), 16 November 2026: adds appointments and staff calendars; 0.3 (draft), 17 November 2026: meetings and personal calendar templates (review of #99); 0.4 (draft), 18 November 2026: referrals, and the referrers named in them |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Approved by** | Not yet approved |
| **Next review** | On approval, then every 12 months, and whenever the processing changes |

This document settles who is responsible for the personal data in MedApp, and on what legal grounds it's processed. The other documents in `docs/governance/` build on it.

**Terms used:**
- **The supplier:** [Company name], which develops MedApp and runs it as a hosted service.
- **The customer:** a clinic, private hospital, NHS trust or other care provider that uses MedApp for its patients.
- **UK GDPR:** the UK General Data Protection Regulation, as amended by the Data (Use and Access) Act 2025 (DUAA).
- **DPA 2018:** the Data Protection Act 2018.

## 1. Controller and processor

### The decision

| Personal data | Controller | Processor | Why |
|---|---|---|---|
| **Patients** recorded in MedApp: names, date of birth, email, NHS number, pathways, stage notes, attached files, appointments (with their notes and cancellation reasons), referrals (with the reason for referral and the letter) and their history, and the audit trail and appointment history about them | The customer | The supplier | The customer decides why patients' data is processed (to deliver and record their care) and the essential means (what is recorded, who sees it, how long it's kept). The supplier processes it only to provide the service, on the customer's documented instructions. |
| **The customer's staff** who use MedApp: usernames, password hashes, roles, sessions (IP address and browser), settings, notifications, calendars (blocked time, such as annual leave, their own day and week templates and default week, and meetings admins arrange), and their actions in the audit trail and appointment history | The customer | The supplier | The customer decides who gets an account and with which role, sets up the activity types (each member of staff, or an admin, their own templates), arranges meetings, and uses the audit trail to supervise its own staff. |
| **Referrers** named on a referral: the referring clinician and their practice, as written on the letter | The customer | The supplier | The customer receives the referral and decides what to record from it; the supplier holds it as part of the patient's record. |
| **Service logs and backups** of the hosted service that contain the above | The customer | The supplier | They exist only to run, secure and recover the customer's service. |
| **The supplier's own business data:** customer contacts, contracts, billing, sales and marketing, support correspondence (the contact details, not the patient data), complaints, the supplier's own staff and contractors, and visitors to its website | The supplier | Not applicable | The supplier decides why and how these are processed, for its own business. |

The supplier and the customer are **not joint controllers**. The supplier doesn't decide the purposes of the patient or staff processing, and doesn't share in its benefits.

### Why the supplier is a processor

The ICO's tests for a processor ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/controllers-and-processors/controllers-and-processors/what-does-it-mean-if-you-are-a-processor/)) fit the supplier's role:
- **Instructions:** the customer decides what to collect and why. MedApp's features (patients, pathways, templates, calendars and appointments, audit) are the means the customer chooses to use.
- **No own purpose:** the supplier gets no benefit from the data beyond payment for the service.
- **Technical decisions only:** the supplier decides the "non-essential means": the hosting, the database, the security controls. The ICO allows a processor to make those.

### What would make the supplier a controller, and the supplier's commitments

A processor that uses customer data for its own purposes becomes a controller of it, with all of a controller's duties for that processing (UK GDPR Article 28(10)). So the supplier commits to the following. They are written into the data processing agreement ([dpa-template.md](dpa-template.md)).
- **No product analytics on customer data.** MedApp has no analytics, tracking or telemetry about what users do. OpenTelemetry (`MedApp.ServiceDefaults`) carries technical logs, metrics and traces, used only to run the service.
- **No training of AI models** on customer data, and no customer data given to AI tools. The supplier's AI coding assistants work only with source code, documentation and synthetic data ([support-access-policy.md](support-access-policy.md)).
- **No benchmarking, sale or sharing** of customer data, aggregated or not, without the customer's written instruction.
- **No secondary use** (research, planning, commercial). If that's ever offered, it needs its own lawful basis, a DPIA update, and the National Data Opt-out ([nhs-context.md](nhs-context.md)).
- **Aggregate service measurements** that contain no personal data, such as uptime, request counts and error rates, are the supplier's own and aren't covered by this.

### Hosting models

- **Hosted service (the only model offered):** the supplier runs MedApp on Microsoft Azure in the UK ([international-transfers.md](international-transfers.md)), one deployment per customer until multi-tenancy is built (roadmap step 8), then shared infrastructure with a dedicated option. The roles above apply to both.
- **Self-hosting** with the Helm chart (`deploy/helm/medapp`) is **not offered** today. If it ever is, the customer runs it, and the supplier processes personal data only when it gives support with access to the customer's system. A separate, shorter agreement would cover that.

## 2. Lawful bases for customers (guidance)

Each customer chooses its own lawful bases, because it is the controller. This section is guidance for customers, and the templates in this folder use it. A customer's DPO must confirm the choice for their organisation.

MedApp holds **special category data**: health data about patients (UK GDPR Article 9). So every customer needs both an **Article 6 lawful basis** and an **Article 9 condition**. For health and social care, the Article 9 condition also needs a **DPA 2018 Schedule 1 condition**.

### Patients

| Customer | Article 6 basis | Article 9 condition | DPA 2018 condition |
|---|---|---|---|
| **NHS bodies** (trusts, foundation trusts, integrated care boards, GP practices delivering NHS services) | **6(1)(e) public task:** providing health care is a function set out in law (the NHS Act 2006 and the Health and Social Care Act 2012). Public task also covers keeping the records the Records Management Code of Practice sets out (the Code is guidance; for NHS bodies the legal duty to keep records is the Public Records Act 1958). Use **6(1)(c) legal obligation** only for a duty laid down in law. | **9(2)(h)** health or social care | Section 10(1)(c) and **Schedule 1, Part 1, paragraph 2** (health or social care purposes) |
| **Private clinics and hospitals** treating self-paying or insured patients | **6(1)(b) contract** where the patient has a contract with the clinic for their treatment. Otherwise **6(1)(f) legitimate interests**, with a written legitimate interests assessment. | **9(2)(h)** | **Schedule 1, Part 1, paragraph 2** |
| **Independent providers delivering NHS-funded care** under an NHS contract | Take advice. NHS England's guidance for shared care records treats such providers as able to rely on **6(1)(e)** for NHS-funded care ([NHS England](https://digital.nhs.uk/data-and-information/information-governance/guidance/information-governance-framework-shared-care-records/requirements)). Use **6(1)(b) or (f)** for private patients. | **9(2)(h)** | **Schedule 1, Part 1, paragraph 2** |

**Points that apply to every customer:**
- **Not consent.** Customers should not rely on consent (Article 6(1)(a) or 9(2)(a)) for recording care. A patient can't freely refuse a record of their own treatment, and consent can be withdrawn at any time, which a care record can't follow. Consent to treatment, and the common law duty of confidentiality, are separate matters (below).
- **No appropriate policy document is needed** for Schedule 1, Part 1, paragraph 2. Paragraph 2 doesn't need one, although some other Schedule 1 conditions do, including paragraph 1 (employment), below ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/lawful-basis/special-category-data/what-are-the-conditions-for-processing/)).
- **Professional secrecy.** Article 9(2)(h) applies only when the data is processed by or under the responsibility of a health professional, or another person who owes a duty of confidentiality (Article 9(3), and DPA 2018 section 11). Customers meet this through their clinicians' professional duties and their staff's contracts. The supplier meets it for its own people through confidentiality clauses ([support-access-policy.md](support-access-policy.md)).
- **Children.** Patients may be children. The bases are the same. Privacy information should be written so a child, or the person with parental responsibility, can understand it ([privacy-notice-patients-template.md](privacy-notice-patients-template.md)).
- **No automated decision-making.** MedApp makes no decisions about patients by automated means, and doesn't profile them. Requesting an appointment when a stage that involves a visit becomes current follows the customer's own template setting; staff decide whether, when and with whom the patient is seen. Articles 22A to 22D (which the DUAA put in place of Article 22) don't apply. That must stay true; see the device rule in roadmap step 6.
- **Other conditions that may apply** for particular records: 9(2)(f) for the establishment, exercise or defence of legal claims (for example keeping a record after a complaint); 9(2)(i) public health, with Schedule 1, Part 1, paragraph 3, if a customer uses MedApp for a public health purpose. Neither is needed for ordinary use.

### The common law duty of confidentiality

UK GDPR isn't the only law. Patient information given in confidence is protected by the common law duty of confidentiality.
- **For direct care,** a patient's **implied consent** covers sharing among the care team, as long as the patient has been told how their information is used (Caldicott Principle 8; [nhs-context.md](nhs-context.md)).
- **MedApp is a tool for direct care,** so implied consent applies to access by the customer's staff involved in the patient's care. It doesn't cover staff looking at records of patients they aren't caring for, which MedApp doesn't yet prevent ([dpia.md](dpia.md), risk 1).
- **The supplier** processes the information as the customer's processor, under a contract with a duty of confidentiality. It isn't a disclosure to a third party.

### The customer's staff

| Customer | Article 6 basis | Notes |
|---|---|---|
| **NHS bodies** | **6(1)(e) public task** for running the service; **6(1)(c) legal obligation** for the security and audit records that Article 32 requires (the DSPT is a contractual requirement, covered by public task) | Staff data isn't special category data in MedApp. MedApp is designed to hold no health or other special category data about staff: calendars record an activity (annual leave, training), not a reason, and staff are told not to write health reasons in a block's note ([dpia.md](dpia.md), risk 25). If a note does reveal health, the customer needs an Article 9 condition for it, usually 9(2)(b) employment, with DPA 2018 Schedule 1, Part 1, paragraph 1 and an appropriate policy document; so the better course is to remove the note. |
| **Private providers** | **6(1)(b) contract** (the employment or engagement contract) for giving staff accounts and calendars; **6(1)(f) legitimate interests** for sessions, audit and security monitoring | A legitimate interests assessment should cover the audit trail, the access monitoring that roadmap step 7 adds, and admins' monitoring rules (telling admins about named staff members' actions: DPIA risk 24). NHS bodies should cover the monitoring rules in their monitoring policy. |

## 3. Lawful bases for the supplier's own processing

The supplier is the controller here. Each activity is in the supplier's own record of processing ([ropa.md](ropa.md), part B).

| Processing | Article 6 basis |
|---|---|
| Customer contacts, contracts and account management | 6(1)(b) contract with the customer organisation; 6(1)(f) for the individuals who are its contacts |
| Billing, accounts and tax records | 6(1)(b) and 6(1)(c) legal obligation (company and tax law) |
| Marketing to business contacts | 6(1)(f) legitimate interests, with the Privacy and Electronic Communications Regulations (PECR) rules for electronic marketing and an opt-out in every message |
| Support requests (the requester's contact details) | 6(1)(b) contract, and 6(1)(f) |
| Complaints about the supplier's own processing | 6(1)(c) legal obligation (DPA 2018 section 164A, from the DUAA) |
| The supplier's staff and contractors, including security vetting and training records | 6(1)(b) and 6(1)(c) |
| Security logs of the supplier's own systems (sign-ins to Azure, GitHub, email) | 6(1)(f) legitimate interests (keeping its systems secure), and 6(1)(c) where Article 32 requires it |
| Recruitment | 6(1)(b) steps before a contract, and 6(1)(f) |

The supplier processes no special category data for its own purposes, except where its staff tell it of a health condition (for example for sickness absence or reasonable adjustments). That relies on 9(2)(b) employment, with DPA 2018 Schedule 1, Part 1, paragraph 1, and **needs an appropriate policy document**. [To write when the supplier first employs staff.]

## 4. Registration, the DPO and accountability

| Item | Decision | Status |
|---|---|---|
| **ICO data protection fee** | The supplier pays the fee as a controller of its own business data. Registration number: [ICO registration number]. Tier: [tier]. | To do. DTAC asks for the number. |
| **Data protection officer** | **The supplier appoints a DPO.** Article 37(1)(c) requires one when a processor's core activities consist of large-scale processing of special category data, which hosting patient records for many care providers is. A small supplier can use an **external DPO service**. Contact: [DPO contact]. The DPO is published in the privacy notices and given to the ICO. | To do |
| **Caldicott Guardian** | Not required for the supplier, which is a processor. NHS customers have their own. The supplier names a **senior information risk owner (SIRO)**: [SIRO name], the director accountable for information risk. | To do |
| **Representative in the UK** | Not needed: the supplier is established in the UK. | Not applicable |
| **Records of processing** | Kept, for both roles ([ropa.md](ropa.md)). The exemption for organisations under 250 staff doesn't apply, because the processing is of special category data and isn't occasional (Article 30(5)). | Draft |
| **DPIA** | A product DPIA ([dpia.md](dpia.md)) that customers adapt for their own. | Draft |

## 5. Data protection principles: how MedApp meets them

| Principle (Article 5) | How | Where it's covered |
|---|---|---|
| Lawfulness, fairness and transparency | The bases above; privacy notice templates | This document; [privacy-notice-patients-template.md](privacy-notice-patients-template.md); [privacy-notice-staff-template.md](privacy-notice-staff-template.md) |
| Purpose limitation | The supplier's commitments in section 1; no secondary use | This document; [dpa-template.md](dpa-template.md) |
| Data minimisation | A small patient record (name, date of birth, email, an optional NHS number); guidance against identifiers in free text | [data-inventory.md](data-inventory.md); [dpia.md](dpia.md) |
| Accuracy | Patients and pathways can be edited; finished stages lock; unlocks are audited | [data-subject-rights.md](data-subject-rights.md) |
| Storage limitation | The retention schedule. **Not enforced by the app yet.** | [retention-schedule.md](retention-schedule.md) |
| Integrity and confidentiality | The security measures | [dpia.md](dpia.md); [security-policies.md](security-policies.md) |
| Accountability | These documents, the DPO, training and the DSPT | [README.md](README.md); [dspt-readiness.md](dspt-readiness.md) |
