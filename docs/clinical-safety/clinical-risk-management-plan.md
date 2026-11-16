# MedApp: clinical risk management plan

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-01, clinical risk management plan (DCB0129) |
| **Version** | 0.1, draft |
| **Date** | 16 November 2026 |
| **System** | MedApp, from `main` at `88dc1fa` (the merge of #56) onwards |
| **Manufacturer** | [Manufacturer name] |
| **Drafted by** | An AI coding agent, for the manufacturer. Not yet reviewed by a person. |
| **Approved by** | [Clinical Safety Officer], [accountable owner] (see [approval](#approval)) |

This plan says how the manufacturer manages clinical risk in MedApp, as DCB0129 (Clinical Risk Management: its Application in the Manufacture of Health IT Systems) requires. It is the first document in the clinical risk management file (`README.md`).

## 1. Scope

### What it covers

- **The product:** MedApp, the web app and API described in `docs/architecture.md`: patients, procedures created from templates, stages worked in order and locked when finished, due dates and the overdue flag, assignments and task lists, notifications, the audit log, and the administration of users, stage definitions and templates.
- **Its whole life:** design, development, testing, release, deployment support, live use, changes, and decommissioning.
- **Every release** that a customer can use with real patients, and every change that touches clinical workflow ([section 8](#8-changes-and-the-release-gate)).
- **What the manufacturer runs for customers,** once it hosts MedApp as a service (Azure, `deploy/azure`): availability, backups and restores, as far as they affect patient care.

### What it doesn't cover

- **Each customer's deployment:** how a customer configures, trains for and uses MedApp is their own clinical risk management under DCB0160. The manufacturer supports it (`dcb0160-deploying-organisations.md`).
- **The content of templates and stage instructions:** customers write them, and are responsible for their clinical accuracy. The hazards that the software adds to that content (for example, how changes reach procedures) are in scope.
- **Medical device regulation:** MedApp isn't a medical device as it stands (`intended-purpose-and-device-decision.md`). If that changes, ISO 14971 and UK MDR 2002 apply too, and this plan has to be revised.

### Current state

MedApp has never been used with real patients, and has never been deployed outside development and a local test cluster (`docs/architecture.md`, section 1.5). This plan sets up clinical risk management before first live use, as DCB0129 expects: alongside development, not after it.

## 2. References

| Reference | What it is |
|---|---|
| DCB0129 | Clinical Risk Management: its Application in the Manufacture of Health IT Systems (specification and implementation guidance), an information standard under section 250 of the Health and Social Care Act 2012 |
| DCB0160 | Clinical Risk Management: its Application in the Deployment and Use of Health IT Systems |
| Data (Use and Access) Act 2025, section 121 | Lets information standards bind IT suppliers directly, from 5 February 2026 |
| NHS England, national review of DCB0129 and DCB0160 | The review and consultation that will lead to new versions ([NHS England](https://www.england.nhs.uk/long-read/national-review-of-clinical-risk-management-standardsdcb0129-and-dcb0160-supporting-information/)) |
| ISO 14971:2019 | Medical devices: application of risk management. Used here for its principles only, as DCB0129 does |
| MHRA, medical device stand-alone software including apps | The device qualification guidance (`intended-purpose-and-device-decision.md`) |
| DTAC 2.0 | The Digital Technology Assessment Criteria, whose clinical safety section asks for this file |
| `docs/architecture.md` | The architecture handbook: how MedApp works, its risks and the roadmap |
| `docs/procedures-ui-plan.md` | The procedures concept and its decisions |

## 3. Roles and responsibilities

| Role | Who | Responsibilities |
|---|---|---|
| **Accountable owner** (top management) | [Name, title], [Manufacturer name] | Owns clinical risk for the manufacturer. Provides the resources this plan needs, including a contracted CSO. Approves this plan and each safety case report with the CSO. Decides when risk is accepted, never against the CSO's advice without recording why. |
| **Clinical Safety Officer (CSO)** | [Name], [profession], registered with [professional body: GMC, NMC, HCPC or another statutory register], number [registration number] | Must be a suitably qualified and experienced clinician with a current professional registration, trained in clinical risk management. Runs this process. Approves the hazard log, each safety case report and each clinical safety sign-off of a release. Can stop a release. The accountable owner can't be the CSO without a clinical registration. Contracted for [n] days a month (step 6 of the roadmap suggests a few). |
| **Deputy CSO** (recommended) | [Name, registration] | Covers the CSO's absence, so that a safety-related release or incident is never blocked or unreviewed. |
| **Maintainer** (developer and operator) | [Name] | Raises hazards, writes the clinical safety section of each pull request, builds and tests the controls, keeps the hazard log up to date with the CSO, and runs incident investigations with the CSO. |
| **AI coding agents** | Claude Code and any other tool, under `AGENTS.md` | Write most of the code. They may draft hazard analyses, controls, tests and these documents, and their review documents (`docs/reviews/`) may be used as evidence. **They can't approve anything:** every clinical safety judgement is made or confirmed by the CSO. |
| **Independent reviewer** (recommended) | [Name, or a second contracted clinician] | Reviews the safety case report before the first live release. |
| **Deploying organisations' CSOs** | Each customer's own | Run DCB0160 for their deployment, report incidents to the manufacturer, and receive the manufacturer's safety case and hazard log. |

**Competence.** The CSO's registration, clinical safety training (for example NHS England's clinical safety training) and experience are recorded in the file and checked once a year. The maintainer completes clinical safety awareness training before working on clinical workflow.

## 4. The clinical risk management process

The process follows DCB0129 and the principles of ISO 14971: identify hazards, estimate and evaluate their risk, control them, check the controls work, and keep watching.

### 4.1 Identifying hazards

Hazards are found by:
- **Walking the workflows:** each workflow in `docs/architecture.md` section 6.3 (creating and working a procedure) and the screens in `docs/procedures-ui-plan.md` section 6, asking "what if" at each step: the wrong patient, the wrong stage, too late, twice, by two people, with the system down. This is a structured what-if technique (SWIFT).
- **Reading the rules in the code:** `MedApp.Application/Procedures/Common` (`ProcedureWorkflow`, `ProcedureChange`, `DueDates`), the handlers and the screens that use them.
- **The engineering reviews** in `docs/reviews/` and the risks in `docs/architecture.md` section 11, for failures with a clinical effect.
- **People who use it:** a hazard workshop with the CSO and at least one clinician from a likely customer before first live use; then user feedback, support requests and incidents.
- **Every change** that touches clinical workflow ([section 8](#8-changes-and-the-release-gate)).

The first hazard log (`hazard-log.md`) came from the first two of these. The workshop hasn't happened yet.

### 4.2 Estimating risk

Each hazard's risk is the combination of:
- **Consequence:** the worst credible harm to a patient that the hazard could lead to.
- **Likelihood:** how likely that harm is, given the controls in place.

Both are rated on the five-point scales below, from the DCB0129 implementation guidance. Ratings are estimates, made with the CSO, and the reasoning is written in the hazard log.

**Consequence**

| Consequence | Meaning |
|---|---|
| **Catastrophic** | Death or permanent life-changing incapacity, affecting many patients. |
| **Major** | Death or permanent life-changing incapacity of one patient; or severe injury or incapacity, from which recovery isn't expected in the short term, affecting several patients. |
| **Considerable** | Severe injury or incapacity from which recovery is expected in the short term; or severe psychological trauma. |
| **Significant** | Minor injury or injuries from which recovery isn't expected in the short term; or significant psychological trauma; or a delay to care that needs extra treatment. |
| **Minor** | Minor injury from which recovery is expected in the short term; minor psychological upset; inconvenience. |

**Likelihood**

| Likelihood | Meaning |
|---|---|
| **Very high** | Certain or almost certain; highly likely to occur. |
| **High** | Not certain, but very possible; reasonably expected to occur in the majority of cases. |
| **Medium** | Possible. |
| **Low** | Could occur, but in the great majority of occasions will not. |
| **Very low** | Negligible or nearly negligible possibility of occurring. |

### 4.3 The risk matrix

The rating is read from this 5×5 matrix, as DCB0129's guidance uses.

| Likelihood ↓ / Consequence → | Minor | Significant | Considerable | Major | Catastrophic |
|---|---|---|---|---|---|
| **Very high** | 3 | 4 | 4 | 5 | 5 |
| **High** | 2 | 3 | 3 | 4 | 5 |
| **Medium** | 2 | 2 | 3 | 3 | 4 |
| **Low** | 1 | 2 | 2 | 3 | 4 |
| **Very low** | 1 | 1 | 2 | 2 | 3 |

### 4.4 Risk acceptability

| Rating | Level | What it means for MedApp |
|---|---|---|
| **5** | Unacceptable | Can't be released. The hazard must be designed out. |
| **4** | Mandatory elimination or control | Can't be released until controls bring it to 3 or below. |
| **3** | Undesirable | Acceptable only if further reduction isn't reasonably practicable, with the CSO's written justification and a dated plan. **No hazard at 3 may be accepted for first live use without that justification.** |
| **2** | Acceptable where further reduction isn't worth its cost | Acceptable. Further controls are still made where cheap. |
| **1** | Acceptable | No action needed. |

**Rules:**
- **As low as reasonably practicable:** every hazard is reduced as far as reasonably practicable, whatever its rating.
- **Order of controls:** design the hazard out first; then a protective measure in the software (a check, a lock, a confirmation, a warning); then information and training. Information alone is the weakest control, and is never the only control for a hazard rated 3 or above.
- **Transferred controls:** a control that relies on the deploying organisation (training, a local process) is marked "transferred" in the hazard log and listed in `dcb0160-deploying-organisations.md`, so the customer's CSO can pick it up.
- **Overall residual risk:** the safety case report states whether the residual risk of the release as a whole is acceptable, not just each hazard's.

### 4.5 Controlling risk and checking the controls

- **Every control has evidence:** a test, a code reference, a document, or a record of training. Software controls are tested by automated tests (NUnit for the API, Vitest for the web app; `docs/architecture.md`, section 8.5), named in the hazard log.
- **New hazards from controls:** each control is checked for hazards it introduces (for example, a confirmation dialog that people learn to click through).
- **Residual risk** is re-rated once a control is in place and verified, not when it's planned.

## 5. The hazard log

- **Where:** `hazard-log.md`, in this folder, under version control with the code. A signed copy of each version used for a safety case is kept with that safety case report.
- **What each hazard records:** an ID, a title, a description, causes, effects, the possible harm, existing controls (with file paths), the initial risk (with today's controls), further controls (each with its type, status and when it's needed), the residual risk (once those controls are in place), whether controls are transferred, an owner and a status.
- **IDs** (`H-01`, `H-02`, ...) are never reused. A hazard that no longer applies is closed with a reason, not deleted.
- **Statuses:** Open (further controls needed), Controlled (residual risk accepted by the CSO), Closed (no longer applies), Transferred (controlled by the deploying organisation).
- **Who changes it:** anyone can propose a change in a pull request; the CSO approves it.

## 6. The clinical safety case report

- **One for each release** that customers can use with real patients, and for each change with a clinical safety impact. `clinical-safety-case-report.md` is the template, filled as far as the current system allows.
- **What it says:** the release, its intended purpose, the risk scheme used, the safety claims with their evidence, the hazards and their residual risks, outstanding defects with a clinical impact, the assumptions about deploying organisations, and the CSO's conclusion.
- **Who signs it:** the CSO, then the accountable owner. A release without a signed safety case report isn't made available for live use.

## 7. Clinical safety incidents

An incident is anything that harmed, or could have harmed, a patient, where MedApp was involved: a defect, a misleading screen, lost or wrong data, an outage, or a use the design didn't foresee.

1. **Reporting.** Customers and users report to [support email or portal], marked "clinical safety". Staff of the manufacturer who notice one report it the same way. Customers also report through their own incident systems (in the NHS, Learn from Patient Safety Events), as DCB0160 requires of them.
2. **Acknowledging:** within one working day, and the same day for anything that harmed a patient or could affect other customers.
3. **Assessing:** the CSO assesses it within two working days: which hazard it belongs to (or a new one), its risk, and whether it's a defect.
4. **Containing:** if other patients are at risk, tell every affected customer what to do now (a workaround, or stop using a function), the same day. Disable or roll back the function where possible (a rollback is a revert on `main`; `AGENTS.md`).
5. **Fixing:** a fix goes through the release gate like any change, with the CSO's sign-off.
6. **Recording:** each incident, its assessment, actions and closure go in the incident log, [location], and the hazard log is updated.
7. **Learning:** a short review after each incident that harmed a patient, or any incident rated 3 or above.

If MedApp ever becomes a medical device, incidents also have to be reported to the MHRA under its vigilance rules.

## 8. Changes and the release gate

Every merge to `main` deploys (`docs/architecture.md`, risk D1), so the gate has to be at the pull request, not later.

### Which changes need a clinical safety review

A change needs one if it touches any of:
- the procedure workflow: `MedApp.Application/Procedures/**`, `MedApp.Domain/Procedures/**`
- patients: `MedApp.Application/Patients/**`, `MedApp.Domain/Patients/**`
- notifications: `MedApp.Application/Notifications/**`, `MedApp.API/Notifications/**`, `MedApp.Web/src/app/LiveNotifications.tsx`
- the screens where clinical work is done or shown: `MedApp.Web/src/pages/{patients,procedures,tasks,audit,setup}/**`, `MedApp.Web/src/components/procedures/**`, `MedApp.Web/src/utils/dates.ts`
- migrations that change procedure, stage, patient or audit data
- authentication and sessions, where they affect who a change is attributed to
- anything in the device triggers (`intended-purpose-and-device-decision.md`, "What would change the decision")
- the intended purpose, the instructions for use, or marketing text

### What the review is

- **The author** fills in the clinical safety section of the pull request (below): which hazards the change affects, any new hazard, the controls and tests.
- **The CSO** reviews and signs it off in the pull request before it's merged, or says it has no clinical impact.
- **The hazard log** and, if needed, the device decision are updated in the same pull request.

### The pull request section (planned)

**Planned:** add this to a pull request template (`.github/pull_request_template.md`) and to the merge flow in `AGENTS.md`. Until then, the author adds it by hand.

```markdown
## Clinical safety

- [ ] This change has no clinical impact (say why), or:
- Hazards affected (from docs/clinical-safety/hazard-log.md): H-..
- New hazards: none / H-.. (added to the hazard log)
- Controls added or changed, and the tests that prove them:
- Does it add a calculation, a score, a priority, a recommendation or an alert from patient data? yes / no
  (yes: reassess docs/clinical-safety/intended-purpose-and-device-decision.md before merging)
- CSO sign-off: [name, date] / not needed (no clinical impact, confirmed by [name])
```

### Until a CSO is appointed

No change goes to live clinical use: there is none today. Changes may still be merged for development and test, with the clinical safety section filled in, and the CSO reviews them all before the first live release.

## 9. When this plan and the file are reviewed

The CSO reviews the plan, the hazard log and the device decision:
- **for every release** with a clinical impact;
- **after any clinical safety incident,** and when a new hazard is found;
- **when the intended purpose changes,** or a feature meets a device trigger;
- **before a new kind of customer or setting** (the first NHS organisation, a new care setting, a new country);
- **when the hosting changes** in a way that affects availability or data (first Azure deployment, a new region, a backup change);
- **when the standards change:** the revised DCB0129 and DCB0160 that follow NHS England's 2026 consultation, and any new UK device definition;
- **when the CSO changes;**
- **at least once a year** otherwise.

## 10. Auditing the process

Once a year, the CSO checks that the process was followed: that every clinical-impact pull request had its section and sign-off, that the hazard log matches the code, that incidents were handled in time, and that training records are current. The result is recorded in the file.

## 11. The clinical risk management file

| Item | Where |
|---|---|
| This plan | `docs/clinical-safety/clinical-risk-management-plan.md` |
| The hazard log | `docs/clinical-safety/hazard-log.md` |
| The safety case reports | `docs/clinical-safety/clinical-safety-case-report.md` (template and the current draft); signed copies at [location] |
| The intended purpose and device decision | `docs/clinical-safety/intended-purpose-and-device-decision.md` |
| Guidance for deploying organisations | `docs/clinical-safety/dcb0160-deploying-organisations.md` |
| The CSO's appointment, registration and training records | [location, not in the repository] |
| The incident log | [location] |
| Clinical safety sign-offs of pull requests | In each pull request on GitHub |

**Retention:** for the life of the product, and at least 8 years after the last deployment is decommissioned.

**Access:** the repository is public today (`docs/architecture.md`, section 1.5). The plan and hazard log can be public, but signed copies, the incident log and anything naming people or customers are kept privately.

## Approval

| Role | Name | Signature | Date |
|---|---|---|---|
| Clinical Safety Officer | [Name, registration] | | |
| Accountable owner | [Name] | | |

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | First draft | AI coding agent, for review |
