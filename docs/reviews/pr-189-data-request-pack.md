# PR #189: Data-request pack

https://github.com/Aliator/MedApp/pull/189 (`feature/data-request-pack`), head `650c9ec5`, reviewed 18 November 2026 (quick review). The plan numbers it PR 36.

Subject access requests (`api/data-requests`): received date, requester, identity check, due date (one calendar month, extendable by two with a reason), Sent, Refused or Withdrawn. A clinician (`SignOffResults`) withholds items with reasons. `GET {id}/pack` (`ManagePrivacy`) builds one ZIP per download (a PDF through the letters engine, `record.json` and the files still held) from `DataRequestPackFilter.Apply` over one patient's export; 409 until the identity check and review are recorded (`DataRequestRules.WhyNoPack`). A daily job sends `DataRequestDueSoon` 7 days before the due date. Migration `AddedDataRequests` only creates tables.

Findings: one low, no change. A throw between `mediator.Send` and the return in `DataRequestsController.cs` (about lines 159 to 161) leaves the pack's stream undisposed until finalisation; the temp file is `DeleteOnClose`, so it still goes.

Checked: withheld items leave all three routes (letter PDFs, result reports, a referral's files, note versions, deletion requests, referral history, and audit, appointment, referral and access-log entries naming a withheld ID); one patient's record only; `no-store` via `AttachmentResponseHeaders`; `DataRequestPackDownloaded` recorded, failing closed, before the build; `ActivityAction` 50 and 53 and `NotificationKind` 42 free on `main`, nothing renumbered.

Remaining risk: history entries are matched by a withheld item's ID in their details, so one naming it without the ID would stay (audit details hold IDs and structured values, never the item's text). The anonymiser diff, the reminder text and `WhyNoPack`'s body weren't read; `DataRequestTests` runs first in the phase's full CI.
