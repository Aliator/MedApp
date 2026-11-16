# Record of processing activities: the supplier

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Approved by** | Not yet approved |
| **Next review** | Every 6 months, when a customer or sub-processor is added or removed, and when the processing changes |

UK GDPR Article 30 requires a written record of processing. The supplier needs two:
- **Part A,** as a **processor** for its customers (Article 30(2)).
- **Part B,** as a **controller** of its own business data (Article 30(1)).

The exemption for organisations under 250 staff doesn't apply, because the processing includes special category data and isn't occasional (Article 30(5)). The record follows the ICO's templates ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/accountability-and-governance/documentation/how-do-we-document-our-processing-activities/)). Customers keep their own record as controllers, using [ropa-customer-template.md](ropa-customer-template.md).

## Part A: processing as a processor (Article 30(2))

### A1. The processor

| Item | Detail |
|---|---|
| **Name** | [Company name], company number [company number] |
| **Address** | [Address] |
| **Contact** | [Privacy contact email] |
| **Data protection officer** | [DPO name], [DPO contact] |
| **ICO registration** | [ICO registration number] |
| **Representative** | Not applicable (established in the UK) |

### A2. The controllers it acts for

One row per customer. Each must have a signed data processing agreement ([dpa-template.md](dpa-template.md)) before any of its personal data is processed.

| Customer (controller) | Address | Contact for data protection | Their DPO | Agreement signed | Deployment | Start |
|---|---|---|---|---|---|---|
| [Customer name] | [Address] | [Customer contact] | [Customer DPO contact] | [Date] | [Dedicated, or shared from step 8] | [Date] |

There are **no customers yet**. MedApp has never held real patient data.

### A3. Categories of processing carried out for each controller

All customers receive the same service, so the categories are the same for each.

| # | Processing | Data subjects | Personal data (see [data-inventory.md](data-inventory.md)) | Operations |
|---|---|---|---|---|
| A3.1 | **Hosting the patient and procedure records** | Patients | Name, date of birth, email; procedures (title, notes, due date, priority, status); stage notes; assignments | Storage, retrieval, display, change, deletion, backup and restore, on the customer's staff's actions |
| A3.2 | **Hosting the audit trail** | Patients; staff users | Who did what to which procedure and when, with old and new values, including copies of notes text | Recording, storage, retrieval by the customer's admins (the full trail) and staff (a per-patient timeline without its free text) |
| A3.3 | **Staff accounts and sign-in** | Staff users | Username, password hash, roles, lockout state, sessions (IP address, browser), settings (including saved searches), notifications | Authentication, authorisation, storage |
| A3.4 | **Live notifications** | Staff users; patients indirectly | Procedure title, assigner's username | Transmission between API copies through Valkey, display |
| A3.5 | **Operating the service** | Staff users; patients indirectly | Application logs and traces, which can contain record IDs and URLs | Monitoring, troubleshooting, security investigation |
| A3.6 | **Support, on the customer's instruction** | Patients; staff users | Whatever the instruction needs, under [support-access-policy.md](support-access-policy.md) | Viewing, correcting, exporting, restoring |
| A3.7 | **Data subject requests and exit, on instruction** | Patients; staff users | All the customer's data | Export, correction, erasure, return and deletion at the end of the contract |

**Special category data:** health data about patients (A3.1, A3.2, A3.4, A3.7, and possibly A3.6).

**What the supplier does not do with customer data:** analytics, profiling, AI training, marketing, sale, research, or any purpose of its own ([roles-and-lawful-bases.md](roles-and-lawful-bases.md), section 1).

### A4. Transfers outside the UK

**None by the supplier.** All customer personal data is stored and processed in Microsoft Azure's UK South region, with backups in the UK. The supplier's own employees may, in an emergency, reach the service from abroad (logged and reported); that isn't a restricted transfer, because they're part of the supplier. **Microsoft's support engineers may, in rare cases, access data from outside the UK** for service operations and support, under its data protection terms; that is a restricted transfer, covered by Microsoft's safeguards. See [international-transfers.md](international-transfers.md) for the assessment and the safeguards.

### A5. Sub-processors

See [sub-processors.md](sub-processors.md). Today: Microsoft (Azure). GitHub holds no customer personal data.

### A6. General description of technical and organisational security measures

A summary. The DPIA ([dpia.md](dpia.md), section 6) links each measure to the code, and marks what's planned.

