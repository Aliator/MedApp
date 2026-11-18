# PR #188: Service alerts

https://github.com/Aliator/MedApp/pull/188 (`feature/service-alerts`), head `fe93824d`, reviewed 18 November 2026 (quick review).

Six time-based `MonitoringEvent`s (8 to 13: referral not triaged, result not signed off, visit not booked, specimen overdue, wait breached, pause review passed) on monitoring rules with new `ThresholdDays` (0 to 90) and `SpecialtyId`. `ServiceAlertsJob` (an `IScheduledJob`, every 15 minutes, under the runner's lease) sends one notification per rule per run (`NotificationKind` 32 to 37) and records each reported item in `MonitoringAlertsSent` (key RuleId, SubjectId) in the same save, so it's idempotent. Migration `AddedServiceAlerts` adds two nullable columns and the table; `Down` deletes the service-alert rules first.

Findings: one low, no change. A rule's first run reports every item already qualifying, possibly a few thousand IDs in one insert; fine at this scale.

Checked: fixed titles, messages holding only counts, days, a specialty name and (results) a username, logs with counts only; alerts built from statuses and dates, never a clinical value; queries filtered in SQL returning Guids; stored numbers in the gaps `main` reserved, nothing renumbered; `Up` only adds.

Remaining risk: the rule validator wasn't read; `MonitoringTests`' two new integration tests run first in the phase's full CI; `MonitoringAlertsSent` rows aren't purged yet (to-do).
