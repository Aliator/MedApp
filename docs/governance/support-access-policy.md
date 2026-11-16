# Support access policy

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [SIRO name], with [DPO name] |
| **Applies to** | Everyone at [Company name], employees and contractors, who could reach customers' MedApp data |
| **Next review** | Every 12 months, and after any incident involving support access |

How and when the supplier's people may access customers' personal data in MedApp. It puts into practice the data processing agreement's limits ([dpa-template.md](dpa-template.md), clauses 3 and 4) and reduces DPIA risk 16.

## Principles

- **Customer data belongs to the customer.** The supplier accesses it only to provide the service, and only as far as the task needs.
- **No access is the default.** Most support can be given without seeing any patient data: from logs, from the customer's own description, or by the customer's admin doing it in MedApp.
- **Every access is on an instruction, by a named person, and recorded.**

## Who may have access

**Authorised people** are listed in [the access register], kept by the SIRO. To be on it, a person must:
- have a contract with a **confidentiality clause** that survives the end of the contract, covering patient information (the duty of confidentiality that UK GDPR Article 9(3) and DPA 2018 section 11 require). **Planned:** the clauses are still to be written into contracts, before the first customer goes live ([target date])
- have completed **data security awareness training** in the last 12 months ([security-policies.md](security-policies.md), training)
- have passed **pre-engagement checks**: identity, right to work, references, and a basic criminal record check (Disclosure and Barring Service, basic level)
- use a **company-managed device** that meets Cyber Essentials, with MFA on every account (**planned**, before the first customer goes live ([target date]); whether MFA is already on every company account is to confirm)

Access is removed on the day someone leaves or no longer needs it, and the register is reviewed every 3 months.

## When access is allowed

| Kind of access | Allowed when | Approval |
|---|---|---|
| **Platform operations** without looking at records (deployments, scaling, restarts, applying migrations, reading logs for errors) | As part of running the service | Standing, for authorised people |
| **Looking at or changing a customer's records** (for example to investigate a fault the customer can't describe, to correct data on request, or to answer a rights request) | Only on the customer's **written instruction**: a support request from a named contact at the customer, saying what is needed | The customer's instruction, recorded in the ticket |
| **Restoring a backup** | On the customer's instruction, or for disaster recovery | The customer, or the SIRO in an emergency, told to the customer the same day |
| **Emergency ("break-glass")** access to stop or contain an incident | When waiting would harm patients or the data | The SIRO, or any authorised person if the SIRO can't be reached; reported to the customer within 24 hours |

## Rules for every access

- **Minimum:** look at only the records and fields the task needs. Prefer read-only access.
- **In place:** never copy customer data out of Azure: not to a laptop, a support ticket, email, chat, a screenshot, a spreadsheet, a test database or a developer's machine.
- **No AI tools on customer data.** The supplier's AI coding assistants work only with code, documentation and synthetic data. Customer data, logs containing it, and support tickets that might contain it must never be pasted into, uploaded to, or made reachable by an AI tool.
- **No customer data in development.** Development and testing use synthetic data only (`AGENTS.md`). A restored backup used for a recovery drill stays in Azure UK, is reached only by authorised people, and is deleted when the drill ends.
- **UK only, with one emergency exception.** Access from outside the UK needs the customer's written approval first. The exception: an **employee** of the supplier who is temporarily abroad may take emergency (break-glass) action on a company-managed device, recorded in the access log and reported to the customer within 24 hours ([dpa-template.md](dpa-template.md), clause 8.2). An employee's access from abroad isn't a restricted transfer, because the employee is part of the supplier, not a separate legal entity; the UK-only rule is the supplier's contractual choice. Access by a **contractor** from abroad is a restricted transfer, and needs the customer's approval and a transfer safeguard ([international-transfers.md](international-transfers.md)).
- **Record it.** Every access to records, restore or break-glass use goes in [the support access log]: who, when, which customer, what was accessed, why, and the instruction or approval. The log is kept for 6 years.
- **Tell the customer.** The customer is told of every access to its records (through the ticket), and of every emergency access within 24 hours.

## Support tickets

- **Customers must not put patient data in support tickets or email.** Use the record's ID in MedApp instead. The support contact form and replies say so.
- **If patient data arrives anyway,** the support person removes it from the ticket and the mailbox, tells the customer, and records it as an incident ([breach-response.md](breach-response.md)). It's usually not a reportable breach, but the customer decides.

## Technical controls

| Control | Status |
|---|---|
| Cluster access through Entra ID only, with no local accounts; Azure role-based access control | In place in design (`deploy/azure/main.bicep`) |
| The database, and the storage account that holds attached files, reachable only from inside the virtual network (private endpoints) | In place in design |
| Passwordless access to the files' storage, with the account key turned off. Until then the app uses the account key from Key Vault, so anyone who can read that secret can read every file | Planned, step 5 |
| MFA on every supplier account (Azure, GitHub, email) | Planned, before the first customer goes live ([target date]); required for Cyber Essentials v3.3. Current state to confirm |
| Just-in-time admin access with Entra ID Privileged Identity Management, time-limited and approved; break-glass accounts monitored | Planned, step 5 |
| A read-only database role for investigations; the app's own least-privilege identity | Planned, step 5 |
| Azure activity and sign-in logs, and database audit, streamed to a log workspace the support person can't change | Planned, step 5 |
| Customer Lockbox for Microsoft's own support | Planned, step 5 |
| An admin view in MedApp that lets the customer's admin do most corrections and exports, so the supplier doesn't need to | Planned, step 7 |

**Until the planned controls exist,** this policy relies on the people on the register and on the access log. That's a known gap, recorded in the DPIA (risk 16).
