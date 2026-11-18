# PR #187: Calendar at the bottom of the side nav, no Referrals badge

https://github.com/Aliator/MedApp/pull/187 (`fix/timeline-test-and-calendar-nav`), reviewed 18 November 2026 (quick review).

Owner's requests in `AppSideNav.tsx`: the `calendar` entry moves from `main` into the footer's `account` entries after `...notepadEntry` (same key, `isAt('/calendar')`, half view and row menu; the folded rail shows it in its footer group); `AwaitingTriageCount` and the nav's `awaitingTriageCountQuery` call are removed (query and endpoint kept). Also fixes the ordering race in #186's new `PatientTimeline` test (wait for the "Updating" spinner to go first).

Findings: two lows, both handled: a stale comment about the triage count (deleted); `AwaitingTriageCount` was exported, but nothing else imports it (grep, typecheck clean).

Remaining risk: none.
