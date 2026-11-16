# Information security policies

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [SIRO name], senior information risk owner, [Company name] |
| **Approved by** | Not yet approved. **None of these policies is in force until approved and dated.** |
| **Applies to** | Everyone at [Company name], employees and contractors, and every system that holds or reaches customer data |
| **Next review** | Every 12 months, and after any significant incident or change |

Short policies, one per section, as roadmap step 3 asks. Each says what's required, and where MedApp already does it. They are the starting point for the ISMS ([isms-risk-register.md](isms-risk-register.md)) and the evidence for the DSPT ([dspt-readiness.md](dspt-readiness.md)).

Other policies in this folder: [support-access-policy.md](support-access-policy.md), [breach-response.md](breach-response.md), [retention-schedule.md](retention-schedule.md) (records retention and disposal), [data-subject-rights.md](data-subject-rights.md) (handling requests for customers), [complaints.md](complaints.md).

## 1. Information security

- **Purpose:** protect the confidentiality, integrity and availability of customers' data, above all patients' health data, and of the supplier's own systems.
- **Responsibility:** the SIRO owns information risk and these policies. The DPO advises on data protection and checks compliance. Everyone is responsible for following them and reporting concerns.
- **Risk-based:** risks are recorded in the risk register and reviewed every quarter. The DPIA covers the risks to individuals.
- **Standards:** the supplier aims for Cyber Essentials (then Cyber Essentials Plus), the DSPT at "Standards Met", OWASP ASVS Level 2 for the application, and ISO/IEC 27001 (roadmap steps 3 and 10).
- **Breaches of policy** are handled through the disciplinary process or the contract.

## 2. Access control

