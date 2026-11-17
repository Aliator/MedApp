# Review: PR #109, emailed links kept when React runs the page's start-up code twice

**PR:** [#109](https://github.com/Aliator/MedApp/pull/109), branch `feature/confirm-email-link`. Reviewed at `148c545`, compared with `main` at `a9157f7` (also the merge base).

**Reviewed:** 17 November 2026, as an independent, adversarial review, without the author's conclusions. Each claim below was checked against the code, React 19.3.1's, TanStack Router's and Testing Library's own files, CI's results, or temporary Vitest probes (written, run and deleted). No browser, AppHost or Docker was started, by the project's rules. **No finding is high or critical:** all 3 are low, and two of them are from before this PR.

## Summary

In development, the confirm email and reset password pages said "This link isn't complete" for a whole link. React's StrictMode runs a page's effects twice, and the second run found the address already cleared. The fix keeps the first link read. It holds for every way the page can mount, keeps the token's protections, and its tests fail without it. The gaps are around it: the export page has the same kind of bug, a link pasted into the same tab isn't read, and most tests still run without StrictMode.

## What changed, and why

5 files, 87 lines added and 9 removed, all in `MedApp.Web`.

- **Two terms first:**
  - **The fragment** is the part of an address after `#`. The browser keeps it to itself and never sends it to a server, so it stays out of server logs and the `Referer` header. Like a note on your side of the letterbox: the postman never sees it. MedApp's emailed links carry their token there (`/confirm-email#user=…&token=…`).
  - **StrictMode** is a check React runs in development only. When a component first appears, React runs its *effects* (code that runs once the page is on screen), undoes them, and runs them again at once, keeping the component's state. It's a fire drill: it proves the start-up code copes with running twice. Production React doesn't drill. MedApp's browser entry wraps the whole app in it (`entry-client.tsx:21-32`).
- **The bug:**
  - `useEmailLink` reads the link from the fragment in an effect, then clears the fragment, so the token doesn't stay in the address bar.
  - Under StrictMode, the second run found no fragment. It set the state to "missing", or to a link kept in sessionStorage from an earlier visit, replacing the link just read.
  - **Who saw it:** anyone on the dev server (the AppHost, `npm run dev`), the UI parity tool, and E2E runs against the AppHost. Not production, and not CI's `e2e` job, which tests the production image.
- **The fix** (`emailLink.ts:78`): `setState((current) => current.status !== 'reading' ? current : …)`.
  - **A functional update** says "change it based on what's there now", rather than "set it to this". Here: fill in the box only while it's still blank. The second run sees a link already there and leaves it.
  - `linkFragment.clear()` now runs before `setState` (`:73`). The order doesn't matter: both happen in the same effect, before React draws again.
  - The doc comment and the inline comment say why (`:64-67, 74-76`).
- **A `strict` option for `renderWithApp`** (`render.tsx:48-51, 60-65`): wraps the whole test app in `<StrictMode>`, outermost, as the browser entry does. Its comment says a StrictMode inside the router wouldn't drill. A probe confirms it: 1 run instead of 2 (Detail explains why).
- **Six new tests:**
  - `emailLink.test.ts:74-104`: the link read is kept; the address's link beats a kept one; "missing" still shows without a link.
  - `ConfirmEmailPage.test.tsx:120-145`: signed out (the link kept for sign-in is the one read) and signed in (confirms with its token).
  - `ResetPasswordPage.test.tsx:36-46`: resets with the link's token.

## Design choices

- **A functional update, rather than a ref that says "already read":**
  - **Instead:** `const read = useRef(false); if (read.current) return; read.current = true;`, as `TwoFactorSetup.tsx:94-102` already does. A ref survives StrictMode's drill.
  - **Chosen because:** the guard is the state itself, so there's nothing extra to keep in step, and the rule reads plainly: the first read wins.
  - **Worse at:** the second run still reads the address and the kept link, then throws the result away. That's harmless (both reads change nothing), but a reader has to follow why. It's also a second style beside the ref guards used for the same problem elsewhere (`TwoFactorSetup`, `RecoveryCodes`, `OneTimePassword`).
- **Reading in an effect at all, rather than in `useState`'s first value:**
  - The server never sees the fragment, so its HTML says "Reading the link". The browser's first render must match that HTML (*hydration*: React takes over the server's page in place). So the read has to come after mounting.
  - Unchanged by this PR, and right.
