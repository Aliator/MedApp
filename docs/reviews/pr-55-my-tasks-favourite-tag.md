# Review: PR #55, no favourite name beside the star on My Tasks

**PR:** [#55](https://github.com/Aliator/MedApp/pull/55), branch `feature/my-tasks-no-favourite-tag`, reviewed at `11f03ff` on top of `main` at `1f88b4e` (the merge of #54). After the review, the branch was rewritten to fix findings 1 and 2 (see each finding's "What happened to it").

**Reviewed:** 16 November 2026, independently of the author's notes. Every claim below was checked against the code, a test run or the GitHub API.

## In one line

My Tasks stops showing its filter twice, and `AGENTS.md` gets a fixed, plain-language shape for review documents like this one.

## What changed, and why

- **The problem:** My Tasks opens filtered to the tasks not started. The search bar already shows that as a **token** (a small label for one filter): "Progress: is Not started". Beside the favourites star sat a second, blue label saying "Not started". The same fact, twice.
- **The fix, a switch on a shared component:** `ListFavourites` (the star and its menu) gets a new **prop** (a setting a page passes to a component), on by default. `ListFilterBar` (the search bar every list uses) passes it on. Only My Tasks turns it off. As reviewed it was `showsName` and hid every name. After finding 2 it's `namesQuickFilters`, and hides only the list's own quick filters; a saved search's name still shows.
- **Screen readers still hear it:** the star's **accessible name** (what a screen reader reads out for the button) still says "Favourites (showing Not started)".
- **Tests:** one new test for the star, and one new check in the My Tasks test.
- **Docs:** `MedApp.Web/AGENTS.md` mentions the switch. The root `AGENTS.md` gets a "Review document format" section: eight fixed sections in plain language, so each review can be understood quickly and explained to someone else.

## Why this, not that

- **A switch per list, not a rule inside the star:**
  - **Instead:** `ListFavourites` could decide by itself, for example by not naming a quick filter whose token already says the same thing.
  - **Chosen because:** it's one line on one page, easy to follow, and no other list changes.
  - **Worse at:** it's all or nothing for a list. On My Tasks it also hides the names people gave their own saved searches (finding 2). And the other lists that start filtered must each opt in by hand; none did (finding 3).
- **Hide the label, not the token:**
  - **Instead:** keep the label and drop the token from the bar.
  - **Chosen because:** the token is the real filter (you can edit or remove it); the label is only a reminder.
  - **Worse at:** nothing on screen now says "this is one of your favourites".
- **Keep the name in what screen readers hear:**
  - **Instead:** take it out of the star's name as well.
  - **Chosen because:** screen reader users lose nothing.
  - **Worse at:** sighted people can't see it. The star has no tooltip (Astryx's `IconButton` shows one only when given a `tooltip`), so its "showing …" is for screen readers only.
- **One fixed review format, not free-form reviews:**
  - **Instead:** keep writing reviews like #51 to #54, long and engineering-first.
  - **Chosen because:** quicker to take in and to explain, and the same shape every time.
  - **Worse at:** less depth up front (it moves to the detail at the end), and new reviews will look different from the earlier ones in the same folder.

## How it was checked

