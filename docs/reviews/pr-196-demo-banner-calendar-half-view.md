# PR #196: Demo banner on the Read Me, Open Calendar on the day widget, half view kept by default

https://github.com/Aliator/MedApp/pull/196 (`feature/demo-banner-on-read-me`), reviewed 18 November 2026 (quick review).

Three owner requests in `MedApp.Web`: the light demo's banner renders only at the top of `ReadMePage` (Reset kept; Switch and Read Me buttons gone), with the first-visit redirect moved to `src/demo/DemoFirstVisit.tsx`, lazy behind `__LIGHT_DEMO__` in `AppLayout` (banner slots removed); `DayCalendar` swaps its Today button (disabled on today) for Open Calendar to `/calendar?date=<shown day>`, and `HomePage` drops its header button; `navSettings` makes "Nav items close the half view" on only when stored as `'true'`.

Findings: one low, fixed: the optional `staffUserId` in Open Calendar's address is now `encodeURIComponent`-encoded.

Checked: no demo code in production (`DemoBanner` imported only by the demo's Read Me); the redirect does nothing on the Read Me, uses `replace`, and runs only in the signed-in layout; stored `'true'`/`'false'` still honoured, only people who never chose see the new default.

Remaining risk: none.
