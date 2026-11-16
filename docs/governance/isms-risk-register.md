# ISMS: risk register and Statement of Applicability (first draft)

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [SIRO name] |
| **Next review** | Every quarter, and after any significant incident or change |

Roadmap step 3 asks for the start of an information security management system (ISMS), so that ISO/IEC 27001:2022 certification in step 10 is an audit rather than a project. This is that start: a risk register for the business, and an outline of the Statement of Applicability. The DPIA ([dpia.md](dpia.md)) covers risks **to individuals**; this register covers risks **to the business and its customers**, and links to the DPIA where they overlap.

## 1. Scoring

- **Likelihood:** 1 rare, 2 unlikely, 3 possible, 4 likely, 5 almost certain.
- **Impact:** 1 negligible, 2 minor, 3 moderate, 4 major, 5 severe.
- **Score:** likelihood times impact. 1 to 6 low, 8 to 12 medium, 15 to 25 high.
- **Appetite:** no high risks once real patient data is held. Medium risks need an owner and a plan. Low risks are accepted.

## 2. Risk register

Scored for MedApp **holding real patient data as it is today**, and after the planned treatment.

| # | Risk | DPIA risk | L | I | Score | Treatment | Step | Owner | Target score |
|---|---|---|---|---|---|---|---|---|---|
| IR1 | A staff account at a customer is taken over and patient data is stolen | 2 | 4 | 5 | **20** | MFA, idle timeout, SSO (throttling, a common-password list and ending sessions on password changes are in place) | 2, 4 | [Lead developer] | 5 |
| IR2 | Inappropriate access by a customer's staff goes undetected | 1 | 4 | 4 | **16** | Read audit, access reports, `Auditor` role | 7 | [Lead developer] | 8 |
| IR3 | A breach isn't detected or reported in time | 13 | 4 | 5 | **20** | Monitoring, alerting, out-of-hours cover, breach procedure, exercise | 3, 5 | [SIRO] | 6 |
| IR4 | Data is lost and backups don't restore | 14 | 3 | 5 | **15** | 35-day restore, geo-redundant UK backups, quarterly drills | 5 | [Lead developer] | 5 |
| IR5 | The database is reached with the admin credential | 12 | 3 | 5 | **15** | Passwordless least-privilege identity; Entra-only SQL | 5 | [Lead developer] | 5 |
| IR6 | Malicious code enters through a dependency or the pipeline | 15 | 3 | 5 | **15** | Images by digest, verified provenance, required reviewer (actions by SHA, scanning, SBOM and provenance attestations are in place) | 1, 2 | [Lead developer] | 6 |
| IR7 | A web vulnerability (XSS, framing) is exploited | 11 | 3 | 4 | **12** | Closing the ASVS gaps, penetration test (security headers and the CSP are in place) | 2, 10 | [Lead developer] | 4 |
| IR8 | Supplier staff misuse access, or customer data reaches tickets, laptops or AI tools | 16, 20 | 2 | 5 | **10** | Support access policy, PIM, access log, training | 3, 5 | [SIRO] | 4 |
| IR9 | The only maintainer is unavailable (key person) | | 3 | 5 | **15** | Runbooks, a second engineer, contracted support, escrow | 5, 10 | [SIRO] | 8 |
| IR10 | Records are kept beyond retention, increasing breach impact and breaching storage limitation | 5 | 5 | 3 | **15** | Retention engine | 7 | [DPO] | 4 |
| IR11 | A rights request can't be answered in time | 6 | 4 | 3 | **12** | Manual procedure now, export in step 7 | 3, 7 | [DPO] | 3 |
| IR12 | Unlicensed commercial dependencies in production (MediatR, FluentAssertions 8) | | 4 | 3 | **12** | Replace or license | 1 | [SIRO] | 2 |
| IR13 | The public repository and image expose the code and its known weaknesses | | 4 | 3 | **12** | Make them private | 1 | [SIRO] | 2 |
| IR14 | Every merge deploys to production with no human approval | 15 | 3 | 4 | **12** | A GitHub environment with a required reviewer | 2, 5 | [Lead developer] | 4 |
| IR15 | A customer's IG team rejects MedApp for missing paperwork | | 5 | 4 | **20** | This pack approved; DSPT; Cyber Essentials; DTAC | 3, 6, 10 | [SIRO] | 4 |
| IR16 | A processor contract or sub-processor term is missing or wrong | | 3 | 4 | **12** | Solicitor review of the DPA; Microsoft's terms recorded | 3 | [DPO] | 4 |
| IR17 | Data processed outside the UK by mistake (a resource in another region) | 17 | 2 | 4 | **8** | Azure Policy allowing only UK regions | 5 | [Lead developer] | 2 |
| IR18 | One customer sees another's data once infrastructure is shared | 18 | 1 today | 5 | **5 today** | Query filters, row-level security, cross-tenant tests | 8 | [Lead developer] | 5 |
| IR19 | MedApp's features drift into being a medical device without anyone noticing | | 2 | 4 | **8** | Intended purpose and change-control rule | 6 | [CSO] | 4 |

## 3. Statement of Applicability: outline

ISO/IEC 27001:2022 Annex A has 93 controls in four themes. The full Statement of Applicability lists each one as applicable or not, with the reason and the evidence. That's for step 10; this outline records the scope and the decisions known now.

**Scope of the ISMS:** the development, hosting, operation and support of MedApp as a service, by [Company name], including its people, company devices, GitHub, Microsoft Azure (UK) and its office or home working locations. Customers' own systems and devices are out of scope.

| Theme | Controls | Applicable? | Main evidence today | Main gaps |
|---|---|---|---|---|
| **Organisational** (5.1 to 5.37) | Policies, roles, asset inventory, access control, supplier relationships, cloud services, incident management, continuity, legal and privacy requirements | All applicable | This pack: policies, ROPA, DPIA, sub-processors, breach procedure; `docs/reviews` as security reviews | Approval, a named SIRO and DPO, supplier register, exercises |
| **People** (6.1 to 6.8) | Screening, terms of employment, awareness and training, disciplinary process, remote working, reporting events | All applicable | [support-access-policy.md](support-access-policy.md) | Contracts, training records, screening |
| **Physical** (7.1 to 7.14) | Secure areas, equipment, clear desk and screen, storage media, disposal | **Mostly inherited from Microsoft** for the data centres (Microsoft's ISO/IEC 27001 certificate). Applicable to company devices and home working: 7.7 clear desk and screen, 7.8 to 7.10 equipment and media, 7.14 disposal. | | Device policy, disposal records |
| **Technological** (8.1 to 8.34) | Endpoint devices, privileged access, authentication, malware, vulnerabilities, configuration, deletion, masking, backup, logging, monitoring, network security, cryptography, secure development, testing, separation of environments, change management | All applicable | Network policies, Pod Security, Key Vault, OIDC, TLS, CI, reviews, synthetic test data, separate environments (`deploy/`) | MFA (8.5), logging and monitoring (8.15, 8.16), backup testing (8.13), vulnerability management (8.8), data deletion (8.10) |

**Privacy:** if buyers ask for privacy certification, extend the ISMS with ISO/IEC 27701:2025 (step 10).
