# Review: PR #56, a standard format for review documents

**PR:** [#56](https://github.com/Aliator/MedApp/pull/56), branch `feature/standard-review-format`, reviewed at `12d5f9a` on top of `main` at `a600c6a` (the merge of #55).

**Reviewed:** 16 November 2026, independently of the author's notes. Every claim below was checked against the files, git or the GitHub API.

## Summary

The "Review document format" section of `AGENTS.md` becomes a neutral, seven-section standard, and each finding still needs the same evidence as before. Personal preferences about how reviews are written move out of the shared file, into each contributor's own local file that git leaves out of commits.

## What changed, and why

Only `AGENTS.md` changes: 10 lines added and 9 removed, all in its last section (lines 306-325).

- **The opening is about the writing, not the reader:** it now asks for reviews that someone who didn't make the change can follow quickly: short sections, bullets with bold lead-ins, short sentences, and any term that isn't obvious explained the first time it appears. Before, it had reader-specific framing, and asked for every technical term to be explained.
- **Standard section names:** "In one line" becomes "Summary" (now one or two sentences), "Why this, not that" becomes "Design choices", and "The honest catch" becomes "Remaining risks". It's like swapping a form's chatty box labels for the ones everyone recognises: the boxes still ask for the same things.
- **Two things dropped:** the "Say it like this" section (a few sentences to explain the change out loud), and the advice to add analogies. Eight sections become seven.
- **Findings keep their rigour:** each still needs a severity and a confidence, a concrete scenario, `file:line` evidence, and the fix with what happened to it. Only "in plain words" is gone from the scenario.
- **A new last line about personal preferences:** preferences for how reviews or explanations are written belong in a contributor's own **git-ignored** file (one that git leaves out of commits), not in `AGENTS.md`. Think of personal sticky notes on your own copy of a shared manual, instead of writing in the copy everyone reads.
- **Why:** `AGENTS.md` is public, and every agent and contributor working here follows it. The PR makes the format describe the project's standard, with nothing reader-specific in it.
- **One whitespace fix:** a blank line inside item 5 lost three trailing spaces. Markdown treats both as a blank line, so it renders the same.

## Design choices

- **Standard names, not the old sections reworded:**
  - **Instead:** keep all eight sections and their names, reworded so nothing is reader-specific.
  - **Chosen because:** names anyone recognises from other reviews and reports, and a shorter format.
  - **Worse at:** the format no longer asks for a short spoken explanation or for analogies, which made a review easy to retell. And the #55 review, written to the old format, now looks different from the ones that follow.
- **Personal preferences in a local file, not in `AGENTS.md`:**
  - **Instead:** name optional extras in the shared format ("a review may add …"), so everyone can see them.
  - **Chosen because:** the shared, public file stays neutral, and each contributor can tune their own reviews without changing them for everyone.
  - **Worse at:**
    - nobody else can see those preferences, so a committed review can follow rules that aren't in the repository (finding 3)
    - the file is ignored only in a clone set up for it (finding 2)
    - the file the line names is read by one agent tool, while `AGENTS.md` (line 3) is written for any AI coding agent

## How it was checked

- **Read:** the whole diff (1 file, 10 lines added and 9 removed), the PR description, the whole "Git workflow" part of `AGENTS.md` (lines 269-325), `docs/reviews/README.md`, and the #55 review, the last one written to the old format.
- **Agrees with the merge flow:** step 3 (`AGENTS.md:294-298`) asks each finding for `file:line`, a scenario, a severity, a confidence and a fix. The format's findings (lines 315-321) ask for the same, plus what happened to the fix. The link at line 297, `#review-document-format`, still matches the unchanged heading at line 306. (An **anchor** is the part of a link after `#` that jumps to a heading.)
- **Old section names:** searched every tracked file for "In one line", "Why this, not that", "honest catch", "Say it like this", "plain-language" and phrases from the old opening. They appear only in the #55 review and its row in `docs/reviews/README.md`, which record #55 as it was. Nothing else, such as `docs/architecture.md` or `docs/reviews/overview.md`, depends on a section's name.
- **Personal or reader-specific text in public files:** searched the tracked docs. `AGENTS.md` now has none; the #55 review still does (finding 1).
- **Ignore rules:** checked the committed ignore files for the file name at line 325. None lists it (finding 2).
- **Visibility:** the GitHub API reports the repository as public.
- **Rendering:** the only whitespace change sits inside a list item, between a nested list and a paragraph. A line of spaces and an empty line are both blank lines in Markdown, so nothing moves.
- **CI** (the checks GitHub runs on every PR): run [43822700566](https://github.com/Aliator/MedApp/actions/runs/43822700566) on `12d5f9a` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`).
- **Not done:** no local build or test run. The change is docs-only, and CI ran both.

## Findings

Most severe first. None is high or critical. All five were fixed in the PR before merging.

### 1. The #55 review still carries the framing this PR removes

- **Severity:** medium. **Confidence:** high that the text is public; whether to change it is a judgement call.
- **What could go wrong:** this PR makes `AGENTS.md` neutral, but the #55 review, already on `main`, still describes the personal detail behind its finding 1 in more words than a public file needs, and keeps the old format's reader-specific framing. Anyone browsing `docs/reviews/` on GitHub, or a search engine indexing `main`, reads it there instead.
- **Evidence:**
  - `docs/reviews/pr-55-my-tasks-favourite-tag.md:60-61`, finding 1's severity line and scenario
  - `docs/reviews/pr-55-my-tasks-favourite-tag.md:17` and `:35`, which describe the old format's purpose
  - on `main` since the merge of #55 (`a600c6a`), in a public repository
- **Fix:** in this PR, reword lines 60-61 to say only what was wrong, in general terms. For example:
  - line 60: "**Severity:** medium. **Confidence:** high."
  - line 61: "**What could go wrong:** the new section's second sentence included a personal detail that doesn't belong in a public repository. Once merged, it's in `main`'s history for good."
  - lines 17 and 35: drop "and explained to someone else" and "and to explain"

  The old text stays in `main`'s history, and rewriting `main` would be out of proportion. Taking it out of the current files keeps it off the pages people browse.
- **What happened to it:** fixed in this PR: the severity line and the scenario now say only that a personal detail was included, and the two old purpose lines are trimmed.

### 2. Nothing committed makes the local file git-ignored

- **Severity:** medium. **Confidence:** high that no committed rule ignores it; medium on how likely it is to bite.
- **What could go wrong:** line 325 sends personal preferences to a contributor's "own git-ignored" file. But the repository's `.gitignore` doesn't list it, so it's ignored only in a clone whose own settings say so. A contributor, or anyone on a fresh clone, follows line 325, creates the file and commits with `git add -A`. Their personal preferences are pushed to a public repository, which is what this PR sets out to prevent. It's a "do not post" sticker that exists only on one copy.
- **Evidence:**
  - `AGENTS.md:325` calls the file git-ignored
  - `.gitignore` has no line for that file name; the other committed ignore files (`MedApp.Web/.gitignore`, `tools/ui-parity/.gitignore`) apply only inside their own folders
- **Fix:** add the file name from line 325 to `.gitignore`, one line under a comment such as `# Personal agent instructions (local only)`. Then "git-ignored" is true in every clone.
- **What happened to it:** fixed in this PR: `.gitignore` lists the file, under "Personal agent instructions (local only)".

### 3. The format doesn't say whether a review may rename or add sections

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the format says "Use these sections, in this order", and line 325 moves preferences about writing into local files. It doesn't say whether those may rename a section or add one. The #55 review and this one both have "The honest catch" and a "Say it like this" section; a review written from `AGENTS.md` alone has "Remaining risks" and neither. Someone collecting the open risks by searching the folder for "Remaining risks" misses those reviews, and an agent that copies the latest review as a template copies sections the format doesn't list. Nothing in the repository says which shape is right.
- **Evidence:**
  - `AGENTS.md:310` ("Use these sections, in this order:") and `:325`
  - `docs/reviews/pr-55-my-tasks-favourite-tag.md:93` and `:100`, and this review's own headings
- **Fix:** say which in the format, in one sentence. For example, "A review may add sections and rename one, but keeps these seven, in this order." Or, to keep every review searchable by section, "Keep these names; a review may add sections."
- **What happened to it:** fixed in this PR: the format now says to keep these names in this order, and that a review may add sections but not rename or drop these. This review's risks section is renamed to match.

### 4. The format doesn't ask for the commit reviewed or the highest severity

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** a branch often changes after its review, as #55's did when it was rewritten to fix its findings. The line numbers in the findings then point at code that has moved, and without the commit reviewed nobody can recheck a finding later. Already, the #55 review's "`AGENTS.md` (lines 306-324)" doesn't match `main` once this PR merges (306-325); only its header's commit makes it checkable. Every review so far has that header by habit, but the format doesn't ask for it, so the next one may not. The format also doesn't ask for the highest severity up front, though a high or critical finding stops the merge and the reviews table has a "Highest risk" column.
- **Evidence:**
  - `AGENTS.md:310-323`: no header in the list
  - line 3 of each review from #51 to #55 names the commit reviewed and its base
  - `docs/reviews/pr-55-my-tasks-favourite-tag.md:129`
- **Fix:** add a line before the list: "Start with the PR's link and branch, the commit reviewed and the `main` it was compared with, the date, and whether any finding is high or critical."
- **What happened to it:** fixed in this PR: the format now starts with a header line asking for the PR, branch, commit reviewed, its base, the date, and whether any finding is high or critical.

### 5. The severities aren't defined where the format asks for them

- **Severity:** low. **Confidence:** medium (grading is partly judgement anyway).
- **What could go wrong:** the format and the merge flow ask for critical, high, medium or low, and a high or critical finding stops a merge. But `AGENTS.md` doesn't say what each means, or link to where it's said. `docs/reviews/README.md` defines high, medium and low, and nothing defines critical. A reviewer working from `AGENTS.md` grades a finding that exposes data as medium (the README calls exposing data high), the flow doesn't stop, and the PR merges. Or two reviews grade the same kind of problem differently.
- **Evidence:**
  - `AGENTS.md:296`, `:298` and `:316`
  - `docs/reviews/README.md:30-34`, "How the risks are graded": high, medium and low only
- **Fix:** in the format, point to the grading ("as graded in `docs/reviews/README.md`"), and add critical there, for example: "**Critical:** loses or exposes data, or breaks `main` or a deploy, now. Stop and fix it first."
- **What happened to it:** fixed in this PR: the format points to the grading in `docs/reviews/README.md`, which now defines critical.

## Remaining risks

- **Preferences outside the repository are invisible:** a committed review can follow rules nobody else can see (finding 3). And the file the new line names is read by one agent tool, though `AGENTS.md` is written for any agent.
- **The old wording stays in history:** the previous opening, and the #55 review's fuller wording, are in `main`'s history (finding 1). Only the current files could change.
- **Proof comes later:** it's a format, so whether it works shows only in the next reviews.

## Say it like this

"The review format in our agent instructions is now a plain, seven-part standard: summary, what changed, design choices, how it was checked, findings, remaining risks and optional detail, with findings as rigorous as before. The main choice was to keep personal preferences about how reviews read out of the shared file, in each contributor's own local file that git ignores. The trade-off is that those preferences are invisible to everyone else, so the format allows extra sections but keeps the standard ones fixed, which keeps every review searchable."

## Detail

- **The sections, before and after:**

  | Before (#55) | After (#56) |
  |---|---|
  | 1. In one line | 1. Summary (one or two sentences) |
  | 2. What changed, and why (with analogies) | 2. What changed, and why |
  | 3. Why this, not that | 3. Design choices |
  | 4. How it was checked | 4. How it was checked |
  | 5. Findings | 5. Findings |
  | 6. The honest catch | 6. Remaining risks |
  | 7. Say it like this | (removed) |
  | 8. Detail (optional) | 7. Detail (optional) |

- **The searches:**
  - `git ls-files | xargs grep -n -i -E "In one line|Why this, not that|honest catch|Say it like this|plain-language"`: only the #55 review and its row in `docs/reviews/README.md`, plus an unrelated "say so in one line" at `AGENTS.md:261`
  - the same over the tracked docs for personal and reader-specific wording: only the #55 review's lines in finding 1
  - `.gitignore` for the file name at `AGENTS.md:325`: no match
  - `gh api repos/Aliator/MedApp --jq .visibility`: `public`
  - CI run 43822700566: `head_sha` is `12d5f9a`, conclusion `success`
- **The fixes for findings 3 to 5, as one edit to the format:**

  ```markdown
  Start with the PR's link and branch, the commit reviewed and the `main` it was compared with, the date, and whether any finding is high or critical.

  Use these sections, in this order. A review may add sections, but keeps these seven, with these names, in this order:
  …
  5. **Findings,** most severe first. Each has:
     - a severity (critical, high, medium or low, as graded in `docs/reviews/README.md`) and a confidence
  ```

  And in `docs/reviews/README.md`, under "How the risks are graded", before "High":

  ```markdown
  - **Critical:** loses or exposes data, or breaks `main` or a deploy, now. Stop and fix it first.
  ```
