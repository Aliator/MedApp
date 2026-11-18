# Review: PR #158, triage

**PR:** [#158](https://github.com/Aliator/MedApp/pull/158), branch `feature/triage`, reviewed at `afb47c0` on 18 November 2026. A quick safety and security review of the triage handlers, guard, notification, timeline, migration and dialogs; nothing was run. CI (full tier, with a new E2E journey) passed: https://github.com/Aliator/MedApp/actions/runs/44326415619. No finding is high or critical.

Adds accept (creating the pathway through `PathwayCreation` in the same save), reject and redirect for referrals awaiting triage, the `ReferralTriaged` (23) notification to the registrant, and each specialty's triager count with the Specialties page's warning.

**Checked:** all three endpoints need `TriageReferrals`, and `GetForTriageOrThrowAsync` (`ReferralRepositoryExtensions.cs:298-306`) also requires membership of the referral's specialty and refuses restricted or anonymised records; there's no admin bypass. The referral's row version and one save for the referral, its history, the pathway and its audit mean a losing triager rolls back whole, with no orphan pathway. Accept goes through `PathwayCreation.FromTemplate`/`Record` and calls the monitor after the save. History holds no free text; the decision, redirect and destination details are admin-only on the timeline; the notification holds the username, outcome and specialty only. Nothing is prefilled or suggested: the dialogs start empty (`AcceptReferralDialog.tsx:62`), the requested priority is shown as text only, and the validator fills no default. The migration only adds two nullable columns.

**Findings, low, left as to-dos:**
- `TimelineMapper.cs` shows the referral's latest `RedirectDetail` on every `SpecialtyChanged` entry, so after two moves admins see the second reason against the first.
- `AcceptReferralHandler.cs:85-89` reports any conflict from the pathway save as "changed meanwhile", even one not caused by the referral's row version (fails closed).
