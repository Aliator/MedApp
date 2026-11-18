# Review: PR #120, a nav row's menu pinned open by a click

**PR:** [#120](https://github.com/Aliator/MedApp/pull/120), branch `feature/pin-row-menu`. Reviewed at `a40deb3e`, compared with `main` at `1b00eaee` (the merge base is `2e40f39c`, the merge of #116; #115 has merged since, touching none of these files).

**Reviewed:** 18 November 2026, at high effort, as a small change. An independent, adversarial review, without the author's conclusions. Each claim below was checked against the code, Astryx's own source in `node_modules`, or temporary Vitest probes (written, run and removed). No browser, AppHost or Docker was started, by the project's rules. **No finding is high or critical:** 1 is medium, 4 are low. All five are fixed in the PR.

**Is it a small change?** Yes. 4 files in `MedApp.Web/src/components/navigation`, 271 lines added and 13 removed, most of them tests. No endpoint, table, migration or dependency, and nothing about sign-in, security, privacy, patient data, clinical safety, CI or deploying. So a review at high is right.

## Summary

A row's menu in the side nav (the ☰ button beside a row) now stays open when you click it, instead of only while the pointer is on it. In the folded nav, a menu pinned that way also keeps its row's flyout out. The idea and the flyout hold are sound. But the click a press leaves behind re-pins a menu that the same press just closed, so the next time the pointer rests on the button, the menu won't close when it leaves (finding 1). A one-function change fixes it and two smaller gaps with it.

## What changed, and why

- **A "pinned" state for each row's menu** (`AppSideNav.tsx:222-232`, `isPinned`).
  - **What it is:** until now, a row's menu opened when the pointer rested on its button for 150 ms and closed 400 ms after it left. That's fine for a glance, but awkward when you want to read or reach the menu. Now a click (or Enter, Space or an arrow key) *pins* it: like pinning a note to a board rather than holding it up, it stays until you take it down.
  - **What closes it:** a second press, Escape, a click elsewhere, or choosing an item. All of those reach the component as Astryx's `onOpenChange(false)`, which unpins (`:357`).
  - **Leaving doesn't close a pinned menu** (`:320-323`): the close timer isn't started.
- **The button's press, click and key handlers** (`:369-387`).
  - **Closed:** a press or click opens it, pinned.
  - **Open because the pointer rested on it:** a press pins it, rather than closing it (before this PR, a press left it open but unpinned).
  - **Pinned:** a press goes through Astryx's own handling, which closes it.
- **Keeping the browser from closing the menu on release** (`holdInvokerThroughPress`, `:184-205`).
  - **What light dismiss is:** the menu is a `popover="auto"` element, which the browser shows in its *top layer* (above every z-index). The browser closes such a popover by itself when you press and release outside it ("light dismiss"), like a dropdown that closes when you click on the page. The button is outside the menu, so releasing a press on it counts as outside.
  - **The exception:** the browser doesn't light-dismiss when the press is on the popover's *invoker*, the element whose `popovertarget` attribute names it. Astryx uses that trick for the press that opens a menu (`DropdownMenu.js:262-283, 1008-1015`). This PR skips Astryx's handler for a press on a hover-opened menu (Astryx's would close it), so it copies the trick: the button is made the invoker during that press, and the attribute is removed a task after the release. Astryx's click capture (`DropdownMenu.js:1103-1111`) removes it early and stops the click toggling the menu.
- **Holding a folded nav's row out** (`NavRail.tsx:36-41, 146-165, 272-291, 332`).
  - **What it is:** in the folded nav (the narrow strip of icons, as in a half view), hovering an icon brings its row out over the page. A pinned menu inside that row needs the row to stay, or the menu goes with it.
  - **How:** each brought-out row gets a `FlyoutHoldContext` with a `hold(isHeld)` function. A pinned menu calls `hold(true)` from an effect while it's open and pinned, and `hold(false)` when that ends. While held, leaving the item doesn't close the row. Letting go closes it, unless the pointer is on it (`isHovered`).
  - Moving to another icon still swaps to that icon's row at once (`NavRail.tsx:76-82`); the browser then closes the pinned menu, which is nested in the old row's popover.
