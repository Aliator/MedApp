# PR #197: Data requests admin only, and "Not listed here" GP practices

https://github.com/Aliator/MedApp/pull/197 (`feature/admin-data-requests-unlisted-practice`), head `766155ec`, reviewed 18 November 2026 (quick review).

All 11 `DataRequestsController` actions need `ManagePrivacy`, and every data-request handler calls `DataRequestRules.EnsureCanHandle` first (`SignOffResults` and `ViewPatients` access removed, review included); the web request route and export section follow. Patients gain an unlisted GP practice (name, address lines, postcode, phone; nullable), exclusive with `PracticeId` through validators and `CK_Patients_OnePractice` (migration `AddedUnlistedGpPractices`, columns plus the constraint only). Letters' practice merge fields and a GP recipient without a `PracticeId` resolve to the patient's own unlisted practice (refused if there's none); anonymisation clears it and scrubs non-directory recipients' copies.

Findings: none. Checked: no data-request action reachable without `ManagePrivacy`; the constraint can't fail on existing rows (new nullable columns); `Down` drops the constraint then the columns; the activity log names the field (`unlistedPractice`), never values.

Remaining risk: `DataRequestTests`, `EndpointCoverageTests` and the migration run first in the next full CI; the Data Requests list page's guard is switched to `ManagePrivacy` by the grids PR.