- **A hand-made `strict` option, rather than Testing Library's own:**
  - Testing Library 16.3 has `reactStrictMode`, per render or for the whole suite (`configure`). It also puts StrictMode outermost (`node_modules/@testing-library/react/dist/pure.js:132-133`).
  - **Worse at:** nothing at run time. It's a few more lines than `render(app, {reactStrictMode: strict})`, and it hides that a suite-wide switch exists (finding 3).

## How it was checked

- **Read:**
  - the diff and the PR description
  - `emailLink.ts`, `ConfirmEmailPage.tsx`, `ResetPasswordPage.tsx`, their tests, `render.tsx`, `setup.ts`, `entry-client.tsx`, `router.tsx`, the two route files
  - the root `AGENTS.md` (Email, password reset and two-factor authentication) and `MedApp.Web/AGENTS.md:51, 96`
  - React 19.3.1's development build (how StrictMode picks what to drill), `@tanstack/history` 1.162.5 (its patched `history.replaceState`, and the key it adds to each history entry), `@tanstack/router-core` 1.171.39 (scroll restoration's storage key), Testing Library 16.3.3 (`reactStrictMode`).
- **Ran:** `npx vitest run` on `emailLink.test.ts`, `ConfirmEmailPage.test.tsx` and `ResetPasswordPage.test.tsx`: 3 files, 54 tests passed.
- **Without the fix** (line 78 put back as it was, then restored): 5 of the 6 new tests fail. The sixth, "still says when there is no link", passes either way, as it must: the old code said "missing" too. It guards the new code's own "missing" branch. The PR says all of them failed before the fix; it's five.
- **Temporary probes,** written, run, then deleted. Each passed:
  - an effect in a page rendered with `strict: true` runs twice; with `<StrictMode>` inside the router, once; without, once
  - after reading, a second link pushed into the same document (fragment only, with `hashchange` and `popstate`): the hook still holds the first token, and the address keeps the second (finding 2)
  - unmounted and mounted again, with a link kept for sign-in: the new page reads the kept link
  - the export page under StrictMode: 2 requests with nothing cached, 2 with the loader's prefetch in flight, 1 with the data already cached; without StrictMode, 1, 1 and 0 (finding 1)
  - the fix suggested for finding 1, applied for the probe and then undone: 1, 1 and 0 requests with and without StrictMode, and the export gone from the cache after the page closes.
- **Swept every effect in `MedApp.Web/src`** (58, in 36 files) for code that breaks when run twice. Only the export page's does (finding 1). The rest set something up and tear it down, guard with a ref (`SettingsSync`, `TwoFactorSetup`, `RecoveryCodes`, `OneTimePassword`, `CalendarGrid`'s first scroll), or repeat harmlessly (the same navigation, announcement or unpin twice).
- **Security, by reading:**
  - the token is read only in the browser, from `window.location.hash`, and sent only in a POST body (`ConfirmEmailPage.tsx:137`, `ResetPasswordPage.tsx:113`)
  - the sign-in redirect is a fixed path, never the current address (`ConfirmEmailPage.tsx:20, 93`)
  - nothing in `src` logs the address
  - scroll restoration keys its sessionStorage by TanStack's random entry key, not the address, since TanStack adds that key on start-up (`@tanstack/history/dist/esm/index.js:178-185`; `router-core/dist/esm/scroll-restoration.js:27-29`), so the token isn't stored there.
- **CI:** all 13 checks pass at `148c545` (run 44035983583, and CodeQL in run 44035983544).
- **Not done:** no browser, AppHost, E2E or full suite. The PR's own browser checks (the dev server, and a production build) weren't repeated.

## Findings

### 1. The export page asks for the export twice in development, so each visit is logged twice (from before this PR)

- **Severity:** low. **Confidence:** high (shown with probes).
- **What could go wrong:** an admin on the dev server (or an E2E or parity run against the AppHost) opens a patient's export.
  - **StrictMode's drill runs the page's cleanup** right after it mounts. The cleanup drops the export from React Query's cache while the page is still showing it.
  - **React Query then builds a new, empty query** for the page and fetches again.
  - **Each request writes a `PatientExported` activity entry,** not deduplicated like views.
  - **So every visit logs two exports:** two browser requests when opened from another page, or the server render's plus one more on a full page load. The access report and activity log in a dev or demo database show double exports.
  - **It breaks the query's own promise:** "fetched once per visit to the export page: never refetched" (`patients.ts:184`). Production is unaffected.
