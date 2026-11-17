# Review: PR #110, several notes per procedure and a pop-out notepad

**PR:** [#110](https://github.com/Aliator/MedApp/pull/110), branch `feature/procedure-notes`, reviewed at `f6e7b93` against `main` at `50ddf0b` (the merge of #109).

**Reviewed:** 17 November 2026, independently of the PR description's conclusions. Each claim below was checked against the code, a test run, or the PR's CI run.

**No finding is high or critical.** Six are medium and six are low. **All twelve are fixed or documented on the branch** (`2f8e3da`, `d9bc070`, `af04def`, `60d47bb`), and a browser check of the fixes is at the end of the findings.

## Summary

- **What it does:** a procedure now holds a list of notes, each with its author, time and every version, instead of one notes field. A personal notepad, open beside any page, can add what's written in it to a procedure as one of those notes.
- **Verdict:** the server side is careful and well tested, and the privacy split (staff never see note text on the timeline) holds. The weak points are in the web app's notepad (lost typing, a spurious conflict, removing the wrong text), the migration's guess at who wrote the old notes, and what happens to notes during a rollout or a rollback.

## What changed, and why

162 files, 9,818 lines added and 246 removed, in two commits (`git diff --stat origin/main...HEAD`).

### Several notes per procedure (`ce62e11`)

- **Notes as a list:**
  - **What it is:** `PatientProcedureNote` holds one note: its author, Markdown text (4,000 characters), when it was written, when last edited, a version number and a row version.
  - **An analogy:** before, a procedure had one whiteboard anyone could rub out and rewrite. Now it has a notebook: each person writes their own entry and signs it, and only they can change it.
  - **The rules:** staff add notes; only the author edits one (a 403 for anyone else, admins included); nobody deletes one. Notes can be added and edited in every status but Cancelled and Archived (`ProcedureWorkflow.WhyNotesCantChange`), and not on a restricted or anonymised record (the usual guard, `GetForUpdateOrThrowAsync`).
- **Versions in their own table:**
  - **What it is:** `PatientProcedureNoteVersion` keeps the text of every version, with who and when, and the patient and procedure copied in (no foreign keys, so it outlives a deleted procedure).
  - **Why:** DPIA risk 4. Before, the audit log copied old and new note text into its `Details`, so correcting or erasing a note left copies behind. Now the audit's `NoteAdded` (13) and `NoteEdited` (14) entries hold only the note's ID and version, and the text lives in one place.
  - **An analogy:** the audit log used to photocopy each page it mentioned. Now it writes down "page 4, version 2", and the pages are kept in one binder.
- **Stale edits are refused:** an edit sends the version it started from (`expectedVersion`), so a save based on an old copy is a 409. The row version (a number the database changes on every save) catches two saves in the same instant.
- **Through `ProcedureChange`:** `AddNote` and `EditNote` write the audit entry, move `LastUpdated`, and are saved in one `SaveChangesAsync`; the monitor is told (no rule watches notes).
- **Viewer-dependent response:** each note carries `canEdit` for the person asking, so the procedure's response now depends on who reads it (`viewerId`, passed by every handler).
- **The timeline:** staff see "Note added" or "Note edited", by whom and when, with no text. Admins see the text, read from the versions (`GetPatientTimelineHandler.WithNoteTextAsync`).
- **The migration `AddedProcedureNotes`:** copies each procedure's old notes (if not blank) into its first note, credited to whoever created the procedure (its `Created` audit entry), at the procedure's creation time, as version 1. **The old `Notes` column stays, unused** (`LegacyNotes`), so old API pods keep working during a rollout. Dropping it is a to-do.
- **Privacy:** the export lists every note and every version (deleted procedures' too); anonymising replaces every version's text and the old column; adding or editing a note counts as a change for retention.
- **The web app:** a Notes section on the procedure page (Add Note, Ctrl+S, Edit on your own notes, Reload after a 409). Edit Procedure has no notes now; New Procedure's notes become the first note.

### The pop-out notepad (`f6e7b93`)

- **What it is:** one notepad per member of staff, Markdown, up to 20,000 characters, kept on the server (`GET`/`PUT api/me/notepad`), never in browser storage.
  - **An analogy:** a sticky pad on the edge of your desk that follows you from room to room, and is kept in the building's safe, not in your pocket.
- **Saving:** the pane saves by itself 1.5 seconds after typing stops, on Ctrl+S, before closing and before signing out. Each save sends the version it started from, so another tab's save in between is a 409.
- **Retention:** cleared (the row deleted) once it has gone 30 days without a change, when it's next read or saved (`Notepad:KeepDays`). Deleted with the account.
- **Where it shows:** docked on the right from 1,024 px wide (pages beside it take their narrow layout), and a full-screen sheet on a narrower window.
- **Add Note to Procedure:** adds the selected text (as plain text) or everything, as a note on an open procedure page, after a confirmation with the patient banner, the procedure and the text. With two procedure pages open (a half view), it asks which. "Remove it from the notepad" starts ticked.
- **Safety and privacy docs:** new hazard H-37 (a note added to the wrong patient), new DPIA risk 26 (patient details in a notepad that a patient's export can't find), and updates to the data inventory, retention schedule, rights procedure, cookies notice and staff privacy notice.

## Design choices

- **Versions in a table, not copies in the audit:**
  - **Instead:** keep copying text into `Details`, as stage and appointment notes still do.
  - **Chosen because:** correction, export and erasure then touch one table, and the audit stays free of clinical text.
  - **Worse at:** two places to read for an admin's timeline (one more query per page), and stage and appointment notes now work differently from procedure notes.
- **Only the author edits; nobody deletes:**
  - **Instead:** let admins correct anyone's note, or allow deletes.
  - **Chosen because:** what a clinician wrote in the record stays theirs, and history is never lost.
  - **Worse at:** a note on the wrong record can't be withdrawn until "entered in error" is built (a to-do), and its text stays in the versions for good.
- **Keep the old column for one release:**
  - **Instead:** drop it in the same migration.
  - **Chosen because:** the chart's two-release rule: old pods still read and write it during a rollout.
  - **Worse at:** text old pods write after the copy lands in a column nothing reads, which the next release plans to drop (finding 5).
- **Notes inside the procedure's response, with no GET of their own:**
  - **Chosen because:** reading notes is then the procedure's `ProcedureViewed`, already logged, and the page needs one request.
  - **Worse at:** the response's `notes` changed from a string to a list, which breaks pages already open in browsers during a deploy (finding 5).
- **The notepad on the server, one per account:**
  - **Instead:** browser storage (cleared at sign-out, and not allowed to hold patient details), or a draft note tied to each procedure.
  - **Chosen because:** jottings during a call often come before you know which record they belong to, and the server is the only safe place for patient details.
  - **Worse at:** patient details can sit outside any record, where a patient's export and anonymisation can't find them (DPIA risk 26; finding 6).
- **Clear on the next read or save, not a scheduled job:**
  - **Chosen because:** MedApp has no scheduled job yet.
  - **Worse at:** a notepad never opened again keeps its text until its account goes, and an active one is never cleared (finding 6).
- **The selection added as plain text:**
  - **Instead:** take the selected part's Markdown from the editor.
  - **Chosen because:** the browser gives the selection as text for free.
  - **Worse at:** formatting is lost, and taking it out of the notepad has to find the text by matching, which can pick the wrong copy (finding 3). The confirmation shows the note as it will be saved, so what's added is never a surprise.
- **Pages take their narrow layout beside the docked notepad:**
  - **Instead:** decide by the room actually left (window width minus the notepad).
  - **Chosen because:** it reuses the half view's rule.
  - **Worse at:** wide screens waste space, and switching layout remounts parts of pages (findings 1 and 10).
- **A confirmation as the control for a note on the wrong patient (H-37):**
  - **Instead:** make the user pick the procedure every time, or type part of the patient's name.
  - **Chosen because:** the banner is the established way MedApp shows "who is this", and it costs one click.
  - **Worse at:** with one procedure open it's preselected, so the check depends on the user reading the banner.

## How it was checked

- **Read:** the PR description; the whole server diff (domain, configurations, both migrations, repositories, handlers, validators, mappers, the controllers, the timeline, export and anonymiser); the migration and integration tests; the web app's notepad components, `ProcedureNotes`, the procedure page, `AppLayout`, `Page`, `halfView`, the API hooks and the export page; the E2E journey; the governance documents and hazard H-37; `main`'s procedure page and demo data for what old code did.
- **Unit tests** (Release, filtered to notes, the notepad, the timeline canary, the anonymiser, the export, details updates and the timeline handler): 208 passed, no warnings shown.
- **Vitest:** `src/components/notepad` and `ProcedureNotes.test.tsx`: 5 files, 41 tests passed.
- **Two temporary Vitest tests**, deleted afterwards (the working tree is clean):
  - **Remount:** opening the notepad on `/patients` left the page's original heading element detached (`isConnected` false). Closing it with a half view open detached both halves' headings. That's finding 1.
  - **Overlapping saves:** typing, Ctrl+S with the save held open, typing again, then Ctrl+S twice, sent `expectedVersion` 3, 4 and 4, and the pane showed the conflict with Reload. That's finding 2.
- **CI** (`gh pr checks 110`, run 44051810287 at `f6e7b93`): all jobs pass, `e2e` and `integration-tests` included, with CodeQL and Trivy. Coverage: every new handler, validator and mapper is at 100% lines; no web file is under 80%.
- **Not run:** Docker, integration tests, the E2E suite, the AppHost or a browser (CI ran the suites; a separate browser check is running).

## Findings

### 1. Opening or closing the docked notepad remounts the whole page: unsaved typing on it is lost, and a half view jumps back to where it started

- **Severity:** medium. **Confidence:** high (reproduced with a temporary test).
- **What could go wrong:**
  - **The page loses its state.** A nurse is typing a stage's notes on a procedure page when the phone rings. They open the notepad to jot the caller's number. The page is rebuilt from scratch, and the half-typed stage notes are gone. Closing the notepad does the same to anything typed since.
  - **Why:** `AppLayout` renders the page directly in the shell when the notepad is closed, and inside a new `HStack` when it's docked. React treats a component that moves to a different parent as a new one, so it unmounts and mounts the whole page.
  - **An analogy:** moving a desk to make room for a side table, but by building a new desk; whatever was on the old one is gone.
  - **The half view resets.** Its router is created once, from `half.path`, the page the half view was opened on. `trackHalf` records where it has got to, but not in `half.path`. So after the remount, a half view that had moved on to a patient's procedure opens its first page again.
  - **The notepad's own typing:** crossing 1,024 px while it's open (a tablet turned to portrait, a window snapped) swaps the docked pane for the sheet. The pane unmounts with its save timer cleared (`clearTimeout` on unmount) and no save, so up to 1.5 seconds of typing is lost.
  - **The procedure page has a second cause:** docking switches it from its wide layout to its narrow one, which also rebuilds its sections (the New note box among them).
- **Evidence:** `MedApp.Web/src/components/navigation/AppLayout.tsx:149-170` (`pages`), `:198-217` (`isNotepadDocked ? <HStack>… : pages`), `:163` (`HalfPane key={half.key} path={half.path}`); `MedApp.Web/src/components/pages/halfView.tsx:58-65` (the router made once from `path`); `MedApp.Web/src/components/notepad/NotepadPane.tsx:171`; `MedApp.Web/src/components/pages/Page.tsx:55-59`.
- **Fix:**
  - **Keep the page where it is:** always render the same wrapper with the page first, and add or remove only the notepad's column after it. The page then never changes parent.
  - **Reopen the half view where it was:** give `HalfPane` the tracked path when it has to be rebuilt.
  - **Save on unmount:** if the notepad has unsaved changes when its pane unmounts, save them.
  - **Test it:** a page's heading element survives opening and closing the notepad, in one view and in a half view.
- **Status:** fixed in the PR (`d9bc070`). The page always sits in the same two wrappers (`display: contents` while the notepad is closed), so it never changes parent; the half view's `HalfPane` gets the tracked path; the pane saves what's unsaved as it unmounts, putting the text in the shared copy first, so the pane taking over starts from it. Tests: the Patients heading and both halves' headings stay the same elements through opening and closing, and the right half stays on the page it moved to (`AppLayout.test.tsx`); a pane that unmounts saves (`NotepadPane.test.tsx`). Checked in the browser too (below). **Left:** between 1,400 and 1,783 px wide, docking still switches the procedure page from its wide to its narrow layout (finding 10's rule), which rebuilds its sections; at 1,920 and below 1,400 nothing switches.

### 2. Two saves asked for while one is in flight send the same version: a false "saved somewhere else", and Reload discards what was typed since

- **Severity:** medium. **Confidence:** high (reproduced with a temporary test).
- **What could go wrong:**
  - On a slow connection, someone types, presses Ctrl+S (save A starts), types more, and presses Ctrl+S again, or the autosave timer fires and they close the pane, before A finishes.
  - Both waiting saves wait for A once, then both start, each sending the version A returned. The first succeeds; the second is a 409.
  - The pane says the notepad was "saved somewhere else (another tab or window)", stops saving, and offers only Reload. Anything typed after that isn't saved, and Reload replaces it with the server's copy.
- **Why:** `saveNow` checks `if (inFlight.current) await inFlight.current;` once. After that await, another waiter may already have started a new save, so this one should wait again.
- **Evidence:** `MedApp.Web/src/components/notepad/NotepadPane.tsx:133-165`, line 135. The temporary test sent versions 3, 4 and 4 and showed Reload.
- **Fix:** `while (inFlight.current) await inFlight.current;` (JavaScript runs the rest up to `inFlight.current = attempt` without a break, so no third save can slip in). Add a Vitest case with a held save and two more requests.
- **Status:** fixed in the PR (`d9bc070`): `while (inFlight.current) await inFlight.current;`. The test holds the first save open, asks for two more, and checks the versions sent are 3 then 4, with no conflict.

### 3. "Remove it from the notepad" removes the first copy of the selected text, which may be another patient's line

- **Severity:** medium. **Confidence:** high (from the code).
- **What could go wrong:**
  - The notepad says "Bed 3: obs stable" and, below it, "Bed 4: obs stable".
  - The nurse selects "obs stable" on the Bed 4 line and adds it to the Bed 4 patient's procedure, with "Remove it from the notepad" ticked (the default).
  - `removeFirst` finds the first "obs stable", on the Bed 3 line, and removes that. The notepad now says "Bed 3:" and "Bed 4: obs stable". Bed 3's jotting is lost before it reached a record, and Bed 4's is still there, ready to be added twice.
  - Short, repeated phrases ("NBM", "bloods sent", "call back") are common in clinical jottings.
- **Why it matters:** H-37 lists "Remove it from the notepad" as a protective control, and this is the path where it acts on the wrong text. The selection is plain text (`toString()`), and the pane then searches the Markdown for it, so it can't know which copy was selected.
- **Evidence:** `MedApp.Web/src/components/notepad/NotepadPane.tsx:214` (the selection as text), `:228-239` (`added`), `:333-337` (`removeFirst`, `indexOf`); `docs/clinical-safety/hazard-log.md:1422-1423`.
- **Fix:**
  - **Remove by position:** have the editor delete its own selection and read the Markdown back.
  - **At least,** remove only when the text occurs exactly once; otherwise say "The selected text appears more than once: remove it yourself", as it already does when it isn't found.
  - **Test it** with repeated text, and add the case to H-37's causes.
- **Status:** fixed in the PR (`d9bc070`), and H-37 updated (`af04def`, version 0.10). The selection now comes from the editor's own Lexical selection (`RichTextField`'s `selection()`, through `$generateJSONFromSelectedNodes`, `@lexical/clipboard` added as a direct dependency at the version already installed), and removing it deletes that range by its position (`removeText()` on a copy of the selection), never by searching. If the box changed since it was selected, nothing is taken out and the pane says so (shown and announced); if the editor can't say what's selected, "Remove it from the notepad" is unticked and disabled, with the reason. `removeFirst` is gone. Tests: the review's "obs stable" case (`RichTextField.test.tsx`, `NotepadPane.test.tsx`), the changed-meanwhile case and the can't-tell case.

