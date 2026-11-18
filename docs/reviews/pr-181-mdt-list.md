# PR #181: MDT list

https://github.com/Aliator/MedApp/pull/181 (`feature/mdt-list`), head `0494ca7c`, reviewed 18 November 2026 (quick review).

Adds MDT items: a pathway listed for an MDT meeting (a calendar meeting with `IsMdt` and `SpecialtyId`) with a question, then Discussed with the outcome stored as written, Deferred or Withdrawn, each with `expectedVersion`. `MdtController` (`api/mdt`): GETs need `ViewPatients` and log `PatientListed`/`PathwayViewed`; list, defer and withdraw need `WorkPathways`; the outcome needs the new `RecordMdtOutcomes` (Clinician, Senior clinician; not in Admin's fixed set). Migration `AddedMdtItems` only adds the table and two meeting columns.

Findings: none. Checked: every action has `[RequirePermission]`; `NotificationKind` 41 and `PatientPathwayAuditAction` 38 to 41 are free on `main` (it ends at 40 and 37), nothing renumbered; the notification title is fixed and its message holds a username and the specialty name; the outcome is saved as typed, never prefilled or scored; the anonymiser replaces the question and outcome; `Up` adds only.

Remaining risk: the GET handlers' logging and `MdtAudit`'s detail contents were read through comments and `EndpointCoverageTests`, not line by line. The migration wasn't tried up and down on LocalDB, and `MdtTests` (integration) runs first in the phase's full CI.