- **Why it's here:** it's the same kind of bug as this PR's, the one the brief asked to look for.
- **Evidence:** `PatientExportPage.tsx:148`; `api/patients.ts:184-186, 192-193`; `MedApp.Application/Patients/Queries/ExportPatient/ExportPatientHandler.cs:28-29`; the probes.
- **Fix:** drop the export only once nothing shows it, after the drill has finished:

  ```tsx
  useEffect(() => {
    const {queryKey} = patientExportQuery(id);
    // StrictMode (development) runs this, then mounts the page again at once: drop it only once nothing shows it.
    return () => queueMicrotask(() => queryClient.removeQueries({queryKey, predicate: (query) => query.getObserversCount() === 0}));
  }, [queryClient, id]);
  ```

  - **Tried in a probe:** one request (none when it was already cached), with and without StrictMode, and gone after the page closes.
  - **The test** "drops the export from the cache when the page closes" (`PatientExportPage.test.tsx:275`) then has to wait for the microtask (`await waitFor(...)`).
  - **Add a test** with `strict: true` that counts one request.
- **What happened to it:** fixed in the PR. The export is dropped from the cache in a microtask, and only when nothing observes it (`getObserversCount() === 0`), so StrictMode's second run keeps it. A new StrictMode test checks it's asked for once (it fails without the fix), and the existing test waits for the removal.

### 2. A link pasted into the same tab isn't read, and its token stays in the address (from before this PR)

