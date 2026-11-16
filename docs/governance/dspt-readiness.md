# DSPT readiness map

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [SIRO name], with [DPO name] |
| **Target** | The 2026-27 Data Security and Protection Toolkit (version 9), as a small IT supplier, at "Standards Met", submitted by 30 June 2027 ([DSPT](https://www.dsptoolkit.nhs.uk/News/169)) |
| **Next review** | Monthly until submission |

Every organisation with access to NHS patient data and systems must complete the DSPT ([DSPT](https://www.dsptoolkit.nhs.uk/Help/Overview)). This maps the evidence the supplier has, or will have, to the toolkit's ten data security standards (the National Data Guardian's), and marks the gaps.

**Before relying on this map:** register as a supplier, download the 2026-27 assertions for the supplier's organisation type ([DSPT 2026-27 assertions](https://www.dsptoolkit.nhs.uk/News/Attachment/943)), and map each numbered assertion to the evidence here. The assertion numbers and wording change each year, so this map works at the level of the standards.

**Status:**
- **Have:** evidence exists today.
- **Draft:** written in this pack, needs approval.
- **Gap:** nothing yet; the step that closes it is shown.

## Leadership obligation 1: people

### Standard 1: personal confidential data

*All staff ensure that personal confidential data is handled, stored and transmitted securely.*

| Evidence the toolkit looks for | Status | Evidence, or the gap |
|---|---|---|
| A named person responsible for data protection; a DPO | Gap (step 3) | Appoint the DPO and SIRO ([roles-and-lawful-bases.md](roles-and-lawful-bases.md), section 4) |
| ICO registration | Gap (step 3) | Pay the fee |
| A record of processing activities | Draft | [ropa.md](ropa.md) |
| DPIAs for high-risk processing | Draft | [dpia.md](dpia.md) |
| Privacy information | Draft | [privacy-notice-supplier.md](privacy-notice-supplier.md); customer templates |
| A data flow map and information asset register | Draft | [data-inventory.md](data-inventory.md) |
| Lawful bases recorded | Draft | [roles-and-lawful-bases.md](roles-and-lawful-bases.md) |
| Rights requests handled on time | Draft | [data-subject-rights.md](data-subject-rights.md); MedApp has a subject access export, restriction and dispute flags, and anonymisation (step 7); the clinician review is by hand |
| Data quality and accuracy | Have (partly) | Validation on every command, including the NHS number's check digit, and no NHS number on two patients (branch `feature/nhs-integrations`); finished stages lock; unlocks audited. Gap: NHS numbers aren't traced with PDS (step 9) |
| Retention schedule, and disposal with a record of destruction | Draft; partly enforced: a review list, keep or anonymise, each logged (step 7); destruction and purging a gap | [retention-schedule.md](retention-schedule.md) |
| National Data Opt-out compliance | Have (not applicable) | [nhs-context.md](nhs-context.md), section 2: direct care only |

### Standard 2: staff responsibilities

*All staff understand their responsibilities under the National Data Guardian's standards, including their obligation to handle information responsibly and their personal accountability for deliberate or avoidable breaches.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| Staff contracts with confidentiality clauses | Gap (step 3) | [support-access-policy.md](support-access-policy.md) requires them |
| Acceptable use and information security policies, read and signed | Draft | [security-policies.md](security-policies.md) |
| Pre-engagement checks | Draft | [support-access-policy.md](support-access-policy.md) |

### Standard 3: training

*All staff complete appropriate annual data security training and pass a mandatory test.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| At least 95% of staff trained in the year, with records | Gap (step 3) | [security-policies.md](security-policies.md), section 8 |
| Specialist training for the DPO and SIRO | Gap (step 3) | |

## Leadership obligation 2: process

### Standard 4: managing data access

*Personal confidential data is only accessible to staff who need it for their current role, and access is removed as soon as it is no longer required. All access to personal confidential data on IT systems can be attributed to individuals.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| Individual accounts, no shared logins | Have | MedApp: individual Identity accounts. Azure: Entra ID only, no local cluster accounts (`deploy/azure/main.bicep`) |
| Role-based access | Have | Two roles enforced on every endpoint except sign-in, sign-out and `whoami` (`MedApp.API/Common/Authentication/AuthPolicies.cs`) |
| Admin accounts limited and reviewed | Partly | Admin-only functions in MedApp; Azure admin group. Gap: just-in-time admin access (step 5); periodic access review (step 7) |
| All access attributable | Partly | Changes to procedures are audited; reads, patient changes, sign-ins and account changes are in the activity log with the user and address (step 7). Behind the ingress the browser's address is recorded (forwarded headers). Gap: a tamper-evident log, and the browser's address for pages rendered on the server, which record the page server's |
| Access removed when no longer needed | Partly | Admins can delete accounts, which ends sessions, and can sign a user out everywhere. Gap: deactivation, last sign-in, review report (steps 4 and 7) |
| MFA for remote and privileged access | **Gap** | Supplier accounts: planned before the first customer ([target date]), current state to confirm. MedApp users: step 4 (the 2026-27 toolkit asks suppliers about MFA or identity federation, with plans due by 30 June 2027) |
| Password policy | Have (partly) | Identity rules and lockout (`MedApp.API/appsettings.json`); generated first passwords; the 10,000 most common passwords refused (the NCSC's list, `CommonPasswordValidator`); sign-in throttling. Gap: NCSC-style rules without composition, and a breached-password check (step 2) |
| Logs that are tamper-proof | Gap (steps 5 and 7) | Ledger tables and a separate log workspace |

### Standard 5: process reviews

*Processes are reviewed at least once a year to identify and improve processes which have caused breaches or near misses, or which force staff to use workarounds which compromise data security.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| Reviews of incidents and near misses | Draft | [breach-response.md](breach-response.md), step 12 |
| Reviews of security findings | Have (partly) | Every merge since #51 has a written review with graded findings (`docs/reviews/`), by a separate AI agent not given the PR's own conclusions, not yet approved by a human; and open findings are tracked ([architecture handbook, section 11](../architecture.md#11-risks-and-technical-debt)) |
| An annual review of policies | Draft | Each document in this pack has a review date |

### Standard 6: responding to incidents

*Cyber-attacks against services are identified and resisted and security breach and incident reporting is enabled. Action is taken immediately following a data breach or a near miss, with a report made to senior management within 12 hours of detection.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| An incident and breach procedure | Draft | [breach-response.md](breach-response.md): assessment within 12 hours, customer told within 48 hours (24 once out-of-hours cover exists) |
| A breach log | Draft | [breach-response.md](breach-response.md), section 6 |
| A tested response (exercise) | Gap (step 3) | A tabletop exercise before go-live |
| Detection: monitoring and alerting | **Gap (step 5)** | No monitoring in any deployed environment today |
| Anti-malware on devices; email protection | Gap (step 3) | Cyber Essentials |

### Standard 7: continuity planning

*A continuity plan is in place to respond to threats to data security, including significant data breaches or near misses, and it is tested once a year as a minimum, with a report to senior management.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| A business continuity and DR plan | Draft | [security-policies.md](security-policies.md), section 7 |
| Backups, separate from the live system, and tested restores | **Gap (step 5)** | Azure SQL point-in-time restore (7 days) in design, never tested |
| An annual test, written up | Gap (step 5) | Quarterly restore drills planned |
| Recovery objectives | Draft | RPO 15 minutes, RTO 4 hours |

## Leadership obligation 3: technology

### Standard 8: unsupported systems

*No unsupported operating systems, software or internet browsers are used within the IT estate.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| Supported runtimes | Have | .NET 10 (long-term support), Node 22, SQL Server 2022 / Azure SQL, Kubernetes with automatic patch upgrades on AKS |
| A software inventory (SBOM) | Partly | An SPDX SBOM is attested with every published image (`.github/workflows/publish.yml`). Gap: SBOMs of the .NET and npm source dependencies, and a licence allow-list (step 1) |
| Patching within 14 days for critical issues | Draft | [security-policies.md](security-policies.md), section 6. Dependabot proposes updates weekly; CI fails on high or critical npm and NuGet advisories and on fixable critical vulnerabilities in the image |
| Supported devices | Gap (step 3) | Cyber Essentials |

### Standard 9: IT protection

*A strategy is in place for protecting IT systems from cyber threats, based on a proven cyber security framework such as Cyber Essentials. This is reviewed at least annually.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| Cyber Essentials | Gap (step 3) | Plus in step 10 |
| Encryption in transit and at rest | Have (in design) | TLS to the ingress and to Azure SQL; TDE (`deploy/azure/main.bicep`). Gap: Valkey traffic (step 2 or 5) |
| Network segmentation | Partly | Default-deny network policies for MedApp's own pods (`deploy/helm/medapp/templates/networkpolicy.yaml`); private Azure SQL. Gap: on Azure, other pods in the cluster can reach the database's private endpoint until a cluster-wide Cilium policy limits it (step 5) |
| Hardened platform | Have | The `restricted` Pod Security Standard, non-root, read-only filesystems (`deploy/helm/medapp/templates/_helpers.tpl`) |
| Secure configuration of web security headers | Have | A nonce-based Content Security Policy, `frame-ancestors 'none'`, `nosniff`, `Referrer-Policy`, `Permissions-Policy`, HSTS and `no-store` on the API (`MedApp.API/Middleware/SecurityHeaders.cs`) |
| Vulnerability scanning and a penetration test | Gap (steps 2, 7 and 10) | |
| Secrets management | Have | Key Vault and External Secrets; no secrets in git |
| Least-privilege service identities | Partly | OIDC and workload identity; the app still uses the SQL admin login (step 5) |

### Standard 10: accountable suppliers

*IT suppliers are held accountable via contracts for protecting the personal confidential data they process and for meeting the National Data Guardian's data security standards.*

| Evidence | Status | Evidence, or the gap |
|---|---|---|
| A list of the supplier's own suppliers that handle data | Draft | [sub-processors.md](sub-processors.md) |
| Contracts with data protection terms | Draft; Microsoft's terms to be recorded | [dpa-template.md](dpa-template.md) for customers; Microsoft's Data Protection Addendum |
| Due diligence on suppliers | Draft | [security-policies.md](security-policies.md), section 5 |
| Supply chain security of the software itself | Partly | Required CI, reviews, OIDC deploys; actions pinned by commit SHA; Dependabot, CodeQL, dependency review and image scanning; SBOM and provenance attestations on each image. Gap: images pinned by digest, attestations verified before deploy (step 2) |

## Summary

| Standard | Ready? |
|---|---|
| 1. Personal confidential data | Mostly drafts; retention and export enforcement are gaps |
| 2. Staff responsibilities | Drafts; contracts to update |
| 3. Training | Gap |
| 4. Managing data access | **Major gaps:** read audit, MFA |
| 5. Process reviews | Good start: the review documents |
| 6. Responding to incidents | Procedure drafted; detection is a major gap |
| 7. Continuity planning | **Gap:** no tested backups |
| 8. Unsupported systems | Good on the platform; SBOM and devices to do |
| 9. IT protection | Strong platform design; Cyber Essentials, headers and testing to do |
| 10. Accountable suppliers | Drafts |

**To reach "Standards Met" by June 2027,** the supplier needs roadmap steps 1 to 5 and 7 done, or at least the items marked as gaps above, plus the step 3 organisational work: the DPO, the ICO fee, training, contracts and Cyber Essentials.