**In place in the code and the deployment design:**
- **Access:** sign-in with a username and password; Identity's salted password hashes; server-generated first passwords; lockout after 5 failures for 5 minutes, with sign-in throttling in front of it; the 10,000 most common passwords refused; server-side sessions in an HttpOnly, Secure, SameSite=Lax cookie, 8 hours long (extendable on request to 24 at most from sign-in); two roles, checked on every API endpoint except sign-in, sign-out and `whoami`; admin-only user management; the full audit trail is admin-only, and staff see a per-patient timeline without its free text.
- **Audit:** every change to a procedure is recorded with who, when and what, in the same database transaction, and kept after deletes.
- **Transport:** TLS to the ingress, HSTS, and TLS to Azure SQL.
- **Storage:** Azure SQL with public access off and a private endpoint; encryption at rest with Microsoft-managed keys (TDE, Azure's default); a delete lock on the database server.
- **Platform:** Kubernetes with default-deny network policies, the `restricted` Pod Security Standard, non-root containers, read-only filesystems, and secrets from Key Vault through External Secrets; Entra ID-only access to the cluster; no stored cloud secrets (OIDC and workload identity).
- **Pages:** no caching of pages that carry personal data (`Cache-Control: no-store`).
- **Development:** synthetic data only; required CI checks; a written review of every merge since #51, by a separate AI agent not given the PR's own conclusions (not yet a second human reviewer).

**Planned before real patient data** (roadmap steps 2, 4, 5 and 7, and step 6's patient banner): a patient banner; MFA; an idle session timeout; a least-privilege, passwordless database identity; monitoring and alerting; tested backups with longer retention; audit of reads and of patient and account changes; retention enforcement; subject access export.

**Organisational:** a DPO; confidentiality clauses; MFA on every company account [to confirm]; annual data security training; a support access policy; a breach procedure; Cyber Essentials; the DSPT (all planned; see [dspt-readiness.md](dspt-readiness.md)).

## Part B: processing as a controller (Article 30(1))

### B1. The controller

As A1.

### B2. Processing activities

| # | Activity | Purpose | Data subjects | Personal data | Lawful basis | Recipients | Transfers | Retention | Security |
|---|---|---|---|---|---|---|---|---|---|
| B2.1 | **Customer relationship and contracts** | Selling to and managing customers | Customer contacts (staff of clinics and trusts) | Name, job title, work email and phone, organisation | 6(1)(b) with the organisation; 6(1)(f) for its contacts | [CRM provider], if one is used | [Depends on the provider] | 6 years after the contract ends | MFA on company accounts [to confirm before approval] |
| B2.2 | **Billing and accounts** | Invoicing, payment, tax | Customer finance contacts | Name, work contact details, invoices | 6(1)(b); 6(1)(c) | [Accounting provider]; HMRC; the supplier's accountant | [Depends on the provider] | 6 years from the end of the financial year | MFA on company accounts [to confirm before approval] |
| B2.3 | **Business marketing** | Telling prospective and existing customers about MedApp | Business contacts | Name, work email, organisation, preferences | 6(1)(f), with PECR rules and an opt-out | [Email marketing provider], if one is used | [Depends on the provider] | Until they opt out, or 2 years without engagement; the suppression list kept indefinitely | MFA on company accounts [to confirm before approval] |
| B2.4 | **Support requests** | Answering customers' questions and problems | Customer staff who raise requests | Name, work email, the request text (**which must not contain patient data**) | 6(1)(b); 6(1)(f) | [Helpdesk or email provider] | [Depends on the provider] | 2 years after the request closes | MFA on company accounts [to confirm before approval] |
| B2.5 | **Complaints and rights requests about the supplier's own processing** | Meeting DPA 2018 section 164A and UK GDPR Chapter III | Complainants, requesters | Contact details, the complaint, correspondence | 6(1)(c) | The DPO; the ICO if it investigates | None | 3 years after closure | MFA on company accounts [to confirm before approval] |
| B2.6 | **Personal data breach records** | Article 33(5) | Anyone affected | Description of the breach and its effects | 6(1)(c) | The DPO; the ICO; affected customers | None | 6 years after closure | MFA on company accounts [to confirm before approval] |
| B2.7 | **The supplier's staff and contractors** | Employment, contracts, vetting, training records | Staff, contractors | Identity, contact, contract, pay, training and vetting records; health data only where disclosed (with an appropriate policy document) | 6(1)(b); 6(1)(c); 9(2)(b) with Schedule 1 paragraph 1 | Payroll provider, pension provider, HMRC | [Depends on the provider] | 6 years after employment ends | MFA on company accounts [to confirm before approval] |
| B2.8 | **Security of the supplier's own systems** | Protecting its systems and customers' data | Supplier staff and contractors | Sign-in logs for Azure (Entra ID), GitHub and email; device records | 6(1)(f); 6(1)(c) | Microsoft; GitHub | Microsoft and GitHub may process sign-in logs outside the UK; see [international-transfers.md](international-transfers.md) | As each service keeps them; at least 12 months where configurable | MFA on every account (required for Cyber Essentials v3.3) [planned; to confirm before approval] |
| B2.9 | **Website** | Information about MedApp | Visitors | [None beyond server logs, if the website uses no analytics] | 6(1)(f) | [Website host] | [Depends on the host] | [30 days for server logs] | [To fill in when the website exists] |
| B2.10 | **Recruitment** | Hiring | Applicants | CV, contact details, interview notes | 6(1)(b); 6(1)(f) | [Recruitment provider] | [Depends on the provider] | 6 months after the role is filled | MFA on company accounts [to confirm before approval] |

Rows marked with brackets are to be filled in when the supplier picks those providers. Each provider that holds personal data for the supplier is a processor of the supplier's and needs its own contract under Article 28.

## Change log

| Date | Change | By |
|---|---|---|
| 16 November 2026 | First draft | [Author] |