- **Tests** (`AppSideNav.test.tsx:394-542`, `NavRail.test.tsx:35-82`): pinning by press, by pressing a hover-opened menu, and by keyboard; the invoker attribute set and removed; the folded row held and let go; the hold function on its own.

## Design choices

- **Pinned state inside the component, rather than Astryx's controlled state alone:**
  - **Instead:** let a click open the menu as Astryx does and use no hover opening at all.
  - **Chosen because:** hover opening is the existing behaviour, quick for a glance, and the PR keeps it.
  - **Worse at:** two ways of opening means two states to keep in step with Astryx's press model, and it's that bookkeeping that slips (findings 1 to 3).
- **Copying Astryx's invoker trick, rather than letting Astryx's press handler run:**
  - **Instead:** call `trigger.onPointerDown` for every press, and reopen the menu after Astryx closes it.
  - **Chosen because:** a close then reopen would flicker, move focus and replay the entrance animation.
  - **Worse at:** it depends on two private details of Astryx (the attribute's timing, and its click capture removing it). jsdom has no light dismiss, so no test can catch an Astryx upgrade that changes either; only a browser can.
- **A hold context from the flyout, rather than the menu reaching into `NavRail`'s state:**
  - **Chosen because:** the row knows nothing about the rail; the context is null in the open nav, so the same `RowMenu` works in both.
  - **Worse at:** `hold(false)` decides "is the pointer on it?" from `mouseover` and `mouseleave` events alone, so it's only as accurate as those events (see finding 4's note on jsdom).

## How it was checked

- **Vitest, as the PR stands:** `AppSideNav.test.tsx` and `NavRail.test.tsx`: 71 tests, all pass.
- **Temporary probes** (added to the test files, run, then removed):
  - **A menu closed by a second press, then hovered:** it opens on hover, but stays open 600 ms after the pointer leaves. Fails as the PR stands (finding 1).
  - **A right-click before the hover delay:** the menu opens 150 ms later and stays open after the pointer leaves. Fails as the PR stands (finding 2).
  - **Enter on a pinned menu's focused button:** the menu stays open. Fails as the PR stands (finding 3).
  - **The suggested fix** (in finding 1) applied: all three pass, and so do all 61 of the file's own tests (64 in all); a real mouse press (`user.click`) still pins.
  - **The "let go while the pointer is on it" test** with the `isHovered` check removed: it still passes (finding 4). With a click that doesn't move the pointer and a 300 ms wait, it passes with the check and fails without it.
