# Review: PR #113, Requires Appointment, the notepad's own window, and blocks dragged in edit mode

**PR:** [#113](https://github.com/Aliator/MedApp/pull/113), branch `feature/notepad-window`, reviewed at `3ab3ea0` against `main` at `894c1fe` (the merge of #110).

**Reviewed:** 17 November 2026, from the diff, the code around it and the PR's CI run, independently of the PR description's conclusions.

**One finding is high:** the notepad's own window outlives signing out, so on a shared computer the last user's jottings, and the patients of the procedures they had open, stay on screen and usable (finding 1). One is medium and six are low. None is fixed yet: this review changes only itself and the index.

## Summary

- **What it does:** a batch of UI changes. "Needs booking" is renamed **Requires Appointment** and leaves the side nav. The notepad can open in **a small window of its own** or **in a half view**, and MedApp's windows keep it in step over a `BroadcastChannel`. Blocked time on the calendar is dragged only while **Edit blocks** is on. Smaller changes: the bell's count sits over its corner, scrollbars take the theme's colours, search bars lose the favourite's name tag, dialogs size themselves by the window, and a nav row's menu stays open while the pointer crosses to it.
- **Verdict:** the rename, Edit blocks and the small changes are careful, well tested and well documented. The notepad window is built carefully for the paths it was designed for (saving, closing, keeping windows in step), but it sits outside the signed-in layout and so misses what that layout does for every other page: it isn't told about sign-out, never checks its session, and has no timeout warning.

## What changed, and why

99 files, 2,080 lines added and 484 removed, in 13 commits (two of them merges of `feature/requires-appointment` and `feature/calendar-drag-blocks`). The only server changes are wording.

### Needs booking renamed Requires Appointment (`a19657d`, `426011a`)

- **What:** the Requested status reads "Requires appointment", the list's page is `/calendar/requires-appointment`, and every message, dialog, timeline line, export label and server message (`AppointmentWorkflow`, `CreateAppointmentValidator`) says the new name. The E2E test and its README follow.
- **Out of the side nav:** the list's nav item is gone. It's reached from the Calendar page's **Requires Appointment (N)** link, which shows how many are waiting, and the Calendar item is marked current on every page under `/calendar`.
- **Saved searches keep their old key** (`list="needs-booking"`).
  - **An analogy:** renaming a folder on a shared drive but leaving its old label on the back, so everyone's shortcuts still find it.
- **Safety documents:** hazard H-30 (a visit the patient needs doesn't happen) records the missing nav item as a cause, rating unchanged, for the clinical safety officer (CSO) to confirm.

### The notepad's own window and half view (`3be1c54`, `fbaeb08`, `3c35ce8`)

- **The window:** a button on the Notepad row opens `/notepad` with `window.open('', 'medapp-notepad', 'popup,…')`.
  - **What that does:** asking for a window by name either finds the one already open with that name, untouched, or opens a blank one, which is then sent to `/notepad`. So a second click brings the window forward instead of reloading it (which would lose a jotting not yet saved).
  - **An analogy:** booking "meeting room B" by name: if you're already in it, you're just shown back to it.
  - A blocked pop-up shows a toast saying how to allow it. Opening the window closes the docked notepad, saving first.
- **The page:** `/notepad` is outside the signed-in layout (no side nav, no notepad inside it), behind the same sign-in guard, so holds such as the forced password change still apply. Close saves, then closes the window. With something unsaved, the browser asks before the window closes (`beforeunload`, the "Leave site?" prompt).
- **The half view:** the row's menu opens the same page in the right half. Its Close closes the half, and closing the left half docks the notepad rather than opening `/notepad` full size (it has no side nav to get back with).
- **Windows in step:** a `BroadcastChannel` named `medapp.notepad`.
  - **What it is:** a browser feature that lets pages of the same site in the same browser post messages to each other. Nothing is stored, and no other site or browser hears it.
  - **An analogy:** an intercom between rooms of one house: anyone in the house can hear, nobody outside can, and nothing is recorded.
  - **What goes over it:** "saved at version N" (other notepads with nothing typed read it afresh), each window's open procedure pages (the window asks as it starts), and "a note was added to procedure X" (pages showing it read it afresh).
  - **Add Note to Procedure** in the window offers the procedure pages open in any MedApp window, each procedure once, with the same confirmation showing the patient (hazard H-37, updated with this new cause).
- **The prompt and explanation removed:** "Jot something down" and the text saying what the notepad is for and when it's cleared, by the product owner's decision. DPIA risk 26 and H-37 record the control as removed; the staff privacy notice still says it.
- **An exact fit:** the writing area now measures the padding, borders and margins below it, so an empty notepad shows no scrollbar.

### Blocked time dragged only in Edit blocks mode (`98c2976`)

- **What:** blocks could be dragged whenever they could be changed, so a slip while opening Lunch could move it. Now an **Edit blocks** toggle (`aria-pressed`) in the toolbar allows it, only on one person's calendar the viewer can change, never the team view, off each time the page opens.
  - **An analogy:** furniture on wheels with the brakes on: unlock first, then move.
- **Saving** goes through `BlockDialog`'s own calls; a template block moves within its own day only, as the API requires. The block shows where it was dropped while saving; a refusal puts it back with the API's words. Opening a block (click or Enter) stays the non-drag way (WCAG 2.5.7). Calendar plan decision 70.

### Smaller changes (`fbaeb08`, `f89e443`, `f741ed7`, `5369e5c`, `3ab3ea0`)

- **The bell's count** sits over the bell's top right corner (absolutely placed, hidden from screen readers, clicks pass through), so the bell is one size in the open and folded nav.
- **Scrollbars** use the theme's emphasised border colour on a clear track (`src/app/scrollbars.css`, `scrollbar-color`); width stays the browser's.
- **Search bars** no longer show the favourite's name beside the star; the star's name and tooltip still say it.
- **Dialogs lay out by the window:** `DialogFrame` sets `OverWholeWindowContext`, so `useIsNarrow()` inside a dialog ignores a half view or the docked notepad, and the booking and meeting dialogs ask `useIsNarrowWindow()`.
  - **Why:** a dialog is drawn over the whole window, so one opened from a half view was squeezed for no reason.
  - **An analogy:** a sheet of paper laid over the whole desk should be measured against the desk, not the drawer it came out of.
- **A nav row's menu stays open** while the pointer crosses the gap to it: an invisible strip (a "bridge") covers the gap while the menu is open, placed in the row's React tree so the pointer on it still counts as on the row.
  - **An analogy:** a plank between a ledge and a shelf, so you don't fall into the gap.
- **Rows menu** items read "15 minutes" and "5 minutes".
- **To-do list:** Keycloak for email, shorter warning banners, Terraform with Argo CD, a static demo, and a hydration mismatch on reloading with a half view open.

## Design choices

- **A real pop-up window for the notepad:**
  - **Instead:** Document Picture-in-Picture (a window that stays on top, Chrome and Edge only), or only the docked notepad and the half view.
  - **Chosen because:** it works in every browser, on a second monitor, and the half view covers the one-window case.
  - **Worse at:** pop-up blockers; a page can only close a window a page opened; and the window lives outside the signed-in layout, so it gets none of what that layout does for a page (sign-out, the session check, the timeout warning: findings 1 and 2).
- **`BroadcastChannel` to keep windows in step:**
  - **Instead:** reach into the opener window through `window.opener`, push through the server (SignalR, already used for notifications), or `storage` events on `localStorage`.
  - **Chosen because:** no server change; it reaches windows MedApp didn't open; nothing is written to storage, which mustn't hold patient details.
  - **Worse at:** a message sent before a window starts listening is lost; it can't say which window a procedure is in; a window that leaves without saying so stays listed; and nothing ties a message to the account that sent it (finding 1).
- **One `/notepad` page for the window and the half view:**
  - **Chosen because:** one component, one set of tests.
  - **Worse at:** the window's own behaviour (`isWindow`) reaches the half view, which then warns before the main window unloads (finding 3).
- **Requires Appointment reached only from the Calendar page:**
  - **Instead:** keep the nav item, or show its count on the Calendar item.
  - **Chosen because:** a shorter side nav, and the count is on the Calendar page's link.
  - **Worse at:** someone who doesn't open the Calendar sees neither the list nor its count (finding 6).
- **Edit blocks as a toggle that starts off:**
  - **Instead:** keep dragging always on, or require a long press.
  - **Chosen because:** an accidental move of Lunch is the common mistake, and a toggle is visible and announced.
  - **Worse at:** one more toolbar control, and a step before a block can be dragged.
- **Dialogs sized by a context in `DialogFrame`, not only per dialog:**
  - **Chosen because:** every composite inside any dialog then gets it right without knowing it's in one.
  - **Worse at:** dialogs built without `DialogFrame` (a `Drawer`, a raw Astryx `Dialog`) don't get it; none does today.
- **Removing the notepad's explanation** (the product owner's decision): the DPIA and H-37 say it was an information control, the weakest kind, and that the privacy notice still says the same. Staff who haven't read the notice are no longer reminded in the notepad itself.

## How it was checked

- **Read:** the PR description; the whole diff; the notepad (`NotepadContext`, `NotepadPane`, `notepadChannel`, `notepadWindow`, `AddToProcedureDialog`, `NotepadWindowPage`, the route); `AppLayout`, `AppSideNav`, `SessionTimeout`, `useLogout`, the API client's hold handling, `settingsStore`; the calendar page, `calendarItems` and the calendar mutations; the server's notepad handler; the governance documents, H-30 and H-37, the ASVS checklist and threat model.
- **Vitest** (`npx vitest run` on named files):
  - the notepad folder, `NotepadWindowPage`, `AppSideNav`, `AppLayout`, `NotificationBell`, `Page`, `calendarItems`, `scrollbars`: 12 files, 179 tests passed;
  - `CalendarPage`, `RequiresAppointmentPage`, `calendarSearch`, `entry-server`, `ListFavourites`: 5 files, 112 tests passed.
- **`npm run typecheck`:** passes (CI's `web` job stopped at `npm audit` before reaching it).
- **.NET** (Release, filtered to `AppointmentWorkflowTests` and `CreateAppointmentHandlerTests`, the renamed messages): 71 passed.
- **CI** (`gh pr checks 113`, run 44170496343 at `3ab3ea0`): `unit-tests`, `integration-tests`, `e2e`, `image`, `helm`, `dependency-review`, CodeQL and Trivy pass. `web` fails at `npm audit` (GHSA-fc5q-39xc-vjpf, picomatch, no fix yet; handled separately), so CI's lint, coverage and production build haven't run on this branch.
- **Not run:** Docker, the AppHost, a browser, the full suites. Findings 1 and 2 are from the code, not reproduced in a browser.

## Findings

### 1. The notepad's own window outlives signing out: the last user's jottings, and the patients of their procedures, stay on screen and usable

- **Severity:** high (exposes data on a normal path). **Confidence:** high (from the code; not reproduced in a browser).
- **What could go wrong:**
  - **On a shared ward computer,** a nurse opens the notepad in its own window and jots "Bed 4, rang about the biopsy result". They sign out from the main window and leave.
  - **The main window** goes to the sign-in page and clears its cache and storage, as designed. **The notepad window is untouched:** it still shows the jotting, and its Add Note to Procedure still lists the procedures that were open, with each patient's name, date of birth and NHS number in the confirmation.
  - **The next person** at the computer sees both. If they sign in, the window quietly works under their session cookie: used there, Add Note to Procedure would add the old text as a note by the new user, and typing in it saves into the new user's notepad if the two happen to be at the same version (otherwise a 409). Nothing in the window says whose notepad it is.
  - **A session that ends another way** (8 hours, "Sign out everywhere", a password reset) does the same. Other tabs check their session when shown again and go to the sign-in page (`SessionTimeout`); the notepad window never checks, so it stays as it was indefinitely.
  - **Without signing out, stale procedures:** a window's procedures are dropped only on `pagehide` (the tab closing) or a new list. Leaving the signed-in layout within the app (to `/login` after a timeout, to a hold page) unmounts it without saying so, so the notepad window keeps offering procedures that are open nowhere. H-37's new cause names only a crash.
- **Why:**
  - Sign-out only touches the window it happens in: `useLogout` navigates that window, clears that window's query cache and storage (`MedApp.Web/src/auth/auth.ts:202-216`). Nothing posts on the channel (`MedApp.Web/src/components/notepad/notepadChannel.ts:9-17` has no such message).
  - The window has no session check: `AppLayout` renders `SessionTimeout` (`MedApp.Web/src/components/navigation/AppLayout.tsx:197`), which turns a 401 into the sign-in page and a cleared browser (`SessionTimeout.tsx:59`, `:99-105`); `NotepadWindowPage` doesn't (`MedApp.Web/src/pages/notepad/NotepadWindowPage.tsx:47-55`). The API client acts only on a 403 hold, not a 401 (`MedApp.Web/src/api/client.ts`, `onResponse`).
  - The window keeps every window's last list (`NotepadContext.tsx:192-199`); the layout posts on change and on `pagehide` only, nothing as it unmounts (`NotepadContext.tsx:210-230`).
  - The documents promise otherwise: "Signing out clears the query cache, so nothing of it is left in this browser" (`MedApp.Web/src/api/notepad.ts:10-11`); "Only you can see it" (`docs/governance/privacy-notice-staff-template.md:27`); ASVS 14.3.1, authenticated data cleared when the session ends (`docs/security/asvs.md:407`); H-37's cause (`docs/clinical-safety/hazard-log.md:1416`).
- **Fix:**
  - **Tell every window at sign-out:** `useLogout` and `SessionTimeout`'s end post `{type: 'signed-out'}` (a helper in `notepadChannel.ts`, as they have no provider). Every `NotepadProvider` that hears it removes the notepad from its query cache and empties its shared procedures; the notepad window then closes (`closeNotepadWindow`, falling back to `/login` rather than `/`).
  - **Say so on leaving:** the layout's provider posts an empty `procedures` list as it unmounts (a clean-up in the posting effect; safe under StrictMode, as the second run posts the list again).
  - **Watch the session in the window:** render `SessionTimeout` in its own-window branch (it also fixes finding 2), so a 401 there clears it too.
  - **Optionally,** tag each message with the signed-in user's ID (from `whoami`) and ignore messages from another account.
  - **Tests:** a `signed-out` message clears and closes the window (`NotepadWindowPage.test.tsx`); the layout's provider posts an empty list on unmount (`NotepadContext.test.tsx`); signing out posts `signed-out` (`AppSideNav.test.tsx`).
  - **Documents:** H-37's cause (leaving the layout, not only a crash), DPIA risk 26's window measure (what happens at sign-out), the comment in `api/notepad.ts`.
- **Status:** open.
- **What happened to it:** fixed in the PR, by the product owner's decision to remove the notepad's own window (`8ec997f`). There's no separate window any more: the notepad opens docked or in a half view, both inside the signed-in layout, so signing out or timing out takes it away with everything else. `/notepad` opened on its own (an old pop-out reopened, or the address typed) goes to Home with the notepad docked, and its server render no longer reads the notepad. The cross-window channel went with the window.

### 2. The notepad window has no warning before the session times out, and a save after it ends only says "could not be saved"

- **Severity:** medium (WCAG 2.2.1 Timing Adjustable, and lost typing in a plausible situation). **Confidence:** high (from the code).
- **What could go wrong:**
  - A clinician keeps the notepad window on a second monitor all shift, with the main window behind it. Five minutes before the 8-hour session ends, the warning opens in the main window, which they aren't looking at.
  - The session ends. They keep typing in the notepad window. Its autosave fails with "The notepad could not be saved." and Retry, which keeps failing: nothing says the session ended, or that signing in again elsewhere and pressing Retry would save it.
  - If they then close the window, the browser's "Leave site?" prompt is all that stands between them and losing the text.
- **Why:**
  - Only the signed-in layout warns (`SessionTimeout` in `AppLayout.tsx:197`; "Sessions time out (WCAG 2.2.1): `SessionTimeout` in `AppLayout` warns five minutes before", `MedApp.Web/AGENTS.md:83`). The window has none (`NotepadWindowPage.tsx:47-55`).
  - Every failed save is the same message (`NotepadPane.tsx:170-174`), unlike `saveProblem.ts:29`, which says "Your session has ended. Please sign in again."
  - The root `AGENTS.md` names one page allowed to end without a warning (the forced password change, deliberately); the notepad window isn't listed.
- **Fix:** render `SessionTimeout` in the window (finding 1's fix), so it warns there with Stay signed in; and turn a 401 on save into "Your session has ended. Sign in again in MedApp, then Retry." Test both in `NotepadWindowPage.test.tsx`.
- **Status:** open.
- **What happened to it:** fixed by the same removal: no window, so no page without `SessionTimeout`. A notepad in another tab is inside the layout, with its warning.

### 3. In a half view, the notepad warns before the main window unloads, and can be docked a second time beside itself

- **Severity:** low. **Confidence:** high (from the code).
- **What could go wrong:**
  - **The warning:** the half view's notepad is `isWindow`, so it adds the `beforeunload` warning to the main window. Reloading MedApp with something unsaved in the half view asks "Leave site?", while the docked notepad never does. The rule says the warning is "in that window only" (`MedApp.Web/AGENTS.md:36`), and the test that the signed-in layout never warns covers only the docked pane (`NotepadPane.test.tsx:605`).
  - **Twice:** with the notepad in the half view, the side nav's Notepad toggle says it's closed (`aria-pressed` false), and pressing it docks a second copy beside the first. Typing in both gives one of them a 409.
- **Evidence:** `MedApp.Web/src/components/notepad/NotepadPane.tsx:257-269` (gated on `isWindow`); `MedApp.Web/src/pages/notepad/NotepadWindowPage.tsx:39` (the half view passes `isWindow`); `MedApp.Web/src/components/navigation/AppSideNav.tsx` (the Notepad row's `aria-pressed={notepad.isOpen}`, the docked provider's state only).
- **Fix:** give the provider a separate "own window" flag (`isWindow && !onClose`) for the warning and for closing the window, or keep the warning in the half view on purpose and say so in `AGENTS.md` with a test. Have the toggle close the half-view notepad (or show as pressed) while it's there.
- **Status:** open.
- **What happened to it:** fixed in the PR (`8ec997f`): the unload warning went with the window, and the side nav's Notepad toggle closes a half view showing the notepad before docking it (`HalfViews.close()`), so it's never beside itself. Tests in `AppSideNav.test.tsx` and `AppLayout.test.tsx`.

### 4. In the notepad window, "Left:" and "Right:" name the halves of other windows

- **Severity:** low. **Confidence:** high (from the code).
- **What could go wrong:** with two MedApp windows each in a half view, the window's Add Note to Procedure can list "Left: Knee review" twice, told apart only by the patient's name under each. "Left" means the left half of some other window, which the window doesn't say. The patient banner after choosing (H-37's control) still shows who it is, so this is about clarity, not a missing check.
- **Evidence:** `MedApp.Web/src/components/notepad/AddToProcedureDialog.tsx:45-46` (`where`); `NotepadContext.tsx` (`sharedProcedures` keeps each entry's `side`).
- **Fix:** in the window, drop the side for procedures from other windows, or say "In another window: Knee review"; keep the patient's name as the description.
- **Status:** open.
- **What happened to it:** fixed by the removal: the half view offers the procedure pages of the layout it's in, whose Left and Right are its own halves.

### 5. Each procedure page's whole response goes to every MedApp window on every change

- **Severity:** low (data minimisation). **Confidence:** high (from the code).
- **What could go wrong:** nothing leaves this browser, but every signed-in window posts its procedure pages' full `Procedure` objects, with every note, stage note and instruction, whenever one changes, and every other window copies them in memory. The notepad window uses the ID, title, patient, `canAddNotes` and `readOnlyReason`. The DPIA describes what's sent as "the procedure pages open, with their patients", which reads narrower than it is.
- **Evidence:** `MedApp.Web/src/components/notepad/NotepadContext.tsx:210-214` (posts `openProcedures`), `:35-40` (`OpenProcedure` holds the whole `Procedure`); `notepadChannel.ts:13`; `docs/governance/dpia.md:495`; the response's `stages`, `notes` and `noteList` (`src/api/schema.d.ts`, `PatientProcedureResponse`).
- **Fix:** post a small projection (`id`, `title`, `patientName`, `patient`, `canAddNotes`, `readOnlyReason`, `side`) and make the DPIA's line say exactly that.
- **Status:** open.
- **What happened to it:** fixed by the removal: the channel is gone, and nothing is sent between windows.

### 6. Requires Appointment left the side nav, H-30 records it, and its rating stays the same

- **Severity:** low (a documented, deliberate change to a draft hazard). **Confidence:** medium (the effect on how often the list is looked at is a judgement).
- **What could go wrong:** before, every page showed a Needs Booking item. Now the list and its count are seen only by someone who opens the Calendar. A Requested appointment has nobody's name on it and nobody is notified (H-30), so the list being out of sight makes "a visit the patient needs doesn't happen" more likely, while the hazard log keeps the rating and leaves the cheaper control (a count in the side nav or on Home) as proposed.
- **Evidence:** `MedApp.Web/src/components/navigation/AppSideNav.tsx:611-630` (only the Calendar item); `docs/clinical-safety/hazard-log.md:1149` (the new cause), `:1173` (the count, still proposed).
- **Fix:** show the count on the Calendar item (a badge, like the bell's, with the item's name saying how many), which keeps the shorter nav; or ask the CSO to re-rate H-30's likelihood with the change.
- **Status:** open.
- **What happened to it:** documented. H-30 records that the list is reached from the Calendar's "Requires Appointment (N)" link, and the proposed visible-count control stays proposed for the CSO; a count on the Calendar nav item is a possible follow-up.

### 7. Old links to `/calendar/needs-booking` now show Not found

- **Severity:** low. **Confidence:** high (from the routes).
- **What could go wrong:** a bookmark, a browser's history, a link pasted into a handover, or a half view kept in this tab's storage (`medapp.halfView`) still points at `/calendar/needs-booking`. After the deploy it's the Not found page, and in a half view a half that shows Not found.
- **Evidence:** `MedApp.Web/src/routes/_app/calendar/needs-booking.tsx` deleted; only `requires-appointment.tsx` remains; `MedApp.Web/src/routes/__root.tsx` (`notFoundComponent`).
- **Fix:** keep a thin `needs-booking.tsx` route whose `beforeLoad` redirects to `/calendar/requires-appointment`, keeping the search, for a release or two.
- **Status:** open.
- **What happened to it:** fixed in the PR (`d7c3403`): `/calendar/needs-booking` redirects to `/calendar/requires-appointment`, keeping the page, sort and patient filters, in the browser and on the server (a 307). Tests in `routeGuards.test.tsx` and `entry-server.test.tsx`.

### 8. The security documents don't cover the new window or the channel

- **Severity:** low (documentation that has drifted). **Confidence:** high.
- **What could go wrong:** the ASVS checklist says sign-out is "easy to find on every authenticated page" (7.4.4, Met); the notepad window is an authenticated page with no sign-out. 14.3.1 and the threat model's T-03 (session theft from a shared machine) don't mention a window that keeps its data after sign-out. The root `AGENTS.md` asks for both to be updated when sessions or a data flow change.
- **Evidence:** `docs/security/asvs.md:253` (7.4.4), `:407` (14.3.1); `docs/security/threat-model.md:95` (T-03); `MedApp.Web/src/pages/notepad/NotepadWindowPage.tsx:47-55`.
- **Fix:** with finding 1's fix, record the window and the channel in the threat model, and either add a small "Signed in as … · Sign out" line to the window or mark 7.4.4 partly met.
- **Status:** open.
- **What happened to it:** fixed by the removal: with no window or channel there's nothing new to record, and sign-out stays on every signed-in page (7.4.4 holds).

## Remaining risks

- **The notepad window's lifecycle is the weak part:** after finding 1's fix it still relies on messages that are lost if posted before a window starts listening, and a window that crashes stays listed until the notepad window is reopened (as H-37 says).
- **Half view without `BroadcastChannel`:** the half view's notepad learns about the page beside it only over the channel, so where a browser lacks it, Add Note to Procedure there stays disabled even with a procedure open on the left.
- **A choice that disappears from the confirmation:** if the procedure chosen in Add Note to Procedure leaves the list while the dialog is open (its window navigates away), nothing is chosen and Add Note is disabled; with one entry left there's no list to choose from, so the dialog must be cancelled and reopened. Safe, but confusing.
- **Scrollbars on custom colours:** the thumb uses the theme's border colour on a clear track, and the custom colour check measures text only, so a custom page or card colour close to that border colour can hide the thumb (as it would a field's outline).
- **CI's web checks haven't run** on this branch (lint, coverage thresholds, the production build), because `npm audit` stops the job first.
- **The DPIA and H-37 changes** (the explanation removed, the new window's cause) are drafts for the DPO and the CSO to confirm.

## Follow-up review of the fixes

**Checked:** 18 November 2026. Commits `8ec997f` (the notepad's own window removed, the notepad kept in a half view) and `d7c3403` (the old Needs booking address redirected), on the branch at `83bd58c`. That merge of `main` touches none of the same files.

**Result:** finding 1, the high one, is fixed. Nothing high or critical remains or is new. Three new findings, all low: two in the code, one in the documents.

### What was checked

- **Nothing of the pop-out is left.** Searched `MedApp.Web/src`, `docs/`, both `AGENTS.md` files and `tools/ui-parity` for the window, the pop-out, `BroadcastChannel`, `notepadWindow`, `isWindow` and `beforeunload`. The button, `notepadWindow.ts`, `notepadChannel.ts`, the unload warning, the icon, the fake channel in the test setup and their tests are all gone. What's left is history: the hazard log's 0.10 row, the reviews of #110 and this one, and the reviews index (finding 11).
- **The half view** (`NotepadPage.tsx`, `NotepadContext.tsx`):
  - Add Note to Procedure offers the layout's own procedure pages (`openProcedures` from the provider it sits inside).
  - Close saves, then closes the half. If the save fails, the half stays open.
  - It closes the docked notepad as it opens. A ref makes that happen once, also under StrictMode. That includes a reload, where the layout reads `medapp.notepadOpen` only after mounting.
  - The side nav's toggle closes a half view showing the notepad, then docks it.
  - The half's provider never writes `medapp.notepadOpen`: `open` and `close` skip storage when it's in a half.
  - Nothing reads `window` or storage while rendering. The half view isn't in the server's render at all, as its path is in `sessionStorage`.
- **`/notepad` on its own:** the server renders only a spinner, in the account's settings, and doesn't read the notepad. The browser then stores the notepad as open and replaces the address with `/`, once, also under StrictMode (`NotepadPage.test.tsx`, `entry-server.test.tsx`). A held account still goes to its hold first.
- **The redirect:** `/calendar/needs-booking` throws a redirect to `/calendar/requires-appointment` in `beforeLoad`, with its search parsed as the new page's. It replaces the history entry. On the server it's a 307 with the query kept. A half view kept at the old address follows it too.
- **The documents:** DPIA risk 26 and version 0.10, the data inventory's `Notepad` row, and H-37's cause, control and 0.11 row now describe the half view, and say the window was removed and why. They match the code. `MedApp.Web/AGENTS.md` matches it too.
- **Ran:**
  - Vitest on the notepad folders, `NotepadPage`, `AppSideNav`, `AppLayout`, `routeGuards`, `entry-server` and `scrollbars`: 10 files, 196 tests passed.
  - `npm run typecheck`: passes. ESLint on the changed source files: clean.
  - Two throwaway tests, deleted afterwards, for findings 9 and 10 below.
  - No Docker, AppHost, browser or full suite.

### Are the "What happened to it" lines true?

- **Findings 1, 2, 4, 5 and 8:** true. The window and the channel are gone. A notepad is now always inside the signed-in layout, with its sign-out, `SessionTimeout` and storage clearing.
- **Finding 3:** true. The warning went with the window, and the toggle closes the half first. But the toggle closes the half without the half's own save-then-close, so a failed save there loses the text (finding 9).
- **Finding 6:** true. It's documented, for the CSO.
- **Finding 7:** true, with tests in both places it names.
- **"Remaining risks" above:** the first two (the window's lifecycle, a half view without `BroadcastChannel`) no longer apply. The third mostly doesn't either: the half view offers only the page beside it.

### New findings

#### 9. Docking the notepad from the side nav, while it's in the half view, can lose what's typed there if that save fails

- **Severity:** low. **Confidence:** high (reproduced in a throwaway test).
- **What could go wrong:**
  - The notepad is in the right half, and its saves are failing (the API is down for a moment, so the half shows "The notepad could not be saved." and Retry).
  - The user presses the side nav's Notepad toggle. The half closes, and the docked notepad opens at once.
  - The half's box saves as it unmounts. The docked box has already started from the copy cached before that. It ignores the copy the half puts in the cache, because that copy has the same version.
  - The save fails, so the docked notepad shows the old text. What was typed in the half is gone, with no message.
  - **Even when the save works,** the docked box shows the old text until it finishes. Typing in that moment gives a 409.
- **Why:** the toggle calls `halfViews.close()` and `notepad.open()` in one click (`MedApp.Web/src/components/navigation/AppSideNav.tsx:652-661`). That bypasses the half's Close, which saves first and stays open if the save fails. The hand-over relies on the unmount clean-up (`NotepadPane.tsx:186-197`). Its comment says the pane taking over "starts from it". But that pane rendered before the clean-up ran, and the version check skips a copy with the same version (`NotepadPane.tsx:201-208`).
- **Already there before this fix:** closing the left half while the notepad is on the right (`AppLayout.tsx:135-142`), and crossing between docked and sheet, hand over the same way. The toggle is a new way in.
- **Fix:** have the toggle close the half's notepad through its own closer (save, then close; nothing docked if it fails). For example, the half's provider registers its `requestClose` with the layout's provider. Then do the same for closing the left half. Test that a failed save leaves the half open and the docked notepad closed.
- **What happened to it:** documented. It's the same hand-over the notepad already makes when the left half closes or the window crosses between docked and sheet: the half's box saves as it goes, and only a save that fails at that moment loses what was typed. Recorded as a known limit; closing the half through its own save-then-close is a possible follow-up.

#### 10. Opening the notepad in the half view, with the docked notepad open, takes the focus out of its box

- **Severity:** low (focus order, WCAG 2.4.3). **Confidence:** high (reproduced in a throwaway test).
- **What could go wrong:** a keyboard user has the docked notepad open, and chooses the Notepad row's half view. The half's box takes the focus. Then the docked notepad closes, and its focus return moves the focus to the side nav's Notepad item, or to the left page's title. They have to find their way back to the box.
- **Why:** closing the docked notepad treats focus inside *any* region named Notepad as lost (`NotepadContext.tsx:138-156`). The half's pane is a region named Notepad too, and it's still on screen. `NotepadPage.test.tsx:133` checks that the docked notepad closes, but not where the focus ends up.
- **Fix:** count only focus in the docked pane or the sheet as lost. For example, mark those two and check for the mark, or skip the focus return when the half view asked for the close. Add the focus check to that test.
- **What happened to it:** fixed in the PR: the docked notepad's focus return no longer treats the notepad in the right half as its own (`NotepadContext.tsx`), so the half's box keeps the focus. A test in `NotepadPage.test.tsx` fails without the fix and passes with it.

#### 11. The PR's description and the reviews index still describe the window

- **Severity:** low (documents). **Confidence:** high.
- **What could go wrong:** someone reading PR #113 or `docs/reviews/README.md` thinks the merged app has a notepad window kept in step over a `BroadcastChannel`.
- **Evidence:** the PR description's notepad bullet, its browser checks and its axe list (`notepad-window`); `docs/reviews/README.md:56`. This review's "Say it like this" and "Detail" describe the fix that was planned, not the one made; this section records the change.
- **Fix:** update the PR description and the index line before merging. Capture the new `notepad-half` parity screen, which replaces `notepad-window`, and run axe on it, if that hasn't been done.
- **What happened to it:** fixed in the PR: the PR's description and this review's row in `docs/reviews/README.md` now describe the half view, not the window.

### Remaining risks after the fixes

- **Hand-overs between notepads** rely on an unmount save (finding 9). A half view replaced by another page loses an unsaved jotting the same way.
- **The DPIA and H-37 changes** are still drafts for the DPO and the CSO.

## Say it like this

"This PR let the notepad open in its own small window, kept in step with MedApp's other windows over the browser's BroadcastChannel, which needs no server change and stores nothing. The trade-off is that the window lives outside the signed-in layout, so it misses what that layout does for every page: it isn't told when someone signs out and never checks its session, which on a shared ward computer leaves the last user's jottings on screen. The fix is to broadcast sign-out to every window and give the notepad window the same session watcher as the rest of the app."

## Detail

### Finding 1: the shape of the fix

In `notepadChannel.ts`, a message and a helper that works without a provider:

```ts
| {type: 'signed-out'}

/** Tells MedApp's other windows this browser signed out, so the notepad's window clears itself. */
export function announceSignOut() {
  if (typeof BroadcastChannel === 'undefined') return;
  const channel = new BroadcastChannel(NOTEPAD_CHANNEL);
  postNotepadMessage(channel, {type: 'signed-out'});
  channel.close();
}
```

`useLogout`'s `onSettled` and `SessionTimeout`'s end call `announceSignOut()` beside `clearStorageAtSignOut()`. In `NotepadProvider`'s listener, with `isOwnWindow` (`isWindow && !onClose`, finding 3) kept in a ref, since the listener is set up once:

```ts
case 'signed-out':
  queryClient.removeQueries({queryKey: notepadQuery().queryKey});
  setShared({});
  // Only the notepad's own window: a half view's notepad is in the window that signed out, which is leaving anyway.
  if (isOwnWindow.current) closeNotepadWindow(); // falling back to /login
  break;
```

And the layout's provider says goodbye in the channel effect itself, before closing the channel. A separate effect's clean-up would be too late: React runs clean-ups in the order the effects are declared, so the channel would already be closed.

```ts
if (isWindow) post({type: 'ask-procedures'});
else post({type: 'procedures', windowId: id, procedures: latestProcedures.current});
return () => {
  // Leaving the signed-in layout (signing out, a hold page, /login after a timeout): no procedure pages here now.
  if (!isWindow) post({type: 'procedures', windowId: id, procedures: []});
  channel.current?.close();
  channel.current = null;
};
```

Under StrictMode the effect runs twice, so the window sees the list, an empty list, then the list again, with the same keys each time, so a choice made in an open dialog comes back with it.

### What was looked at and found sound

- **The rename:** every user-facing "Needs booking" is gone; saved searches keep their key; the status icon, tokens, export and timeline follow; the server's messages and their tests match (71 passed); `AppointmentStatus` numbers are untouched.
- **Edit blocks:** blocks on past days and in the team view never move; a template block stays in its day, matching `ChangeTemplateBlockForDayValidator.SameDay`; the mutations await their refetch, so a saved block doesn't jump back before the new calendar arrives; a refused move refetches and shows the API's words.
- **Server-side rendering:** the channel opens only in effects and where `BroadcastChannel` exists; the window's provider starts open on both server and browser; `/notepad` renders on the server with the account's settings and redirects held accounts (`entry-server.test.tsx`).
- **The bridge:** it's rendered only while the menu is open, `aria-hidden`, and its `data-nav-bridge` keeps the row's buttons shown.
