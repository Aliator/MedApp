# Review of PR #183: consent records with a stage gate

- **PR:** https://github.com/Aliator/MedApp/pull/183, branch `feature/consent-records`. Quick review (low effort) of `c61872ad`, 18 November 2026; the branch was then merged with `main` (results sign-off, #182) and `AddedConsentRecords` rebuilt on top of `AddedResults`.
- **What it does:** PR 31 of `docs/centre-phases-3-5-plan.md`. Adds `ConsentForm` and `ConsentRecord`, `NeedsConsent` on stage definitions and pathway stages, the `RecordConsent` permission, `api/pathways/{id}/consent-records`, `api/consent-records/{id}/withdraw|entered-in-error|scan` and `api/consent-forms`. Audit actions `ConsentRecorded = 42` to `ConsentEnteredInError = 44`. The web app has a Consent card on the pathway page, a Consent Forms page and a Needs consent checkbox.

**Checked:**
- The gate (`StageGates.WhyConsentMissing`) only asks for a Valid record for that stage. Every finish route goes through `FinishPathwayStageHandler` and `StageFinishing.Finish`.
- A scan can't be deleted on its own (`DeleteAttachmentHandler`, 409). Entered in error soft-deletes it with an `AttachmentDeleted` entry.
- The staff timeline never carries the withdrawal or correction detail (`TimelineMapper`, canary extended). The anonymiser replaces the detail; the export lists every record.
- `[RequirePermission]` is on every new action, with rows in `AccessBeforeAndAfterPermissionsTests`. `RecordConsent` is in `NotForAdmin`.
- Audit numbers 42 to 44 were free (MDT took 38 to 41, results 24 to 28). The migration only adds: two tables, two `bit` columns defaulting to false, and permission rows. Down drops only those.

**Findings:**
- **Low:** skipping a `NeedsConsent` stage isn't gated (`PathwayWorkflow.WhySkipNotAllowed`). This is intended: a skipped stage wasn't done, so it needs no consent. It's now documented in `docs/agents/pathways.md`.

**Remaining risk:**
- The first merge of `main` (MDT list) was recreated by hand with `git commit-tree` after a `git stash` dropped the merge state. Both parents are correct, but the tree was resolved manually.
- `ConsentTests` (integration) and the role-migration consent test haven't run yet. They first run in the phase's full CI.
