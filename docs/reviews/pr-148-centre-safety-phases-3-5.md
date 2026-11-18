# Review: PR #148, safety and DPIA documents for Phases 3 to 5

**PR:** [#148](https://github.com/Aliator/MedApp/pull/148), branch `feature/centre-safety-phases-3-5`, reviewed at `eb46e01` on 18 November 2026. A quick review of the diff (docs only); nothing was run. No findings.

Combines the plan's safety PRs 11, 18 and 29: the device decision (0.10) gains a reassessment per phase, five claims not to make and designed function rows; the hazard log (0.18) gains draft hazards H-45 to H-64; the DPIA (0.15) gains planned risks 29 to 40 in their own tables; the DCB0160 guidance (0.6) gains the deploying organisation's planned controls and downtime steps.

**Checked:** the device decision is consistent: lab reports are stored as documents and never read, compared or flagged; questionnaire totals are stored as written and never calculated; branches are chosen by a clinician with none pre-selected; letters merge only identity and administrative data; incoming referrals are matched by a person; a missed appointment never discharges or re-prioritises anyone. H-45 to H-64 and DPIA risks 29 to 40 are free on `main` (it ends at H-44 and risk 26; 27 and 28 are reserved for Phase 2). Nothing is described as held before a feature holds it: every new risk is marked "Planned: applies when PR n merges".

**Remaining risk:** the estimates are drafts for a clinical safety officer to replace.
