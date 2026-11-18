# PR #198: Home page day widget without day buttons or "Nothing booked"

https://github.com/Aliator/MedApp/pull/198 (`feature/home-day-widget`), reviewed 18 November 2026 (quick review).

`DayCalendar` (only on the home page) drops its Previous day and Next day buttons and the "Nothing booked" line for an empty working day; it always shows today, with Open Calendar beside the heading. A day off or a closed day with nothing on it still shows `notWorkingWords(day)` (`!hasAppointments && !day.isWorkingDay`).

Findings: none. Checked: nothing still uses the removed `setDate`, `IconButton` or `nextWeekday`; the not-working-day and closed-day messages are kept.

Remaining risk: none.
