# Review: PR #115, deleting procedure notes and asking an admin to

**PR:** [#115](https://github.com/Aliator/MedApp/pull/115), branch `feature/note-deletion`, reviewed at `d21c056` against `main` at `894c1fe` (the merge of #110, where the branch starts). `main` has since moved to `058b1c1` (#112: CI, `AGENTS.md` and docs only); the branch still merges with it cleanly (`git merge-tree`).

**Reviewed:** 18 November 2026, independently of the PR description's conclusions. Each claim below was checked against the code, a test run, or the PR's CI run.

**Two findings are high.** All twelve are fixed or documented on the branch (`e829813f`, `d185315c`, `40aeac9b`); see each finding's "What happened to it". The required `integration-tests` check fails at `d21c056`, for two separate reasons: the patient's export never includes the requests to delete notes (a real bug), and two migration tests from #110 now read columns that don't exist yet at their step. Four findings are medium and six are low.

## Summary

- **What it does:** admins can delete a note on a procedure, with a reason; staff can ask an admin to. Deleting is soft: the note disappears from the procedure page for everyone, but the database keeps it, with who deleted it, when and why. Every admin is told about a request, decides it in a dialog that shows the patient and the note, and the person who asked is told the outcome.
- **Verdict:** the server side is carefully built: the authorisation is right, the note and request are guarded against two saves at once, staff never see the free text, and the notifications follow their rules. But one copy step leaves the requests out of the patient's export, and CI is red because of it and a broken older test. The design questions are about what happens around a deletion: a note edited after the admin looked at it, an author who never hears their note went, a wrong-patient note that stays in the wrong patient's export, and old API copies during a rollout.

## What changed, and why

112 files, 6,765 lines added and 111 removed, in four commits (`git diff --stat origin/main...HEAD`), 1,996 of them the migration's generated designer file.

### Deleting a note (admins)

- **What it is:** `POST .../patient-procedures/{id}/notes/{noteId}/delete` with `{reason}` (admins only, 204). The note gets `DeletedAtUtc`, `DeletedByUserId` and `DeletionReason` (500 characters, required), and every response leaves it out.
- **An analogy:** taking a page out of a patient's folder and putting it in a locked drawer with a slip saying who took it out and why. Nobody on the ward sees it any more, but it isn't shredded.
- **Why soft:** a clinical record must account for everything that was in it. The note's row and every version (#110's version table) stay, the audit log gets a `NoteDeleted` entry (15) with the note's ID, version and reason but never its text, admins see it on the patient timeline with its text, and the patient's export lists it as deleted.
- **When:** in any status of the procedure, Cancelled and Archived included, because it's a correction (a note on the wrong patient). Never on a restricted or anonymised record (the usual `GetForUpdateOrThrowAsync` guard, a 409).
- **A deleted note can't be edited or deleted again** (409, `ProcedureNotes.DeletedMessage`).

### Asking for a deletion (staff)

- **What it is:** `POST .../notes/{noteId}/deletion-requests` with `{reason}` (staff, 201). It writes a `ProcedureNoteDeletionRequest` row: the note, procedure and patient, who asked (ID and username), why, when, and a status (`Pending = 0`, `Approved = 1`, `Declined = 2`, `Closed = 3`).
- **An analogy:** a "please remove this page" slip handed to the supervisors, who each find a copy in their in-tray.
- **No foreign keys,** like the audit log: a request outlives a deleted procedure, and a patient's export and anonymisation find it by the patient's ID.
- **One waiting request per note,** enforced three ways:
  - the handler checks the note's `PendingDeletionRequestId` first;
  - setting that pointer changes the note's **row version** (a number SQL Server bumps on every save; a save carrying an older number is refused), so two requests at the same instant can't both land;
  - a **filtered unique index** (`IX_ProcedureNoteDeletionRequests_NoteId_Pending`: at most one row per note where the status is Pending) is the database's last word.
- **Notifications:** every admin but the requester gets kind 18, "Note deletion requested", with a message of the requester's username only.

### Deciding a request (admins)

- **What it is:** `GET api/procedures/note-deletion-requests/{id}` (admins) shows the request with the note's text, the patient as the patient banner shows them, the procedure's title, who asked, when and why; `POST .../{id}/decision` with `{deleteNote, reason?}` deletes the note (with the admin's reason, or the requester's) or keeps it.
- **The read is in the activity log** as `ProcedureViewed` (or `PatientViewed` once the procedure is deleted), and `EndpointCoverageTests` classifies the new GET.
- **The requester is told** (kind 19 "Note deleted" or 20 "Note kept"), unless they decided it themselves.
- **Two admins at once:** the request has its own row version, so the second decision is a 409 that names who decided and how.
- **Side effects elsewhere:** deleting a note directly approves a waiting request; deleting a procedure closes its waiting requests; anonymising replaces every reason and closes waiting requests.

