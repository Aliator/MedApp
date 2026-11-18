# Review: PR #177, retention classes per pathway template

[PR #177](https://github.com/Aliator/MedApp/pull/177), branch `feature/retention-classes`, compared with `main` on 18 November 2026. **One high finding, fixed in the PR.**

## Summary
Pathway templates get a retention class (Standard, Oncology, Maternity, Mental health), copied onto each pathway. A record's minimum period is the longest of the adult or child rule and its pathways' classes.

## Findings

### High: lowering a template's class shortened existing records' periods (fixed)
- **What could go wrong:** the template's edit rewrote every existing pathway to the new class in either direction. An admin setting a template from Oncology back to Standard, even by mistake, would bring its patients' records into the retention review up to 22 years early, one action away from anonymisation.
- **Evidence:** `PatientPathwayStageRepository.UpdatePathwayTemplateAsync`, through `UpdatePathwayTemplateHandler`.
- **Fix (in this PR):** a template's class change now only **raises** existing pathways. `RetentionClassYears.ShorterThan` lists the classes with a shorter period (Standard counts as shortest). `RaisePathwayRetentionClassesAsync` rewrites only pathways in those classes. A shorter class changes the template, and so new pathways, but never existing ones. New tests: `Handle_StandardToOncology_RaisesExistingPathwaysFromEveryShorterClass`, `Handle_LoweringTheClass_LeavesExistingPathwaysAsTheyAre` and `ShorterThan_*`. The template editor says a shorter class doesn't shorten existing records. The plan's PR 27 note and hazard H-57 are updated.
- **Not done:** the class change isn't recorded in a history. Templates have no audit log and template edits aren't in the activity log, so there was no obvious place for it.

## Remaining risks
- A deleted pathway's class stops counting (`docs/todo.md`).
- The class years are proposed defaults until the DPO confirms them.
