# MedApp: clinical safety case report

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-03, clinical safety case report (DCB0129) |
| **Version** | 0.3, draft |
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
  - 22 hazards are logged (`hazard-log.md`), rated on DCB0129's consequence table, which counts how many patients a harm affects.
  - **One is rated 4** (mandatory elimination or control) with today's controls: wrong template content (H-13), because one wrong template reaches every patient whose procedure comes from it.
  - **Twelve are rated 3 (undesirable):** wrong patient (H-01), wrong record (H-02), a missed overdue procedure (H-04), a forgotten blocked procedure (H-05), a stage finished without the work done (H-07), not learning of assigned work (H-09), unavailability (H-10), times shown an hour out (H-12; its defect is fixed by PR #62, which brings it to 2 once verified), a correction not reaching procedures under way (H-14), concurrent edits (H-15), data lost or restored (H-20) and work left on a deleted account (H-21).
  - **At first live use,** with the controls marked "before first live use" built, none is above 3 and three stay at 3: H-07, H-10 and H-13.
  - **Residual,** with every further control built, H-13 stays at 3 and the rest come down to 2.
- **The conclusion of this draft:** **this release is not yet acceptable for live clinical use.** The controls marked "before first live use" in the hazard log have to be built and verified first, and the CSO has to justify accepting H-07, H-10 and H-13 at 3 (the go-live rule, `clinical-risk-management-plan.md` section 4.4). MedApp has never been used with real patients, so there is no live use to stop.
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
- **The risk scheme:** DCB0129's consequence table (five levels, minor to catastrophic, each counting whether one or several patients are affected), its five likelihood levels (very low to very high), and its 5×5 matrix, giving ratings 1 (acceptable) to 5 (unacceptable). Each hazard has three ratings: today, at first live use, and residual. The go-live rule uses the rating at first live use (the plan, section 4.4).
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

**Limits:** changes to templates and stage definitions aren't audited (H-13). Since roadmap step 7, patient changes (which fields, not their values) and reads are recorded in the activity log (H-03). Times were shown an hour out in summer at `88dc1fa` (H-12); fixed by PR #62.

### Claim 3: Due dates mean the same thing everywhere

| Evidence | Where |
|---|---|
| A due date is a whole day, due by the end of that day in UK time, stored as that moment in UTC | `DueDates` |
| Overdue has one rule, used in memory and in SQL | `ProcedureWorkflow.IsOverdue`, `ProcedureSearch.Filter` |
| Tests cover summer, winter and both clock-change days | `DueDatesTests` |

**Limits:** "overdue" is only a badge; today, nothing alerts anyone (H-04), and My Tasks opens on work not yet started, so started overdue work isn't on its default view (H-04, H-09). Blocked procedures are never overdue (H-05). Due dates have no time of day (H-11).

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

**Not yet supported.** Today one hazard is at 4 and twelve at 3; at first live use, three would stay at 3. See [section 7](#7-outstanding-hazards-and-defects).

## 6. Hazards and their risk

From `hazard-log.md`, version 0.2. **At first live use** counts only the controls marked "before first live use"; **residual** counts every further control.

| ID | Hazard | Initial | At first live use | Residual | Controls needed before first live use |
|---|---|---|---|---|---|
| H-01 | The wrong patient's procedure is worked | 3 | 2 | 2 | Patient banner (built on branch `feature/nhs-integrations`, to verify); date of birth in lists |
| H-02 | Work is recorded on the wrong record | 3 | 2 | 2 | Patient banner (built on branch `feature/nhs-integrations`, to verify); patient named in confirmations |
| H-03 | A patient's details are changed or deleted without trace | 2 | 2 | 2 | Patient changes recorded (fields, not old values; step 7) |
| H-04 | An overdue procedure is missed | 3 | 2 | 2 | Overdue notifications; started work counted on My Tasks; an unassigned-and-due view; local review process |
| H-05 | A blocked procedure is forgotten | 3 | 2 | 2 | A "past due date" flag on blocked procedures; started work counted on My Tasks |
| H-06 | A stage is finished in error | 2 | 2 | 2 | None |
| H-07 | A stage is recorded as finished when the work wasn't done | 3 | **3** | 2 | Notes, or "nothing to record", before finishing; CSO justification |
| H-08 | A procedure is cancelled, completed or archived in error | 2 | 2 | 2 | None |
| H-09 | A member of staff doesn't learn of work assigned to them | 3 | 2 | 2 | Started work counted on My Tasks; information for users |
| H-10 | MedApp is unavailable | 3 | **3** | 2 | Printable task list and summary; downtime procedure; monitoring; CSO justification |
| H-11 | Due dates don't mean what staff think | 2 | 2 | 2 | Information on the form |
| H-12 | Times are shown an hour out | 3 | 2 | 2 | The fix, built in PR #62; to verify |
| H-13 | A template or stage definition has wrong content | **4** | **3** | **3** | Approval step for template changes (brought forward); audit of template and definition changes; local template governance; CSO justification |
| H-14 | A correction to a template doesn't reach procedures under way | 3 | 2 | 2 | Open procedures listed when a template in use is edited; local review process |
| H-15 | Two people edit the same procedure at once | 3 | 2 | 2 | Optimistic concurrency; keep the draft on a conflict |
| H-16 | The screen shows out-of-date information | 2 | 2 | 2 | None |
| H-17 | Stage instructions aren't read | 2 | 2 | 2 | None |
| H-18 | A change is recorded under the wrong person's name | 2 | 2 | 2 | None |
| H-19 | Part of the clinical record is lost by deletion | 2 | 2 | 2 | Anonymisation guarded: typed name, reason, refused while procedures are under way (step 7) |
| H-20 | Data is lost, or restored to an earlier point | 3 | 2 | 2 | Tested restore; restore runbook |
| H-21 | Work assigned to a deleted account drops off every task list | 3 | 2 | 2 | Refuse to delete an account with open assignments, or require reassignment first; leaver process |
| H-22 | Care under way is delayed because the patient's record is restricted | 2 | 2 | 2 | Confirming the procedures under way when restricting; blocking and cancelling still allowed; banners; a local procedure for restriction requests |

[CSO: confirm or change each rating, and record the reasons for any change.]

## 7. Outstanding hazards and defects

### Hazards not yet controlled to an acceptable level

The go-live rule uses the rating **at first live use.** For each hazard at 3 or above today, the CSO confirms that its "before first live use" controls are built and verified. For H-07, H-10 and H-13, which stay at 3 even then, the CSO also writes a justification for accepting them at 3, or asks for more controls first.

| Hazard | Today | At first live use | Decision | Justification or evidence | CSO |
|---|---|---|---|---|---|
| H-01 | 3 | 2 | [Controls verified / not verified] | | |
| H-02 | 3 | 2 | | | |
| H-04 | 3 | 2 | | | |
| H-05 | 3 | 2 | | | |
| H-07 | 3 | 3 | [Accepted at 3 / not accepted] | | |
| H-09 | 3 | 2 | | | |
| H-10 | 3 | 3 | [Accepted at 3 / not accepted] | | |
| H-12 | 3 (2 with the fix in PR #62) | 2 | [Fix verified / not verified] | | |
| H-13 | 4 | 3 | [Accepted at 3 / not accepted] | | |
| H-14 | 3 | 2 | | | |
| H-15 | 3 | 2 | | | |
| H-20 | 3 | 2 | | | |
| H-21 | 3 | 2 | | | |

### Known defects with a clinical impact

| Defect | Hazard | State |
|---|---|---|
| Timestamps read from the database are sent without a time zone, and the web app reads them as local time, so they show an hour early in summer | H-12 | Fixed by PR #62, which marks every time read from the database as UTC, so the API sends them with a `Z`, and reads times without an offset as UTC in the web app. To verify against SQL Server and in a browser during British Summer Time. |
| My Tasks opens on work not yet started, so work already under way, including overdue and blocked work, isn't on its default view | H-04, H-05, H-09 | Known. A count of started work, or opening on all open work, proposed before first live use. |
| Deleting an account leaves its open procedures assigned to it, on nobody's task list | H-21 | Known. Refusing the delete, or requiring reassignment first, proposed before first live use. |
| Live notifications stop reconnecting after about 42 seconds | H-09 | Known (`docs/architecture.md`, risk R11). Planned, step 2. |
| No optimistic concurrency: the last save wins | H-15 | Known (risk R8). Planned, step 6. |
| Sessions have no idle timeout | H-18 | Known (risk S5). Planned, step 2. Sessions now end when a password is changed or reset, and an admin can sign a user out everywhere (`feature/security-baseline`, not yet merged). |
| Patient changes and deletions are recorded without their old values | H-03 | Known (risk S2). Who, when and which fields since step 7; old values planned (step 7). |

[For each release: list the open defects from the issue tracker that have a clinical impact.]

## 8. Assumptions about deploying organisations

This report holds only if each deploying organisation:
- **identifies patients** at the point of care by its own positive identification process;
- **governs its templates:** clinical sign-off of each template and stage definition before use, and review when a template changes;
- **trains its staff** in MedApp, including what finishing a stage means, what a due date means, that notifications aren't to be relied on, and that My Tasks must be set to all open work to show everything assigned to them;
- **has at least two admins,** so assignment and unlocking never wait on one person;
- **reassigns a leaver's open procedures** before their account is removed;
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
| 0.2 | 16 November 2026 | After the review of #60: ratings on DCB0129's consequence table, a rating at first live use and the go-live rule on it, H-21, My Tasks' default view, H-12's fix in PR #62 | AI coding agent, for review |
| 0.3 | 16 November 2026 | H-12's defect fixed by PR #62 | AI coding agent, for review |
