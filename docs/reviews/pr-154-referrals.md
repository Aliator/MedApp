# Review: PR #154, referrals

**PR:** [#154](https://github.com/Aliator/MedApp/pull/154), branch `feature/referrals`, reviewed at `450412e` on 18 November 2026. A quick privacy and safety review of the controller, `ReferralChange`, logging, timeline, anonymisation, export, guards and migration; nothing was run. CI (full tier) passed: https://github.com/Aliator/MedApp/actions/runs/44312875603. No finding is high or critical.

Adds referrals (`Referrals`, `ReferralEvents`, `ReferralsController`), the inbox and referral pages, the letter as an attachment target, a timeline feed, and their export, anonymisation and retention. Triage is the next PR.

**Checked:** register, edit, withdraw and entered-in-error need `RegisterReferrals`; search, get and the count `ViewPatients`. Get records `ReferralViewed` (fails closed), search `PatientListed` per row, letters `AttachmentsViewed`; the count names no patient. Each change saves its structured history in one `SaveChangesOrConflictAsync` with a row version. The staff timeline gets no free text (practice, reason and detail are admin-only); no notification, email or log carries referral text. Restricted and anonymised records refuse register, edit and attachments; withdraw and entered-in-error stay allowed while restricted. `PatientAnonymiser` replaces every free-text column and scrubs the history; the export covers every personal-data column. `AddedReferrals` adds `Attachments.ReferralId` as nullable, so existing rows keep their data.

**Findings, low, left as to-dos:**
- `RegisterReferralHandler.cs` checks the restriction on an untracked read, so a restriction made in the gap before the save lets one referral through (as other create paths do).
- `ReferralSearch.cs:52-56` filters by patient name but the handler records no `PatientsSearched` entry with the filter names, unlike patient search.
