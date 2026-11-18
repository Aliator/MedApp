# Review: PR #144, procedures renamed to pathways

**PR:** [#144](https://github.com/Aliator/MedApp/pull/144), branch `feature/pathways-rename`, reviewed at `72e8331` on 18 November 2026. A quick review of the migration, `beforePathways.ts` and the deploy and CI diff, not the 1,002-file rename itself; nothing was run. The PR's `ci:full` run passed: https://github.com/Aliator/MedApp/actions/runs/44287602356. No finding is high or critical.

Renames Procedure to Pathway and stage templates to pathway templates in the code, database, API (`api/pathways`, no aliases), web addresses and screens, with screen-only labels (Awaiting review, On hold, Discharged, Target date, My Worklist, Stage Library), by the committed script `tools/rename-procedures-to-pathways.py`.

**Checked:** `RenamedProceduresToPathways` only drops and re-adds keys and renames tables, columns and indexes (no `DropTable` or `DropColumn`); the four cascades stay cascades (lines 136, 144, 159, 174); the filtered note-deletion index is renamed both ways (99, 319); `Down` reverses `Up`. Its data updates are narrow: `attachedTo` only for file actions 10 to 12 on the exact JSON pair, and placeholder titles only on anonymised records. No stored number changed. Old addresses and pins are redirected or read under their old keys (`beforePathways.ts:12-13`, `53-56`).

**Findings, both low and accepted:**
- `UPDATE Notifications SET Title = N'Anonymised pathway' WHERE Kind = 0 AND Title = N'Anonymised procedure'` (about line 213) doesn't check the patient is anonymised, so a real pathway titled exactly "Anonymised procedure" would have its assignment notifications relabelled.
- `Down` turns every "Anonymised pathway" title on an anonymised record back into "Anonymised procedure", including one that had that title before the migration.

**Remaining risk:** the migration can't run while older pods are serving, and a rollback needs `Down`: acceptable only while nothing is live (the chart's README says so). The renamed screens hadn't been looked at in a browser before merging.
