# Review: PR #174, stage override permissions split

**PR:** [#174](https://github.com/Aliator/MedApp/pull/174), branch `feature/stage-permissions`, reviewed at `e20b04c` on 18 November 2026. A quick read of the migration, the catalogue and every former `OverrideStages` use; nothing was run, and CI wasn't waited for (the owner's per-phase testing). No findings.

Splits `OverrideStages` into `UnlockStages` (unlock and reinstate stages) and `FinishWithoutVisit` (finish or skip a Required-visit stage without its visit), adds the Senior clinician starting role, takes the visit bypass from Admin, and adds the monitoring event `StageFinishedWithoutVisit` (14, `NotificationKind` 38).

**Checked:** `SplitOverrideStages` gives both halves only to roles that held `OverrideStages` and adds Senior clinician only if no role has its name or ID; the only access lost is Admin's `FinishWithoutVisit`; no Admin-only permission lands on an editable role; nothing outside comments and migrations still names `OverrideStages`. The monitoring message goes through `MonitoringNotifications`, which allows no typed free text (no pathway title, notes or reasons). `Down` restores `OverrideStages` only to roles holding both halves (refuses rather than grants).

**Remaining risk:** during a rollout, older API copies refuse unlocking, reinstating and the bypass to everyone but Admin (fails closed).
