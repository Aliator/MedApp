# Review: PR #96, the calendar's Book or Block menu kept open after a mouse selection

**PR:** [#96](https://github.com/Aliator/MedApp/pull/96), branch `feature/calendar-drag-block`, reviewed at `cf08ea4` against `main` at `3d081d4`.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Each claim below was checked against the code, a test run, or the running app (driven with throwaway Playwright scripts in Chromium). **No finding is high or critical:** 1 is medium and 4 are low.

## Summary

Selecting free time on the Calendar with the mouse now opens the "Book appointment or Block time" menu and keeps it open. The grid reports a pointer selection a moment after the press ends (`setTimeout(…, 0)`), and a new end-to-end journey drags over an hour and blocks it. The fix works for mouse, touch and pen in Chromium. But the menu it rescues opens in the window's top-left corner, not beside the selection (finding 1).

## What changed, and why

5 files, 81 lines added and 5 removed.

- **The problem:** dragging or clicking over free time should open a small menu. With the mouse, the menu opened and closed again at once, and the selection disappeared. With the keyboard (Enter), it worked.
- **Why it closed:** the menu is an Astryx `Popover`, which uses the browser's own popover feature with *light dismiss*: the browser closes it when a press starts outside it and is released.
  - **The timing:** the grid reported the selection inside its handler for the mouse release (`pointerup`). React drew the menu straight away, still inside that handler. Then the browser finished handling the release: the press had started on the grid, outside the menu, so it closed the menu.
  - **An analogy:** a waiter who clears any plate when someone taps the table outside it. The plate was put down while the hand that asked for it was still lifting, so that same tap cleared it.
- **The fix** (`MedApp.Web/src/components/calendar/CalendarGrid.tsx:366-374`): on release, the grid saves the selection and reports it with `window.setTimeout(() => commit(selected), 0)`.
  - **`setTimeout(…, 0)`** runs the code as soon as the browser has finished what it's doing now: this press, its release and its click. Like saying "after you" at a door. By the time the menu opens, the press is over, so nothing can count as outside it.
  - **The keyboard path is unchanged:** Enter still reports the selection at once (`CalendarGrid.tsx:316-320`). Keys don't trigger light dismiss.
- **Unit tests** (`CalendarGrid.test.tsx:146-180`): the click and drag tests now check that nothing is reported during the release itself, then wait for it to be reported.
- **A new end-to-end journey** (`MedApp.E2ETests/Calendar/BlockTimeTests.cs`). An *end-to-end journey* drives a real browser against the running app. This one:
  - drags from 14:00 to 14:45 on a weekday three weeks ahead, in day view
  - checks the menu opens, and is still open half a second later
  - chooses Block time, saves the dialog, and finds a 14:00 to 15:00 block on the calendar
  - deletes the block through its dialog.
  - **Why it's needed:** jsdom, the simulated browser the unit tests use, has no light dismiss, so only a real browser can show the menu closing itself.
- **Docs:** the journey in `MedApp.E2ETests/README.md`, and the deferred report in `CalendarGrid`'s entry in `MedApp.Web/AGENTS.md`.

## Design choices

- **Report the selection a moment later, rather than commit on `click`:**
  - **Instead:** report the selection from the `click` that follows the release. It also comes after the browser's light dismiss.
  - **Chosen because:** a timer needs no assumptions about `click`. Which element gets the click after a drag with *pointer capture* (the grid's column holds the pointer for the whole drag) has differed between browsers. A timer behaves the same for mouse, touch and pen.
  - **Worse at:** a pointer selection now arrives later than a keyboard one. Every caller and test has to allow for that, and the timer isn't cancelled if the grid goes away first (Remaining risks).
- **Defer in the grid, rather than in the page:**
  - **Instead:** keep the grid's callback immediate and defer only in `CalendarPage.onSelectRange`, next to the `Popover` that needs it.
  - **Chosen because:** any future screen that opens a menu or dialog from a selection gets the fix for free. The page's "straight to booking" path (a calendar you can only book on) benefits too.
  - **Worse at:** a generic grid now carries a rule that exists for one kind of caller, and its contract is no longer the same for pointer and keyboard.
- **A timer, rather than a microtask or an animation frame:**
  - **Instead:** `queueMicrotask` or `requestAnimationFrame`.
  - **Chosen because:** a microtask runs before the browser's own release step, so it would be too early. An animation frame would work but pauses in background tabs. `setTimeout(…, 0)` is the usual "after this event" idiom. Astryx uses it for the same reason (`node_modules/@astryxdesign/core/dist/Layer/useLayer.js:810-813`).
  - **Worse at:** nothing important.
- **Keep the menu's light dismiss, rather than turn it off:**
  - **Instead:** `hasLightDismiss={false}`, with the page closing the menu on outside presses itself. Astryx's own tool for this, `claimPress` (`Layer/gestureCounter.js`), isn't exported.
  - **Chosen because:** the browser's light dismiss also handles Escape, other popovers and focus. Rebuilding it would be more code and more risk.
  - **Worse at:** nothing, for this bug.
- **A browser journey as the regression test, rather than only unit tests:**
  - **Chosen because:** jsdom can't show the bug.
  - **Worse at:** it writes to the signed-in account's calendar, so it has to clean up and avoid other data (finding 3). It also finds the menu by its name wherever it is, so it can't see where it opens (finding 1).

## How it was checked

- **Read:** the diff and PR description; `CalendarGrid.tsx`, `CalendarPage.tsx` and their tests; `DayCalendar.tsx`, the only other user of `CalendarGrid` (read-only, no `onSelectRange`); Astryx 0.7.15's `Popover`, `usePopover`, `useLayer`, `gestureCounter` and `outsidePanClaim`; the E2E base class (`E2ETest`, `E2EApi`, `E2EData`), `BookingTests` and the README; `WorkingHours` and `CalendarProjection` (which days are working days).
- **CI:** all 13 checks pass at `cf08ea4` (run 43958329602), including `e2e`, where `BlockTime_DraggedOverFreeTime_OpensTheMenuAndBlocksThatTime` passed in 4 s against the production image.
- **Unit tests:** `npx vitest run src/components/calendar/CalendarGrid.test.tsx src/pages/calendar/CalendarPage.test.tsx`: 2 files, 41 tests passed.
  - **A temporary test** (written, run and deleted) made a real left press and release, then ran `expect(onSelectRange).not.toHaveBeenCalled()` straight after, as an existing test does. It passed, and the selection arrived just after (finding 4).
- **The new journey, locally:** `E2E_USERNAME=admin E2E_PASSWORD=admin dotnet test MedApp.E2ETests -c Release --filter "FullyQualifiedName~BlockTimeTests"`: passed in 8 s. Afterwards, the `admin` calendar on Monday 7 December (the day it used) had no blocks.
- **In the running app,** signed in as the parity tool's admin account, at 1440 x 900, en-GB, Europe/London, on Tuesday 15 December. Nothing was saved:
  - **Mouse drag, day view (10:00 to 11:00):** the menu opens and is still open 600 ms later. The focus is on "Book appointment".
  - **Mouse drag, week view:** the same.
  - **A single click:** the same, for 15 minutes.
  - **Touch tap** (Playwright's touchscreen): the menu stays. The events came as pointerdown, pointerup, click, then the menu opening.
  - **Pen** (mouse events sent with `pointerType: 'pen'`): the menu stays.
  - **Keyboard** (a slot, then Enter): the menu opens, as before.
  - **Where the menu opens:** at x 4, y 0 (the window's top-left corner, over the side nav) in every case, keyboard included. The selection was at x 351, y 370 in day view, and x 542, y 370 in week view (finding 1).
  - **Escape:** closes the menu, and the focus goes to the last slot of the selection.
  - **Clicking Go to date, Whose calendar or New while the menu is open:** the menu closes, but the focus goes back to a grid slot, not the control clicked (finding 2).
  - **Clicking another free time while the menu is open:** the menu closes, and the new time isn't selected. 10 times out of 10.
  - **A calendar you can only book on:** the calendar's response was rewritten in the browser to say `canEdit: false`, without changing any data. A click and a drag (11:00 to 12:00) each open Book Appointment, which stays open with that time. Cancelled.
  - **Touch drag** (touch events sent through the browser's debugging protocol): the browser cancels the pointer to scroll, and nothing is selected. This is from before this PR (Remaining risks).
- **Not done:**
  - Firefox and Safari weren't tested; only Chromium is installed here.
  - No real touch screen, pen or screen reader was used.
  - `main` wasn't run (no branch switch). That the menu closed before the fix rests on the PR's statement and the code. The menu's position doesn't depend on this PR: the keyboard path, which this PR doesn't change, opens it in the same place.

## Findings

### 1. The menu opens in the window's top-left corner, not beside the selection

- **Severity:** medium. **Confidence:** high (measured, and explained by Astryx's code).
- **What could go wrong:** someone drags over 10:00 to 11:00 in the middle of the screen. The highlight stays, but nothing appears near the pointer. The menu is in the top-left corner, over the side nav, 350 to 600 px away.
  - They may think nothing happened, and drag again. That press is outside the menu, so it closes the menu and the second selection is lost too.
  - In a half view, the menu for the right-hand half opens over the left-hand page.
  - This was the same for the keyboard before this PR. But a mouse selection never kept the menu open until now, so this PR makes it the usual path.
- **Why:** the `Popover` is anchored by `anchorRef={selectionRef}` (`MedApp.Web/src/pages/calendar/CalendarPage.tsx:340-348`), which points at the selection's highlight, a plain `<span>` with no button in it (`CalendarGrid.tsx:576-584`).
  - **What Astryx needs:** in this mode it needs the anchor to be, or contain, a `<button>` or `role="button"`. Finding none, it logs the warning the PR mentions and stops before registering the element (`node_modules/@astryxdesign/core/dist/Popover/Popover.js:564-574`).
  - **The result:** the menu never gets an anchor. *CSS anchor positioning* (the browser placing one element against another) has nothing to place it against (`Layer/useLayer.js:1121-1125`), so it falls back to the corner.
  - **For screen readers:** for the same reason, nothing gets `aria-expanded` or `aria-controls`.
  - **The PR description** lists the warning under "Known, not changed" and says it "doesn't affect the menu". It does.
  - **Why the tests don't catch it:** the journey finds the menu by its name, wherever it is (`BlockTimeTests.cs:39-44`), and no UI parity screen shows it.
- **Evidence:** `CalendarPage.tsx:340-348`; `CalendarGrid.tsx:576-584`; `Popover.js:564-574`; the measurements under How it was checked, and Detail.
- **Fix:**
  - **Give the `Popover` an anchor it accepts.** Two options to try:
    - Anchor it to the slot button where the selection ended (the page already keeps it as `heldAnchor`), so it gets the button-and-dialog attributes too. Check that a click on that slot while the menu is open still behaves.
    - Or use Astryx's `usePopover` hook, whose `triggerRef` takes any element, on the highlight.
  - **Check it in the browser:** in `BlockTimeTests`, check that the menu's box sits beside the selection's highlight.
  - **Add a parity screen:** a `calendar-selection-menu` screen in `tools/ui-parity/screens.ts` (it only opens the menu).
  - **Correct the PR description.**
- **What happened to it:** fixed in the PR. A held selection is now a `<button>` (`tabIndex={-1}`, named "Selected time, 13:00 to 14:00", still letting presses through to the slots), so Astryx's `Popover` finds its trigger, anchors the menu to it and sets `aria-expanded`. While it's being dragged out it stays a plain highlight. Measured in Chromium afterwards: in week view the menu opens right beside the selection, level with it (selection to x 728, menu from x 733, both at y 435); in day view, where the selection spans the column and there's no room to its right, it opens level with it over its left part (x 392, y 634), not in the corner. The selection says `aria-expanded="true"`, and Astryx's anchor warning is gone. The button's text sits where the dragged highlight's does (1 px from the top), so nothing jumps on release. A page test checks the button, its name and `aria-expanded`; the E2E journey checks the menu is level with the selection.

### 2. Closing the menu by clicking another control sends the focus back to the grid

- **Severity:** low. **Confidence:** high (measured).
- **What could go wrong:** after a drag, someone changes their mind and clicks into Go to date to type a date. The menu closes, but the focus jumps back to a grid slot, which shows no focus ring after a click. What they type goes to the grid instead of the field:
  - nothing appears in the field
  - the arrow keys move around the grid
  - Enter selects a slot and opens the menu again.
  - **Measured, the same with other controls:** Whose calendar opens its list, and New opens its menu, but the focus stays on the slot in both.
- **Why:** `releaseSelection` runs on every close and always focuses the slot (`CalendarPage.tsx:159-162`, called from `:343-345`). When the close comes from a press on another control, the browser has already moved the focus there, and this takes it back. It predates this PR, but before it a mouse user never had the menu open to close.
- **Evidence:** `CalendarPage.tsx:152, 159-162, 343-345`.
- **Fix:** return the focus to the slot only when the menu closes with the focus still in it, or lost to the page (`document.body`): Escape, or the hidden Close button. A press on another control then keeps the focus it moved. In the journey, or a new one, click Go to date while the menu is open and expect the date field to have the focus.
- **What happened to it:** fixed in the PR. Closing the menu gives the focus back to the selected time only if nothing else has it: the page's body, or something on its way out (the closing menu, marked `data-selected-time-menu`, or the selection). Measured: pressing Today with the menu open leaves the focus on Today; Escape puts it back on the selected time. A page test covers Escape.

### 3. The journey leaves its block behind if it fails part-way, and assumes free time it didn't make

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **No clean-up:** the journey makes a block through the UI (`BlockTimeTests.cs:50`) and deletes it through the UI at the end (`:57-61`), but registers no clean-up with `Data.OnCleanUp`. The README asks for one for things made through the UI too (`MedApp.E2ETests/README.md:77`).
    - **Failing after the save leaves the block:** say the block's name changes, so the locator at `:54` finds nothing, or the Change Blocked Time dialog changes. A 14:00 to 15:00 block then stays on the E2E account's calendar: locally the dev `admin`, and through `e2e.yml` a deployed admin.
    - **The next run hides the real error:** every rerun that day then fails at its first step, waiting for "…, 14:00, free".
  - **Free time it didn't make:** the journey needs 14:00 to 15:00 free on the account's calendar three weeks ahead (`:21-28`), against the README's "Rely on nothing you didn't make" (`:78`).
    - **True today:** in CI (a fresh database, no week template), and locally (checked: no account has a default week template).
    - **A deployment can differ:** an admin whose default week blocks that hour fails it.
    - **Bank holidays:** they aren't known yet (`MedApp.Application/Calendar/Common/WorkingHours.cs:6`). Once they are, a run 21 days before Christmas or Boxing Day lands on one.
  - **Two runs at once** against one database pick the same hour, and the second fails, or finds two blocks.
- **Evidence:** `MedApp.E2ETests/Calendar/BlockTimeTests.cs:21-28, 50, 54-61`; `MedApp.E2ETests/README.md:77-78`.
- **Fix:**
  - **Clean up by a unique note:** type a note from `E2EData.UniqueName("Block")` in Block Time. Straight after saving, register a clean-up that reads the account's calendar for that day (`GET /api/calendar?From=…&To=…`) and deletes blocks with that note (`DELETE /api/calendar/blocks/{id}`). Add both calls to `E2EApi`.
  - **Optionally, find free time first:** pick the hour from that day's free time through the API, rather than assume 14:00.
- **What happened to it:** fixed in the PR. The journey finds an hour that's free through the API first (a working day three to six weeks ahead with no block, from a template or not, and no appointment, trying 14:00, 10:00, 15:00, 11:00, 16:00 and 09:00), and registers a clean-up before making anything, which deletes any block at exactly that hour however the test ends. Two new `E2EApi` helpers: `CalendarAsync` and `DeleteBlockAsync`. It passes against the AppHost.

### 4. A unit test's "nothing selected" check can no longer see a pointer selection

- **Severity:** low. **Confidence:** high (shown with a temporary test).
- **What could go wrong:** "ignores other buttons, and a pointer that is cancelled or never moves far" presses the right mouse button and lets go (`CalendarGrid.test.tsx:401-402`). Then it checks `expect(onSelectRange).not.toHaveBeenCalled()` at once (`:408`).
  - **A selection now arrives a moment later.** So if the right-button guard (`CalendarGrid.tsx:350`) were removed, that release would make a selection, reported only after the check had run. The test would still pass.
  - **Shown with a temporary test:** a left press and release, then the same immediate check, passed (How it was checked).
- **Evidence:** `MedApp.Web/src/components/calendar/CalendarGrid.test.tsx:401-408`; `CalendarGrid.tsx:350, 373`.
- **Fix:** before a "not called" check that follows a pointer release, let the timer run, for example `await act(() => new Promise((resolve) => setTimeout(resolve, 0)))`, or use fake timers there and `vi.runAllTimers()`.
- **What happened to it:** fixed in the PR. The test waits past the deferred commit (20 ms) before checking that nothing was selected.

### 5. The comment and the description blame the click; the release alone closed the menu

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the comment says a menu opened during the release "would otherwise take the release, and the click after it, as a press outside it". The PR description says the same.
  - **What actually closes it:** the menu is a native `popover="auto"` (`Layer/useLayer.js:1755`). The browser light-dismisses it at the release, judged by where the press began. Astryx says so itself: "Light dismiss runs as the pointerup default action after listeners" (`useLayer.js:810`).
  - **The click plays no part:** nothing in `Popover` closes on a click. On touch, the click came before the menu opened, and it didn't matter (How it was checked).
  - **Why it matters:** someone reading the comment would rule out committing on `click`, which would also work. They might also look for a click handler that isn't there.
- **Evidence:** `CalendarGrid.tsx:370-371`; the PR description's "The cause".
- **Fix:** for example: "The browser closes a light-dismiss popover when a press that began outside it is released, so a menu opened during this release would close at once."
- **What happened to it:** fixed. The comment now says the release itself closes the menu, and the PR description was corrected, including the anchor warning, which did affect the menu.

## Remaining risks

- **The menu's place** (finding 1), until it's anchored.
- **Changing the time takes two clicks:** clicking another free time while the menu is open closes the menu and drops the new selection (10 of 10 in Chromium).
  - **Why:** two things the browser schedules separately race: the popover's close event, and the timer. The close ran last each time.
  - **Other browsers** could order them the other way and reopen the menu at the new time. Either outcome is reasonable, but it isn't designed.
- **Touch can tap, not drag:** a finger dragging over free time scrolls the grid, the browser cancels the pointer, and nothing is selected. The slots have no `touch-action: none`. This is from before this PR, but `MedApp.Web/AGENTS.md` and the PR say a drag selects.
- **The timer isn't cancelled:** not when the grid unmounts, and not when a new press starts.
  - **A stale selection could be reported:** with a second press inside the same few milliseconds as the release, or with the grid replaced (say, by its error message) in that gap.
  - **Not reproducible by hand.** A ref, and `clearTimeout` on unmount and on the next press, would close it.
- **A second finger:** lifting a second finger reports the first finger's selection while it's still down. `onColumnPointerUp` doesn't check which pointer it is. From before this PR.
- **The menu doesn't say which time:** its name is "Selected time", so a screen reader user hears "Selected time", then "Book appointment", without the time.
- **Not tested:** Firefox and Safari, real touch and pen hardware, and screen readers.

## Say it like this

"Dragging over free time on the calendar opened a Book or Block menu that closed instantly, because the browser counted the same mouse release that opened it as a click outside it. The grid now reports the selection a moment later, with a zero-delay timer, after the browser has finished with that press. The trade-off is that pointer selections now arrive slightly later than keyboard ones, which code and tests have to allow for, and the review found the menu still needs a proper anchor to sit beside the selection."

## Detail

### The order of events, before and after

| Step | Before | After |
|---|---|---|
| 1. Press (`pointerdown`) on a free slot | The selection starts, and the column holds the pointer | Same |
| 2. Release (`pointerup`), the grid's handler | Reports the selection. React draws at once (a discrete event), and the `Popover` opens inside this handler | Saves the selection and starts a zero-delay timer |
| 3. Release, the browser's own step | Light dismiss: the press began outside the open menu, so it closes | Nothing is open, so nothing happens |
| 4. `click` | Nothing | Nothing |
| 5. The timer | (none) | Reports the selection. React draws, and the menu opens with the press over |

On touch, the click from a tap can come later than step 5. It doesn't matter: light dismiss happens at the release, not the click.

### Measured in Chromium (1440 x 900)

| Input | Menu stays open | Focus after it opens | Menu box (x, y, w x h) | Selection box (x, y) |
|---|---|---|---|---|
| Mouse drag, day view | Yes | Book appointment | 4, 0, 175 x 74 | 351, 370 |
| Mouse drag, week view | Yes | Book appointment | 4, 0, 175 x 74 | 542, 370 |
| Mouse click | Yes | Book appointment | 4, 0, 175 x 74 | 351, 699 |
| Touch tap | Yes | Book appointment | 4, 0, 175 x 74 | 351, 282 |
| Pen tap | Yes | Book appointment | 4, 0, 175 x 74 | 351, 546 |
| Keyboard (Enter) | Yes | Book appointment | 4, 0, 175 x 74 | 351, 216 |

### Where to look in Astryx 0.7.15

- `Popover/Popover.js:564-574`: the anchor (`anchorRef`) must be or contain a button, or the popover is never anchored.
- `Layer/useLayer.js:1755`: `popover="auto"` when light dismiss is on, which the browser closes by itself.
- `Layer/useLayer.js:810`: "Light dismiss runs as the pointerup default action after listeners."
- `Layer/useLayer.js:1086` and `Layer/gestureCounter.js`: a guard that stops a menu reopening from the same press that closed it. It doesn't affect this fix with the mouse, where the click has passed before the menu closes.
