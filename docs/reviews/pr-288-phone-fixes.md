# PR #288 quick review: phone fixes

- PR: https://github.com/Aliator/MedApp/pull/288 (branch `claude/eloquent-feynman-wysbc1` into `main`)
- Commit reviewed: dbffad4aec9d4d0d0b31897eeaf8db45491ac67f
- Date: 24 November 2026

## What it does
UI-only narrow-layout changes in MedApp.Web: touch-screen side nav without row menus or half views, the bell in the phone top bar, the Sign Out confirmation lifted to `AppLayout` (via `AskToSignOutContext`, with the nav falling back to its own `SignOutDialog`), new `ActionsMenu`/`ActionsList`, and RowCards, TableActions, FormSection, FormCard, TwoColumns, PanelCard, MdtItemList and the MDT, retention, hospitals and setup pages adapted. It also adds a mobile audit doc.

## Findings
None. I checked these:
- Sign Out still goes through `ConfirmDialog` and then `useLogout().mutate()`, at app level or in the nav. The session still ends the same way.
- MDT actions keep their `canRecordOutcome`, `canChoosePresenter`, `canDefer` and `canWithdraw` gates, and each one opens the same `setAsking` dialog.
- The retention menu's Keep and Anonymise still open the `setKeeping` and `setAnonymising` confirmations. Neither is called directly.
- The diff has no backend, API or schema changes.

## Remaining risk
- The gates were checked in the diff only. I didn't run the build or the tests.
- Dialogs inside the phone drawer weren't checked in a browser.

Author's note: the Sign Out confirmation was checked in Chromium at 390x664 and 360x740 (touch): the dialog shows (358x178 / 328x178) and confirming reaches /login.