### 4. The migration credits all of a procedure's old notes to whoever created it, even text others wrote later, and then lets the creator edit it

- **Severity:** medium. **Confidence:** high (from the code and the audit format).
- **What could go wrong:**
  - Before this PR, any member of staff could edit a procedure's single notes field, and teams added to it over time. `main`'s own demo data does exactly that: the person who starts a stage appends to the notes (`ProcedureReplay.cs:221` on `main`).
  - The migration makes that whole text one note, by the procedure's creator, at the creation time, never edited.
  - So the record says the creator wrote, on day one, words another clinician added weeks later. And the creator, as its "author", can now edit those words as their own.
  - The facts are available: each old notes edit is a `DetailsUpdated` audit entry with `changes.notes`, by its user.
- **Why it matters:** this PR's own rule is "what someone wrote in the clinical record stays theirs" (`EditProcedureNoteHandler`). The migration breaks it for every procedure whose notes more than one person touched.
- **Evidence:** `MedApp.Infrastructure/Migrations/20261117081149_AddedProcedureNotes.cs:87-96` (author from the `Created` entry, `CreatedAtUtc = p.CreatedAt`, `LastEditedAtUtc = NULL`); `MedApp.Application/Procedures/PatientProcedures/Commands/EditProcedureNote/EditProcedureNoteHandler.cs:24`; `MedApp.IntegrationTests/Migrations/ProcedureNotesMigrationTests.cs` (no case with a later edit).
- **Fix:**
  - **When anyone but the creator changed the notes** (a `DetailsUpdated` entry, action 1, with `$.changes.notes`), leave the author empty ("Unknown", which nobody can edit).
  - **Set `LastEditedAtUtc`** to the last notes change, so the page says "Edited".
  - **Test it:** a procedure whose notes another user edited. The SQL is in Detail.
  - The migration hasn't been applied to a lasting database, so it can be edited in place.
