# PR #206: main's full suite, third round

- **PR:** https://github.com/Aliator/MedApp/pull/206, branch `fix/main-full-suite-3`, from `main` at `23bd8faf`; 18 November 2026.
- **What it does:** fixes the full-suite failures on `main` after #200 (grid tabs) and #201 to #205 (runs 44402545110, 44404657901, 44404704959, 44407139262). Issue #203.

## Failures, causes and fixes

- `PatientTests.AddPatient_ThroughTheForm_IsFoundWithTheFilterBar`, `UserTests.AddUser_NewUserSignsInWithTheOneTimePasswordAndMustChooseTheirOwn`, `UserTests.DeleteUser_ConfirmedWithYourPassword_RemovesTheAccount` (E2E): since #200 `ListFilterBar` renders a `TabList` of saved searches and shows `PowerSearch` only after "List options …" → Search, so `GetByRole(Combobox, "Search patients|users")` timed out. Fix: `E2ETest.OpenListSearchAsync(label)` (returns the combobox; opens the menu and picks Search when it isn't visible), used by `FilterByLastNameAsync` and `SearchUsersAsync`.
- `routeGuards.test.tsx` "opens the Lab on To send for those who send specimens, asking for that view" (web): #200 moved the Lab's worklist to `POST api/specimens/search` (`SpecimenWorklistRequest` body, number kept out of URLs); the test mocked `GET` and read `View` from the query, so the unmocked POST left it waiting until the 5 s timeout. The code matches #200's decision (To send for those without `ProcessSpecimens`); the test now mocks the POST and asserts a body with `view: 'ToSend'`.
- `EmailCodeTests.SignIn_EmailsACode_ThatSignsInOnce` (integration, flaky, run 44404657901): `LoginHandler` creates the challenge at t0 with `EmailLifetime`; `EmailCodeService.SendAsync` reads now again at t1 after several DB calls and caps the code at the challenge's expiry, so the email's lifetime is `10 min - (t1 - t0)`, floored to 9 when the gap exceeded the old 60 ms (0.001 min) tolerance. Fix: `EmailTemplates.TwoFactorCode` floors `lifetime + 5 s`; unit cases 9.95 → 10 and 9.5 → 9. The code's real expiry is unchanged; the email can overstate it by under 5 s.

## Findings

None beyond the above.

## Remaining risk

- `OpenListSearchAsync` assumes the page opens on its tabs or the search; a page that shows the search only after loading could race the visibility check, in which case the menu reads Tabs and the helper times out.

Independent quick review (18 November 2026): no critical or high findings. One low, accepted: `EmailTemplates.TwoFactorCode` floors `lifetime + 5 s`, so the email can state up to 5 seconds more than the code has (9:55 reads "10 minutes"); the code itself still expires on time. No test weakened: `routeGuards.test.tsx` still checks `view: 'ToSend'` (from the POST body), and the E2E helper only reveals the search bar.