- **Astryx's source**, read for: the trigger's props, the press model (`hooks/useMenuPress.js:820-882`: only a primary, non-Ctrl press is handled), `handleTriggerPress`, `handleButtonClick` (a press's click is "spent"), the click capture, the keyboard handler, and the popover mode (`Layer/useLayer.js:1755`).
- **CI** on the PR: every job passed (`web`, `unit-tests`, `integration-tests`, `e2e`, `image`, `helm`, `dependency-review`, CodeQL).
- **Not done here:** no browser. The PR says it checked hover, pinning, a second click, Escape and the folded nav in Chromium; it doesn't say it hovered the button again after closing one by a click.

## Findings

### 1. Medium: a menu closed by a second click is left pinned, so the next hover opens a menu that won't close by itself

- **Confidence:** high (reproduced in Vitest; the cause is in the order of events, which is the same in a browser).
- **What could go wrong:** you click a row's ☰, read the menu, and click ☰ again to close it. Later you rest the pointer on it, glance, and move on. The menu stays open on top of the page. In the folded nav, the row's flyout stays out too. It closes only with Escape, a click, or choosing an item.
- **Why:** the second press goes to Astryx, which closes the menu at once (`handleTriggerPress`, `DropdownMenu.js:1021-1031`), and `onOpenChange(false)` unpins it. React re-renders before the click that follows the press, so `onClick` sees a closed menu and takes the "closed" branch: `setIsPinned(true)`, then `trigger.onClick`, which does nothing because Astryx marks that click as spent (`DropdownMenu.js:1080-1084`). The menu is closed but pinned. The next hover opens it with `isPinned` already true. The comment at `:367-368` says the click after is spent; it is for Astryx, but not for this handler.
- **Evidence:** `AppSideNav.tsx:374-378`; the probe in "How it was checked". The PR's tests miss it because they open with `fireEvent.pointerDown` without a pointer type, which Astryx ignores (`useMenuPress.js:832-835`), and never hover again after a second press.
- **Fix:** pin only when the menu actually opened during the handler. One helper does it for the press, the click and the keys, and also fixes findings 2 and 3 (`trigger.onClick` closes a pinned menu from the keyboard; for a mouse it's spent, as before):

  ```diff
  @@ RowMenu
     const [isPinned, setIsPinned] = useState(false);
  +  // Set when the menu reports opening, so a press or a key pins it only if it really opened it (not a right-click,
  +  // and not the click a press that closed it leaves behind).
  +  const didOpen = useRef(false);
     const holdFlyout = useContext(FlyoutHoldContext);
  @@
  +  const openPinned = (open: () => void) => {
  +    didOpen.current = false;
  +    open();
  +    if (didOpen.current) setIsPinned(true);
  +  };
     const setSoon = (open: boolean, delay: number) => {
  @@ onOpenChange
             window.clearTimeout(timer.current);
  +          didOpen.current = open;
             setIsOpen(open);
  @@ the button
               onPointerDown={(event) => {
  -                if (!isOpen) setIsPinned(true);
  -                if (!isOpen || isPinned) trigger.onPointerDown(event);
  +                if (!isOpen) openPinned(() => trigger.onPointerDown(event));
  +                else if (isPinned) trigger.onPointerDown(event);
                   else holdInvokerThroughPress(event.currentTarget);
               }}
               onClick={(event) => {
  -                if (!isOpen) {
  -                  setIsPinned(true);
  -                  trigger.onClick(event);
  -                } else if (!isPinned) {
  +                if (!isOpen) openPinned(() => trigger.onClick(event));
  +                else if (!isPinned) {
                     window.clearTimeout(timer.current);
                     setIsPinned(true);
  -                }
  +                } else trigger.onClick(event);
               }}
               // Opened from the keyboard, it's pinned too.
  -              onKeyDown={(event) => {
  -                if (!isOpen && ['Enter', ' ', 'ArrowDown', 'ArrowUp'].includes(event.key)) setIsPinned(true);
  -                trigger.onKeyDown(event);
  -              }}
  +              onKeyDown={(event) => (isOpen ? trigger.onKeyDown(event) : openPinned(() => trigger.onKeyDown(event)))}
  ```

  This relies on Astryx reporting the open during the call (`openAndFocus` checks `acceptedOpenRef` right after `popover.show()`, `DropdownMenu.js:990-998`). When a show has to wait for another popover, the menu opens unpinned, which is the safe side. Add the probe as a test: open with `user.click`, close with `user.click`, hover again, leave, and expect it closed.
- **What happened to it:** Fixed in the PR, with the suggested `openPinned`: a press, click or key pins the menu only when the menu reported opening during it. A test opens and closes it with real clicks, then hovers and leaves (closed). Checked in Chromium too: after a second click closes it, resting on the button opens it and leaving closes it.

### 2. Low: a right-click, middle-click or Ctrl-click on a closed button pins the menu without opening it

- **Confidence:** high (reproduced in Vitest; read from Astryx's press model).
- **What could go wrong:** you rest the pointer on ☰ and right-click within 150 ms (or Ctrl-click on a Mac). Astryx ignores the press (`useMenuPress.js:832-840`), but `onPointerDown` has already set `isPinned` (`AppSideNav.tsx:370`). The hover timer then opens the menu, pinned: it stays when the pointer leaves, and holds a folded row out. A touch that starts on the button and turns into a scroll does the same.
- **Evidence:** `AppSideNav.tsx:369-373`; the probe in "How it was checked".
- **Fix:** the same `openPinned` change as finding 1, which pins only when the menu opened.
- **What happened to it:** Fixed in the PR by the same change. A test right-presses the closed button, then hovers and leaves: the menu opens and closes as an unpinned one.

### 3. Low: Enter, Space or a screen reader's activation on a pinned menu's button doesn't close it

- **Confidence:** medium (reproduced in Vitest for Enter; not tried with a screen reader).
- **What could go wrong:** a menu button is expected to toggle its menu (the WAI-ARIA menu button pattern). With focus back on a pinned menu's button (Shift+Tab, or a pointer-opened menu), Enter or Space fires a click. `onClick`'s pinned case does nothing (`AppSideNav.tsx:374-382`), so the menu stays open. A mouse press closes it, so the PR's "a second click closes" is true for the mouse only. Escape still closes it, so nobody is stuck. A click that leaves an open menu open is from before this PR, but this PR makes closing by the button a feature.
- **Evidence:** `AppSideNav.tsx:374-382`; Astryx's keyboard handler leaves Enter and Space on an open menu to the click (`DropdownMenu.js:1129-1134`).
- **Fix:** `else trigger.onClick(event);` for the pinned case, as in finding 1's diff. Astryx's click handler ignores a mouse press's spent click, so only keyboard and assistive technology clicks close it there.
- **What happened to it:** Fixed in the PR: a pinned menu's click goes to the menu's own click handling, which ignores a press's spent click. A test closes a pinned menu with Enter on its button.

### 4. Low: two tests can't fail for the reason they give

- **Confidence:** high (each checked by removing the code it's meant to cover).
- **What could go wrong:**
  - **"keeps a row out when let go while the pointer is still on it"** (`NavRail.test.tsx:67-82`) checks the row is visible straight after `hold(false)`. Closing waits 150 ms (`CLOSE_DELAY_MS`), so it passes with the `isHovered` check removed (`NavRail.tsx:164`). Adding a wait isn't enough on its own: in jsdom, `user.click` moving onto the row's button fires the item's `mouseleave`, so the row closes even with the check.
  - **The press tests** (`AppSideNav.test.tsx:408, 448, 463`) open with `fireEvent.pointerDown(button)`, which has no pointer type, so Astryx ignores it and the click opens the menu. The real mouse path (Astryx opening on the press, then the spent click) is never run, and that's where finding 1 is.
- **Evidence:** the probes in "How it was checked".
- **Fix:**
  - In the NavRail test, click without moving the pointer and wait past the close delay. Checked: passes with the code, fails without the `isHovered` check.

    ```diff
    -    await user.click(await screen.findByRole('button', {name: 'Hold and let go'}));
    +    // No pointer movement: the pointer stays on the item.
    +    fireEvent.click(await screen.findByRole('button', {name: 'Hold and let go'}));
    +    await new Promise((resolve) => setTimeout(resolve, 300));
         expect(screen.getByRole('button', {name: 'Hold and let go'})).toBeVisible();
    ```
  - Add a test that opens and closes with `user.click` (a real mouse press), as in finding 1.
- **What happened to it:** Fixed in the PR: the "let go" test now clicks with no pointer movement and waits past the row's close delay (it fails without the `isHovered` check), and the new tests above use real presses.

### 5. Low: `RowMenu`'s description now sits on the wrong function, and doesn't mention pinning

- **Confidence:** high.
- **What could go wrong:** `holdInvokerThroughPress` and its comment were inserted between `RowMenu`'s doc comment and `RowMenu` (`AppSideNav.tsx:177-207`). Editors show the nearest comment, so hovering `holdInvokerThroughPress` shows two descriptions, and `RowMenu` shows none. That description also still says the menu "closes once the pointer has left the button and the menu", which is no longer true once it's pinned.
- **Evidence:** `AppSideNav.tsx:177-182, 183-190, 207`.
- **Fix:** move `holdInvokerThroughPress` (with its comment) above `RowMenu`'s comment, and add a sentence to the description: "A click, or the keyboard, pins it open until it's closed."
- **What happened to it:** Fixed in the PR: `holdInvokerThroughPress` moved above `RowMenu`'s description, which now says how a click pins the menu.

## Remaining risks

- **The invoker trick depends on Astryx's internals.** It copies a private Astryx function and relies on Astryx's click capture. jsdom has no light dismiss, so only a browser check would catch an Astryx upgrade that breaks it: the menu would close on the release of the pinning press.
- **Touch wasn't checked.** A tap on a pinned menu's button is closed by light dismiss, not by Astryx's press model (a finger's press on an open menu does nothing there). Whether React sees that before the click decides whether it reopens. Worth one try on a phone-sized window with touch emulation.
- **One shared close timer in `NavRail`.** `hold(false)` for a row that's no longer out calls `hideSoon` for it, which clears whatever close is pending, possibly another row's. The window is a tick wide (the pinned menu closes in the same render as the swap), and the shared timer is from before this PR.
- **Unmounting while held** (the nav unfolding while a menu is pinned) can leave one 150 ms timer that sets state on an unmounted `NavRail`. Harmless in React 19.
- **No browser test** covers hover menus or pinning; only the PR's scripted Chromium run has.

## Say it like this

"A row's menu in the side nav used to open only while you hovered it; now a click pins it open, and in the folded nav it keeps its row out too, until you click again, press Escape or click elsewhere. The tricky part was the browser's own 'click outside closes it' behaviour, which counted the click on the button as outside, so the button is briefly made the menu's official opener, the same trick the design system uses. The trade-off is two ways of opening the same menu, hover and click, which have to be kept in step: the review found the click after a closing press re-pinned the menu, fixed by pinning only when the menu has actually opened."

## Detail

### The order of events on a second press (finding 1)

1. `pointerdown` on the button: `RowMenu` sees `isOpen && isPinned`, calls `trigger.onPointerDown`.
2. Astryx's press model calls `handleTriggerPress('mouse')`: the menu is open, so it calls `onOpenChange(false)` and marks the gesture's click as spent.
3. `RowMenu`'s `onOpenChange(false)`: `setIsOpen(false)`, `setIsPinned(false)`. These are updates in a discrete event (a press), so React renders before the next event.
4. `click`: the new `onClick` sees `isOpen === false`, so `setIsPinned(true)` and `trigger.onClick(event)`, which returns at once (`isTriggerClickFromPress()`).
5. Result: closed, but pinned. Nothing clears it until the menu next closes.

### Every way in and out, after the suggested fix

| How | Closed | Open from hovering | Pinned |
|---|---|---|---|
| **Hover, then leave** | Opens after 150 ms, unpinned | Closes 400 ms after leaving | Stays |
| **Mouse press** | Opens pinned (Astryx, on the press) | Pins (invoker trick) | Closes (Astryx) |
| **Right or Ctrl-click** | Nothing | Nothing (invoker set, then removed) | Nothing |
| **Enter, Space, arrows** | Opens pinned, focus on the first item | Enter or Space pins (the click); ArrowDown moves into the menu | Enter or Space closes |
| **Escape, a click elsewhere, an item** | Not applicable | Closes | Closes, unpins, lets the folded row go unless the pointer is on it |
| **Another icon in the folded nav** | Swaps rows | Swaps; the menu closes with its row | Swaps; the menu closes with its row, then lets go |

### Accessibility

- `aria-expanded` and `aria-controls` still come from Astryx's trigger props; pinning changes neither.
- Pinning is invisible to a screen reader, which is fine: it only changes what the pointer does.
- A pinned menu meets WCAG 1.4.13 (content on hover stays until dismissed, and Escape dismisses it) better than a hover-only one.
