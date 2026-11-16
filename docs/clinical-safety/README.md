# MedApp: clinical safety

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

This folder is MedApp's clinical risk management file under DCB0129, the NHS standard for manufacturers of health IT. It's step 6 of the roadmap in `docs/architecture.md` ("Clinical safety and the medical device decision").

## Status

- **Drafted on 16 November 2026,** against `main` at `88dc1fa`, by an AI coding agent for the manufacturer, from the code and the handbook.
- **Nothing here is approved.** No Clinical Safety Officer (CSO) has been appointed, and no clinician has reviewed it. The ratings are estimates for the CSO to confirm.
- **MedApp has never been used with real patients.** The drafts conclude that it isn't ready to be, until the controls marked "before first live use" in the hazard log are in place.

## The documents

| ID | Document | What it's for | For |
|---|---|---|---|
| CS-01 | [clinical-risk-management-plan.md](clinical-risk-management-plan.md) | How clinical risk is managed: scope, roles (with the CSO), the process, the 5×5 risk matrix and acceptability, incidents, the release gate, and when the file is reviewed | The manufacturer and its CSO |
| CS-02 | [hazard-log.md](hazard-log.md) | 20 hazards in the system as it is, each with causes, effects, existing controls linked to the code, initial and residual risk, and the further controls needed | The CSO; deploying organisations |
| CS-03 | [clinical-safety-case-report.md](clinical-safety-case-report.md) | The safety case report template, filled as far as the current system allows: claims, evidence, outstanding hazards and defects, assumptions | Each release; deploying organisations; DTAC |
| CS-04 | [intended-purpose-and-device-decision.md](intended-purpose-and-device-decision.md) | The intended purpose, and the reasoning that MedApp isn't a medical device under UK MDR 2002, with what would change that | The accountable owner; the CSO; buyers |
| CS-05 | [dcb0160-deploying-organisations.md](dcb0160-deploying-organisations.md) | What a customer must do under DCB0160, what the manufacturer gives them, the controls transferred to them, and a starter hazard log | Deploying organisations' CSOs |

## The main findings

- **The device decision:** MedApp is an administrative workflow tool, not a medical device. It follows the path of a procedure with no decisions, and doesn't interpret clinical data. Features that calculate, score, prioritise, triage, recommend or flag from patient data would change that, and trigger a new assessment before they're merged.
- **Nine hazards are rated 3 (undesirable) today:** the wrong patient (H-01), the wrong record (H-02), a missed overdue procedure (H-04), a forgotten blocked procedure (H-05), a stage finished without the work done (H-07), unavailability (H-10), times shown an hour out (H-12), wrong template content (H-13) and concurrent edits (H-15). None is rated 4 or 5.
- **A suspected defect:** times read from the database seem to be sent without a time zone and shown as browser-local time, an hour early in summer (H-12). It needs reproducing and fixing.

## Decisions made in drafting

The owner asked for every choice to be made for them. These were:
- **Not a medical device,** for the reasons in CS-04, with the intended purpose written to keep it so.
- **The risk scheme:** DCB0129's five consequence and five likelihood levels, and its 5×5 matrix with ratings 1 to 5. A 3 can't be accepted for first live use without the CSO's written justification.
- **Initial and residual risk:** initial means with today's controls; residual means once the further controls are built and verified.
- **The assumption behind every rating:** customers identify patients at the point of care themselves, and use MedApp within its intended purpose.
- **Which controls come first:** the ones marked "before first live use" in the hazard log, chosen for the hazards at 3, and for anything that costs almost nothing (wording on forms, information for users).
- **The release gate is at the pull request,** because every merge deploys. The pull request section is written out in CS-01 but not yet added as a template, so it doesn't change the repository's workflow before a CSO is appointed.
- **Incident times:** acknowledge within one working day (same day if a patient was harmed), the CSO assesses within two working days, and affected customers hear the same day if others are at risk.
- **Retention of the file:** the life of the product, and at least 8 years after the last deployment ends.
- **AI agents** may draft clinical safety work but can't approve it; only the CSO can.
- **Placeholders** stand for every name, organisation, registration number and contact. Nothing names a real person or customer.

## What's needed to finish it

1. **Appoint a CSO** (a registered clinician, contracted for a few days a month) and, ideally, a deputy.
2. **Hold a hazard workshop** with the CSO and clinicians from a likely customer, and update the hazard log.
3. **Verify H-12,** and fix it if it's real.
4. **Build the "before first live use" controls,** with tests, and re-rate the hazards.
5. **Have the CSO sign** the plan, the hazard log, the device decision and the first safety case report, and the accountable owner sign the plan and the device decision.
6. **Add the clinical safety section** to a pull request template and to the merge flow in `AGENTS.md`.
7. **Write the instructions for use** and the training material that CS-05 refers to.
