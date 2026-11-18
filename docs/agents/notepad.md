# Notepad

Each member of staff's notepad: read before changing it.

Each member of staff has one notepad (`Notepad`, keyed by account ID): Markdown jottings beside every page of the web app (`MedApp.Application/Notepads`).
- **Endpoints:** `GET`/`PUT api/me/notepad` (`{text, expectedVersion}`), for anyone signed in (`[Authorize]`, no role), always the caller's own; the response carries `version` (0 for none) and `keepDays`. A save from another version is a 409 (`NotepadRules.ChangedMeanwhileMessage`); the row version and the key catch two saves at once (also 409).
- **Kept on the server, never in the browser,** because it may hold patient details. The web app keeps only whether it's open (`medapp.notepadOpen`).
- **Retention:** deleted when next read or saved once it has gone `Notepad:KeepDays` (30, at least 1, checked at startup) without a change, so one in use is never cleared (ageing its text is a to-do). A save that finds it expired is a 409 (`NotepadRules.ClearedMessage`), so old text isn't saved again. The operational purge (`docs/agents/jobs.md`) deletes untouched ones too, once its dry run is turned off; its delete checks the date in the same statement, so a notepad saved meanwhile stays. Deleted with the account (cascade).
- **Not in the activity log:** it's the user's own, not linked to a patient (`EndpointCoverageTests` says so), so a patient's export and anonymisation can't find it. The DPIA accepts that (risk 26), with the 30 days and **Add note to pathway** as the controls.
- **Add note to pathway** (web only) adds its text, or the selected part (as Markdown, from the editor's selection), as a note on an open pathway (`POST .../notes`), after a confirmation showing the patient as the banner does (hazard H-37). "Remove it from the notepad" removes exactly that range by position, never by searching for its words, and says so when it can't.