- **Read:** the whole diff (7 files, 60 lines added and 7 removed), the PR description, and the code around it: the star, the search bar, My Tasks, and the six other lists that use the bar.
- **Lint and type check** (`npm run lint`, `npm run typecheck`): clean.
- **Tests:** Vitest (the React app's test runner) on the star, the bar, My Tasks and the setup pages: 7 files, 70 tests, all passing.
- **Do the tests really test the change?** Yes. In a scratch copy, I broke the code on purpose to see whether the tests notice (a **mutation test**):
  - showing the label whatever the switch says: both new tests fail
  - the bar not passing the switch on: the My Tasks test fails
- **Scratch tests for the findings,** in the same copy:
  - My Tasks with a saved search named "Urgent to start": the star's name says it, and nothing on screen does (finding 2).
  - Stage definitions, opened as usual: "Active" shows in the bar and again beside the star (finding 3).
- **CI** (the checks GitHub runs on every PR): run [43820114284](https://github.com/Aliator/MedApp/actions/runs/43820114284) on `11f03ff` passed (`web`, `helm`, `unit-tests`, `image`). The coverage comment lists no file under 80%.
- **Server-side rendering** (the server builds each page first, then the browser takes over): safe. The switch is a fixed value, and nothing new reads the browser, so the server and the browser draw the same thing.
- **GitHub API:** the repository is public (finding 1).
- **Not done:** UI parity screenshots and the axe accessibility audit (both need the app running), and no screen reader or real browser. The PR says the full parity set (47 screens) passed.

## Findings

Most severe first. None is high or critical.

### 1. A personal health detail is published in the public `AGENTS.md`

- **Severity:** medium. **Confidence:** high that it's public; whether that was meant is for the owner to say.
- **What could go wrong:** the new section's second sentence names a medical condition of the owner. The repository is public, and the same paragraph says recruiters may ask about the project. A recruiter or employer reading `AGENTS.md` learns something about the owner's health that people usually choose when, and whether, to share. Once merged, it's in `main`'s history for good.
- **Evidence:**
  - `AGENTS.md:308`, the second sentence
  - the GitHub API reports the repository as `public`
  - the commit that adds it, `11f03ff`, is already pushed
- **Fix:** say what the reader needs, not why. For example: "Write for a reader who wants to take the change in quickly: plain language, short sections, bullets with bold lead-ins, and short sentences." If it shouldn't be public at all, rewrite the branch's commit before merging (amend it and force-push, which replaces the branch's history; fine on a feature branch), because a later commit that removes it leaves it in the history. GitHub may still serve the old commit by its ID for a while.
- **What happened to it:** fixed before merging. The sentence now reads "Each one must be quick and easy to take in", and the branch was rewritten and force-pushed, so no commit on it carries the detail. `11f03ff` is no longer on any branch, though GitHub may serve it by its ID for a while.

### 2. On My Tasks, a saved search's own name disappears too

- **Severity:** low. **Confidence:** high (shown by a scratch test).
- **What could go wrong:** someone saves "Not started, Urgent" as "Urgent to start" and makes it their starting search. Before this PR, My Tasks opened with a blue "Urgent to start" label beside the star. Now the bar shows "Progress: is Not started" and "Priority: is Urgent", and nothing on screen says it's their saved search. A screen reader still reads it from the star; a sighted user has to open the menu, where "Save this search…" is greyed out as "Already saved".
- **Why it's a gap:** the reason for the change (the bar's token already says it) holds for the list's quick filters, not for a name someone chose. The comments describe the change as about the progress filter, but the code hides every name.
- **Evidence:**
  - `MedApp.Web/src/components/tables/ListFavourites.tsx:122` hides the label whatever is showing, quick filter or saved search (`showing` comes from both, lines 71-73)
  - `MedApp.Web/src/pages/tasks/MyTasksPage.tsx:249` turns it off for the whole list
  - the narrower descriptions: `MyTasksPage.tsx:200-201` ("Not started and Started are favourites, not named beside the star") and `MedApp.Web/AGENTS.md:25` ("whose bar already shows its progress filter as a token")
- **Fix:** hide only quick filters' names. In `ListFavourites`, always show the label when what's showing is a saved search (sketch under Detail), and add a test with a saved search on My Tasks. Or, if hiding every name is what's wanted, say so in both comments.
- **What happened to it:** fixed before merging, as in the sketch. The switch is now `namesQuickFilters` and hides only quick filters' names. A new test checks that a saved search is still named beside the star, and the comments say so.

### 3. Three other lists still show the same duplicate

- **Severity:** low. **Confidence:** high (Stage definitions shown by a scratch test; the other two by reading the code).
- **What could go wrong:** Stage definitions and Templates open filtered to Active. Their bar shows "Status: is Active", and a blue "Active" sits beside the star: the very duplicate this PR removes from My Tasks. The procedures list does the same with "Assigned to me" when the person's setting starts it there. So one list has no label and three still do.
- **Evidence:**
  - `MedApp.Web/src/pages/setup/setupList.ts:48-52` (the "Active" quick filter) and `:58-61` (the bar starting on Active)
  - `MedApp.Web/src/pages/setup/StageDefinitionsPage.tsx:247-256` and `TemplatesPage.tsx:161-170`: no `showsFavouriteName`
  - `MedApp.Web/src/pages/procedures/ProceduresListPage.tsx:57-58` (the "Assigned to me" quick filter) and `:207` (the bar shows it when the list starts on it)
  - `MedApp.Web/AGENTS.md:46` names My Tasks, Stages and Templates together as the lists that start filtered
- **Fix:** one rule in `ListFavourites` fixes this and finding 2: don't name a quick filter that sets one filter whose token says the same ("Active", "Not started", "Assigned to me"); keep the name of a saved search, and of a quick filter that says more than its token (the patients list's "Over 65" says more than "Date of birth: is on or before …"). Then no list needs the switch. The smaller fix: pass `showsFavouriteName={false}` on the three lists.
- **What happened to it:** left as a to-do (`docs/todo.md`, Features). The PR was asked for My Tasks only.

## The honest catch

- **It's a switch, not a rule.** The star can't tell a quick filter's name the bar already shows from one it doesn't, so each list has to choose (finding 3).
- **When a quick filter is showing on My Tasks,** nothing visible says so beside the star. The star's "showing …" is for screen readers only.
- **The old commit may linger** (finding 1). The branch no longer has it, but GitHub can serve a commit by its ID for a while after it leaves every branch.
- **Not seen in a browser by this review.** Parity screenshots, the axe audit and a screen reader weren't run here.

## Say it like this

"On My Tasks the filter showed up twice, once in the search bar and once as a label by the favourites star, so I took the label out and kept it in what screen readers hear. I made it an opt-in switch on the shared component, so no other list changed, and it hides only the list's built-in quick filters, never a search someone saved and named. The trade-off is that each list has to opt in; a smarter rule inside the component could fix the other lists at once."

## Detail

- **The change in the star** (`ListFavourites.tsx:122`):

  ```tsx
  {showsName && showing && <Token label={showing} color="blue" />}
  ```

- **A sketch of the fix for finding 2:** tell a quick filter from a saved search, and hide only the first.

  ```tsx
  const quickShowing = quick.find((q) => keyOf(normalize(q.values)) === currentKey)?.label;
  const savedShowing = favourites.saved.find((f) => keyOf(normalize(f.values)) === currentKey)?.name;
  const showing = quickShowing ?? savedShowing;
  // …
  {(showsName || !quickShowing) && showing && <Token label={showing} color="blue" />}
  ```

  The rule in finding 3 would go further: a quick filter that sets one filter, and whose label is what that filter's token shows (the choice's label, such as `Active` or `Not started`, or a yes-or-no field's label, such as `Assigned to me`), isn't named; then `showsName` can go.
- **The mutation tests,** in a copy of `MedApp.Web` exported with `git archive` and sharing its `node_modules`:
  - `{showing && <Token …>}` at `ListFavourites.tsx:122`: "leaves the name out beside the star when told to, while the star still says it" and "starts on the tasks not started, with that filter in the search and their templates" both fail
  - without `showsName={showsFavouriteName}` at `ListFilterBar.tsx:277`: the My Tasks test fails; the bar's own tests don't check the pass-through, so My Tasks is what covers it
- **The scratch tests' results:**
  - My Tasks, search `{priority: 'Urgent'}`, a saved search "Urgent to start" (`progress: notStarted, priority: Urgent`): the star is "Favourites (showing Urgent to start)"; no element outside the bar and the menu has that text; the bar reads "Progress: is Not started" and "Priority: is Urgent".
  - Stage definitions, no search: the star is "Favourites (showing Active)"; one "Active" outside the bar, the menu and the table, which is the label beside the star.
- **The new format in `AGENTS.md` (lines 306-324):** it agrees with the merge flow, whose step 3 still asks each finding for `file:line`, a scenario, a severity, a confidence and a fix, and the link `#review-document-format` resolves. It drops the old "what's worth keeping" part; "Why this, not that" covers some of that.
