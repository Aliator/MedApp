# Review: PR #116, a nav row's menu kept open under a slow pointer, a thinner bell count, and even half views

**PR:** [#116](https://github.com/Aliator/MedApp/pull/116), branch `feature/nav-menu-gap`. Reviewed at `48fbadf5`, compared with `main` at `1e1a33a9` (also the merge base).

**Reviewed:** 18 November 2026, at high effort, as a small change. An independent, adversarial review, without the author's conclusions. Each claim below was checked against the code, Astryx's own source in `node_modules`, or temporary Vitest probes (written, run and deleted). No browser, AppHost or Docker was started, by the project's rules. **No finding is high or critical:** all 5 are low.

**Is it a small change?** Yes. 5 files in `MedApp.Web/src/components/navigation`, 37 lines added and 11 removed. No endpoint, table, migration or dependency, and nothing about sign-in, security, privacy, patient data, clinical safety, CI or deploying. So a review at high is right.

## Summary

Three fixes from using the app. A row's menu in the side nav no longer closes when the pointer crosses slowly from its button to the menu. The bell's unread count has less padding. And the two halves of a half view are now equal, whatever is on each page. The menu fix is sound and survives a menu closing mid-animation. The gaps are small: its tests don't run the part that fixes the reported bug, the bell's "no padding above or below" changes nothing, and two comments give the wrong reason.

## What changed, and why

- **The invisible strip across the gap to a row's menu** (`AppSideNav.tsx:211-267`).
  - **What it is:** a row's menu opens beside the nav, a few pixels off its edge. Leaving the button for the menu crosses that gap, which belongs to neither, so the menu would close. Since #113, an invisible strip (the *bridge*) covers the gap while the menu is open. It's in the row's React tree (a portal), so the pointer on it still counts as on the row. Like a plank across a ditch: you're never off the path.
  - **The bug:** the strip was measured one frame after the menu was moved into place, while the menu was still sliding in. So it stopped about 8 px short of where the menu came to rest. A slow pointer stood in that hole for over 200 ms, and the menu closed.
  - **The fix, three parts:**
    - **Measured again once the menu settles** (`:242-254`): after the first measure, it asks the menu's surface for its running animations (`getAnimations({subtree: true})`), waits for them all to end (`Promise.allSettled` of each `finished`), then measures again. Astryx plays a menu's entrance on that surface (`DropdownMenu.js:787-789`), so this is the right element.
    - **Wider:** the strip now reaches 24 px *into* the menu and 8 px above and below both (`BRIDGE_OVERLAP_PX`, `BRIDGE_MARGIN_PX`, `:172-175`). The menu is a `popover` in the browser's *top layer* (a layer above every z-index), so the part of the strip under it never gets the pointer. That overlap hides any small error in where the menu settles.
    - **Slower to close:** 400 ms after the pointer leaves, not 200 (`MENU_CLOSE_DELAY_MS`, `:171`).
  - **A cancelled flag** (`isCancelled`, `:215, 243, 262-265`): if the menu closes (or the colour mode changes) before the animations end, the late re-measure does nothing.
- **The bell's count** (`NotificationBell.tsx:50-52`): `paddingBlock: 0` and `paddingInline: var(--spacing-1)` on the count's `Badge`. See finding 1: only the side padding actually changes.
- **Even half views** (`AppLayout.tsx:22-28`): `flexBasis: 0` and `minWidth: 0` on each half.
  - **What flex-basis is:** a flex item's starting width, before spare room is shared out. `auto` means "start from my content's width". With both halves on `auto`, a page with a wide part (the notepad's editor toolbar) started wider, and stayed wider: 858 px against 533 at 1,440 px, by the PR's measure. With `0`, both start from nothing and share the room equally, like two people splitting a bill evenly instead of by what each ordered.
  - The same `HALF` style is used for the page beside the docked notepad with no half view (`AppLayout.tsx:227`). There, the notepad's column doesn't grow or shrink, so the page gets the rest either way: no change.
- **Tests** (`AppSideNav.test.tsx:478-484`, `AppLayout.test.tsx:85-86`): the strip's new geometry (228 to 324 across, 92 to 228 down), a 600 ms wait instead of 400, and each half's `minWidth` and `flexBasis`.

## Design choices

- **Measure again after the animations, rather than measure without the animation's offset:**
  - **Instead:** read the box where the menu *will* settle, ignoring the transform the entrance applies. Astryx has exactly that internally (`Layer/settledBox.js`), but doesn't export it for apps.
  - **Chosen because:** it uses only public browser APIs, and the 24 px overlap covers the frames before the second measure.
  - **Worse at:** for the length of the entrance (a fraction of a second), the strip is the mid-slide one; and it waits on every animation in the menu, including any that never end (finding 3).
- **A longer close delay (400 ms):**
  - **Instead:** keep 200 ms and rely on the strip alone.
  - **Chosen because:** belt and braces: a pointer that wobbles off the strip still has time to get back. 300 to 500 ms is the usual range for menus that open on hover.
  - **Worse at:** a menu stays 0.4 s after the pointer has gone onto the page. It can't leave two menus open: the menus are `popover="auto"`, so opening another row's menu closes this one at once (`Layer/useLayer.js:1755`, and the browser's popover stack). A click on the page in that time closes it too (light dismiss).
- **Equal halves, rather than halves sized by content:**
  - **Chosen because:** a half view promises halves; the divider shouldn't move with what's on each page.
  - **Worse at:** a page with something wider than its half now gets exactly half, so more of it may be cut off (finding 5).

## How it was checked

- **Vitest:** `AppSideNav.test.tsx`, `AppLayout.test.tsx` and `NotificationBell.test.tsx`: 3 files, 84 tests, all pass.
- **Type check:** `npm run typecheck`, clean.
- **Two temporary probes** (in `src/components/navigation`, deleted after), stubbing `HTMLElement.prototype.getAnimations`, which jsdom lacks:
  - With an animation pending, the strip is first 96 px wide; the menu's box then moves from 300 to 310 and the animation finishes; the strip becomes 106 px. **The re-measure works.**
  - With an animation pending, Escape closes the menu; the animation then finishes with the menu's box moved: no strip comes back, and React logs nothing. **The cancelled flag holds.**
- **Astryx's source**, read for: where the entrance plays (the surface), the popover mode (`auto`), `StackItem`'s styles (`Stack/stackItem.stylex.js`) and `Badge`'s (`src/Badge/Badge.tsx:36-60`).
- **CI** on the PR: `unit-tests`, `image`, `helm`, `dependency-review` and CodeQL passed; `web`, `integration-tests` and `e2e` were still running when this was written.
- **Not done here:** no browser, so no screenshots of the bell or a half view. The PR says it checked all three in a browser against the dev API.

## Findings

### 1. Low: the bell's count is no shorter; "no padding above or below" changes nothing, and a single digit is now taller than it is wide

- **Confidence:** medium (read from Astryx's source; not seen in a browser).
- **What could go wrong:** the request was a less padded count. Astryx's `Badge` already has `paddingBlock: 0` and a fixed height of `--spacing-5`, 20 px (`node_modules/@astryxdesign/core/src/Badge/Badge.tsx:42-44`). So the new `paddingBlock: 0` does nothing, and the count stays 20 px tall. The only real change is the side padding, from 8 px to 4 px. A count of 1 to 9 is then about 15 px wide and 20 px tall: an upright capsule rather than a round dot. If what looked too padded was its height, it isn't fixed.
- **Evidence:** `NotificationBell.tsx:50-52`; the comment at `:50` says "no padding above or below the number".
- **Fix:** decide which was meant. To make it shorter, set its height (`blockSize: 'var(--spacing-4)'`) and a matching `minInlineSize`, so one digit is a circle; or keep the side padding change and drop `paddingBlock` and the comment's claim. Left to the author.
- **What happened to it:** fixed in the PR: the count is 16px tall (`blockSize` and `lineHeight` `var(--spacing-4)`, from Astryx's 20) and at least as wide as it's tall, with 4px either side; measured in a browser at 19 by 16 for "13" (a single digit is a 16px circle). The comment says what it does.

### 2. Low: the tests don't run the part that fixes the reported bug

- **Confidence:** high.
- **What could go wrong:** jsdom has no `getAnimations`, so the test never takes the re-measure path or the cancelled flag (`AppSideNav.tsx:243, 252-253`). Deleting both would leave every test green. The longer wait (600 ms) doesn't test the close delay either: hovering the strip never leaves the row in React's tree, so the menu would stay open with any delay. What's tested is the strip's new size.
- **Evidence:** `AppSideNav.test.tsx:478-485`. The two probes under "How it was checked" show both paths work and are short to add.
- **Fix:** add the two probes as tests (stub `getAnimations` on the surface, move the menu's box, finish the animation, check the strip's width; close before it finishes, check no strip). For the delay, unhover the button onto the page and check the menu is still open before 400 ms (fake timers). Left to the author.
- **What happened to it:** fixed in the PR: a new test reports two animations from the menu's surface (one ending, one endless), checks the strip first measured mid-animation, then measured again once the ending one finishes and the menu has moved (96px, then 106px).

### 3. Low: one never-ending animation in a menu stops the re-measure

- **Confidence:** medium (no row menu has one today, as far as the code shows).
- **What could go wrong:** `getAnimations({subtree: true})` returns every animation in the menu, including infinite ones (a spinner, a pulsing badge). An infinite animation's `finished` never settles, so `Promise.allSettled` never resolves, and the strip keeps its mid-slide size: the dead zone this PR fixes comes back for that menu. Astryx guards against exactly this in its own wait, keeping only animations that end on their own (`Layer/useLayer.js:229-245`, `endsOnItsOwn`).
- **Evidence:** `AppSideNav.tsx:252-253`.
- **Fix:** keep only animations that end: `.filter((a) => a.effect?.getComputedTiming().endTime !== Infinity)`. Left to the author.
- **What happened to it:** fixed in the PR: only animations that end by themselves (`getComputedTiming().endTime` finite) are waited for, as Astryx's own wait does; the same test covers an endless one.

### 4. Low: the half views' comment gives the wrong cause, and `minWidth: 0` was already there

- **Confidence:** high.
- **What could go wrong:** the comment says each half "won't shrink below its narrowest". But Astryx's `StackItem` already sets `min-width: 0` on every item (`Stack/stackItem.stylex.js`, `minSizeResetStyles`, applied by `stackItem()` to all sizes). The real cause was `flex-basis: auto`: each half started from its content's width. So `minWidth: 0` is a no-op, and someone later trusting the comment could look in the wrong place. The new test's `minWidth` check reads only the inline style, so it can't tell either way.
- **Evidence:** `AppLayout.tsx:25-28`; `AppLayout.test.tsx:85`.
- **Fix:** say `flexBasis: 0` is the fix (each half starts from nothing, so the room is shared equally), and either drop `minWidth: 0` or call it a guard against a change in Astryx. Left to the author.
- **What happened to it:** fixed in the PR: the comment says `flexBasis: 0` is the fix (`StackItem`'s fill starts each half from its content's width) and that `minWidth: 0` only repeats `StackItem`'s own.

### 5. Low: wide pages in a half are now cut off more, and only the notepad's half was looked at

- **Confidence:** medium.
- **What could go wrong:** a half hides what's too wide (`overflowX: 'hidden'`), with no sideways scroll. Before, a page with a wide part pushed its half wider, taking room from the other. Now it gets exactly half, so anything that can't shrink or wrap below half is cut off. At a 1,280 px window, with the folded nav, each half is about 600 px. Pages are meant to use their narrow layout in a half (`useIsNarrow()`), and the notepad's toolbar scrolls itself, so this is likely fine; but the PR checked only the notepad's half.
- **Evidence:** `AppLayout.tsx:28, 170-181`.
- **Fix:** capture the UI parity screens that show a half view (`half-view`, `notepad-half`, `procedure-restricted-half` in `tools/ui-parity/screens.ts`) before merging, as `AGENTS.md` asks for a visible change. Left to the author.
- **What happened to it:** checked: the parity screens with half views (`half-view`, `notepad-half`, `procedure-restricted-half`) were captured against this branch and looked at; each page fits its half and nothing is cut off (the procedure page and the Audit Log side by side lay out in their narrow forms).

## Remaining risks

- **The strip is measured twice and then never again.** If the nav scrolls, the window resizes, or the rows move (a pin's label loading) while a menu is open, the strip stays where it was. From before this PR.
- **Reopening a menu shows the last strip for one frame**, until the new measure (the old `bridge` state isn't cleared on close). From before this PR, and harmless: the strip is invisible.
- **The menu opening and closing on hover** has no browser test anywhere; the only checks of the real timing were by hand.

## Say it like this

"A row's menu in the side nav closed if you moved slowly from its button to the menu, because the invisible bridge across the gap was measured while the menu was still sliding in. Now it's measured again once the animation ends, reaches a little into the menu, and the menu waits 400 ms before closing. The trade-off is that a menu lingers a fraction of a second after you leave; and I also made half views split exactly in half, which means a page with a wide part has to fit its half rather than borrowing room from the other."

## Detail

### The bridge's geometry

- With the menu settled at `to` and the button at `from`: left `from.right`, top `min(from.top, to.top) - 8`, width `to.left + 24 - from.right`, height `max(from.bottom, to.bottom) + 8 - top`. In the test's numbers: 228, 92, 96, 136.
- **Other rows:** the strip starts at the button's right edge. Every row's button ends at the same x, so the 8 px above and below cover only the neighbouring rows' padding to the right of their buttons, never their buttons.
- **The page:** in the expanded nav, the strip covers the 8 px between the nav's edge and the menu, plus the menu's first 24 px (under the menu). Pages are inset 24 px, so no content sits there.
- **The folded nav's flyout:** the flyout is itself a popover in the top layer, so the part of the strip over the flyout is under it. Crossing the flyout's padding does leave the row (its padding isn't in the row's tree) and starts the close timer; reaching the strip cancels it. The longer delay helps here too.
- **No match:** if the menu ends up to the left of the button (flipped for lack of room), `to.left > from.right` is false and there's no strip.

### Server-side rendering

- The measure runs only in `useLayoutEffect` and `requestAnimationFrame`, and the strip renders only while `isOpen && bridge`, both false on the server. `getAnimations` is checked with `typeof` before use. Nothing new runs on the server.

### Accessibility

- The strip is `aria-hidden`, empty and not focusable, as before.
- Keyboard use is unchanged: the timers are only for the mouse.
- The longer delay helps WCAG 1.4.13 (content on hover stays while the pointer moves onto it); Escape still closes the menu.

### The probes

Both used the strip test's layout mocks, plus:

```ts
let finish!: () => void;
const finished = new Promise<void>((resolve) => (finish = resolve));
HTMLElement.prototype.getAnimations = () => [{playState: 'idle', effect: null, finished}];
```

then moved the menu's mocked box before calling `finish()`.
