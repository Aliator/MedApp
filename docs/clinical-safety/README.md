# MedApp: clinical safety

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

This folder is MedApp's clinical risk management file under DCB0129, the NHS standard for manufacturers of health IT. It's step 6 of the roadmap in `docs/architecture.md` ("Clinical safety and the medical device decision").

## Status

- **Drafted on 16 November 2026,** against `main` at `88dc1fa`, by an AI coding agent for the manufacturer, from the code and the handbook. Revised the same day after the review of #60 (`docs/reviews/pr-60-clinical-safety.md`).
- **Nothing here is approved.** No Clinical Safety Officer (CSO) has been appointed, and no clinician has reviewed it. The ratings are estimates for the CSO to confirm.
- **MedApp has never been used with real patients.** The drafts conclude that it isn't ready to be, until the controls marked "before first live use" in the hazard log are in place and the CSO has justified the hazards that stay at 3.

## The documents

| ID | Document | What it's for | For |
|---|---|---|---|
| CS-01 | [clinical-risk-management-plan.md](clinical-risk-management-plan.md) | How clinical risk is managed: scope, roles (with the CSO), the process, DCB0129's consequence table and 5×5 risk matrix, acceptability and the go-live rule, incidents, the release gate, and when the file is reviewed | The manufacturer and its CSO |
| CS-02 | [hazard-log.md](hazard-log.md) | 21 hazards in the system as it is, each with causes, effects, existing controls linked to the code, further controls, and three ratings: today, at first live use, and residual | The CSO; deploying organisations |
| CS-03 | [clinical-safety-case-report.md](clinical-safety-case-report.md) | The safety case report template, filled as far as the current system allows: claims, evidence, outstanding hazards and defects, assumptions | Each release; deploying organisations; DTAC |
| CS-04 | [intended-purpose-and-device-decision.md](intended-purpose-and-device-decision.md) | The intended purpose, and the reasoning that MedApp isn't a medical device under UK MDR 2002, with what would change that | The accountable owner; the CSO; buyers |
| CS-05 | [dcb0160-deploying-organisations.md](dcb0160-deploying-organisations.md) | What a customer must do under DCB0160, what the manufacturer gives them, the controls transferred to them, and a starter hazard log. Not to be given to a customer until the manufacturer's CSO has signed it | Deploying organisations' CSOs |

## The main findings

- **The device decision:** MedApp is an administrative workflow tool, not a medical device. It follows the path of a procedure with no decisions, and doesn't interpret clinical data. Features that calculate, score, prioritise, triage, recommend or flag from patient data would change that. A rule in the merge flow of `AGENTS.md` makes such a change update CS-04 before it merges.
- **Today, one hazard is rated 4 and twelve are rated 3,** on DCB0129's consequence table, which counts how many patients a harm affects:
  - **4:** wrong template content (H-13), because one wrong template reaches every patient whose procedure comes from it.
  - **3:** the wrong patient (H-01), the wrong record (H-02), a missed overdue procedure (H-04), a forgotten blocked procedure (H-05), a stage finished without the work done (H-07), not learning of assigned work (H-09), unavailability (H-10), times shown an hour out (H-12), a correction not reaching procedures under way (H-14), concurrent edits (H-15), data lost or restored (H-20), and work left on a deleted leaver's account (H-21).
- **At first live use,** with only the "before first live use" controls built, none is above 3, and three stay at 3 (H-07, H-10 and H-13). Each needs the CSO's written justification. H-13 gets there only because its approval step for template changes is brought forward.
- **Two product behaviours the drafts first relied on, and now don't:** My Tasks opens on "Not started", so it hides work already under way (H-04, H-05, H-09); and deleting a leaver's account leaves their open work assigned to nobody (H-21).
- **A defect, confirmed:** times read from the database were sent without a time zone and shown as browser-local time, an hour early in summer (H-12). The fix is in PR #62 (open).

## Choices proposed in drafting

These are proposals for the CSO and the accountable owner to confirm:
- **Not a medical device,** for the reasons in CS-04, with the intended purpose written to keep it so. It allows workflow reminders about dates staff set, so planned overdue notifications don't contradict it.
- **The risk scheme:** DCB0129's consequence table (with its count of patients affected), likelihood scale and 5×5 matrix, rating 1 to 5.
- **Three ratings per hazard:** today; at first live use, counting only the controls marked "before first live use"; and residual, counting every further control.
- **The go-live rule:** no hazard above 3 at first live use, and each hazard at 3 justified in writing by the CSO.
- **The assumption behind every rating:** customers identify patients at the point of care themselves, and use MedApp within its intended purpose.
- **Which controls come first:** the ones marked "before first live use" in the hazard log, chosen for the hazards at 3 or 4, and for anything that costs almost nothing (wording on forms, information for users). H-13's approval step is brought forward because a 4 can't be released.
- **The release gate is at the pull request,** because every merge deploys, and covers every change under `MedApp.*` or `deploy/`. It starts when a CSO is appointed, since nobody can sign it before then. The device question applies now, through `AGENTS.md`.
- **Incident times:** acknowledge within one working day (same day if a patient was harmed), the CSO assesses within two working days, and affected customers hear the same day if others are at risk.
- **Retention of the file:** the life of the product, and at least 8 years after the last deployment ends.
- **AI agents** may draft clinical safety work but can't approve it; only the CSO can.
- **Placeholders** stand for every name, organisation, registration number and contact. Nothing names a real person or customer.

## What's needed to finish it

1. **Appoint a CSO** (a registered clinician, contracted for a few days a month) and, ideally, a deputy.
2. **Hold a hazard workshop** with the CSO and clinicians from a likely customer, following handovers, leavers, bank and agency staff and night shifts on purpose, and update the hazard log.
3. **Merge the H-12 fix** (PR #62), then re-rate H-12 and update this README, CS-03's defects table and the handbook's step 6 status line together.
4. **Build the "before first live use" controls,** with tests, and re-rate the hazards.
5. **Have the CSO sign** the plan, the hazard log, the device decision, the first safety case report and CS-05, and the accountable owner sign the plan and the device decision.
6. **Switch on the release gate:** add the clinical safety section to a pull request template and to the merge flow in `AGENTS.md`.
7. **Write the instructions for use** and the training material that CS-05 refers to.