### The web app

- **The procedure page:** admins see Delete on every note, staff Request Deletion; each opens a confirmation showing the note and asking for a reason. A note with a waiting request shows a "Deletion requested" token, and admins get Review Request beside it.
- **The admin's dialog** (`NoteDeletionRequestDialog`) opens from the notification (`/procedures/<id>?deletionRequest=<request id>`) or from Review Request.
- **Elsewhere:** the timeline and the audit log describe `NoteDeleted`; the export page shows deleted notes and a Note Deletion Requests section; `schema.d.ts` was edited by hand.

### The migration `AddedNoteDeletion`

- **Up:** four nullable columns on `PatientProcedureNotes`, one on `Notifications`, and the new table with three indexes. Adding nullable columns is a metadata-only change in SQL Server, so it's quick on a large table, and old API copies don't notice.
- **Down:** drops all of it, so deleted notes show again and the requests are lost; `docs/todo.md` says so.

### Documents

- **Clinical safety:** a new draft hazard **H-38** ("A procedure's note is deleted wrongly"); H-37's "entered in error" control is now admin deletion.
- **Governance:** the data inventory, DPIA, retention schedule, rights procedure, staff privacy notice and ROPA are updated.
- **Operations:** the chart's README lists what old pods do during a rollout; `docs/todo.md` lists what's left (restore, a list of waiting requests, withdrawing a request, purging).

## Design choices

- **Soft delete, hidden from everyone but admins:**
  - **Instead:** a hard delete, or "entered in error" (still shown, struck through).
  - **Chosen because:** nothing in the clinical record is lost, and a note on the wrong patient stops being shown to the people treating them.
  - **Worse at:** staff see no trace of it on the procedure page, so whoever relied on it doesn't know it went (finding 4), and its text stays on the wrong patient's record for the record's whole life (finding 5).
- **Only admins delete; staff ask:**
  - **Instead:** let a note's author delete their own note.
  - **Chosen because:** a second person checks every deletion, which is the main control for H-38.
  - **Worse at:** a wrong note stays visible until an admin gets to the request, and the request is only reachable through a notification (finding 7).
- **A pointer on the note to its waiting request (`PendingDeletionRequestId`):**
  - **Instead:** rely on the filtered unique index alone, or look the request up each time.
  - **Chosen because:** the note's existing row version then also stops two requests racing, and the procedure page can show "Deletion requested" without another query.
  - **Worse at:** the pointer has to be kept in step in four places (request, decide, delete, anonymise), and raising a request bumps the note's row version, so an author saving at the same instant gets a 409 they didn't cause.
- **Requests in their own table, with no foreign keys:**
  - **Instead:** audit entries only, or columns on the note.
  - **Chosen because:** a request has its own life (pending, decided by someone), and must outlive a deleted procedure for the export.
  - **Worse at:** one more table every export, anonymisation and copy of the record must remember, which is exactly where finding 1 slipped.
- **`POST .../delete` rather than `DELETE`:**
  - **Chosen because:** the reason travels in a body, and HTTP `DELETE` with a body is poorly supported by proxies and clients.
  - **Worse at:** less obvious in the API's shape; harmless.
- **Every admin notified, no queue:**
  - **Instead:** an admin page listing waiting requests.
  - **Chosen because:** notifications already exist; the list is a planned further control.
  - **Worse at:** a request can drop out of sight (finding 7).
- **Free-text reasons, admins only:**
  - **Instead:** a fixed choice (wrong patient, duplicate, error, other) with an optional note, as anonymisation does.
  - **Chosen because:** it's quick to write and says exactly what was wrong.
  - **Worse at:** a reason can name another patient, and nothing can tell a wrong-patient deletion from any other (finding 5).

## How it was checked

- **Read:** the PR description; the whole server diff (domain, contracts, EF configuration, the migration, both repositories, the handlers, validators, mappers, controllers, timeline, export and anonymiser); the unit and integration tests; the web components, route, API hooks, bell, timeline and audit wording, and export page; the governance documents, hazard H-38 and H-37, the chart's README and `docs/todo.md`.
- **Unit tests** (Release, filtered to note deletion, notes, the timeline canary, the anonymiser, the export mapper, the monitoring and restriction conventions, deleting a procedure, editing a note and notifications): 285 passed.
- **Vitest:** the eight changed web test files: 177 passed.
- **`git merge-tree`** with today's `main`: no conflicts.
- **CI at `d21c056`** (run 44220535286): `unit-tests`, `web`, `e2e`, `image`, `helm`, `dependency-review`, CodeQL and Trivy pass. **`integration-tests` fails, with three tests** (findings 1 and 2). Coverage: every new handler, validator and mapper is at 96% lines or more; no web file is under 80%.
- **Not run:** Docker, the integration or E2E suites, the AppHost, a browser. The CI log was enough to place each failure.