- **Least privilege:** people and programs get only the access their job needs.
- **Individual accounts:** no shared accounts, for people or for programs. Every account has an owner.
- **MFA on every account** that reaches company or customer systems: Azure (Entra ID), GitHub, email, the password manager, the domain registrar. Cyber Essentials v3.3 requires MFA for cloud services. **Status: planned** before the first customer goes live ([target date]); whether it's already on for every account is to confirm.
- **Admin access is separate and time-limited:** admin roles are used only when needed. Planned: Entra ID Privileged Identity Management (step 5).
- **Joiners, movers and leavers:** access is granted on approval by the SIRO, reviewed every 3 months, and removed on the day someone leaves.
- **Programs:** use managed identities and federation, not stored secrets. MedApp already does: GitHub signs in to Azure with OIDC, External Secrets with workload identity (`deploy/azure/main.bicep`). Still to do: the app's own database identity (step 5).
- **In MedApp:** two roles enforced by the API on every endpoint except sign-in, sign-out and `whoami`; admin-only user management; the full audit trail is admin-only, and staff see a per-patient timeline without its free text (who changed what on their procedures and when, with old and new due dates, priorities and statuses, past assignees, and the usernames recorded at the time (deleted accounts' included): structured values, never free text). MFA, SSO and an `Auditor` role are planned (step 4).
- **Customer data:** only under [support-access-policy.md](support-access-policy.md).

## 3. Acceptable use

Everyone at the supplier must:
- use only **company-managed devices** for company and customer work, with disk encryption, automatic updates, a screen lock, and anti-malware, as Cyber Essentials requires
- keep passwords in the **company password manager**, and never reuse them
- never put **customer data** on a device, in email, chat, a ticket, a document or an AI tool ([support-access-policy.md](support-access-policy.md))
- never put **secrets** in source code, tickets or chat; secrets live in Key Vault or the password manager
- use **AI tools** only with code, documentation and synthetic data, and only the tools the SIRO has approved, on terms that don't let the provider train on the supplier's code [list the approved tools and their terms]
- report anything suspicious (a phishing email, a lost device, an unexpected access) to the SIRO at once

## 4. Secure development

How MedApp is built. Most of this is already in place, in `AGENTS.md` and CI.

| Requirement | Status |
|---|---|
| **Every change through a pull request** to `main`, with required CI checks (`web`, `image`, `helm`, `unit-tests`, `integration-tests`, `e2e`) | In place (repository ruleset) |
| **Planning, implementation and a separate review** of every change: a written review of every merge since #51, by a separate AI agent not given the PR's own conclusions, and not yet approved by a human; a high or critical finding blocks the merge | In place since #51 (`AGENTS.md`, "Effort for each stage of a change" and the merge flow; `docs/reviews/`). No second human reviewer yet (next row) |
| **Tests** for all new behaviour, with coverage gates for the web app | In place (`.github/workflows/main.yml`) |
| **Synthetic data only** in code, tests, seeds, fixtures and screenshots | In place (convention, `AGENTS.md`) |
| **Secrets never in git;** generated or chosen and kept in a secret store | In place |
| **A human approves** each change before merge | **Not yet:** the repository needs no approvals. Add a required reviewer, at least for production deploys (step 2) |
| **Dependencies with known licences,** an SBOM, and licence checks | Planned (step 1) |
| **Static analysis** (CodeQL), dependency review, image scanning, a ZAP scan | Planned (step 2) |
| **Actions and images pinned by digest,** and provenance attestations | Planned (step 2) |
| **A threat model** and an **ASVS Level 2** checklist | Planned (step 2) |
| **Privacy by design:** any change that alters what personal data is held, who can see it, where it goes or how long it's kept updates `docs/governance/` (at least [data-inventory.md](data-inventory.md) and the DPIA) in the same pull request | In place: a step in `AGENTS.md`'s feature checklist, and a check in every merge review |
| **Clinical safety review** of changes to clinical workflow | Planned (step 6) |

## 5. Supplier management

- **Register:** every supplier that holds company or customer data is in [the supplier register], with what it holds, where, its certifications, its contract and its review date.
- **Before using a supplier** that will hold customer data: check its security (ISO/IEC 27001 or equivalent), data location (UK or adequate country), breach terms, sub-processors and exit terms; sign an Article 28 contract; update [sub-processors.md](sub-processors.md) and give customers 30 days' notice.
- **Every 12 months:** review each supplier's certificates and any incidents.
- **Today's suppliers that matter:** Microsoft (Azure) and GitHub ([sub-processors.md](sub-processors.md)); the planned email provider (step 4) and monitoring service (step 5).

## 6. Vulnerability and patch management

| Item | Rule |
|---|---|
| **Critical and high** vulnerabilities in MedApp's code, its dependencies, its image or its platform | Fixed or mitigated **within 14 days** of a fix being available (the DSPT's expectation for suppliers), sooner if exploited |
| **Medium** | Within 30 days |
| **Low** | At the next planned change, or accepted in the risk register |
| **Base images and the runtime** (.NET, Node, SQL Server, Valkey) | Rebuilt at least monthly, and at once for a critical fix |
| **AKS** | Patch upgrades and node images automatic (`deploy/azure/main.bicep`); a maintenance window to be set (step 5) |
| **Company devices** | Automatic updates; operating systems and apps on supported versions only (Cyber Essentials) |
| **How vulnerabilities are found** | Today: NuGet and npm audit warnings, and reviews. Planned: Dependabot, CodeQL, image scanning (step 2); a yearly penetration test (steps 7 and 10) |
| **Reporting a vulnerability** | A `SECURITY.md` with a contact and a disclosure process (planned, step 1) |

## 7. Business continuity and disaster recovery

| Item | Target or rule | Status |
|---|---|---|
| **Availability** | 99.9% a month | Target only; nothing measured yet (step 5) |
| **Recovery point objective (RPO)** | 15 minutes of data at most | Azure SQL's point-in-time restore supports it; never tested |
| **Recovery time objective (RTO)** | 4 hours | Not tested |
| **Backups** | Point-in-time restore for 35 days; monthly long-term backups for 12 months; geo-redundant within the UK | 7 days, locally redundant, today (step 5) |
| **Restore drills** | Every quarter, recorded with timings | Not started (step 5) |
| **Rebuild** | The whole environment rebuilt from Bicep and Argo CD in UK West, at least once a year | Not started (step 5) |
| **Runbooks** | Restore, rebuild, rotate a secret, roll back, Node down, certificate expiry, suspected breach, in `docs/runbooks/` | Not started (step 5) |
| **Downtime procedure** for customers | Paper or offline fallback while MedApp is unavailable | Planned (step 6) |
| **Key person** | One maintainer today. A second engineer able to deploy and restore, and written runbooks | Planned (step 10) |

## 8. Training and awareness

- **Everyone with access to customer data** completes data security awareness training **before** access and **every 12 months**, covering confidentiality, data protection, phishing, breaches, and this policy set. The DSPT expects at least 95% of staff to have completed it in the year.
- **The DPO and SIRO** take role-specific training.
- **Records** of who completed what and when are kept for 6 years.
- **Content:** [the chosen course, for example the NHS's Data Security Awareness e-learning where available to suppliers, or an equivalent commercial course].

## 9. Information assets

- **The information asset register** is [data-inventory.md](data-inventory.md) for MedApp's data, plus [the supplier's own asset list] for company systems and devices.
- **Each asset has an owner,** who is responsible for its risks, access and retention.
