# Review: PR #152, sites, rooms and closed days by site

**PR:** [#152](https://github.com/Aliator/MedApp/pull/152), branch `feature/sites-rooms`, reviewed at `56234a0` on 18 November 2026. A quick review of the migration, the controllers' permissions and `ClosedDaySet`; nothing was run. CI (full tier) passed: https://github.com/Aliator/MedApp/actions/runs/44304909656. No finding is high or critical.

Adds sites and rooms (`api/sites`, `api/rooms`, the Sites and Rooms page), a fixed "Main site", and an optional site on closed days.

**Checked:** `AddedSites` (`20261118122751`) adds `ClosedDays.SiteId` as nullable, so existing closed days become "closed everywhere"; the new unique index `IX_ClosedDays_Date_SiteId` treats nulls as equal, keeping one closed-everywhere day per date; Main site is inserted before its foreign key. `ClosedDaySet` (`ClosedDaySet.cs:21,42,52`) builds `IsClosed` and `NameOn` only from closed-everywhere rows, so a site's own closures never reopen a closed date. `SitesController` reads need `UseApp` and changes `ManageSetup`; closed-day reads `UseApp`, changes and the bookings count `ManageSetup`. No stored numbers.

**Finding, low, accepted:** rolling back deletes site-specific closed days so the old one-per-date index can be rebuilt (commented in `Down`).

**Remaining risk:** a site's closed day closes nobody's calendar until working hours (PR 22) tie staff to sites.
