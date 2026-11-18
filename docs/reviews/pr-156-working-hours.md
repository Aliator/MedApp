# Review: PR #156, working hours per person and per site

**PR:** [#156](https://github.com/Aliator/MedApp/pull/156), branch `feature/working-hours`, reviewed at `815ede5` on 18 November 2026. A quick review of `WorkingTime`, `CalendarClashes.Find`, the changed validators and handlers, the migration and the permissions; nothing was run. CI (full tier) passed: https://github.com/Aliator/MedApp/actions/runs/44317082162. No finding is high or critical.

Adds `StaffHours` and `SiteHours`, works out each person's day from their hours cut to their site's opening hours less closed days (`WorkingTimeSet`), replaces the fixed 08:00 to 18:00 weekday in every calendar rule, and adds `ManageClinics` (Admin, Booking, Manager).

**Checked:** `CalendarClashes.Find` (`CalendarClashes.cs:37`) starts with the working-day check on hours loaded inside the calendar lock, so a closed day, a day not worked or a time outside someone's hours is refused under the lock; moved blocks are checked before and inside it; meetings check each attendee while holding the locks; walk-ins check hours and ignore closed days, as decided. People with no hours of their own get Monday to Friday, 08:00 to 18:00 (`WorkingTime.cs:86-97`), so existing calendars are unchanged. Times go through `UkTime`. `ManageClinics` is editable (not Admin-only), granted by the migration only to the fixed Booking and Manager IDs. The migration creates two tables and inserts permission rows; nothing existing changes.

**Findings, low, left as to-dos:**
- `UpdateMeetingHandler.cs:70` checks every attendee's hours on any edit, so after someone's hours shrink a title-only edit (or accepting again, `ReplyToMeetingHandler.cs:71`) is refused until they're removed. It refuses more, never less.
- A deleted account's `StaffHours` stay, keeping its site "in use" (`SiteRepository.cs:71`) and unarchivable.

**Remaining risk:** the screens weren't captured; the calendar week, calendar templates and sites screens should be looked at.
