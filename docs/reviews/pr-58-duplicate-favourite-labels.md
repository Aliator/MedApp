# Review: PR #58, no duplicate quick filter labels on the setup and procedures lists

**PR:** [#58](https://github.com/Aliator/MedApp/pull/58), branch `feature/duplicate-favourite-labels`, reviewed at `e6f8699` on top of `main` at `88dc1fa` (the merge of #56).

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a test run or the GitHub API. **No finding is high or critical;** all three are low.

## Summary

Stage definitions, Templates and the procedures lists stop showing their starting filter twice, by turning off the quick filter name beside the favourites star, as My Tasks did in #55. It's right for every quick filter that only repeats its token; the setup lists' "All" is the exception, and a saved search that matches a quick filter is now named one way on screen and another to screen readers.

## What changed, and why

- **The problem:** these lists can open already filtered. Stage definitions and Templates open on Active, and the procedures lists (all procedures, and one patient's) open on Assigned to me when Account Settings says so. The search bar shows that filter as a **token** (a small chip for one filter, such as "Status: is Active"), and beside the favourites star sat a blue label saying it again ("Active"). The #55 review recorded this as its finding 3, and `docs/todo.md` kept it as a to-do.
- **The fix, one line per page:** each of the three pages passes `namesQuickFilters={false}` to `ListFilterBar` (the search bar every list uses), which hands it to `ListFavourites` (the star and its menu). With it off, the name of a **quick filter** (one of the list's own ready-made searches in the star's menu) no longer shows beside the star. It's the switch #55 added for My Tasks, and nothing in the shared components changes. Think of a crate stencilled "Apples" with a second sticker on it saying "Apples": the sticker goes, the stencil stays.
- **What still says it:** the token in the bar, which is the real filter (it can be edited or removed), and the star's **accessible name** (what a screen reader reads out for the button), such as "Favourites (showing Active)". A saved search's name still shows beside the star, as it was chosen on purpose.
- **Comments:** each page gets a one-line comment saying why the switch is off.
- **Tests:** each page's test of its starting state gains two checks. The star still names the quick filter, and that name appears nowhere outside the bar, the star's menu and (on the setup lists) the table, whose status column says Active on every active row. The Templates test also checks that the bar starts on "Status: is Active".
- **Docs:** `MedApp.Web/AGENTS.md:25` names all four lists that turn the names off, and says when a list should. The to-do in `docs/todo.md` is removed as done.

## Design choices

- **The same switch on each list, not a rule inside the star:**
  - **Instead:** `ListFavourites` could leave out a quick filter's name by itself whenever that quick filter sets one filter whose token already says its name, and the switch could go. The removed to-do offered both.
  - **Chosen because:** three one-line changes on the pages, no change to the shared component, and the same mechanism My Tasks already uses.
  - **Worse at:**
    - it's all or nothing for a list: on the setup lists it also hides "All", which repeats no token (finding 1)
    - a list whose quick filters are mixed, like the audit log's, can't use it without losing names that say more than their tokens
    - each new list that starts on a quick filter has to opt in, guided only by one sentence in `MedApp.Web/AGENTS.md`, which states a different rule from the one applied (finding 3)
- **Drop the to-do, not reword it:**
  - **Instead:** keep a shorter to-do for the rule inside the star.
  - **Chosen because:** the problem it described is gone on every list that starts filtered.
  - **Worse at:** the rule-based alternative is now written down only in the #55 review.
- **Keep the name in the star's accessible name** (inherited from #55):
  - **Instead:** take it out there too.
  - **Chosen because:** screen readers lose nothing.
  - **Worse at:** nothing visible says it. The star has no tooltip, so on these four lists nothing beside the star shows that a quick filter is on.

## How it was checked

- **Read:** the whole diff (8 files, 22 lines added and 3 removed), the PR description, and the code around it: the star, the bar, the three pages and their quick filters (`setupList.ts`, `ProceduresListPage.tsx`), the other lists that use the bar (My Tasks, Patients, Users, the audit log), the test helpers `searchBar` and `pickFavourite`, and the #55 review.
- **Each quick filter's name against its token,** on all seven pages that have quick filters (table under Detail). On the changed lists, every name repeats its token except the setup lists' "All", which has none (finding 1).
- **Lint and type check** (`npm run lint`, `npm run typecheck`): clean.
- **Tests:** Vitest (the React app's test runner) on the setup pages' folder, the procedures list, My Tasks, the star and the bar: 8 files, 85 tests, all passing.
- **Do the tests really test the change?** Yes. In a scratch copy, the labels were turned back on in all three pages (a **mutation test**: breaking the code on purpose to see whether the tests notice). Each page's starting test fails at its new check, with exactly one stray element: the label beside the star. The helper `searchBar()` is only the PowerSearch box, not the row that also holds the star, so the checks' filter can't hide the label they look for.
- **Scratch tests for the findings,** in the same copy, each run with the PR's code and with the labels back on:
  - Stage definitions showing everything (`status: 'any'`): the bar is empty, the star is "Favourites (showing All)", and "All" appears nowhere outside the bar, the star's menu and the table. With the labels on, it appears once, beside the star (finding 1).
  - The procedures list on Assigned to me, with a saved search "My procedures" that sets the same filter: the label beside the star says "My procedures", and the star's name says "Favourites (showing Assigned to me)". With the labels on, both say "Assigned to me" (finding 2).
- **Docs against the code:** `MedApp.Web/AGENTS.md:25` against the four pages it names and the three that keep their names (Patients, Users, the audit log) (finding 3), and the page comments against their quick filters (finding 1). A search of the tracked files for other descriptions of the label beside the star found only `MyTasksPage.tsx:200-202` and the #55 review, both still accurate.
- **CI** (the checks GitHub runs on every PR): run [43825527971](https://github.com/Aliator/MedApp/actions/runs/43825527971) on `e6f8699` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`). The coverage comment lists no .NET class or React file under 80%.
- **Server-side rendering** (the server builds each page first, then the browser takes over): safe. The switch is a fixed value, and nothing new reads the browser.
- **Not done:** the full `npm test` locally (CI's `web` job ran it), UI parity screenshots, the axe accessibility audit, a screen reader and a real browser.

## Findings

Most severe first. None is high or critical. All three were open when this review was written.

### 1. "All" on the setup lists repeats no token, so hiding it does lose something

- **Severity:** low. **Confidence:** high on the behaviour (shown by a scratch test); medium on how much it matters.
- **What could go wrong:** on Templates, the "Status: is Active" token is removed, or "All" picked from the star, to see archived templates too. The bar is then empty, showing only its placeholder. Before this PR, a blue "All" beside the star said the list now includes archived ones; now nothing beside the bar does. These lists start on Active, so an empty bar is the unusual state here, not the default. The facts are still on screen, less directly: the subtitle drops "active" (for example "3 templates" instead of "2 active templates"), and archived rows say Archived. The `admin-templates` parity screen (`/admin/templates?status=any`) is this exact state, so its screenshot loses the label.
- **Why it's a gap:** the PR's reasoning is that each name repeats its token, so nothing is lost. "All" sets no filter, so it has no token to repeat. The PR description ("Every quick filter on these lists only repeats its token, so nothing is lost"), both page comments and `MedApp.Web/AGENTS.md` all say otherwise.
- **Evidence:**
  - `MedApp.Web/src/pages/setup/setupList.ts:51`: `{label: 'All', values: {}}`
  - `setupList.ts:58-61`: with status `any`, the bar gets no status, so no token
  - `MedApp.Web/src/components/tables/ListFavourites.tsx:43-48` and `:80`: an empty quick filter matches an empty bar, so "All" is what's showing
  - the comments at `MedApp.Web/src/pages/setup/StageDefinitionsPage.tsx:257` and `TemplatesPage.tsx:171` ("Each quick filter's name is its token's"), and `MedApp.Web/AGENTS.md:25`
  - `tools/ui-parity/screens.ts:247`
- **Fix:** an empty search reads as "everything" almost anywhere, so accepting this is reasonable; then say so. In both comments: "Each quick filter's name is its token's (Status: is Active), and All's is the empty bar, so it isn't repeated beside the star." In `MedApp.Web/AGENTS.md:25`: "…when each quick filter's name just repeats what its bar shows". If the cue matters, the rule inside the star (see Design choices) would keep "All" named and hide only "Active" and "Archived".
- **What happened to it:** open.

### 2. A saved search that matches a quick filter is named one way on screen and another to screen readers

- **Severity:** low. **Confidence:** high (shown by a scratch test).
- **What could go wrong:** on the procedures list, "Assigned to me" is picked from the star, then saved with "Save this search…" as "My procedures", perhaps as the starting search. Saving is allowed, because only a matching saved search counts as "Already saved". With this PR, the blue label beside the star says "My procedures", while the star's accessible name says "Favourites (showing Assigned to me)". What's on screen and what a screen reader reads out disagree, so the same list goes by two names. Before this PR, both said "Assigned to me". The same happens on the setup lists (for example "Archived" saved as "Retired stages"), and on My Tasks since #55.
- **Why:** with the names off, the label prefers the saved search's name, but the star's name uses `showing`, which prefers the quick filter's.
- **Evidence:**
  - `MedApp.Web/src/components/tables/ListFavourites.tsx:82`: `const showing = quickShowing ?? savedShowing;`
  - `ListFavourites.tsx:83`: `const label = namesQuickFilters ? showing : savedShowing;`
  - `ListFavourites.tsx:132` (the label, from `label`) and `:139` (the star's name, from `showing`)
  - `ListFavourites.tsx:84` and `:112-114`: saving is refused only when a saved search matches
  - the code is from #55; this PR brings it to three more lists, and saving a quick filter under a name of its own is most likely on the procedures list
- **Fix:** prefer the saved search's name in both places, as it was chosen on purpose. At `ListFavourites.tsx:82`, `const showing = savedShowing ?? quickShowing;` (sketch under Detail). Then the label and the star agree whenever a label shows, on every list. Add a `ListFavourites` test with a saved search that has a quick filter's values and `namesQuickFilters: false`, expecting the label and the star's name both to say the saved name. Or refuse to save a quick filter's search ("Already a quick filter").
- **What happened to it:** open.

### 3. The documented rule doesn't match which lists turn the names off

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** `MedApp.Web/AGENTS.md:25` now says a list turns the names off "when each quick filter's name just repeats the token its bar shows". Users' quick filters fit that exactly ("Admins" is "Role: is Admin", "Users" is "Role: is User"), yet Users keeps its names: pick "Admins" and the bar says "Role: is Admin" with a blue "Admins" beside the star, the duplicate this PR removes elsewhere. "All" doesn't fit the rule (finding 1), yet loses its name. The rule actually applied is "the list starts, or can start, on a quick filter", where the duplicate shows before anything is picked. A new list built from the docs, or a tidy-up of Users, gets a different answer from the docs than from the code. And the audit log can't follow either rule with a switch: "Deletions" repeats "Action: is Deleted", but "Today", "Last 7 days" and "Assignments" say more than their tokens.
- **Evidence:**
  - `MedApp.Web/AGENTS.md:25`
  - `MedApp.Web/src/pages/users/UsersPage.tsx:351-354`
  - `MedApp.Web/src/pages/audit/AuditLogPage.tsx:33-39`
  - the line this PR removes from `docs/todo.md` (line 20 on `main`), the only place besides the #55 review that described the rule inside the star
- **Fix:** state the rule the code follows. For example: "…which a list turns off when it starts, or can start, on a quick filter its bar already shows as a token: My Tasks, the procedures lists, Stage definitions and Templates". Or turn the names off on Users too, if the duplicate after a pick matters as much as at the start. If the rule inside the star is still wanted for mixed lists like the audit log, keep a to-do line for it.
- **What happened to it:** open.

## Remaining risks

- **Opt-in per list:** a new list that starts on a quick filter shows the duplicate again unless its page sets the switch, and only one sentence in `MedApp.Web/AGENTS.md` says so.
- **Nothing visible says a quick filter is on** on My Tasks, Stage definitions, Templates or the procedures lists. The star's "showing …" is for screen readers only, as the star has no tooltip.
- **Parity screenshots will change:** `admin-stages` and `admin-templates` lose their labels, and so do `procedures-list` and `patient-procedures` if the parity account starts on Assigned to me. The PR doesn't say the screens were captured; the merge flow asks for the full set when a branch changes the UI.
- **Not seen in a browser by this review.** Parity screenshots, the axe audit and a screen reader weren't run here.

## Say it like this

"Stage definitions, Templates and the procedures lists no longer show their starting filter twice, once as a token in the search bar and again as a label by the favourites star. It reuses the switch My Tasks got in #55, so it's one line per page and the shared component doesn't change. The trade-off is that the switch is all or nothing for a list, so the setup lists' 'All', which has no token, loses its label too, and each new list has to remember to opt in."

## Detail

- **Every quick filter, the token it shows, and whether its name says more:**

  | List | Quick filter | Token in the bar | Name says more? |
  |---|---|---|---|
  | Stage definitions, Templates (names off in this PR) | Active | Status: is Active | No |
  | | Archived | Status: is Archived | No |
  | | All | none: the bar is empty | Yes (finding 1) |
  | Procedures, all and one patient's (names off in this PR) | Assigned to me | Assigned to me: only | No |
  | | Overdue | Overdue: only | No |
  | | Unassigned, Blocked, In review | Status: is Unassigned, Blocked, In review | No |
  | My Tasks (names off since #55) | Not started, Started | Progress: is Not started, Started | No |
  | | Overdue, Blocked | Overdue: only; Status: is Blocked | No |
  | Users (names on) | Admins, Users | Role: is Admin, User | No (finding 3) |
  | Audit log (names on) | Today, Last 7 days | When: is on or after (a date) | Yes |
  | | Status changes, Deletions | Action: is Status changed, Deleted | No |
  | | Assignments | Action: is any of Assigned, Unassigned | Yes, a summary |
  | Patients (names on) | Under 18, 65 and over | Date of birth: is on or after, or on or before (a date) | Yes |

- **A sketch of the fix for finding 2,** in `ListFavourites.tsx`:

  ```tsx
  const quickShowing = quick.find((q) => keyOf(normalize(q.values)) === currentKey)?.label;
  const savedShowing = favourites.saved.find((f) => keyOf(normalize(f.values)) === currentKey)?.name;
  // A saved search's name wins, beside the star and in its accessible name alike.
  const showing = savedShowing ?? quickShowing;
  const label = namesQuickFilters ? showing : savedShowing;
  ```

  On a list with the names on (Patients), the label then also names a saved search before a quick filter with the same values, so both settings of the switch prefer the saved name.
- **The mutation test,** in a copy of `MedApp.Web` exported with `git archive`, with its `node_modules` linked to the worktree's: `namesQuickFilters={false}` changed to `{true}` in all three pages. The three starting tests fail, each with `expected [ <span dir="ltr" …(1)></span> ] to deeply equal []`:
  - `ProceduresListPage.test.tsx:95`, "starts on Assigned to me when Account Settings says so, until its filter is removed (mine=false)"
  - `StageDefinitionsPage.test.tsx:43`, "lists the active definitions by name, with what uses them; archived ones from the search"
  - `TemplatesPage.test.tsx:43`, "lists the active templates, with their stages and how many procedures use them"
- **The scratch tests' results:**
  - Stage definitions, search `{status: 'any'}`: the bar's text is only its label, "Search stages"; the star is "Favourites (showing All)"; the subtitle is "2 stages"; no "All" outside the bar, the menu and the table (one with the labels on).
  - Procedures list, search `{mine: true}`, a saved search `{id: 'f1', name: 'My procedures', values: {mine: true}}` for the list `procedures`: the star is "Favourites (showing Assigned to me)"; one "My procedures" outside the bar, the menu and the table, and no "Assigned to me". With the labels on: no "My procedures", one "Assigned to me".
- **A doc note outside the diff:** `MedApp.Web/AGENTS.md:46` gives the lists that start filtered as My Tasks, Stages and Templates, without the procedures lists, which can start on Assigned to me. This PR doesn't touch that line; it's incomplete rather than wrong.