## Findings

### 1. The patient's export never includes the requests to delete notes

- **Severity:** high. **Confidence:** high (CI reproduces it, and the code shows why).
- **What could go wrong:**
  - A patient asks for a copy of their record (Article 15). The admin exports it. Every request staff made to delete a note on their procedures, with its reason and outcome, is missing, though the governance pack, the export page's section and the staff privacy notice all say it's there.
  - The export page shows "Note Deletion Requests (0)", so nobody notices.
- **Why:** `ExportPatientHandler` builds a second `PatientRecord` (to add the export's own activity entry) by copying each list by hand, and doesn't copy the new one. `NoteDeletionRequests` defaults to an empty list, so the compiler can't catch it.
  - **An analogy:** photocopying a file to add a cover note, and forgetting to copy one of the sections.
- **Evidence:**
  - `MedApp.Application/Patients/Queries/ExportPatient/ExportPatientHandler.cs:31-41` (the copy, without `NoteDeletionRequests`);
  - `MedApp.Application/Patients/Common/PatientRecord.cs:34` (`= []`, not `required`, unlike `Activity` and the others);
  - CI: `NoteDeletionTests.RequestThenDelete_EndToEnd` fails at `MedApp.IntegrationTests/Procedures/NoteDeletionTests.cs:222` ("Expected collection to contain a single item, but the collection is empty"), the export assertion;
  - the claims: `docs/governance/data-subject-rights.md` (Of access row), `dpia.md` (the export row under risk 6), `privacy-notice-staff-template.md:28`.
- **Why the tests missed it:** `PatientExportMapperTests` tests the mapper with a record built by the test; `ExportPatientHandlerTests` wasn't extended; and the privacy canary only checks that text is *absent* after anonymising, which a missing list passes.
- **Fix:**
  - Add `NoteDeletionRequests = record.NoteDeletionRequests` to the copy.
  - Make it impossible to forget next time: mark `NoteVersions`, `NoteDeletionRequests` and `Attachments` `required` in `PatientRecord` (as the other lists are), or make `PatientRecord` a `sealed record` and write `record with { Activity = [.. record.Activity, entry] }`, which copies everything.
  - Add an `ExportPatientHandlerTests` case that a request in the record comes out in the response.
- **Status:** open, for the author to fix before merging.
- **What happened to it:** fixed in `e829813f`. `ExportPatientHandler` now copies the record with a `with` expression (`PatientRecord` is a `sealed record`), so every list comes along, and `NoteVersions`, `NoteDeletionRequests` and `Attachments` are `required`, so code that builds a record can't leave one out (every builder now sets them). New test: `ExportPatientHandlerTests.Handle_EveryListInTheRecord_IsInTheExport`, which fails without the fix (the request list came out empty).

### 2. Two of #110's migration tests now fail, because they read notes through today's model

- **Severity:** high (the required `integration-tests` check fails, so the PR can't merge). **Confidence:** high (CI log).
- **What could go wrong:** `ProcedureNotesMigrationTests` migrates a scratch database only as far as `AddedProcedureNotes`, then reads `PatientProcedureNotes` as whole entities. EF asks for every column today's model has, including this PR's `DeletedAtUtc`, `DeletedByUserId`, `DeletionReason` and `PendingDeletionRequestId`, which don't exist at that step: "Invalid column name 'DeletedAtUtc'".
  - **An analogy:** checking an old version of a form against today's checklist, and failing it for boxes that weren't on the form yet.
- **Evidence:** `MedApp.IntegrationTests/Migrations/ProcedureNotesMigrationTests.cs:65` and `:113` (`.ToListAsync()` and `.ToDictionaryAsync(...)` on the entity); CI: `Up_EachProcedureWithNotes_GetsThemAsItsFirstNote` and `Up_NotesEditedByOthers_HaveNoAuthor_AndAreMarkedEditedAtTheLastEdit` fail with SQL error 207. The `Down` test passes because `CountAsync` selects no columns.
- **Fix:**
  - Read only the columns that existed at that migration, by projecting: `.Select(n => new { n.Id, n.PatientProcedureId, n.AuthorUserId, n.Text, n.CreatedAtUtc, n.LastEditedAtUtc, n.Version })` before `ToListAsync`/`ToDictionaryAsync` (the assertions already compare those properties), or with raw SQL.
  - Add a line to `AGENTS.md`'s Migrations section: a migration test reads with a projection or SQL, never whole entities, because a later migration can add columns to the same table.
- **Status:** open.
- **What happened to it:** fixed in `e829813f`. `ProcedureNotesMigrationTests` reads notes and versions through projections of the columns that exist at `AddedProcedureNotes` (`NoteAtMigration`, `VersionAtMigration`) and the old column with `Select(p => p.LegacyNotes)`; the other migration tests already read at the latest migration. `AGENTS.md`'s Migrations section has the rule (`d185315c`). Not run locally (Docker); the two failing CI tests read exactly these queries.

### 3. Deleting, or deciding, doesn't say which version the admin saw: a note edited meanwhile is deleted unseen

- **Severity:** medium. **Confidence:** high (from the code; nothing in the request carries a version).
- **What could go wrong:**
  - A nurse asks for a note to be deleted: "result recorded on the wrong procedure". The note's author sees "Deletion requested" and fixes the note instead, editing it into the right result (version 2).
  - The admin opened the request earlier and is still reading version 1's text. They click **Delete note**. The server deletes version 2, which the admin never saw, and nobody but the admin is told (finding 4).
  - The same happens with the direct Delete: its confirmation shows the text cached on the page.
  - The request dialog shows only when the note was written, not that it was edited after the request (`lastEditedAtUtc` is in the response, unused), so even a fresh load gives the admin no hint that the requester's reason was about other words.
- **Why it matters:** H-38's main protective control is "the request's dialog shows the note's text as it reads now". The row version only catches two saves within milliseconds of each other, not the minutes or hours a person spends reading.
  - **An analogy:** signing off a document someone has quietly swapped since you read it.
- **Evidence:**
  - `MedApp.Contracts/Procedures/PatientProcedures/Requests/DeleteProcedureNoteRequest.cs:4-8` and `MedApp.Contracts/Procedures/NoteDeletionRequests/Requests/DecideNoteDeletionRequestRequest.cs:4-14` (a reason, no version);
  - `DeleteProcedureNoteHandler.cs:24-43` and `DecideNoteDeletionRequestHandler.cs:33-48` (no version check);
  - `EditProcedureNoteHandler.cs` (an edit is allowed while a request waits);
  - `MedApp.Web/src/components/procedures/NoteDeletionRequestDialog.tsx:169` (created time only), `ProcedureNotes.tsx:371` (the cached text);
  - `docs/clinical-safety/hazard-log.md:1461`.
- **Fix:**
  - Add a required `expectedVersion` to both requests, as an edit already has; refuse with a 409 when `note.Version` differs ("This note was edited after you opened it. Read it again before deleting it."). These are new endpoints, so no old client sends the body without it.
  - Add `Version` to `NoteDeletionRequestNoteResponse`, and have both dialogs send the version they showed.
  - Show "Edited …" in the request dialog, and a warning when `lastEditedAtUtc` is after `requestedAtUtc`.
  - Unit tests for both handlers; add the cause to H-38.
- **Status:** open.
- **What happened to it:** fixed in `e829813f` and `40aeac9b`. Deleting and deciding (to delete) carry the note's `expectedVersion` (required; a 400 without it), and a note edited since is a 409, `ProcedureNotes.EditedSinceOpenedMessage` ("This note was edited after you opened it. Read it again before deleting it."). The request's note now has `version`; both dialogs send the version they showed. The request dialog shows "Edited …" and warns when the edit came after the request. Keeping a note needs no version. Tests: both handlers' stale-version cases, the validators, the integration test `DeleteAndDecide_NoteEditedSinceTheAdminOpenedIt_Are409AndKeepIt`, and the dialogs' Vitest tests. H-38 gains the cause and the control (`d185315c`).

### 4. A note's author is never told it was deleted, so a wrong deletion has only one witness

- **Severity:** medium. **Confidence:** medium (a design judgement; the code is plain).
- **What could go wrong:**
  - An admin deletes a note directly, as a duplicate, but it was the only place a nurse recorded an allergy mentioned on the phone. Nobody else knows: there's no requester to tell, the author isn't told, and staff see no trace on the procedure page. Only the admins' timeline shows it.
  - A note really was on the wrong patient. Deleting it fixes the wrong record, but the right patient's record still lacks it (H-37's effect: "the right one's record misses it"), and the one person who could add it there, its author, doesn't know it's gone.
- **Why:** notifications go only to admins (on a request) and to the requester (on a decision). H-38 counts "the requester is told the outcome" as its detective control, which doesn't cover direct deletion at all.
- **Evidence:** `MedApp.Application/Procedures/NoteDeletionRequests/Common/NoteDeletionNotifications.cs:49-73` (only `RequestedByUserId`); `DeleteProcedureNoteHandler.cs:46-47` (tells someone only when a request was waiting); `docs/clinical-safety/hazard-log.md:1463` and `:1419`.
- **Fix:**
  - Tell the author: a new kind (`NoteDeletedForAuthor = 21` or similar, inside `IsNoteDeletion`'s range and `HasFixedTitle`), a fixed title ("Your note was deleted"), a message of usernames only ("admin deleted a note you wrote on a procedure."), linking to the procedure. Not when the author deleted it themselves, has no account ("Unknown"), or is also the requester (one notification is enough).
  - Add it to H-38's detective controls, and to H-37's corrective one: the author can add the note to the right patient.
  - Update the data inventory's notification row and `AGENTS.md`'s kinds.
- **Status:** open.
- **What happened to it:** fixed in `e829813f`. A new kind, `AuthorsNoteDeleted = 21` (inside `IsNoteDeletion` and `HasFixedTitle`), "Your note was deleted", "admin deleted a note you wrote on a procedure.", linking to the procedure: sent on a direct delete and on an approved request, unless the author deleted it themselves, asked for it (they get kind 19), or isn't known. Tests: `NoteDeletionNotificationsTests` (each case and the free-text canary), both handlers, and the integration test. H-38's detective controls, `AGENTS.md`'s kinds and the data inventory say so (`d185315c`); the bell has its icon (`40aeac9b`).

### 5. A note deleted because it was on the wrong patient stays in that patient's export, and the right patient's rights never reach it

- **Severity:** medium. **Confidence:** medium (the mechanism is certain; how often it happens depends on use).
- **What could go wrong:**
  - A note about patient B is written on patient A's procedure, and an admin deletes it ("wrong patient"). It's kept on A's record.
  - **A's export** contains B's clinical text, labelled Deleted with the reason, which may name B too. Releasing it is a disclosure unless the person checking the export spots it.
  - **B's export** never contains it, and **anonymising B** never touches it: both find data by `PatientId`, and the note is filed under A.
- **Why it matters:** the PR's main use case is the wrong-patient note. The DPIA (risk 4) accepts that the text stays on the wrong record, but not what that means for both patients' rights. The rights procedure's third-party rule is general, and nothing in the data marks which deletions were "wrong patient".
  - **An analogy:** a letter filed in the wrong person's folder, moved to that folder's back pocket. It's still in the wrong folder when someone asks for a copy of it, and the right person's folder still doesn't have it.
- **Evidence:** `MedApp.Application/Patients/Common/PatientExportMapper.cs:73-80` (text and reason of deleted notes); `MedApp.Web/src/pages/patients/PatientExportPage.tsx:317-321`; `MedApp.Infrastructure/Data/Repositories/PatientRecordRepository.cs:22-49` (everything by `PatientId`); `docs/governance/dpia.md:211`; `docs/governance/data-subject-rights.md:52`.
- **Fix:**
  - Make the deletion reason a fixed choice (wrong patient, duplicate, written in error, other) with an optional note, as anonymisation's reason is. Then the export page can flag every "wrong patient" note for the reviewing clinician to withhold, or leave its text out by default and list only that one existed.
  - Say in the rights procedure and DPIA risk 4 that such a note is the other patient's data, and that the right patient's requests won't find it.
  - Later: a "move to the right patient" action (an admin, both banners shown), which fixes both records at once.
- **Status:** open.
- **What happened to it:** fixed in `e829813f`, `d185315c` and `40aeac9b`. The reason is a fixed choice, `NoteDeletionReason` (`WrongPatient = 0`, `WrittenInError = 1`, `Duplicate = 2`, `Other = 3`, stored as its number), with an optional `Detail` (500 characters, required for Other), on the note (`DeletionReason`, `DeletionDetail`) and the request (`Reason`, `Detail`). The export flags a note deleted as wrong patient (`IsAboutAnotherPatient`), and the export page shows a warning on it for the reviewing clinician. Anonymising replaces the details (the audit's `detail` key is free text now). `data-subject-rights.md` and DPIA risk 4 say it's the other patient's data, that the right patient's requests won't find it, and that the author is told so they can add it there; a "move to the right patient" action is in `docs/todo.md`. AddedNoteDeletion was regenerated rather than adding a second migration, as it's applied nowhere but test databases (tested up and down on LocalDB).

### 6. During a rollout, or after a rollback, anonymising on an old API copy leaves the new reasons in place for good

- **Severity:** medium. **Confidence:** high (the mechanism); the window is narrow.
- **What could go wrong:**
  - During a rollout (or for days after a rollback), an admin anonymises a patient on a pod running `main`'s code. That code doesn't know `DeletionReason` or the requests table, so it leaves every deletion reason and every request reason as they were, and leaves waiting requests open.
  - Anonymisation can't be run again ("already anonymised", 409), so nothing ever removes them. The free text that can name a patient stays on a record that says it's anonymised.
- **Why it matters:** the chart's README says of this migration that "nothing old pods use changes", and asks admins only to hold off deleting notes. True of the schema, but not of what anonymisation promises.
- **Evidence:** `MedApp.Application/Patients/Common/PatientAnonymiser.cs:82-99` (only new code replaces them); `MedApp.Application/Patients/Commands/AnonymisePatient/AnonymisePatientHandler.cs:35-36` (refused once anonymised); `deploy/helm/medapp/README.md:108-110`.
- **Fix:**
  - Say it in the README's list: hold off anonymising as well as deleting notes during a rollout, and after a rollback.
  - Make it repairable: a migration in the next release (or an idempotent admin command) that sets both reasons to `PatientAnonymiser.RemovedText` and closes waiting requests for every patient whose `AnonymisedAtUtc` is set. It does nothing when there's nothing to fix.
- **Status:** open.
- **What happened to it:** documented in `d185315c`. The chart's README now says to hold off anonymising (as well as deleting notes) during a rollout and after a rollback, and why; `docs/todo.md` has the idempotent clean-up for the next release (replace both details and close waiting requests for every anonymised patient).

### 7. A request is reachable only through a notification, which can scroll out of sight

- **Severity:** low (documented as planned). **Confidence:** high.
- **What could go wrong:** the bell shows each person's newest 20 notifications. An admin with monitoring rules can get 20 more within a shift, and the request scrolls off. An admin who joins later never gets it at all. The wrong-patient note stays on screen for every member of staff until someone happens to open that procedure and see "Deletion requested".
- **Evidence:** `MedApp.Application/Notifications/Queries/GetMyNotifications/GetMyNotificationsQuery.cs:9` (`DefaultTake = 20`); `docs/todo.md:68`; H-38's further controls.
- **Fix:** build the list of waiting requests before first live use (even a count on the Administration menu), and until then don't count it in H-38's estimates.
- **Status:** documented as a to-do and a further control.
- **What happened to it:** documented, as before: the list of waiting requests stays a to-do and a further control of H-38. Not built in this PR.

### 8. `canDelete` and `canRequestDeletion` don't say what the signed-in user can do

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** a member of staff's response says `canDelete: true` for every note, and an admin's says `canRequestDeletion: true`. The web app works around it by checking the role itself. Any other client, or a later page that trusts the flag as `AttachmentResponse.CanDelete` can be trusted ("Whether the signed-in user may delete it"), offers staff a Delete that ends in a 403.
- **Evidence:** `MedApp.Application/Procedures/Common/ProcedureNoteMapper.cs:32-33`; `MedApp.Contracts/Procedures/PatientProcedures/Responses/PatientProcedureNoteResponse.cs:28-38`; `MedApp.Contracts/Attachments/Responses/AttachmentResponse.cs:48`; `MedApp.Web/src/components/procedures/ProcedureNotes.tsx:239`.
- **Fix:** pass whether the viewer is an admin into the mapper (as the timeline does), so `canDelete` is false for staff, and let the web app use the flags alone.
- **Status:** open.
- **What happened to it:** fixed in `e829813f` and `40aeac9b`. `CanDelete` is true only for an admin and `CanRequestDeletion` only for anyone else, both while the record can change, like attachments' flags. `ProcedureActor.IsAdmin` (from the session's role) and `GetPatientProcedureByIdQuery.ViewerIsAdmin` carry the role into every procedure response. The web app now uses the flags alone. Tests: `ProcedureNoteMapperTests` (staff, admin, closed procedure), the controller tests, and the integration test `Flags_SayWhatTheSignedInUserCanDo`.

### 9. The direct Delete doesn't show the patient, and the request dialog opens over any procedure's page

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **Delete:** the confirmation shows the note but not the patient, and as a modal it covers the page's banner. With a half view open (two procedure pages side by side, since #110), the admin can't see in the dialog which patient's note they're deleting. H-38 says the direct delete is "under the procedure page's patient banner".
  - **The link:** `/procedures/A?deletionRequest=<a request about procedure B>` opens B's request over A's page. Links come from the server's notifications, which match, so this needs a mistyped or edited link; the dialog does show B's banner.
- **Evidence:** `MedApp.Web/src/components/procedures/ProcedureNotes.tsx:338-385`; `MedApp.Web/src/pages/procedures/ProcedurePage.tsx:106`; `docs/clinical-safety/hazard-log.md:1461`.
- **Fix:** show the compact `PatientBanner` in the Delete confirmation, as `AddToProcedureDialog` does; open the request dialog only when `request.procedureId` is the page's, and otherwise go to the right page.
- **Status:** open.
- **What happened to it:** fixed in `40aeac9b`. The Delete confirmation shows the patient as `PatientBanner` does, with the note and the reason. `?deletionRequest=` opens the dialog only when the request belongs to that page's procedure; otherwise it goes to the right procedure's page (replacing the history entry). Vitest tests for both.

### 10. The decide handler isn't held to the procedure-command conventions, and its delete path's token isn't checked

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** `DecideNoteDeletionRequestHandler` saves a `ProcedureChange`, so it should tell the monitor after saving, with the request's token. It does today, but:
  - `ProcedureCommandsMonitoringTests` finds handlers only in `MedApp.Application.Procedures.PatientProcedures.Commands`, so it skips this one (in `Procedures.NoteDeletionRequests.Commands`);
  - its own `Handle_PassesCancellationToken` runs only the Keep path, so the delete path's token to `SaveAsync`, the monitor and the notification isn't checked.
  - A later edit that drops either would pass every test.
- **Evidence:** `MedApp.UnitTests/Application/Monitoring/Common/ProcedureCommandsMonitoringTests.cs:166-171`; `MedApp.UnitTests/Application/Procedures/NoteDeletionRequests/Commands/DecideNoteDeletionRequest/DecideNoteDeletionRequestHandlerTests.cs:206-216`.
- **Fix:** widen the convention to `MedApp.Application.Procedures` handlers whose request is under `.Commands.`, add a Decide case to its "after the save, with the token" cases, and a token test for the delete path.
- **Status:** open.
- **What happened to it:** fixed in `e829813f`. `ProcedureCommandsMonitoringTests` now finds every command handler in `MedApp.Application.Procedures` that takes the procedure repository (less `RequestNoteDeletionHandler`, named as saving no change), and runs `DecideNoteDeletionRequestHandler`'s delete in its after-the-save-with-the-token cases. `DecideNoteDeletionRequestHandlerTests.Handle_DeleteNote_PassesCancellationToken` checks the save, the monitor, the notifications and the username lookup.

### 11. The old notes column keeps the text of a deleted note, and the plan for dropping it could bring that text back

- **Severity:** low. **Confidence:** medium (it depends on how the to-do is built).
- **What could go wrong:** #110's migration copied each procedure's old `Notes` column into its first note and left the column in place. Delete that first note (wrong patient), and its text is still in `PatientProcedures.Notes`. The to-do for dropping the column first copies "any old-column text that differs from the first note" into a new note by "Unknown". If "first note" means the first one still shown, the deleted text comes back, visible to all staff.
- **Evidence:** `MedApp.Application/Procedures/Common/ProcedureNoteMapper.cs:57-65` (the old column is read only when there are no notes, so nothing shows it today); `ProcedureChange.DeleteNote` (doesn't touch `LegacyNotes`); `docs/todo.md:74-76`.
- **Fix:** say in the to-do that the comparison includes deleted notes (so a procedure whose matching note was deleted is skipped), and test that case.
- **Status:** open.
- **What happened to it:** fixed in `e829813f`. Deleting a procedure's oldest note (the one #110's migration copied the old column into) clears `PatientProcedures.Notes` too, and the audit entry says so (`oldNotesFieldCleared`); a later note leaves it. Tests in `ProcedureNotesTests`. The "drop the column" to-do now counts deleted notes when comparing, and asks for a test that a deleted first note's text never comes back (`d185315c`).

### 12. Not yet seen in a browser, and the API types were written by hand

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - `AGENTS.md` asks for a look in the browser, parity screenshots and the axe audit of each visible change; the PR says "Browser: before merging". Two new dialogs, new buttons and a token haven't been seen, and `tools/ui-parity/screens.ts` has no screen for them.
  - Every note's buttons are named just "Delete" or "Request Deletion" (as "Edit" already is), so a screen reader user tabbing through hears the same name for each note.
  - `schema.d.ts` was edited by hand rather than generated (`npm run gen:api`); it looks right, but the next generation may differ.
- **Evidence:** the PR description; `MedApp.Web/src/components/procedures/ProcedureNotes.tsx:240-276`; `MedApp.Web/src/api/schema.d.ts`.
- **Fix:** add screens for the Delete confirmation and the request dialog, capture them and run `npm run a11y -- -g "<screen>"`; give each button an accessible name with its note (for example "Delete the note by s.nurse, 17 Nov 10:32"); regenerate the schema with the AppHost running and commit any difference.
- **Status:** open.
- **What happened to it:** fixed in `40aeac9b`: each note's buttons are named for their note ("Delete note by k.ellison, 16 Oct 2026, 16:47", "Request deletion of note by …", "Review request for note by …", "Edit note by …"), starting with the visible words. The browser check was run separately on a throwaway database; its observations are fixed too: the leftover hidden "Loading the request" (Astryx's spinner's live region) is gone, a 409 on Request Deletion reads the procedure again so it shows "Deletion requested", and Review Request sits with the token. Not done here: parity screenshots and the axe audit of the new dialogs (no AppHost), and regenerating `schema.d.ts` (hand-edited again for the new fields).

## Remaining risks

- **No undo, no queue, no withdrawal:** a wrong deletion is undone only by copying the text back from the admins' timeline (losing its author and time); requests have no list (finding 7); staff can't withdraw their own; requesters aren't told when a request is closed undecided. All in `docs/todo.md`.
- **Deleted text is kept for the record's life:** purging after a retention window isn't built, and is the DPO's call.
- **Orphans after a patient is deleted:** a patient whose last procedure was deleted can then be deleted; their requests, like their note versions and audit entries, have no foreign key and stay, beyond any export or anonymisation. This PR adds one more such table.
- **Notification floods:** a member of staff can raise a request for every note, each telling every admin, which pushes everything else off their bell.
- **H-38 is a draft:** no clinician or CSO has reviewed it, and its estimates assume controls that findings 3, 4 and 7 weaken.
- **Rolling the migration back** drops the requests and un-deletes every note; documented, with "restore from a backup" as the way out.
- **The branch is behind `main` by #112;** it merges cleanly now, but this file's row in `docs/reviews/README.md` sits beside #112's, so expect a one-line conflict there.

## Say it like this

"Admins can now delete a note from a procedure, and staff can ask them to, but deleting only hides the note: it's kept with who removed it and why, so the clinical record never loses anything. The main choice was a second pair of eyes instead of letting authors delete their own notes; the trade-off is that a wrong note stays visible until an admin decides, and the deleted text, even another patient's, stays on that record until it's anonymised."

## Detail

### Finding 1's fix

```diff
 // MedApp.Application/Patients/Queries/ExportPatient/ExportPatientHandler.cs
         var withExport = new PatientRecord
         {
             Patient = record.Patient,
             Procedures = record.Procedures,
             ProcedureHistory = record.ProcedureHistory,
             NoteVersions = record.NoteVersions,
+            NoteDeletionRequests = record.NoteDeletionRequests,
             Activity = [.. record.Activity, entry],
```

And in `PatientRecord.cs`, so the compiler refuses a copy that forgets one:

```diff
-    public IReadOnlyList<ProcedureNoteDeletionRequest> NoteDeletionRequests { get; init; } = [];
+    public required IReadOnlyList<ProcedureNoteDeletionRequest> NoteDeletionRequests { get; init; }
```

(the same for `NoteVersions` and `Attachments`, then set them wherever a `PatientRecord` is built, unit test builders included).

### Finding 2's fix

```diff
 // MedApp.IntegrationTests/Migrations/ProcedureNotesMigrationTests.cs:65 (and the same at :113)
-        var notes = await _context.PatientProcedureNotes.AsNoTracking().ToListAsync();
+        // Only the columns that exist at AddedProcedureNotes: later migrations add more to this table.
+        var notes = await _context.PatientProcedureNotes.AsNoTracking()
+            .Select(n => new { n.Id, n.PatientProcedureId, n.AuthorUserId, n.Text, n.CreatedAtUtc, n.LastEditedAtUtc, n.Version })
+            .ToListAsync();
```

### Finding 3's fix, in outline

- `DeleteProcedureNoteRequest` and `DecideNoteDeletionRequestRequest` gain `int ExpectedVersion` (required when deleting); the commands carry it.
- Both handlers, after finding the note: `if (note.Version != request.ExpectedVersion) throw new ConflictException(ProcedureNotes.EditedSinceOpenedMessage);`.
- `NoteDeletionRequestNoteResponse` gains `Version`; `NoteDeletionReasonDialog` sends `note.version`, the request dialog `request.note.version`, and shows "Edited <time>" with a warning when it's after the request.

### Why the rest of the concurrency holds

- **Two requests at once:** both update the note row; the second either fails its row version check (`RequestedMeanwhileMessage`) or, if EF inserts first, blocks on the first's index key and then breaks the filtered unique index (`AlreadyRequestedMessage`). Either way a 409, and `TwoRequestsAtOnce_OnlyOneIsMade` passes in CI.
- **Two decisions at once, or a decision and a direct delete:** both update the request row and the note row, so the later save fails a row version check and is a 409 (`DeletedMeanwhileMessage`).
- **A request and the procedure's deletion:** the deletion removes the tracked note rows with a row version check, so whichever saves second is a 409.

### Authorisation, checked

- Delete, the GET and the decision are `[Authorize(Roles = "Admin")]`; the request is the Staff policy. `AuthorisationTests` covers the three admin-only routes.
- The note is always looked up inside the procedure named in the path, and a decision derives the procedure and note from the request, so neither route can reach another procedure's note by ID.
- Staff never receive a reason: not in the note response, the timeline (`TimelineMapper` sets `Reason` and `From` only for admins, and `TimelineStaffCanaryTests` now includes `NoteDeleted`), or the notifications. The one thing staff learn about someone else's request is the "already asked" 409, which names who asked and when.