- **Severity:** low. **Confidence:** high for the hook (shown with a probe); medium for browsers (from the HTML standard, not tried in one).
- **What could go wrong:**
  - A mail client cut a reset link short. The page says "This link isn't complete. Open it from the email again."
  - The person copies the whole link from the email and pastes it into the same tab.
  - Only the fragment differs from the address the tab has, so the browser doesn't reload. It's a *same-document navigation* (the HTML standard's "navigate to a fragment").
  - The page is the same one, its effect doesn't run again, and it still says the link isn't complete.
  - **The whole token now sits in the address bar,** and in a new Back-button entry, and nothing clears it. A reset token works for 30 minutes.
  - **The same for a second confirm link** pasted over the first: the page keeps the first.
- **Does this PR's guard make it worse?** No. The effect never ran again for a fragment change, before or after the fix. The guard only acts within one mount.
- **Evidence:** `emailLink.ts:71-81` (one read, `[]` dependencies, no `hashchange` listener); the probe.
- **Fix:**
  - In the same effect, listen for `hashchange` (and remove the listener in its cleanup).
  - When the address holds a whole link, reload: `if (parseEmailLink(window.location.hash)) window.location.reload();`. The new page reads and clears it as usual, and nothing from the old link survives (the reset page would otherwise keep showing the old link's "expired" message from `reset.error`).
  - **The finer version:** take the new link into the state, overriding the first read, clear the fragment, and have each page reset its request (`reset.reset()`, or a `key` on the token).
  - **A test:** read one link, push a second, fire `hashchange`, and expect the reload (or the second token) and an empty fragment.
- **What happened to it:** left as a to-do in `docs/todo.md` (Privacy): listen for `hashchange` and reload when a whole link arrives.

### 3. Most tests still run without StrictMode, and the rule isn't written down

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the next effect that can't run twice ships the way this bug and finding 1 did: green tests, a broken dev server.
  - **Only the 6 new tests drill.** `renderApp` (the whole app, with its real routes and guards) has no `strict` option at all.
  - **Nothing else would catch it:** CI's `e2e` job tests the production build, where StrictMode is off.
  - **The rule isn't written down.** `MedApp.Web/AGENTS.md:96`, the web app's testing rule, doesn't say that effects must cope with running twice, or how to test it.
- **Evidence:** `render.tsx:51, 65, 70-83`; `src/test/setup.ts:11`; `MedApp.Web/AGENTS.md:96`; `@testing-library/react/dist/pure.js:132-133`.
- **Fix:**
  - **Use Testing Library's own option:** `render(app, {reactStrictMode: strict})`. Give `renderApp` the same option.
  - **Write it down:** add to the Tests bullet in `MedApp.Web/AGENTS.md` that an effect must cope with running twice (StrictMode, in development), and to test one that reads or clears something with `strict: true`.
  - **Later, try the whole suite in StrictMode:** `configure({reactStrictMode: true})` in `src/test/setup.ts`, then fix what fails (finding 1 at least). Tests that count requests may need a look. Not tried here: it means running the full suite.
- **What happened to it:** documented, and left as a to-do. `MedApp.Web/AGENTS.md` (Tests) now says effects must survive running twice and how to test it; `docs/todo.md` (Testing) has running more of the suite under StrictMode.

## Remaining risks

- **"Off the browser's history" means the tab's Back button.** `linkFragment.clear()` replaces the tab's history entry, so Back never shows the token. The browser's own History page and address-bar suggestions may still keep the address as first opened, token included. Not checked in a browser. The tokens limit the harm: a reset token lasts 30 minutes and works once; a confirm token needs its own account signed in. The hook's comment (`emailLink.ts:64-65`) could say "the tab's history".
- **A reload loses the link.** Once read, the token lives only in the page's memory (and, for confirming while signed out, in sessionStorage). Reloading the reset page says "This link isn't complete". That's by design, as the token leaves the address; people open the email again.
- **Half a link falls back to a kept one.** On `/confirm-email`, an address with only `#user=…` uses a link kept from an earlier sign-in in this tab, if there is one, rather than saying it's incomplete (`emailLink.ts:72`). The API still checks the account and the token, so it can only confirm what the signed-in account was sent. From before this PR.
- **Development React is only ever run by hand and by the parity tool.** A bug that shows only under StrictMode stays hidden from CI unless a test opts in (finding 3).
- **The tests use a memory history, not the browser's.** They don't cover TanStack Router noticing `clear()`: it patches `history.replaceState` and reloads its location (`@tanstack/history/dist/esm/index.js:333-337`), on the same route, so the page isn't remounted. The PR's browser checks cover it.

## Say it like this

"In development, React runs a page's start-up code twice, to catch code that can't cope with that. Our emailed-link pages read the token from the address and then wiped it, so the second run found nothing and said the link was broken. The fix accepts only the first read, with React's 'update from what's there now' form, so the token still never leaves the browser. The trade-off is a wasted second read, and a safety net of a few StrictMode tests rather than the whole suite running the way development does."

## Detail

### Every way the page can mount

| Situation | What runs | Result |
|---|---|---|
| Opened, production | The effect once | The link, and the fragment cleared |
| Opened, development | The effect, the drill's cleanup (none), the effect again; both updates are queued before React draws again | The first link read (tests and probe) |
| A hot reload in development | React keeps the state and runs the effect again, like the drill | The link kept (reasoned, not run) |
| Away and back (to sign in, then back to `/confirm-email`) | A new page, starting at `reading` | The kept link (probe) |
| A reload, or the link opened in a new tab | A new document | Read afresh |
| A link pasted into the same tab | No new page, no read | The old state; the token left in the address (finding 2) |
| The server's render | No effects | "Reading the link" |

### The two updates, step by step, in development

1. React shows the page in `reading`, matching the server's HTML.
2. **The effect's first run** reads `#user=…&token=…`, clears the fragment, and queues "if still reading, set found".
3. **The drill's second run** reads nothing (or a kept link), and queues "if still reading, set missing" (or the kept link).
4. **React draws:** it applies the first update (`reading` to found), then the second (found stays found).

Before the fix, step 3 queued a plain "set missing", applied last.

### Why a StrictMode inside the router doesn't drill

- **How React 19.3 picks what to drill** (`react-dom/cjs/react-dom-client.development.js:20356-20425`): after each commit, it walks down from the root looking for parts that have just mounted. It drills one only if it's already inside StrictMode where it starts, and it doesn't look inside it for a StrictMode further down.
- **The page mounts after the router has loaded,** as a new part whose top is the route, outside any StrictMode. So `renderWithApp(<StrictMode><Page /></StrictMode>)` runs the page's effects once (probe: 1).
- **The `strict` option** puts StrictMode above everything, so the route mounts inside it (probe: 2).

### The probe's numbers for finding 1

| How the export page opens | Requests, StrictMode | Requests, without | With the suggested fix (both) |
|---|---|---|---|
| Nothing cached | 2 | 1 | 1 |
| From another page (the loader's prefetch still in flight) | 2 | 1 | 1 |
| Already cached (a full page load: the server's render fetched it) | 1 | 0 | 0 |

### The fix for finding 2, in the hook

```ts
useEffect(() => {
  const link = parseEmailLink(window.location.hash) ?? fallback?.() ?? null;
  if (window.location.hash) linkFragment.clear();
  // eslint-disable-next-line react-hooks/set-state-in-effect
  setState((current) => (current.status !== 'reading' ? current : link ? {status: 'found', ...link} : {status: 'missing'}));
  // A link opened again in this tab changes only the fragment, so the page doesn't reload by itself: start afresh.
  const onHashChange = () => {
    if (parseEmailLink(window.location.hash)) window.location.reload();
  };
  window.addEventListener('hashchange', onHashChange);
  return () => window.removeEventListener('hashchange', onHashChange);
  // eslint-disable-next-line react-hooks/exhaustive-deps
}, []);
```

`linkFragment.clear()` uses `history.replaceState`, which fires no `hashchange`, so the hook's own clearing doesn't set it off.
