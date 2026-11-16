# Review: PR #95, every page lined up whether or not it scrolls

**PR:** [#95](https://github.com/Aliator/MedApp/pull/95), branch `feature/page-alignment`, reviewed at `e095e4a` against `main` at `ea4a700`.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a test run, or the running app (measured with Playwright in Chromium with real, space-taking scrollbars). **No finding is high or critical;** all 4 are low.

## Summary

The signed-in layout now always keeps room for a scrollbar in its main area, and in each half of a half view, so a page that scrolls sits in exactly the same place as one that doesn't. The Email Address page's password hint is also reworded. The fix works everywhere it was measured; what it costs is a blank 15 px strip on the right of every page that doesn't scroll.

## What changed, and why

3 files, 46 lines added and 4 removed.

- **The problem:** on Windows, a scrollbar takes up width (15 px in Chromium at 100% scaling). A page tall enough to scroll had 15 px less room than one that didn't.
  - **On wide windows** (from about 1,880 px wide), pages are capped at `PAGE_MAX_WIDTH` and centred. A scrolling page's title sat 7.5 px left of every other page's. At 1920 x 1080: 306.5 px against 314 px.
  - **On narrower windows,** the left edge never moved, but the right edge did: cards on a scrolling page ended 15 px earlier.
  - Only Account Settings and the accessibility statement scroll at 1920 x 1080. On a 1280 x 720 window, Email Address scrolls too.
- **The main fix** (`MedApp.Web/src/components/navigation/AppLayout.tsx:24-30, 144`): a `<style>` element with one CSS rule, `[role="main"]:not(:has([data-half-views])) { scrollbar-gutter: stable; }`.
  - **`scrollbar-gutter: stable`** tells the browser to reserve the scrollbar's space even when there's nothing to scroll. Like a car park that keeps one space marked for the scrollbar, whether or not it's there today: every other car is parked in the same place either way.
  - **`[role="main"]`** is Astryx `AppShell`'s main area, the element that scrolls (`node_modules/@astryxdesign/core/dist/AppShell/AppShell.js:821-838`). `AppShell` has no prop for this (its `className`, `style` and `xstyle` go on its outer element), so a rule is the only way in.
  - **`:not(:has([data-half-views]))`** leaves the main area alone when it holds a half view. `:has()` is a CSS selector that matches an element by what's inside it.
- **The half view marker** (`AppLayout.tsx:165`): the half view's `HStack` gets `data-half-views=""`, which Astryx passes through to the element (`HStack` hands its props to `Stack`, which spreads them: `node_modules/@astryxdesign/core/dist/Stack/Stack.js:138`).
- **Each half keeps its own room** (`AppLayout.tsx:22`, used at `:166` and `:172`): `HALF` adds `scrollbarGutter: 'stable'` to each half's inline style.
  - The PR says each half's title stays 24 px in. It did before too. The real gain, measured, is that **the divider no longer moves**: before, it sat at 736, 744 or 751 px (at 1440 wide), depending on which half scrolled, and the right half's page moved with it. Now it's always at 744.
- **The email hint** (`MedApp.Web/src/pages/account/EmailPage.tsx:204`): "To check it's you before where your reset links go changes." became "We ask for this to check it's you, as password reset links will go to the new address." The old sentence didn't parse. The new one reads well when adding a first address and when changing one.
- **Tests** (`AppLayout.test.tsx:34-60`): the style element is rendered, and a half view carries the marker (finding 1 says what they don't check).

## Design choices

- **Reserve the gutter, rather than making the scrolling pages `fill`:**
  - **Instead:** follow `MedApp.Web/AGENTS.md`'s "scroll the cards, not the page" and make Account Settings and the accessibility statement `fill` pages.
  - **Chosen because:** any page that isn't `fill` scrolls on a short enough window (Email Address does at 1280 x 720), so this would fix two pages, not the cause.
  - **Worse at:** nothing important. Those two pages could still become `fill` pages for their own sake.
- **A CSS rule, rather than a prop on `AppShell`:**
  - **Instead:** ask Astryx for a `contentScrollbarGutter` prop (or a way to style the main area), and use it.
  - **Chosen because:** Astryx 0.7.15 has none, and its versions are pinned and upgraded in their own PRs.
  - **Worse at:** the rule depends on an Astryx detail, the main area being a `div` with `role="main"`. An upgrade that changed it would turn the fix off silently (finding 1).
- **`scrollbar-gutter: stable`, rather than `overflow-y: scroll`:**
  - **Instead:** always show a scrollbar on the main area.
  - **Chosen because:** the same width is kept, but without an empty, greyed-out scrollbar track on every page.
  - **Worse at:** Safari added `scrollbar-gutter` only recently (18.2, as far as is known), where `overflow-y: scroll` works everywhere. Safari on macOS hides scrollbars by default anyway, so it rarely matters.
- **The gutter on one edge, rather than both:**
  - **Instead:** `scrollbar-gutter: stable both-edges`, which reserves the same space on the left as well.
  - **Chosen because:** it costs only 15 px of width, not 30.
  - **Worse at:** the margins are uneven: 24 px on the left of a page, 39 px on its right (finding 2). Simulated, `both-edges` gives even 39 px margins at 1440 wide, and at 1920 wide puts every page back where the non-scrolling pages were before (314 px), even on both sides. In a half view it centres the divider (39 px either side).
- **`:has()` and a marker, rather than rendering the rule only when there's no half view:**
  - **Instead:** `{!half && <style>…</style>}`. `AppLayout` already knows whether a half view is open.
  - **Chosen because:** the rule's text never changes, so the server and the browser always render the same thing.
  - **Worse at:** it needs `:has()` (Chrome 105, Safari 15.4, Firefox 121) and a marker attribute. A browser without `:has()` drops the whole rule and behaves as before, so nothing breaks. The conditional rule would also render the same on both, since the server never renders a half view.
- **An inline `<style>` element, rather than a stylesheet file:**
  - **Instead:** a `.css` file imported by the app.
  - **Chosen because:** the app avoids its own CSS files (Astryx's rules), and `AccessibilityProvider`, `CalendarGrid` and the export page already add rules this way.
  - **Allowed by the page's security policy:** the Content Security Policy (the header that says where a page may load scripts and styles from) allows inline styles: `style-src 'self' 'unsafe-inline'` (`MedApp.API/Middleware/SecurityHeaders.cs:87`).

## How it was checked

- **Read:** the diff and PR description; `AppLayout.tsx` and its test, `Page.tsx`, `halfView.tsx` and `EmailPage.tsx`; Astryx's `AppShell`, `LayoutContent`, `useScrollRegionProps`, `Stack`, `HStack`, `StackItem`, `useScrollLock` and `scrollbarGutter`; the email handlers (`RemoveMyEmailHandler`, `AccountEmailService.RemoveAsync`); the page CSP.
- **CI:** all 13 checks on the PR pass at `e095e4a` (web, unit, integration and E2E tests, image, helm, CodeQL, Trivy, dependency review).
- **Tests:** `npx vitest run src/components/navigation/AppLayout.test.tsx src/pages/account/EmailPage.test.tsx`: 2 files, 32 tests passed.
  - A temporary test (written, run and deleted) showed that jsdom, the simulated browser Vitest uses, supports both `:has()` and a computed `scrollbar-gutter`. So the rule's effect can be tested (finding 1).
- **Measured in the running app** with throwaway Playwright scripts, signed in as the parity tool's account, with its settings saves blocked so nothing was written:
  - **Chromium with real scrollbars** (`ignoreDefaultArgs: ['--hide-scrollbars']`). Each page was measured with the rule on, then with its style sheet turned off in the page, to stand in for `main`.
  - **27 pages at 1920 x 1080 and 1440 x 900:** every signed-in list, form, admin page, account page, the accessibility statement, a patient and a procedure. With the rule, every title is at 306.5 px at 1920 and 284 px at 1440, and there's one `role="main"`.
  - **Window widths from 390 to 2560 px** on five pages, including the mobile layout (Astryx's drawer starts below 768 px). No page gained or lost a scroll, changed height, or overflowed sideways.
  - **Half views** at 1920, 1440 and 1000 wide, three pairs: a `fill` page and a scrolling page each way round, and two that don't scroll. The main area has no gutter, each half has 15 px, and each title is 24 px in. Opening a half view from the nav, without a reload, drops the main area's gutter at once.
  - **Dialogs and the mobile drawer**, which lock scrolling: Add User, the Notifications popover on two scrolling pages, and the drawer at 760 wide. Nothing moved. Astryx's lock is on the document, which never scrolls here.
  - **The server's first paint** (JavaScript off) against the page after hydration (when the browser's React takes over the server's HTML), on four pages: the same title position and gutter. The server's HTML already holds the `<style>` element.
  - **The Email Address page,** with no address on the account (the form's "Add an email address" state).
- **Not done:**
  - Firefox and Safari weren't tested; only Chromium is installed here.
  - Printing wasn't checked.
  - The Email page's Remove dialog wasn't opened: it needs an address on the account, which would change the database. Its texts were read in the code.
  - The parity screenshots weren't re-run (they save the parity account's settings). A default Playwright launch also showed the 15 px gutter, so they do show the change.

## Findings

All are low. None blocks merging.

### 1. The tests check the rule's text, not that it reaches the main area

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the first test checks that the style element holds `STABLE_SCROLLBAR`, then that `STABLE_SCROLLBAR` contains `[role="main"]` and `scrollbar-gutter: stable`. Those last two compare the constant with itself.
  - **If Astryx stopped using `role="main"`** (say, a semantic `<main>` element in an upgrade), the rule would match nothing and pages would drift apart again. Every test would still pass.
  - **The halves' gutter isn't checked at all.** Deleting `scrollbarGutter: 'stable'` from `HALF` fails no test, though the second test's name says "each keeping its own room".
  - jsdom has no layout, so no unit test can measure a title's position. But it does apply the rule: tried in a temporary test, `getComputedStyle(screen.getByRole('main')).scrollbarGutter` is `'stable'` without a half view and isn't with one.
- **Evidence:** `MedApp.Web/src/components/navigation/AppLayout.test.tsx:42-44, 59`; `AppLayout.tsx:22`.
- **Fix:** test the effect, not the string:
  - Without a half view: `expect(screen.getAllByRole('main')).toHaveLength(1)` and `expect(getComputedStyle(screen.getByRole('main')).scrollbarGutter).toBe('stable')`.
  - In a half view: the main area's computed `scrollbarGutter` isn't `'stable'`, and each half's `style.scrollbarGutter` is (`[aria-label="Left half"]`, `[aria-label="Right half"]`).
  - Both pass today (checked in the temporary test, since deleted).
- **What happened to it:** fixed in the PR. The tests now read the rule's effect as computed styles, which jsdom applies (`:has()` included): with no half view, the one `role="main"` has `scrollbar-gutter: stable both-edges`; with a half view open, the main area has `auto` and each half has `stable both-edges`. If Astryx stopped rendering `role="main"`, or the selector stopped matching, these tests fail.

### 2. The reserved room shows as a blank strip, so margins are uneven

- **Severity:** low. **Confidence:** high for the measurements; whether it matters is a judgement.
- **What could go wrong:** with space-taking scrollbars (Windows, most Linux desktops, macOS with "Always show scroll bars"), every page that doesn't scroll, which is most of the app, now has a 15 px empty strip on its right.
  - **Full view, windows up to about 1,860 px wide:** 24 px between the side nav and a page, 39 px between the page and the window's edge. Before, those pages were even (24 and 24). Visible on Patients at 1440 x 900: the table's right edge is noticeably further from the edge than its left is from the nav.
  - **Half views:** when the left half doesn't scroll, the divider sits 39 px from the left page and 24 px from the right one. Before, it was 24 and 24 (it was uneven only while the left half scrolled).
  - **Wide windows:** most pages moved 7.5 px left (314 to 306.5 px at 1920) to meet the two that scroll, rather than the other way round.
  - It's the cost of this approach, and pages that scroll already looked like this. Nothing overflows, wraps or changes height.
- **Evidence:** `MedApp.Web/src/components/navigation/AppLayout.tsx:22, 30`; the measurements under Detail.
- **Fix:** either of these.
  - **Accept it knowingly,** and say in the `STABLE_SCROLLBAR` comment that pages keep an empty strip where a scrollbar would go.
  - **Use `scrollbar-gutter: stable both-edges`** on the main area and the halves. Simulated in the running app: even 39 px margins at 1440 wide, every page at 314 px with 54 px either side at 1920 (where most pages already were), and a centred divider. The cost is 15 px more width, and titles 15 px further right on windows under about 1,860 px.
- **What happened to it:** fixed in the PR, as suggested: `stable both-edges`, on the main area and on each half. Measured afterwards with real scrollbars at 1920 x 1080: every page's title starts at 314 px with an even 15 px on both sides (Patients, Insights, Account Settings, Email Address, the accessibility statement, Admin Settings), and in a half view the divider sits in the middle of the main area (984 px of 48 to 1920). The cost is 15 px more width, accepted for even margins.

### 3. The Email page's other texts don't fit every state

- **Severity:** low. **Confidence:** high (from the code; the Remove dialog wasn't opened, see How it was checked). These lines aren't changed by this PR.
- **What could go wrong:** the new hint is clear for adding and for changing an address. Two texts around it aren't.
  - **Remove, with only an unconfirmed address:** the Remove Email Address button shows when there's only a pending address (one waiting for its link to be opened). The dialog then says "Password reset links can't reach you without one", but none reach them now. What Remove actually does there is cancel the pending change. Afterwards the page says "Your email address has been removed."
  - **Remove, with both:** Remove clears the confirmed address and the pending one (`AccountEmailService.RemoveAsync`). The dialog doesn't say the change waiting for its link is cancelled too.
  - **Where email isn't available:** the card is titled "Change email address" even with no address, while the form, when email is available, says "Add an email address" in that case.
  - **A nuance in the new hint:** "password reset links will go to the new address" is true once the link is opened; until then they go to the old one. The field above says so ("It's used once you open the link"), so this is fine as it stands.
- **Evidence:** `MedApp.Web/src/pages/account/EmailPage.tsx:94, 122, 152, 183, 212`; `MedApp.Infrastructure/Common/Identity/AccountEmailService.cs:73-86`; `MedApp.Application/Authentication/Emails/Commands/RemoveMyEmail/RemoveMyEmailHandler.cs`.
- **Fix:**
  - Word the dialog from the state. With only a pending address: title "Cancel Email Change", text "Stop the change to {pendingEmail}? The link we sent won't work. Enter your password to confirm." With both: add "The change to {pendingEmail} is cancelled too."
  - Title the unavailable card like the form: `email || pendingEmail ? 'Change email address' : 'Add an email address'`.
- **What happened to it:** fixed in the PR. The Remove dialog now says what Remove does for what the account has: the address in use (reset links can't reach you without one), the address in use and a change waiting (it cancels the change, and its link stops working), or only an address waiting (that link stops working; nothing about reset links, as none reach the account yet). The success message for the last says that address was removed. The card shown when email isn't available is titled "Add an email address" when there's no address. New tests cover the three dialog wordings and both titles.

### 4. The PR description and the comments describe the effect slightly wrongly

- **Severity:** low. **Confidence:** high (measured).
- **What could go wrong:** the next change is made on their word.
  - **The PR description** says "Before, the pages that scroll were at 300 px at 1920 wide". Measured at this commit, with the rule turned off, they were at 306.5 px, where every page is now; the pages that don't scroll were at 314 px. So the fix moved most of the app 7.5 px left, not the two scrolling pages right.
  - **The description and `STABLE_SCROLLBAR`'s comment** talk about the title moving. That happens only from about 1,880 px wide. On narrower windows the left edge never moved; the right edge did (finding 2).
  - **`HALF`'s comment** says the gutter stops a half's page "moving sideways when it starts to" scroll. A half's title never moved. What it stops is the divider, and so the other half's page, moving by up to 15 px.
  - **"(Windows)"** in the comment: Linux desktops, and macOS with scrollbars always shown, are the same.
- **Evidence:** `MedApp.Web/src/components/navigation/AppLayout.tsx:18-21, 24-29`; the PR description's "How it was checked".
- **Fix:** in the comments, say that the gutter keeps every page's edges in the same place (the left edge on wide windows, the right edge on all), that in a half view it holds the divider still, and "where scrollbars take up space (Windows, most Linux desktops)".
- **What happened to it:** fixed. The comments name the right benefit for the halves (the divider stays put, in the middle), and "Windows, and most Linux desktops". The PR description was corrected: before, the pages that scroll were at 306.5 px and the rest at 314 px at 1920 wide; now every page is at 314 px.

## Remaining risks

- **Reloading with a half view open fails hydration (from before this PR).** The server can't read the browser tab's storage, so it renders one page; the browser reads it and renders two. Seen on every such reload in this review: "Hydration failed because the server rendered HTML didn't match the client", and React rebuilds the whole page in the browser. The cause is `AppLayout.tsx:66-69` (`useState(() => storedPath())`), unchanged here. The PR's rule follows whatever is rendered, so it adds nothing to it. Worth a to-do: read the stored half view after mounting.
- **The rule leans on Astryx's markup.** It depends on Astryx 0.7.15 putting `role="main"` on the main area's `div`. An upgrade should check it; finding 1's test would.
- **Firefox and Safari weren't tested.** As far as is known, Firefox before 121 drops the rule (no `:has()`), and Safari before 18.2 ignores `scrollbar-gutter`. Either way they behave as before the PR, so nothing breaks.
- **Cards still shift inside.** A card body or other area that scrolls on its own (a `PanelCard`, the Insights area) still narrows by a scrollbar's width when it starts to scroll. Only `CalendarGrid` reserves room. That's outside this PR's aim, which is the page frame.
- **Printing wasn't checked.**

## Say it like this

"On Windows a scrollbar takes up width, so a page long enough to scroll sat slightly off from the others. MedApp now always keeps that scrollbar's space free in the main area, and in each half of a split view, using the CSS property `scrollbar-gutter`, so every page lands in the same place whether it scrolls or not. The trade-off is a thin empty strip on the right of pages that don't scroll, which was chosen over a fix that would have meant changing the design system or turning every long page into one that scrolls inside cards."

## Detail

### Where the title starts, and the right margin (px)

Measured with the rule on ("after") and with its style sheet turned off ("before"). The right margin is the space between the page's content and the window's edge. "Scrolling" means Account Settings and the accessibility statement.

| Window | Title, pages that don't scroll: before → after | Title, scrolling pages: before → after | Right margin, pages that don't scroll: before → after |
|---|---|---|---|
| 2560 x 1440 | 634 → 626.5 | not measured | 374 → 382 |
| 1920 x 1080 | 314 → 306.5 | 306.5 → 306.5 | 54 → 62 |
| 1880 x 900 | 294 → 286.5 | not measured | 34 → 42 |
| 1860 down to 1000 (12 widths; scrolling pages at 5 of them) | 284 → 284 | 284 → 284 | 24 → 39 |
| 760 and 390 (Astryx's mobile layout) | 24 → 24 | 24 → 24 | 24 → 39 |

- The left margin of pages that don't scroll stays 24 px at 1860 and under. Scrolling pages had a 39 px right margin before and after.
- The main area's height never changed (it's 1080 at 1920 x 1080 and 900 at 1440 x 900, with the rule or without), and no page overflowed sideways at any width.

### Half views at 1440 x 900 (px)

| Pair (left + right) | Divider, before → after | Right page starts, before → after | Gap left of divider / right of it, after |
|---|---|---|---|
| Patients + Account Settings (right scrolls) | 736 → 744 | 761 → 769 | 39 / 24 |
| Patients + My Tasks (neither scrolls) | 744 → 744 | 769 → 769 | 39 / 24 |
| Accessibility statement + Users (left scrolls) | 751 → 744 | 776 → 769 | 39 / 24 |

Each half's title is 24 px from its half's left edge in every case, before and after. With `both-edges` on the halves (simulated), the gaps are 39 / 39.

### Overlays and first paint (1920 x 1080)

| Check | Result |
|---|---|
| Add User dialog (Users) | Title 306.5 → 306.5; the main area keeps its 15 px gutter; Astryx sets `overflow: hidden` on the document, not the main area |
| Notifications popover (Account Settings, accessibility statement) | No movement |
| Mobile drawer, 760 wide (Account Settings) | No movement |
| Server's HTML with JavaScript off, then hydrated (Patients, Account Settings, Email Address, accessibility statement) | Same title position (306.5) and gutter (15) in both |
| Style elements and main areas in the page | One of each, with or without a half view |
