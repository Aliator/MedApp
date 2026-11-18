# Review: PR #143, the Phase 2 plan of the community specialist centre

**PR:** [#143](https://github.com/Aliator/MedApp/pull/143), branch `feature/centre-phase2-plan`, reviewed at `46d40a5` on 18 November 2026. A quick review of the diff (docs only); nothing was run. No findings.

Adds `docs/centre-phase2-plan.md` (specialties, roles with admin-managed permissions, referrals and triage, the waiting-time clock and stage filters, with the owner's answers to its questions) and updates the device decision (version 0.9) and the hazard log (0.17, hazards H-40 to H-44).

**Checked:** the device decision is consistent: a clinician chooses the outcome, template and priority and MedApp records it; nothing prefills or suggests a priority, outcome or template, and "MedApp triages referrals" is a forbidden claim. Each new hazard's controls match the plan's decisions (H-40: 39, 44; H-41: 19, 25; H-42: 35; H-44: 12, 20). H-40 to H-44 don't clash with H-01 to H-39. The plan's stored numbers are free on `main` (`ActivityAction` ends at 41, `NotificationKind` at 22).

**Remaining risk:** the hazards are drafts until a clinical safety officer reviews them.