- **Status:** fixed in the PR (`2f8e3da`). The review's SQL as written fails on SQL Server (error 8124: an aggregate mixing an outer column and an inner one), so it's written with a CTE of the notes edits, `MAX` for the last edit and `EXISTS` for an edit by anyone else; the version row then has no user, an empty username, and the last edit's time. `ProcedureNotesMigrationTests` has the mixed-author case (and creator-only edits, a title-only edit by someone else, and no creator).

### 5. Notes written by old code during a rollout or after a rollback land in a column nothing reads, which the plan then drops; old browser tabs crash or lose edits

- **Severity:** medium. **Confidence:** high for the server side; medium for the browser side (it depends on what each old tab has loaded).
- **What could go wrong:**
  - **Old pods:** the migrations Job runs before the new pods start, so for a minute or two old pods serve on the new schema. A procedure created there with notes, or an Edit Procedure save that changes them, writes only the old `Notes` column. The new code never reads it.
  - **A rollback:** `AGENTS.md` says roll back by reverting on `main`, and the database isn't rolled back. The old app then runs for as long as the revert stands: every note added since the deploy vanishes from view, staff see the pre-migration text, and their edits go into the old column. Rolling forward doesn't copy them again (the migration has already run).
  - **The drop:** the to-do says to drop the column in the next release. Done as written, that deletes clinical text that exists nowhere else (except, for edits, the audit log's copy).
  - **Old browser tabs:** the response's `notes` changed from a string to a list. A page loaded before the deploy runs `procedure.notes?.trim()` on a list and crashes. An Edit Procedure page opened before the deploy and saved after sends `notes`, which the new API silently ignores (unknown JSON fields are skipped): a 200, and the change is gone.
  - **The chart's README** lists what old pods can't do for #99's migrations (`deploy/helm/medapp/README.md:99-103`). This PR adds nothing there.
- **Evidence:** `MedApp.Contracts/Procedures/PatientProcedures/Responses/PatientProcedureResponse.cs` (`Notes` is now a list), `.../Requests/UpdatePatientProcedureRequest.cs` (`Notes` removed); `main`'s `MedApp.Web/src/pages/procedures/ProcedurePage.tsx:548`; `docs/todo.md` ("Drop the old `PatientProcedures.Notes` column"); `deploy/helm/medapp/README.md:97-103`.
- **Fix:**
  - **Before dropping the column,** add a migration that turns any old-column text that doesn't match the procedure's moved first note into a new note by "Unknown", or one that refuses to drop while such text exists. Say so in the to-do.
  - **Document the window** in the chart README's list: what old pods write, and what a rollback shows.
  - **For one release, refuse rather than ignore:** keep `notes` on the PATCH request and answer a non-null one with a 400 ("Notes are now added on the procedure's page. Reload the page."). Keep a `notes` string on the response (empty, or the newest note's text) so old pages don't crash, or add a "new version, reload" prompt to the app.
- **Status:** fixed in the PR (`2f8e3da`, `af04def`). `PatientProcedureResponse.Notes` is a string again (deprecated: the first note's text, or the old column's), with the list as `NoteList`; `PATCH` with `notes` is a 400 (`NotesMovedMessage`); the chart's README lists what old pods do and what a rollback shows; the to-do for dropping the column now starts with copying, or reporting, any old-column text that differs from the first note, and removes the shims in the same release. Integration tests for both shims.

### 6. DPIA risk 26 understates the risk: the 30-day clear only catches notepads nobody uses, and the impact is rated "Minimal"

- **Severity:** medium. **Confidence:** high (from the code and the documents).
- **What could go wrong:**
  - **The rule is "30 days without a change" to the whole notepad.** A clinician who uses it every day never reaches 30 days, so a patient's details jotted in January are still there in June, unless they clear them by hand.
  - **A subject access request misses it.** The patient's export can't find those details, and anonymisation can't remove them (the DPIA says so).
  - **The DPIA's residual risk** says "low with the scheduled purge". A purge of notepads untouched for 30 days doesn't touch an active one either, so it wouldn't get there.
  - **The impact "Minimal"** sits oddly with risk 3 (oversharing in free text), rated "Significant": a jotting about a phone call can be health data ("rang, worried about the biopsy result").
- **Evidence:** `MedApp.Application/Notepads/Common/NotepadRules.cs:16-17`; `docs/governance/dpia.md:139` (risk 26's rating), `:486-499` (its measures and residual risk); `docs/governance/retention-schedule.md:86`.
- **Fix:** choose one, and make the documents say exactly that:
  - **Age the text, not the last save:** record when the notepad last went from empty to holding text, and after `KeepDays` show "Your notepad has held text since 3 November: add it to a record or clear it" (or clear it, after warning).
  - **Or accept it plainly:** rate the impact Significant, give residual medium even with the purge, and add a step to the rights procedure (an admin searches notepads for the patient's name and NHS number when answering a request). For the DPO to confirm.
- **Status:** documented (`af04def`), and the notepad's own wording made true (`d9bc070`). Risk 26 is now Probable and Significant, **High**; the measures say the 30 days restart with every change; residual stays medium with the purge, low only once text is aged by when it was written, which is a new to-do; a rights procedure step is listed for the DPO. The data inventory, retention schedule, staff notice and the governance README say the same. For the DPO to confirm.

### 7. A note's edit sends the version it has at saving time, not the one it started from

- **Severity:** low. **Confidence:** high (from the code).
- **What could go wrong:** the same procedure is open in both halves of a half view (or two tabs). The author opens Edit on a note on the left, then edits and saves the same note on the right (version 2). The left copy is refreshed in the cache, so its `note.version` is now 2, but its editor still holds the text from version 1. Saving on the left sends version 2, which matches, and silently replaces the right-hand edit. The versions table keeps it, so nothing is lost for good, but the 409 the design promises never comes.
- **Evidence:** `MedApp.Web/src/components/procedures/ProcedureNotes.tsx:148` (`expectedVersion: note.version` from the current render), `:209` (the draft fixed when the editor opens); `MedApp.Web/src/components/forms/RichTextField.tsx:26-31` (not controlled).
- **Fix:** keep the version with the editing state when Edit is pressed (`{id, version}`) and send that.
- **Status:** fixed in the PR (`d9bc070`): the editing state keeps `{id, version}` from when Edit was pressed. A test changes the cached note to version 2 mid-edit and checks version 1 is sent, and the 409 shows beside the note.

### 8. Every tab shows the notepad, each with its own old copy, so typing in an older tab is a 409 and Reload loses it

- **Severity:** low. **Confidence:** high (from the code).
- **What could go wrong:** whether the notepad is open is kept in `localStorage`, which every tab shares, so opening it in one tab opens it in all of them on their next load. Each reads the notepad once (`staleTime: Infinity`, and the app never refetches on focus). Someone writes in tab A, then switches to tab B, which still shows the old text, and adds a line: a 409, "saved somewhere else", and Reload replaces their line with tab A's text.
- **Evidence:** `MedApp.Web/src/api/notepad.ts:17`; `MedApp.Web/src/app/queryClient.ts:9`; `MedApp.Web/src/components/notepad/NotepadContext.tsx:10-27`.
- **Fix:** when the tab becomes visible again and the box has no unsaved changes, read the notepad afresh. Or tell other tabs about a save (a `BroadcastChannel`).
- **Status:** fixed in the PR (`d9bc070`): with nothing typed, the pane reads the notepad afresh when the tab is shown again (`visibilitychange`, and the window's `focus`), and a newer copy replaces the box's; a newer version with the same text is just adopted. Tests for both, and that nothing is read over unsaved typing.

### 9. Accessibility: the docked notepad sits inside the main landmark, and focus isn't moved to it or back

- **Severity:** low. **Confidence:** medium (from the code; axe wasn't run).
- **What could go wrong:**
  - **Landmarks:** the pane is a `complementary` landmark inside `main`, because the docked column is inside the shell's content area. Screen reader users hear it as part of the page, and axe's `landmark-complementary-is-top-level` rule flags it.
  - **Focus:** opening it from the side nav leaves focus on the nav, so a keyboard user has to tab through the whole page to reach it. Closing it with its own Close button removes the focused button, so focus falls back to the start of the document. There's no shortcut for it.
- **Evidence:** `MedApp.Web/src/components/navigation/AppLayout.tsx:198-217`; `MedApp.Web/src/components/notepad/NotepadPane.tsx:60, 67`.
- **Fix:** move focus into the box when it opens, and back to the side nav's Notepad item when it closes; place the docked column outside `main` if `AppShell` allows (or drop the landmark role for a labelled region); consider a shortcut; include a screen with the notepad open in the axe run (`tools/ui-parity`).
- **Status:** fixed in the PR (`d9bc070`, `60d47bb`). The pane is a labelled `region`, not a `complementary` landmark (AppShell has no slot outside `main`); it takes the focus when its user opens it (not when it reopens with a page); closing gives the focus back to the opener, or the side nav's Notepad, or, on a phone whose nav drawer has gone, the page's title. The side nav item is a toggle (`aria-pressed`, a filled icon), never `aria-current`. The axe run includes the notepad screens (below). No shortcut yet.

### 10. Every page takes its narrow layout beside the docked notepad, however wide the window

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** on a 2,560 px screen, a page beside the 24rem (384 px) notepad has about 2,000 px, but shows the layout meant for a half view or a small window. At 1,024 px it has about 400 px beside the side nav, which is tighter than any layout was designed for.
- **Evidence:** `MedApp.Web/src/components/pages/Page.tsx:55-59`; `MedApp.Web/src/components/notepad/NotepadContext.tsx:41-44`.
- **Fix:** while docked, go narrow only when the window minus the notepad is under the usual 1,400 px (a `(max-width: 1783px)` query), and consider showing the sheet below about 1,280 px.
- **Status:** fixed in the PR (`d9bc070`): beside the docked notepad, pages go narrow only below `(max-width: 1783px)` (`NARROW_BESIDE_NOTEPAD_QUERY`). At 1,920 the procedure page keeps its Stages column. The sheet still starts below 1,024 px.

### 11. The tests miss the cases above, and the E2E journey's "shows on the page" check also matches the notepad

- **Severity:** low. **Confidence:** high.
- **What's missing:**
  - **Web:** overlapping saves (finding 2), repeated text (finding 3), the page surviving the notepad opening (finding 1), and an edit's version fixed when it starts (finding 7).
  - **Migration:** a procedure whose notes someone else edited (finding 4).
  - **API:** a PATCH that still sends `notes` (finding 5), and two edits of one note at the same instant (the row version's 409, `PatientProcedureRepository.ConflictMessage`).
  - **The E2E journey:** `Expect(Main).ToContainTextAsync(jotting)` is meant to show the note on the procedure page. The docked notepad is inside `main` too, so the check could pass from the notepad's own text. It's saved by the API check after it.
- **Evidence:** `MedApp.E2ETests/Notepad/NotepadTests.cs` (the `Main` check after Add Note); the test lists in `NotepadPane.test.tsx`, `ProcedureNotes.test.tsx` and `ProcedureNotesMigrationTests.cs`.
- **Fix:** add the tests with the fixes; in the E2E journey, look for the note inside the procedure page's Notes list (`GetByRole(List, "Notes")`).
- **Status:** fixed in the PR: the tests listed with each finding, the API tests for a PATCH that sends `notes` and the 201's `Location`, and the E2E journey looks for the note in the procedure page's Notes list. Not added: two edits of one note at the same instant (the row version's 409).

### 12. Smaller hardening and drift

- **Severity:** low. **Confidence:** high.
- **The items:**
  - **Note versions for the admin timeline** aren't limited to the patient, and every version of each note is read when only the two needed are (`MedApp.Infrastructure/Data/Repositories/ProcedureAuditRepository.cs:33-40`). Filter by `PatientId` and by the versions wanted.
  - **`Notepad:KeepDays` isn't checked.** Zero or less makes every read delete the notepad and every save a 409 (`NotepadOptions.cs`, `NotepadRules.cs:16-17`). Validate it on start (at least 1).
  - **The 201's `Location`** names a note URL that has no GET (`MedApp.API/Controllers/PatientProceduresController.cs:131`). Point it at the procedure, or leave it out.
  - **The procedure page's Add Note** says "Too long" but still sends, for a 400 (`ProcedureNotes.tsx:115-125`). Disable it, as the notepad's dialog does.
  - **The hazard log's history** puts version 1.0 above 0.9, and "1.0" reads as an issued version for a draft (`docs/clinical-safety/hazard-log.md:1467-1468`). Call it 0.10 (or 1.0 draft, placed below 0.9).
- **Status:** fixed in the PR (`2f8e3da`, `d9bc070`, `af04def`): the versions are read by patient and by the version numbers needed; `Notepad:KeepDays` under 1 stops the API at startup; the 201's `Location` is the procedure; Add Note is disabled (and Ctrl+S does nothing) while too long; the hazard log's entry is 0.10, after 0.9.

## Browser check

A separate browser check of `f6e7b93` (before the fixes) found these; then the fixes were checked in Chromium (Playwright), at 1,920, 1,366, 960 and 390 px wide, in light and dark, against a throwaway copy of the demo data (`MedApp_Preview`, dropped afterwards), signed in as an admin.

| Found before the fixes | What happened | Seen after |
|---|---|---|
| **Focus:** adding a note from the page's New note box sent the focus to `<body>` (the box is rebuilt empty) | Fixed: the new box takes the focus | The focus is in the New note box after adding |
| **The editor's toolbar was cut off** in the 384 px pane and at 390 px, with no sign more existed | Fixed: the toolbar's buttons wrap onto a second row (one layout rule, `WRAPPING_TOOLBAR_CSS`, in `RichTextField`, so stage notes and every rich text box get it) | Inline code and Link sit inside the pane at every width |
| **An upper-case procedure ID** in the address stopped the notes list updating after adding | Fixed: the procedure routes read `$id` in lower case (`lowerCaseId`) | A note added on `/procedures/<ID in capitals>` shows at once |
| **The admin timeline showed raw Markdown** for note text | Fixed: the text is shown with `RichText` (compact) | Bold and a list show formatted, no `**` |
| **New Procedure's hint was visually hidden** (a hidden label hides its description) | Fixed: the box is labelled "First note", shown, with its hint | The hint shows under the label |
| **A note's 409 showed at the top of the Notes section**, possibly off screen | Fixed: it shows under the note being edited, with Reload | The alert and Reload are in the edited note's row |
| **Blocked procedures** (cancelled, archived, restricted) said nothing about why notes couldn't be added | Fixed: the section says why (the restriction's reason, or the status), as the stage section does | "Notes can't be added or edited while the procedure is cancelled." |
| **The export page's procedure notes had no heading** | Fixed: each procedure's title is a heading (level 3), and its notes are under "Notes (n)" (level 4) | Both headings show |
| **An empty, never-used notepad** said "Not saved yet" | Fixed: "Nothing written yet" | Shown on a notepad with no row |
| **At 960 px the sheet's editor** stayed 12rem tall with a large empty area below | Fixed: the writing area is fitted to the room left in the pane, docked or a sheet (a `ResizeObserver`, at least 12rem) | The editor reaches the footer at every width |
| **Selected text lost its formatting** | Fixed with finding 3: the selection is Markdown from the editor | Bold and a list item keep their formatting in the confirmation, the saved note and the timeline |
| **Already on `main`: Escape after Tab doesn't close a dialog** | Cause found, left as a to-do (`docs/todo.md`, Accessibility): Tab moves the focus to the dialog's Close button, whose tooltip shows on keyboard focus and is a layer of its own in Astryx's dismissal stack, so the first Escape dismisses the tooltip and the second closes the dialog. It's Astryx's behaviour (WCAG 1.4.13 asks for the tooltip to be dismissible), not MedApp's code | Log Out at 1,366: one Escape leaves it open (the tooltip goes), the second closes it |

**The fixes, as seen:**
- **No rebuild:** text typed in the New note box survives opening and closing the docked notepad at 1,920; with a half view (the right half moved on to Add New Patient, a field typed in), opening the notepad keeps both halves and the field.
- **Layout:** at 1,920 the procedure page keeps its two columns beside the notepad; at 1,366 it's narrow (as it is without the notepad below 1,400); at 960 and 390 the notepad is a full-screen sheet.
- **Focus and roles:** opening from the side nav puts the focus in the notepad's box, docked and as a sheet; its own Close gives it back to the side nav's Notepad (docked, and at 960) or the page's title (390, where the nav drawer has closed). The nav item has `aria-pressed`, no `aria-current`; there's no complementary landmark in `main`.
- **Add Note to Procedure:** a selection across a bold word and a list item is added as `**worried** about the result.` and `- Bloods taken`; with "Bed 3: obs stable" and "Bed 4: obs stable", selecting the second "obs stable" and adding it takes out exactly that one, leaving Bed 3's line.
- **Axe** (`tools/ui-parity`, WCAG 2.2 A and AA) on `patient-view`, `patient-view-narrow`, `procedure-view`, `procedure-notes`, `notepad-open` and `notepad-add-note`, in dark, light and light with a colour theme: 18 runs, **no violations**. The notepad screens in `screens.ts` now find the notepad as a region.

## Remaining risks

- **A note on the wrong record stays.** Until "entered in error" is built, a wrong-patient note can only be edited by its author, and its text stays in the versions, and in that patient's export, for good. The export's clinician review step (to hold back third-party data) is still a to-do.
- **Stage and appointment notes** still copy their text into the audit and the appointment history (DPIA risk 4 stays high).
- **No scheduled purge** of notepads, sessions or notifications.
- **Notepad typing in the last 1.5 seconds** is lost if the tab is closed or the session times out (there's no save on page unload, and an ended session can't save).
- **Old browser tabs after any deploy:** MedApp has no "a new version is available" prompt, so any change to a response's shape can break pages already open (finding 5 is one case).
- **A partly selected list item** is added without its bullet (as copying does); a whole item keeps it.
- **The procedure page's wide and narrow layouts** are separate trees, so between 1,400 and 1,783 px wide, docking the notepad still rebuilds the page's sections (finding 1).
- **Escape after Tab in a dialog** takes two presses (Astryx's tooltip; a to-do).
- **Edits have no time limit:** an author can change a note months later. The version history and the "Edited" label keep that visible.

## Say it like this

> "Procedure notes became a list of signed entries, each with its full edit history kept in its own table, so the audit log no longer carries copies of clinical text. I chose author-only edits and no deletes, which protects the record but means a note on the wrong patient can't be withdrawn until 'entered in error' is built. The notepad is kept on the server because it can hold patient details, and the trade-off is that those details sit outside any patient's record, so we clear it after 30 days and steer people to add what matters as a note, after a confirmation that shows the patient."

## Detail

### Finding 2: the save queue

```ts
// NotepadPane.tsx, saveNow
clearTimeout(timer.current);
// Another save may start while this one waits: wait for each in turn.
while (inFlight.current) await inFlight.current;
if (stateRef.current.kind === 'conflict') return false;
```

### Finding 3: remove only an unambiguous match

```ts
/** The text without `part`, or null when it isn't there as selected, or is there more than once. */
export function removeOnly(text: string, part: string): string | null {
  const at = text.indexOf(part);
  if (at < 0 || text.indexOf(part, at + 1) >= 0) return null;
  return (text.slice(0, at) + text.slice(at + part.length)).replace(/\n{3,}/g, '\n\n').trim();
}
```

The announcement for null then needs to cover both cases ("could not be taken out of the notepad, as it isn't there exactly once: remove it yourself").

### Finding 4: crediting the old notes honestly

Between the two `INSERT`s in `AddedProcedureNotes.Up`, so the version rows pick up the result (their `Username` then needs to be empty when the author is):

```sql
-- Notes edited after the procedure was created: when, and whether by anyone but its creator.
UPDATE n
SET LastEditedAtUtc = edits.LastAt,
    AuthorUserId = CASE WHEN edits.ByOthers > 0 THEN NULL ELSE n.AuthorUserId END
FROM PatientProcedureNotes n
CROSS APPLY (
    SELECT MAX(a.AtUtc) AS LastAt,
           COUNT(CASE WHEN n.AuthorUserId IS NULL OR a.UserId <> n.AuthorUserId THEN 1 END) AS ByOthers
    FROM PatientProcedureAuditEntries a
    WHERE a.PatientProcedureId = n.PatientProcedureId
      AND a.Action = 1 -- DetailsUpdated
      AND JSON_QUERY(a.Details, '$.changes.notes') IS NOT NULL
) edits
WHERE edits.LastAt IS NOT NULL;
```

### Finding 1: why the page remounts

```tsx
// AppLayout.tsx, today: the page's parent changes when the notepad docks.
{isNotepadDocked ? (
  <HStack data-notepad-docked="">
    <StackItem …>{pages}</StackItem>   {/* pages is now two levels deeper */}
    …
  </HStack>
) : (
  pages                               {/* pages is the shell's direct child */}
)}
```

React keeps a component only while it stays at the same place in the tree with the same type. Here `pages` moves, so React throws the old page away and builds a new one. Always rendering the wrapper, and adding or removing only the notepad's column after the page, keeps the page in place.
