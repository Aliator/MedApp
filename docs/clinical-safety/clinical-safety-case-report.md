# MedApp: clinical safety case report

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-03, clinical safety case report (DCB0129) |
| **Version** | 0.1, draft |
| **Date** | 16 November 2026 |
| **Release** | [Release name or number]. Drafted against `main` at `88dc1fa` (the merge of #56). |
| **Manufacturer** | [Manufacturer name] |
| **Drafted by** | An AI coding agent, for the manufacturer. Not yet reviewed by a person. |
| **Approved by** | [Clinical Safety Officer], [accountable owner] (see [sign-off](#10-sign-off)) |

## How to use this template

- **One report for each release** that customers can use with real patients, and for each change with a clinical safety impact (`clinical-risk-management-plan.md`, section 6).
- **This copy is filled in** as far as the system at `88dc1fa` allows. The parts only the CSO can write are in square brackets.
- **For a new release:** copy this file to `safety-case-<release>.md`, update every section against that release and the hazard log as it then stands, and keep the signed copy (`clinical-risk-management-plan.md`, section 11).

## 1. Summary

- **What MedApp is:** software that records and coordinates the stages of patients' procedures, with task lists, due dates and an audit trail. It isn't a medical device (`intended-purpose-and-device-decision.md`).
- **The safety position at `88dc1fa`:**
  - 20 hazards are logged (`hazard-log.md`). None is rated 4 or 5.
  - **Nine are rated 3 (undesirable)** with today's controls: wrong patient (H-01), wrong record (H-02), a missed overdue procedure (H-04), a forgotten blocked procedure (H-05), a stage finished without the work done (H-07), unavailability (H-10), times shown an hour out (H-12, suspected), wrong template content (H-13) and concurrent edits (H-15).
  - With the further controls in the hazard log, every hazard comes down to 2 or below.
- **The conclusion of this draft:** **this release is not yet acceptable for live clinical use.** The controls marked "before first live use" in the hazard log have to be built and verified first, or the CSO has to justify accepting the remaining hazards at 3. MedApp has never been used with real patients, so there is no live use to stop.
- **[CSO's conclusion: to be written by the CSO.]**

## 2. The release

| | |
|---|---|
| **Version** | [Release], built from commit [SHA]; this draft: `88dc1fa` |
| **What's in it** | Patients; procedures from templates; stages worked in order, finished and locked, unlocked by admins; status workflow; due dates and overdue; assignments and My Tasks; assignment notifications; the procedure audit log; administration of users, stage definitions and templates (`docs/architecture.md`, section 1.1) |
| **Changes since the last safety case** | None: this is the first. [For later releases: the pull requests with a clinical impact, and their clinical safety sections.] |
| **Where it runs** | [Customer deployment, or the manufacturer's hosted service on Azure UK South.] Today it has run only in development and on a local test cluster. |
| **Who uses it** | Trained clinical and administrative staff of the deploying organisation, and its administrators |

## 3. Intended purpose and scope

The intended purpose, its users, population and environment, and what it's not for, are in `intended-purpose-and-device-decision.md`. In short: MedApp records and coordinates the steps of a procedure; it doesn't interpret clinical data, prioritise patients or recommend treatment.

**Out of scope of this report:** each organisation's deployment, configuration, templates, training and local processes, which its own DCB0160 safety case covers (`dcb0160-deploying-organisations.md`).

## 4. Clinical risk management

- **The process:** `clinical-risk-management-plan.md`.
- **The risk scheme:** five consequence levels (minor to catastrophic), five likelihood levels (very low to very high), and the 5×5 matrix from the DCB0129 implementation guidance, giving ratings 1 (acceptable) to 5 (unacceptable). Acceptability rules are in the plan, section 4.4.
- **The CSO:** [name, profession, professional body and registration number].
- **How the hazards were found:** by reading the code at `88dc1fa`, and walking each workflow in `docs/architecture.md` section 6.3 and `docs/procedures-ui-plan.md`. **Not yet done:** the hazard workshop with clinicians from a likely customer, and any review by the CSO. [CSO: record the workshop, its attendees and what it added.]

## 5. Safety claims and the evidence for them

Each claim is backed by evidence that exists today, and says where it falls short.

### Claim 1: The workflow rules that protect the record are enforced by the server, not just the screen

| Evidence | Where |
|---|---|
| Stages can only be worked in order; only the current stage can be edited or finished; a finished stage is locked | `ProcedureWorkflow.WhyStageCantBeEdited`, `ProcedureChange.FinishStage` |
| A change that breaks a rule is refused with a 409, whatever the screen shows | Every procedure command handler, for example `FinishProcedureStageHandler`, `UpdateProcedureStageNotesHandler` |
| A procedure can only be completed with every stage finished and locked | `ChangePatientProcedureStatusHandler` |
| Status changes follow one table; blocking and cancelling need a reason | `ProcedureWorkflow.AllowedStatusChanges`, `ChangePatientProcedureStatusValidator` |
| Only admins can unlock stages, assign staff, delete procedures and change templates | `[Authorize(Roles = "Admin")]` on those endpoints (`docs/architecture.md`, section 5.7) |
| Tests | `ProcedureWorkflowTests` (41 cases), `ProcedureChangeTests` (19), and a test class for every procedure handler and validator under `MedApp.UnitTests/Application/Procedures` |

**Limits:** the role attributes on endpoints aren't covered by tests, except the own-password and create-user endpoints (`docs/architecture.md`, risk S7). Nothing stops two people's changes overwriting each other (H-15).

### Claim 2: Every change to a procedure can be traced to a person and a time

| Evidence | Where |
|---|---|
| Every procedure change writes audit entries, saved in the same transaction as the change | `ProcedureChange.Audit`, `PatientProcedureRepository.SaveAsync` |
| Notes changes keep the old and new text | `ProcedureChange.UpdateStageNotes` |
| Audit entries outlive deletion of the procedure (no foreign keys), and keep the username and title as they were | `PatientProcedureAuditEntry`; ADR-7 in `docs/architecture.md` |
| Tests | The procedure command handler tests check the audit entries the commands write, and `ProcedureChangeTests` checks the entries `ProcedureChange` writes |

**Limits:** changes to patients, templates and stage definitions aren't audited (H-03, H-13). Reads aren't logged. Times may be shown an hour out (H-12).

### Claim 3: Due dates mean the same thing everywhere

| Evidence | Where |
|---|---|
| A due date is a whole day, due by the end of that day in UK time, stored as that moment in UTC | `DueDates` |
| Overdue has one rule, used in memory and in SQL | `ProcedureWorkflow.IsOverdue`, `ProcedureSearch.Filter` |
| Tests cover summer, winter and both clock-change days | `DueDatesTests` |

**Limits:** "overdue" is only a badge; nothing alerts anyone (H-04). Blocked procedures are never overdue (H-05). Due dates have no time of day (H-11).

### Claim 4: Finished work isn't rewritten by later changes to templates

| Evidence | Where |
|---|---|
| Each procedure copies its template's stage names, descriptions and instructions when it's created | `CreatePatientProcedureHandler` |
| Templates and definitions in use are archived, not deleted | the template and definition delete and archive handlers |

**Limits:** the same rule means a correction doesn't reach procedures already under way (H-14), and template changes have no history or approval (H-13).

### Claim 5: Optional parts fail soft

| Evidence | Where |
|---|---|
| If server rendering fails, the browser renders the page | `SsrPages`, `SsrPagesTests`, `SsrClientTests` |
| Notifications are stored before they're pushed, so a failed push shows at the next page load | `NotificationSender` |
| A failed migration stops the rollout and leaves the old version running | ADR-10 |

**Limits:** there's no monitoring, so failures go unnoticed (H-10); the live connection stops retrying after about 42 seconds (H-09).

### Claim 6: The residual risk of the release is acceptable

**Not yet supported.** Nine hazards are at 3. See [section 7](#7-outstanding-hazards-and-defects).

## 6. Hazards and their risk

From `hazard-log.md`, version 0.1.

| ID | Hazard | Initial | Residual (once further controls are in place) | Controls needed before first live use |
|---|---|---|---|---|
| H-01 | The wrong patient's procedure is worked | 3 | 2 | Patient banner; date of birth in lists |
| H-02 | Work is recorded on the wrong record | 3 | 2 | Patient banner; patient named in confirmations |
| H-03 | A patient's details are changed or deleted without trace | 2 | 2 | None (patient audit planned, step 7) |
| H-04 | An overdue procedure is missed | 3 | 2 | Overdue notifications; an unassigned-and-due view; local review process |
| H-05 | A blocked procedure is forgotten | 3 | 2 | A "past due date" flag on blocked procedures |
| H-06 | A stage is finished in error | 2 | 2 | None |
| H-07 | A stage is recorded as finished when the work wasn't done | 3 | 2 | Notes, or "nothing to record", before finishing |
| H-08 | A procedure is cancelled, completed or archived in error | 2 | 2 | None |
| H-09 | An assignment notification isn't received | 2 | 2 | Information for users |
| H-10 | MedApp is unavailable | 3 | 2 | Printable task list and summary; downtime procedure; monitoring |
| H-11 | Due dates don't mean what staff think | 2 | 2 | Information on the form |
| H-12 | Times are shown an hour out (suspected) | 3 | 1 | Verify and fix |
| H-13 | A template or stage definition has wrong content | 3 | 2 | Audit template and definition changes; local template governance |
| H-14 | A correction to a template doesn't reach procedures under way | 2 | 2 | None |
| H-15 | Two people edit the same procedure at once | 3 | 2 | Optimistic concurrency; keep the draft on a conflict |
| H-16 | The screen shows out-of-date information | 2 | 2 | None |
| H-17 | Stage instructions aren't read | 2 | 2 | None |
| H-18 | A change is recorded under the wrong person's name | 2 | 2 | None |
| H-19 | Part of the clinical record is lost by deletion | 2 | 2 | None |
| H-20 | Data is lost, or restored to an earlier point | 2 | 2 | Tested restore; restore runbook |

[CSO: confirm or change each rating, and record the reasons for any change.]

## 7. Outstanding hazards and defects

### Hazards not yet controlled to an acceptable level

H-01, H-02, H-04, H-05, H-07, H-10, H-12, H-13 and H-15, at 3. For each, the CSO either confirms that its "before first live use" controls are built and verified, or writes a justification for accepting it at 3:

| Hazard | Decision | Justification or evidence | CSO |
|---|---|---|---|
| H-01 | [Controlled / accepted at 3 / not accepted] | | |
| H-02 | | | |
| H-04 | | | |
| H-05 | | | |
| H-07 | | | |
| H-10 | | | |
| H-12 | | | |
| H-13 | | | |
| H-15 | | | |

### Known defects with a clinical impact

| Defect | Hazard | State |
|---|---|---|
| Timestamps read from the database are sent without a time zone, and the web app reads them as local time, so they show an hour early in summer | H-12 | Suspected, from reading the code. To reproduce and fix. |
| Live notifications stop reconnecting after about 42 seconds | H-09 | Known (`docs/architecture.md`, risk R11). Planned, step 2. |
| No optimistic concurrency: the last save wins | H-15 | Known (risk R8). Planned, step 6. |
| Sessions have no idle timeout | H-18 | Known (risk S5). Planned, step 2. |
| Patient changes and deletions aren't audited | H-03 | Known (risk S2). Planned, step 7. |

[For each release: list the open defects from the issue tracker that have a clinical impact.]

## 8. Assumptions about deploying organisations

This report holds only if each deploying organisation:
- **identifies patients** at the point of care by its own positive identification process;
- **governs its templates:** clinical sign-off of each template and stage definition before use, and review when a template changes;
- **trains its staff** in MedApp, including what finishing a stage means, what a due date means, and that notifications aren't to be relied on;
- **has at least two admins,** so assignment and unlocking never wait on one person;
- **reviews overdue, unassigned and blocked procedures** on a set schedule;
- **has a downtime procedure,** with printed task lists, and a way to enter work done during downtime afterwards;
- **keeps the clinical record** of what was done in its own clinical systems, using MedApp to coordinate the steps;
- **runs DCB0160:** its own CSO, hazard log and safety case for the deployment, and reports incidents to the manufacturer;
- **uses MedApp within its intended purpose,** and not for time-critical or emergency care.

These are listed for the customer in `dcb0160-deploying-organisations.md`.

## 9. Conclusion and recommendation

- **For this draft:** MedApp at `88dc1fa` should not be used with real patients until the controls marked "before first live use" are built and verified, the CSO has reviewed every hazard, and the hazard workshop has been held.
- **[CSO's conclusion:** whether the residual risk of this release is acceptable for its intended purpose, under the stated assumptions, and any conditions on its use.**]**

## 10. Sign-off

| Role | Name | Registration (CSO) | Signature | Date |
|---|---|---|---|---|
| Clinical Safety Officer | [Name] | [Professional body and number] | | |
| Accountable owner | [Name] | n/a | | |

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | Template, filled in for `88dc1fa` | AI coding agent, for review |
