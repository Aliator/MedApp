# PR #195: Nav without badges, attention card, folders, MDT and deprecations

https://github.com/Aliator/MedApp/pull/195 (`feature/nav-calendar-home`), head `0cc8b2b8`, reviewed 18 November 2026 (quick review).

Owner's batch in `MedApp.Web`: the side nav's count badges and the category "current page" dot go (the bell keeps its badge; the count endpoints stay); a home-page "Needs your attention" card (`NeedsAttention.tsx`) shows counts only, each query gated on `ViewPatients` plus the acting permission (`SignOffResults`, `ApproveLetters`/`SendLetters`, `TriageReferrals`); folders renamed Account Management and Calendar; Practices moves into Calendar with `/practices` now needing `ManageSetup` (API unchanged, so the patient form's picker still works); Data Requests moves under Oversight (`ManagePrivacy`); the MDT nav item becomes an MDT button on the Calendar page plus "Open MDT List" on an MDT meeting; Clinics and Sites and Rooms are hidden but kept and work by address, marked deprecated in code and docs; two to-dos.

Findings: one low, fixed: `PracticesPage.tsx`'s comment still said the page was for anyone who can view patients.

Checked: no patient names or free text on the card and no call that could 403; no route guard weakened (practices made stricter); the only `/practices` link is the gated nav item; only a text line changes in the demo's tour.

Remaining risk: the nav has two "Calendar" entries (the admin folder the owner named, and the Calendar page link); the Data Requests route guard is left to the data-request permissions PR.
