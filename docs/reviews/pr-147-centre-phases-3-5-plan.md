# Review: PR #147, the Phases 3 to 5 plan of the community specialist centre

**PR:** [#147](https://github.com/Aliator/MedApp/pull/147), branch `feature/centre-phases-3-5-plan`, reviewed on 18 November 2026. A quick skim of the device positions, section 5 (stored numbers) and section 7 (outside the team); nothing was run. No finding is high or critical.

Plans Phases 3 to 5 (decisions 69 to 180, PRs 11 to 41, hazards H-45 to H-64, DPIA risks 29 to 40): specimens, results sign-off and letters; scheduled jobs, branching pathways, sites, rooms, working hours, clinic sessions, service alerts and reminders; MDT lists, consent, commissioner reports, questionnaires, private patients, the data-request pack, video appointments, a malware scanner, NHS adapters with offline stand-ins, multi-tenancy and go-live.

**Checked:** each feature's device position keeps "the clinician decides and the app records": lab reports are stored as sent and never read or flagged, questionnaires are never scored, consent isn't assessed, a missed appointment never discharges or re-prioritises anyone, and alerts work from time and status, not clinical values. Stored numbers continue Phase 2's without overlap; `AppointmentEventAction` 9 and 10 and `AttachmentScanStatus.Pending = 3` were confirmed free on `main` (they end at 8 and 2). Named dependencies are permissive (PDFsharp and MigraDoc MIT, Markdig BSD-2-Clause, Atkinson Hyperlegible OFL; QuestPDF rejected for its revenue-threshold licence).

**Finding, low, fixed in the PR:** the reminders section said `sub-processors.md` and `international-transfers.md` would be "checked"; it now says they must be updated in the same PR as the real sender, before it can send anything.

**Remaining risk:** section 7 lists what only people outside the team can do (safety officer sign-off, NHS onboarding and assurance, a penetration test, contracts); the plan's adapters run on offline stand-ins until then.
