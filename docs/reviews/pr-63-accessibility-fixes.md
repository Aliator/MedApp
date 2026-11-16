# Review: PR #63, a session timeout warning, Ctrl+S, a colour contrast warning and more announcements

**PR:** [#63](https://github.com/Aliator/MedApp/pull/63), branch `feature/accessibility-fixes`, reviewed at `166bf94` on top of `main` at `88dc1fa` (the merge of #56). `main` has since moved on to `a506079`, with #57 to #61 merged (#59, the forced password change, among them); what that means for this PR is part of the review.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a test run, a scratch program or the GitHub API. **No finding is high or critical;** two are medium and ten are low.

## Summary

Four accessibility items from the to-do list (WCAG 2.2 AA): a warning five minutes before a session times out, with "Stay signed in" (two new endpoints extend a session, up to 24 hours from sign-in); Ctrl+S (⌘S on a Mac) to save the form that's open; a warning on custom colours that leave text hard to read; and screen reader announcements for saved forms and finished stages. Each works as described and is well tested. The main gaps: the colour check measures only the darkest text, so grey supporting text can be unreadable without a warning, and the architecture handbook, and the governance and clinical safety documents merged to `main` since, still say a session lasts a fixed 8 hours.

## What changed, and why

- **A session timeout warning** (WCAG 2.2.1, "Timing adjustable": a time limit needs a warning and a simple way to extend it). Before, a session ended 8 hours after sign-in with no warning, and the next save simply failed. Think of a parking meter that now flashes five minutes before it runs out, with a button to add time, but never past 24 hours from when the car was parked.
  - **The domain:** `UserSession` gets `Lifetime` (8 hours), `MaxLifetime` (24 hours), `LatestExpiryUtc` (sign-in plus 24 hours), `CanExtend(now)` and `Extend(now)`, which moves the expiry to 8 hours from now, capped at the latest, and never earlier than it was (`MedApp.Domain/Authentication/UserSession.cs:10-52`).
  - **The API:** `GET api/auth/session` returns a `SessionResponse` (`expiresAtUtc`, `expiresInSeconds` by the server's clock, `canExtend`), and `POST api/auth/session/extend` extends the session and sends the cookie again with the new expiry. Both are `[Authorize]` (anyone signed in), read the session from the cookie, and are sent `Cache-Control: no-store` (`MedApp.API/Controllers/AuthController.cs:76-114`). Reading never extends; nothing else extends either, so using the app doesn't (no "sliding" session).
  - **The layers between:** `GetSessionHandler`, `ExtendSessionHandler`, `SessionMapper`, and two new `ISessionService` methods, implemented in `SessionService` with a tracked load and one `SaveChangesAsync`.
  - **The web app:** `SessionTimeout` in the signed-in layout asks once, sleeps until five minutes before the end, then shows a dialog counting down, with Stay signed in (or OK at the 24-hour limit) and Sign out. It asks again as the warning is due, as the session ends, and when the tab is shown again (timers sleep in hidden tabs). A 401 sends it to `/login?expired=true`, where the login page says the session timed out.
- **Ctrl+S saves the open form.** One `keydown` listener (`SaveShortcutProvider`, in the signed-in layout) keeps a list of forms that can be saved (`useSaveShortcut`). `pickSaver` picks the innermost form holding the focus; otherwise, with no dialog open, the page's only form; otherwise none. `FormCard` joins by itself (it calls `requestSubmit()`, so the same `isProcessing` guard as its Save button applies), the stage notes join (`StageWork`), and the Add or Edit Stage dialog joins (`ConfirmDialog`'s new `hasSaveShortcut`). It's listed in the shortcuts dialog. While any form has joined, it stops the browser's own "Save page" dialog, even when it saves nothing.
- **A warning on unreadable custom colours** (WCAG 1.4.3, contrast at least 4.5:1). `src/utils/contrast.ts` holds WCAG's relative luminance and contrast ratio, and a CSS-style `color-mix` in sRGB. `textContrast` measures a chosen page, card or side nav colour against the theme's main text colour (for cards, also against the darker or lighter header bars mixed from it), and the Colours page shows a warning on the field under 4.5:1, saying which way to move the colour.
- **More announcements** (WCAG 4.1.3, status messages). A sighted person sees a form close and the next page open; a screen reader said nothing. Now the seven form pages say "Patient added", "Procedure saved", "Stage added" and so on, the procedure page says "Notes saved" and "Bloods finished. Next stage: Review", when announcements are on (Account Settings, Accessibility; off by default). Each is said only on success, and before any navigation, so the next page's name follows it in the same breath (the announcer joins messages that come within 150 ms).
- **A flaky test fixed:** a patient validation test counted the error text before Astryx's live region had copied it; it now waits (`PatientPages.test.tsx:82-83`).
- **Docs:** the root `AGENTS.md` (Auth) and `MedApp.Web/AGENTS.md` (Accessibility) describe the sessions, Ctrl+S and its exception to the shortcut rules, the contrast check and the announcements; four items leave `docs/todo.md`.

## Design choices

- **A fixed session that's extended on request, not a sliding one:**
  - **Instead:** extend the session on every request (a sliding expiry), so active staff are never interrupted.
  - **Chosen because:** an unattended screen still ends at 8 hours; nothing the page does by itself, such as server rendering or the notifications connection, keeps a session alive.
  - **Worse at:** active staff are interrupted at 7 hours 55 minutes; anyone at the screen can press Stay signed in (finding 9); and there's still no idle timeout.
- **A 24-hour hard limit, relying on WCAG's twenty-hour exception:**
  - **Instead:** let the 8-hour limit be extended at least ten times, as 2.2.1's "Extend" option asks.
  - **Chosen because:** a time limit over 20 hours is exempt, and a session of more than a day is a security cost.
  - **Worse at:** the 8-hour limit can be extended only two or three times before the cap, and the last extension may add only minutes (signed in at 09:00: extended at 16:55 to 00:55, at 00:50 to 08:50, at 08:45 to 09:00). The dialog doesn't say how much time Stay signed in adds.
- **A separate `GET api/auth/session`, with whoami untouched:**
  - **Instead:** add the expiry to `GET api/auth/whoami`.
  - **Chosen because:** whoami is anonymous, is read on the server for every rendered page, and is cached in the browser until sign-in or sign-out (`staleTime: Infinity`), so an expiry there would be stale and baked into server-rendered HTML. Making whoami extend the session would turn every page load into an extension. Leaving it untouched is right.
  - **Worse at:** one more request per app load.
- **Counting down from the server's seconds left, not from `expiresAtUtc`:**
  - **Instead:** count down to `expiresAtUtc` by the device's clock.
  - **Chosen because:** a device whose clock is wrong still warns at the right moment.
  - **Worse at:** the end is recalculated from `Date.now()` on every answer, so it's never quite the same number twice (finding 6).
- **Ctrl+S always on, and working inside fields:**
  - **Instead:** follow the shortcuts switch, like the single-key shortcuts, which start off.
  - **Chosen because:** WCAG 2.1.4 is about single-key shortcuts; one with a modifier can't fire by accident while typing, and inside a field is where it's wanted.
  - **Worse at:** it's hard to discover. The shortcuts list opens only with `?`, which works only once the shortcuts are turned on, and the Save buttons don't mention it. And it blocks the browser's Save page dialog whenever any form is open, even when it saves nothing (finding 8).
- **Measuring custom colours against the main text colour only:**
  - **Instead:** measure every text colour that sits on that surface.
  - **Chosen because:** one comparison per surface, kept in step with the theme by a test.
  - **Worse at:** the secondary text colour is much lighter in light mode and much darker in dark mode, so a mid-tone colour passes for the main text and fails for the rest (finding 1).
- **A fixed warning text, without the ratio:**
  - **Instead:** show the measured ratio ("3.2:1").
  - **Chosen because:** the words don't change while the colour picker is dragged, so a screen reader says the warning once, not at every step.
  - **Worse at:** it doesn't say how far off the colour is. `formatRatio` was written for the ratio and is left unused (finding 11).

## How it was checked

- **Read:** the whole diff (64 files, 1,876 lines added and 113 removed), the PR description, and the code around it: the session authentication handler, the cookie service, `SessionService`, the exception middleware, `DialogFrame` and Astryx's `Dialog` (its first focus goes to the dialog's title, so a key pressed as the warning opens activates nothing), the rich text editor package (it stops no key events and binds no Ctrl+S), `KeyboardShortcuts`, the half view, the announcer, and the theme's text tokens.
- **.NET:** a full rebuild (`dotnet build MedApp.sln -c Release --no-incremental`): 0 errors and 1 warning, in the deprecated Blazor client (`UsersTable.razor.cs:29`, CS0414), which this PR doesn't touch; so no new warnings. `dotnet test MedApp.UnitTests -c Release`: 789 passed.
- **Web:** `npm run lint` and `npm run typecheck`: clean. `npx vitest run`: 729 tests in 86 files, all passing.
- **The timing-sensitive tests:** the patient pages, `SessionTimeout` and `SaveShortcut` test files, run six more times: 42 of 42 each time. So the flaky-test fix holds here.
- **A scratch .NET 10 program** (outside the repository): how a time read back from SQL Server behaves as a cookie expiry and in JSON (finding 4).
- **A scratch script:** contrast ratios for the theme's main and secondary text on sample colours and on every bundled colour theme (finding 1, and the table under Detail).
- **Trial merges** with `git merge-tree`, which writes nothing to the working tree: with #59's branch, then with `main` at `a506079`. Both stop at one conflict, in `ChangePasswordPage.tsx` (finding 3).
- **`main` since the PR branched:** #59's `PasswordChangeRequired` middleware, its client redirect and its `/change-password` route; and every statement of the session's length in `docs/`, on the branch and on `main` (finding 2).
- **CI:** run [43830034351](https://github.com/Aliator/MedApp/actions/runs/43830034351) on `166bf94` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`). The coverage comment lists no .NET class or React file under 80% (web lines 99.8%, branches 93.6%).
- **Server-side rendering** (the server builds each page first, then the browser takes over): safe. `SessionTimeout` and `SaveShortcutProvider` touch `window`, `document` and the clock only in effects, `SessionTimeout` renders nothing until its first tick, and the contrast check is pure maths. The server rendering test renders a signed-in page through the layout that holds both.
- **Not done:** a real browser, a screen reader, a sleeping computer, UI parity screenshots, the axe audit, and the new `SessionService` methods against a real database. No migration was needed: the PR changes no schema.

## Findings

Most severe first. None is high or critical. All were open when this review was written; each says below what happened to it.

### 1. The contrast warning checks only the main text, so a custom colour can leave grey text unreadable without a warning

- **Severity:** medium. **Confidence:** high (measured).
- **What could go wrong:** in light mode, a custom card colour of `#a8b8d0` (a soft blue-grey) gets no warning: the main text on it is 6.7:1. But the theme's secondary text (`#5f5f64`) on it is 3.2:1, and on a page colour of `#999999` it's 2.2:1, while the main text passes at 5.4:1. Secondary text is everywhere: the page header's back link and subtitle sit on the page colour, field descriptions and placeholders on cards, and the side nav's "Administrator" or "User" on the nav colour (a nav of `#606060`: 5.6:1 for the main text, 2.8:1 for the secondary). The warning says nothing, so the colour looks safe. Any colour in light mode whose relative luminance is between about 0.26 and 0.69 does this, which is most mid-tones.
- **Evidence:**
  - `MedApp.Web/src/app/colours.tsx:75`: `TEXT_COLOURS` holds only the main text colour per mode
  - `colours.tsx:99-105`: `textContrast` compares against it alone
  - `MedApp.Web/src/themes/medapp/medapp.css:206`: `--color-text-secondary: light-dark(#5f5f64, #ababb3)`; `:759`: placeholders use it too
  - secondary text on each surface: `MedApp.Web/src/components/pages/PageHeader.tsx:56` and `:65` (page), `MedApp.Web/src/components/navigation/AppSideNav.tsx:575` (side nav), and 29 uses of `color="secondary"` across the app
- **Also:** measured against secondary text, three of the bundled colour themes are under 4.5:1 already: Y2K's light page (4.19:1, where the back link and subtitle sit), and the header bars of Chocolate's light cards and Butter's dark cards (4.41:1 each). The test "passes every colour theme" can't see this, as it checks only the main text. The axe audit runs one colour theme, so it doesn't either.
- **Fix:** measure the worse of the main and secondary text (diff under Detail), extend the test that compares `TEXT_COLOURS` with the theme to `--color-text-secondary`, and decide whether to adjust Y2K's light page (and the two header bars) or accept them knowingly. Disabled text is exempt from 1.4.3, so it can stay out.
- **What happened to it:** fixed. `textContrast` now measures the worse of the main and secondary text (`TEXT_COLOURS` holds both per mode), on the card and its header bars, and the test comparing them with the theme covers `--color-text-secondary` too. The three bundled themes just under 4.5:1 on secondary text (Y2K's light page, Chocolate's light and Butter's dark card header bars) were left as they are, so they don't change how they look: a to-do item in `docs/todo.md` (Accessibility), and the theme test lists them by name.

### 2. The docs still say a session lasts a fixed 8 hours, including the cookie notice and a clinical hazard

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** once this PR merges, a session can last up to 24 hours, but the docs say otherwise in five places, two of them meant for customers and for the clinical safety case:
  - **On this branch,** the architecture handbook says "8 hours from sign-in, fixed" in its session table, its sign-in walkthrough and its risk S5; the PR updated `AGENTS.md` but not these.
  - **Merged to `main` since** (#60 and #61): the cookie notice tells staff the session cookie is deleted "after 8 hours", the data inventory says "8 hours at most", and the hazard log's H-18 (a change recorded under the wrong person's name) gives "a session lasts a fixed 8 hours" as a cause.
  - **H-18 matters most,** because this PR changes that cause. Anyone who sits down at an unattended, signed-in screen in its last five minutes can press Stay signed in and keep it for up to 24 hours from sign-in. H-18's planned control is "an idle timeout, with a warning", which this isn't. A change to a hazard's cause is one the clinical safety officer assesses.
- **Evidence:**
  - this branch: `docs/architecture.md:458`, `:650`, `:1026` ("8 hours from sign-in, fixed; no idle timeout"), `:1277` (risk S5, "a fixed 8 hours") and `:1554` ("the fixed 8-hour lifetime")
  - `main` at `a506079`: `docs/governance/cookies-and-storage.md:19` and `:39` (the notice's own words: "It's deleted when you sign out, or after 8 hours"), `docs/governance/data-inventory.md:38`, and `docs/clinical-safety/hazard-log.md:673` (H-18's causes; its further controls follow)
- **Fix:** change each to "8 hours from sign-in, extendable on request ('Stay signed in') to at most 24 hours from sign-in" (the ones from `main` once it's merged in), add the extension to H-18's causes, and record the warning as what it is (a warning before a fixed limit, not an idle timeout) for the clinical safety officer to assess.
- **What happened to it:** fixed. Every statement of a fixed 8 hours now says 8 hours, extendable on request to 24 at most from sign-in: `docs/architecture.md` (the services, the sign-in walkthrough, the session table, risk S5 and the ASVS note), and in `docs/governance/` the cookie notice and its table, the data inventory, the DPA template, the DPIA, the retention schedule and the ROPA. Hazard H-18 gains the extension as a cause, marked as a change for the clinical safety officer to assess, and says the warning isn't the planned idle timeout.

### 3. Merging `main` stops at a conflict in the change password page, and #59's forced change page has no warning

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **The conflict:** #59 moved the change password form into `usePasswordChangeForm` and `PasswordChangeFields`; this PR added `announce('Password updated')` inside the old form's `onSuccess`. GitHub can't merge the PR until it's resolved. Taking `main`'s side alone drops the announcement, but this PR's own test (`ChangePasswordPage.test.tsx`, `expectAnnounced('Password updated')`) would then fail, so it can't be lost silently.
  - **The forced change page:** `/change-password` is outside the signed-in layout, so it has no timeout warning and no Ctrl+S. And `PasswordChangeRequired` lets a user who must change their password reach only whoami, their settings, sign-in, sign-out and their own password change, so both session endpoints are a 403 there anyway. Someone who leaves that page open as their 8 hours end gets no warning, and their change then fails.
  - **What fits:** on a page that's already open when its user's password is reset, the warning's check gets the same 403 as any other call, and #59's client sends the page to `/change-password` with a full load. `SessionTimeout` treats only a 401 as the end (or another failure once the end has passed), so it doesn't fight that.
- **Evidence:**
  - the conflict: `git merge-tree --write-tree origin/main 166bf94` reports `CONFLICT (content)` in `MedApp.Web/src/pages/account/ChangePasswordPage.tsx` only
  - on `main`: `MedApp.API/Common/Authentication/PasswordChangeRequired.cs:45-57` (`IsAllowed`), `MedApp.Web/src/api/client.ts:65-72` (the redirect on a 403), `MedApp.Web/src/routes/change-password.tsx:12-16` (outside `_app`)
  - this PR: `MedApp.Web/src/components/navigation/SessionTimeout.tsx:55` and `:73`
- **Fix:** resolve by taking `main`'s file and saying it in `usePasswordChangeForm`'s `onChanged` (diff under Detail). For the forced page, either allow `GET /api/auth/session` and `POST /api/auth/session/extend` in `IsAllowed` and render `SessionTimeout` on that page, or record that the page has no warning.
- **What happened to it:** the conflict was resolved by taking `main`'s `ChangePasswordPage.tsx` and announcing in `usePasswordChangeForm`'s `onChanged`, as shown under Detail. The session endpoints were deliberately not added to `PasswordChangeRequired`'s allowlist: the forced change page has no timeout warning, and ends at the session's normal expiry. That's recorded in the root `AGENTS.md` (Auth).

### 4. At the 24-hour limit, on a server not running on UTC, the cookie ends before the session

- **Severity:** low. **Confidence:** high (shown by a scratch program).
- **What could go wrong:** EF Core reads SQL Server's `datetime2` back with `DateTimeKind.Unspecified`. Once `Extend` caps a session, its expiry is `CreatedAtUtc + 24 hours`, from the database, so it's Unspecified. The controller passes it to the cookie, whose `Expires` is a `DateTimeOffset`, and .NET treats an Unspecified time as the server's local time. On a server on UK time in summer, a session capped at 09:00 UTC gets a cookie that ends at 08:00 UTC: the browser drops it an hour early, every call is a 401, and the warning (timed by the database) is still 55 minutes away. The same happens when Stay signed in is pressed again at the cap. The JSON is affected too: `expiresAtUtc` goes out as `"2026-07-02T09:00:00"`, with no `Z`, which a browser reads as its own local time; the web app ignores that field, so nothing shows it today. The containers run on UTC, so only a development machine in summer sees the cookie problem.
- **Evidence:**
  - `MedApp.Domain/Authentication/UserSession.cs:28` (`LatestExpiryUtc` from the stored sign-in time) and `:47-51`
  - `MedApp.Infrastructure/Common/Authentication/SessionService.cs:62-83` (both loads; nothing marks the kind)
  - `MedApp.API/Controllers/AuthController.cs:110` and `MedApp.API/Common/Authentication/SessionCookieService.cs:12`, `:34`
  - the scratch program: an Unspecified `2026-07-02T09:00` became a cookie expiry of `08:00Z` on a machine on "GMT Standard Time"
- **Fix:** #62 (open, branch `fix/utc-times`) adds a `UtcDateTimeConverter` to every `DateTime` the database context reads, which fixes this with nothing more here. If this PR merges first, mark the kind in `SessionMapper.ToResponse` (`DateTime.SpecifyKind(session.ExpiresAtUtc, DateTimeKind.Utc)`); the controller takes the cookie's expiry from that response, so both are fixed. Add a test with an Unspecified sign-in time either way.
- **What happened to it:** fixed. #62 hadn't landed, so `SessionMapper.ToResponse` marks the expiry UTC (`DateTime.SpecifyKind`); the controller takes the cookie's expiry from that response. `SessionMapperTests.ToResponse_ExpiryReadBackWithoutAKind_MarksItUtc` builds a session with an Unspecified-kind time, as EF Core reads it back.

### 5. If the first session check fails, no warning is ever set

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** the app asks `GET api/auth/session` once, when the signed-in layout opens, and never retries. If that one request fails other than with a 401 (a deploy restarting the API, a network blip, a 502 from the proxy), there's no end to count to, so no timer is set. In a clinic where the app stays open all day, the session then ends at 8 hours with no warning, and the next save fails: the very thing this feature exists to prevent. Nothing goes to the login page either. Only showing the tab again (switching away and back) asks again.
- **Evidence:** `MedApp.Web/src/api/session.ts:31` (`retry: false`); `MedApp.Web/src/components/navigation/SessionTimeout.tsx:60` (no end, no timers) and `:85-90` (the only other trigger)
- **Fix:** retry anything but a 401, and keep asking while there's no answer, for example `retry: (failures, error) => !isUnauthorized(error) && failures < 3` and `refetchInterval: (query) => (query.state.status === 'error' ? 60_000 : false)`; add a test where the first answer is a 503.
- **What happened to it:** fixed. `sessionQuery` retries anything but a 401 (three times), and while the last check failed it asks again every minute (`SESSION_RETRY_MS`). A test has the first check and its retries fail, then warns as usual.

### 6. A closed warning comes back whenever the tab is shown again

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the warning is kept closed for "the end it was closed for" (`dismissedFor`), compared by exact number. But the end is recalculated from `Date.now()` with each answer from the API, so it's never the same number twice. Closed with Escape, the close button or OK (at the 24-hour limit), the warning reopens as soon as the tab is shown again, which asks the API again. At the limit, where OK is the only answer, it reopens every time the tab comes back. The test "stays closed once closed, until the end moves" asks the API only once, so it can't see this.
- **Evidence:** `MedApp.Web/src/api/session.ts:17`; `MedApp.Web/src/components/navigation/SessionTimeout.tsx:50`, `:101` and `:110`
- **Fix:** compare with a tolerance (`dismissedFor !== null && Math.abs(endsAt - dismissedFor) < 5_000`), or keep the server's `expiresAtUtc` in `SessionExpiry` and key the dismissal on that. Add a test that refetches after closing.
- **What happened to it:** fixed. A closed warning stays closed while the end is within 5 seconds of the one it was closed for (`SAME_END_MS`). A test asks the API again after closing it, then moves the end and sees it warn again.

### 7. "Your session timed out" may not be read out

- **Severity:** low. **Confidence:** medium (it depends on the screen reader).
- **What could go wrong:** when the session ends, the app moves to the login page in the browser, without a page load. The banner is a `role="status"` region inserted together with its text. Live regions are announced when their content changes, and many screen readers don't announce a region that arrives already filled; and the page's own name is announced only when announcements are on, which they aren't by default. A screen reader user whose warning ran out hears nothing, and may not know why they're on the login page.
- **Evidence:** `MedApp.Web/src/pages/login/LoginPage.tsx:69-76`; `MedApp.Web/src/components/navigation/SessionTimeout.tsx:96`
- **Fix:** `role="alert"` on this banner (alerts are announced when they appear), or move the focus to the "Sign in" heading after the redirect. Check with NVDA during the screen reader pass.
- **What happened to it:** fixed. The timed-out banner on the login page is `role="alert"`, and its test checks that. Hearing it with NVDA stays part of the screen reader pass.

### 8. Ctrl+S sometimes does nothing, silently, and stops the browser too

- **Severity:** low. **Confidence:** medium (on how often it misleads).
- **What could go wrong:** whenever a form has joined, Ctrl+S is taken from the browser, but in several cases it then saves nothing and says nothing: two forms open in a half view with the focus in neither (a click on blank space moves the focus to the page); any dialog open; a form dialog whose focus is still on its title, where Astryx puts it on opening; the stage notes unchanged. Someone used to Ctrl+S may believe their changes were saved, then close the half.
- **Evidence:** `MedApp.Web/src/components/forms/SaveShortcut.tsx:76-78` (it prevents the default before choosing), `:39` and `:44` (the cases that choose none); `MedApp.Web/src/components/procedures/StageWork.tsx:87`
- **Fix:** when Ctrl+S is taken but nothing is saved, say so briefly, on screen and to screen readers ("Nothing saved: click in the form to save it"). The rule itself (never guess between two forms) is right.
- **What happened to it:** fixed. When Ctrl+S is taken from the browser but saves nothing, an Astryx toast says so ("Nothing saved: click in the form you want to save, then try again.", or "Nothing to save." when the chosen form had nothing it could save), which screen readers hear through Astryx's live regions. A `save` that has nothing to do returns `false` (stage notes unchanged, a form dialog that can't be confirmed yet).

### 9. A session can now be kept for 24 hours by anyone holding it, and a lockout still doesn't end it

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** extending can't make a session last for ever: the cap is counted from `CreatedAtUtc`, which never changes, and it's enforced on the server. But a session cookie copied from a shared workstation, or an unattended screen in its last five minutes, can now be kept for 24 hours instead of 8 (one `POST api/auth/session/extend` every 8 hours). Locking the account, or the user changing their own password, still doesn't end existing sessions (risk S5, `docs/architecture.md:1277`). An admin's reset is better since #59: the session is held to changing the password, which needs the generated one.
- **Evidence:** `MedApp.Domain/Authentication/UserSession.cs:17`, `:28`, `:41-52`; `MedApp.API/Controllers/AuthController.cs:97-114`; `MedApp.Infrastructure/Common/Authentication/SessionService.cs:31-55` (validation checks the session and the user, not a lockout)
- **Fix:** end a user's other sessions on a lockout and on a password change (a `RevokeAllForUserAsync`), and record the longer limit in risk S5 and hazard H-18 (finding 2). An idle timeout remains the real control.
- **What happened to it:** handled elsewhere. Ending a user's other sessions on a password change, a reset and a role change is being built on the security baseline branch, so it isn't built here. The longer limit is recorded in risk S5 (`docs/architecture.md`) and as a cause in H-18, with that work listed among H-18's further controls.

### 10. Deleting a procedure isn't announced, though deleting a patient is

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** "Patient deleted" is said before the app leaves the page; deleting a procedure leaves the page in silence. `MedApp.Web/AGENTS.md` now says "A new form or action that saves and moves on does the same", and this action moves on. The setup lists' archive and delete, a status change, unlocking, and assigning or unassigning staff are silent too, but they stay on the page, where the list or the page itself changes.
- **Evidence:** `MedApp.Web/src/pages/procedures/ProcedurePage.tsx:349-352`; `MedApp.Web/src/pages/patients/EditPatientPage.tsx:84`; `MedApp.Web/AGENTS.md:37`
- **Fix:** `announce('Procedure deleted')` before the navigation, with a test.
- **What happened to it:** fixed. The procedure page says "Procedure deleted" before it leaves, with a test.

### 11. Small drift: a mapper missing from the list, an unused helper, undeclared 401s

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the root `AGENTS.md` lists every mapper "so far", and `SessionMapper` isn't there, so the next change may not find it. `formatRatio` is exported and tested but used nowhere. The two endpoints return 401 (their summaries say so) but declare only 200, so the Swagger document and the generated types don't show it.
- **Evidence:** `AGENTS.md:102-105`; `MedApp.Web/src/utils/contrast.ts:51-54`; `MedApp.API/Controllers/AuthController.cs:83` and `:100`
- **Fix:** add `SessionMapper` to the list; remove `formatRatio` or show the ratio; add `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` to both endpoints and run `npm run gen:api`.
- **What happened to it:** fixed. `SessionMapper` is in the root `AGENTS.md`'s list of mappers, `formatRatio` is gone, and both endpoints declare their 401 (`[ProducesResponseType(StatusCodes.Status401Unauthorized)]`), with `schema.d.ts` updated by hand to match, as `gen:api` would write it.

### 12. The tests miss some failure paths, and the session tests run on real time

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** a later change could announce on failure, or break the database side of sessions, without a test noticing.
  - **Announcements:** every new test checks that a success is announced; none checks that a failed save isn't. The code is right today (each `announce` sits in `onSuccess`).
  - **Sessions in the database:** `SessionService`'s new methods have no tests (it's excluded from coverage as needing a real database), and every session test uses `DateTimeKind.Utc`, so nothing sees finding 4.
  - **Timing:** the `SessionTimeout` tests use real timers, with sessions a second long and waits of up to 4 seconds. They passed every run here, but they're slower than they need to be and depend on the machine's speed.
  - **The flaky-test fix:** it's sound: `waitFor` retries until Astryx's live region has its copy of the error. But it still counts copies of the text, which depends on how Astryx announces; checking the field's own error would be firmer.
- **Evidence:** `MedApp.Web/src/pages/patients/PatientPages.test.tsx:82-83`; `MedApp.Web/src/components/navigation/SessionTimeout.test.tsx:31`, `:66`, `:99`; `MedApp.UnitTests/Common/Constants/AuthTestConstants.cs` (the new session times)
- **Fix:** a "not announced on failure" case on one form page and on the stage finish; fake timers (`vi.useFakeTimers({shouldAdvanceTime: true})`) for `SessionTimeout`; the test from finding 4.
- **What happened to it:** fixed. A failed patient add and a refused stage finish are checked not to be announced; the `SessionTimeout` tests run on fake timers (`vi.useFakeTimers({shouldAdvanceTime: true})`), with the mock counting down to a fixed end; and finding 4's test covers an Unspecified-kind time. `SessionService` itself still has no database test.

## Remaining risks

- **No idle timeout yet.** The warning is for the fixed limit; an unattended screen stays signed in until it (hazard H-18).
- **After timing out, sign-in goes to the start page,** not back to the page that was open, and unsaved work is lost. WCAG 2.2.5 (keeping data when signing in again) is AAA, so not required.
- **A sleeping computer:** checking again relies on the tab being reported hidden and then visible, which browsers do for a locked screen on most systems; it wasn't tried in a real browser.
- **Ctrl+S on keyboards whose letters aren't Latin** (Russian, Greek): `event.key` is that layout's letter, so the shortcut isn't caught and the browser's Save page dialog opens. Checking `event.code === 'KeyS'` as well would cover them.
- **Ctrl+S held down repeats.** Each form's own guard (`isProcessing`, `isBusy`) stops double saves once the first has started; `event.repeat` isn't ignored.
- **The screen reader pass by hand** is still on the to-do list, and the new dialog, banner and announcements haven't been heard yet.
- **Merging this PR:** `main` (with #59, #60 and #61) has been merged into it, and findings 1 and 2 dealt with. If #62 lands first, its converter and `SessionMapper`'s `SpecifyKind` overlap harmlessly.

## Say it like this

- This PR fixes four accessibility gaps. Sessions now warn five minutes before they end, like a parking meter that flashes before it runs out, and "Stay signed in" adds time, up to 24 hours from sign-in. Ctrl+S saves the form that's open. The Colours page warns about custom colours that make text hard to read. And screen readers now hear "Patient saved", "Bloods finished. Next stage: Review", and so on.
- It's careful work and well tested, and nothing in it is high or critical.
- Two things should be fixed or accepted on purpose. The colour check looks only at the darkest text, so the lighter grey text can be unreadable with no warning. And the docs (the architecture handbook, and the cookie notice, data inventory and hazard log merged since) still say a session lasts a fixed 8 hours; the hazard log needs the clinical safety officer to look at the change.
- Before it can merge, `main` needs merging in, with one conflict in the change password page.

## Detail

### The session, over a day

Signed in at 09:00 UTC, with Stay signed in pressed at each warning:

| Pressed at | New end | Added | `canExtend` after |
|---|---|---|---|
| (sign-in) | 17:00 | 8 h | yes |
| 16:55 | 00:55 | 7 h 55 min | yes |
| 00:50 | 08:50 | 7 h 55 min | yes |
| 08:45 | 09:00 (the cap) | 10 min | no: the next warning offers only OK |

### Contrast, main and secondary text

Ratios from the scratch script, with WCAG's formula (the same as `contrast.ts`; the 0.04045 threshold and WCAG's older 0.03928 give the same result for 8-bit colours).

| Surface and mode | Colour | Main text | Secondary text | Warned today? |
|---|---|---|---|---|
| Light page | `#999999` | 5.35:1 | 2.23:1 | no |
| Light card | `#a8b8d0` | 6.70:1 (header bars included) | 3.15:1 | no |
| Side nav | `#606060` | 5.55:1 | 2.76:1 | no |
| Light page, Standard | `#d8e0ef` | 11.50:1 | 4.79:1 | no (right) |
| Light page, Y2K | `#cccffa` | 10.08:1 | 4.19:1 | no |
| Light card bars, Chocolate | from `#ede4d4` | over 4.5:1 | 4.41:1 | no |
| Dark card bars, Butter | from `#3a2a1f` | over 4.5:1 | 4.41:1 | no |

A sketch of the fix for finding 1:

```ts
/** The text shown on the page and cards in each mode: the theme's primary and secondary text colours. */
export const TEXT_COLOURS: Record<ColorMode, {primary: string; secondary: string}> = {
  light: {primary: '#25252a', secondary: '#5f5f64'},
  dark: {primary: '#f0f0f9', secondary: '#ababb3'},
};

export function textContrast(mode: ColorMode, part: ColourPart, colour: string): number {
  // The side nav keeps dark mode's text in both modes.
  const text = TEXT_COLOURS[part === 'nav' ? 'dark' : mode];
  const surfaces = part === 'card' ? [colour, headerBarColour(mode, colour)] : [colour];
  // The worse of either text on any of the surfaces (a very dark colour in light mode is worse for the darker text).
  return Math.min(...surfaces.flatMap((s) => [contrastRatio(text.primary, s), contrastRatio(text.secondary, s)]));
}
```

### Resolving the conflict with #59 (finding 3)

Take `main`'s `ChangePasswordPage.tsx`, then:

```diff
 import {useState, type FormEvent} from 'react';
+import {useAnnounce} from '../../app/announcements';
 import {useCurrentUser} from '../../auth/auth';
@@
   const me = useCurrentUser();
+  const announce = useAnnounce();
   const [isUpdated, setIsUpdated] = useState(false);
-  const form = usePasswordChangeForm({username: me.username, onChanged: () => setIsUpdated(true)});
+  const form = usePasswordChangeForm({
+    username: me.username,
+    onChanged: () => {
+      // Its success banner isn't a live region (only errors are), so it's said here.
+      announce('Password updated');
+      setIsUpdated(true);
+    },
+  });
```

### The session endpoints and #59's middleware

| Request, for a user who must change their password | Allowed by `PasswordChangeRequired`? | What happens |
|---|---|---|
| `GET /api/auth/whoami`, `GET /api/auth/me/settings` | yes | as before |
| `GET /api/auth/session` | no: 403 `password_change_required` | the client goes to `/change-password`; `SessionTimeout` ignores a 403 until the end has passed |
| `POST /api/auth/session/extend` | no: 403 | as above; the button shows "could not be extended" until the page goes |
| `PUT /api/auth/users/{own name}/update-password` | yes | the change; no warning on that page |

### Smaller notes

- **A 404 in a race:** the authentication handler checks the session with `DateTime.UtcNow`, then the handler loads it again. If it ends between the two, the answer is a 404 (`NotFoundException`), not the documented 401. The web app then ends it at the next check, so it does no harm.
- **Signed out in another tab:** the other tabs find out at their next check, and their login page says the session "timed out".
- **Rich text and Ctrl+S:** the editor reports each change as it happens, so the notes saved are the notes typed; Ctrl+S inside it reaches the app's listener (the stage notes test covers this).
- **`requestSubmit()` and a disabled Save button:** `FormCard`'s Save button is never disabled, only busy, so Ctrl+S can't get round a disabled button there; a form that disables its submit button would need its own check.
