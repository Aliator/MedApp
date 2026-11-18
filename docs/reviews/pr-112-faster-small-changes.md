# Review: PR #112, CI skips docs-only pushes, lighter small changes, and a dated npm audit exception

**PR:** [#112](https://github.com/Aliator/MedApp/pull/112), branch `feature/faster-small-changes`. Reviewed at `f241c5e`, compared with `main` at `894c1fe` (also the merge base).

**Reviewed:** 17 November 2026, as an independent, adversarial review, without the PR's own conclusions. Each claim below was checked by reading the workflow, by running its two scripts locally against real commits and real `npm audit` output, and through the GitHub API (the ruleset, past runs, job logs). No container, AppHost, browser or test suite was run. **No finding is high or critical:** 1 is medium and 4 are low.

## Summary

The PR makes CI skip its build and test jobs for a push that changes only `docs/`, on top of a commit whose run passed. So a review document pushed last no longer waits about 9 minutes for tests of code that didn't change. It also has the main session make small changes itself and review them at high effort, and lets `npm audit` pass one unfixable picomatch advisory until 17 December. The mechanics are careful, and failed safe in every case tried. The one real gap: a skipped run vouches for the branch as it was tested against an older `main`, and `main`'s ruleset doesn't require a PR to be up to date before it merges.

## What changed, and why

3 files, 123 lines added and 7 removed.

- **A `changes` job** (`.github/workflows/main.yml:9-53`) decides whether this push needs the build and tests.
  - **What it reads:** the push's before and after commits (from the `pull_request` event), GitHub's comparison of the two, and whether this workflow passed on the before commit.
  - **What it says:** `code=false` only when all of these hold: the after commit contains the before one ("ahead"), fewer than 300 files changed, every file's name (old and new, for a move) is under `docs/`, and the before commit has a passing run. Anything else, or any API error, gives `code=true`.
  - **Why:** in the merge flow, the last push is usually the review document alone, and it re-ran every suite (6 to 8 minutes) on unchanged code.
  - **The compare API**, explained: GitHub lists the files that differ between two commits. Like the "track changes" summary between two saved versions of a document.
- **Six jobs wait for it, and skip on `false`:** `web`, `image`, `e2e`, `helm`, `integration-tests` and `unit-tests` (`main.yml:58-60, 149-151, 236-238, 399-401, 543-545, 562-564`). Each has `needs: changes` and `if: ${{ !cancelled() && needs.changes.outputs.code != 'false' }}`.
  - **Required checks**, explained: the six jobs `main`'s ruleset insists pass before a merge. GitHub counts a job skipped by its own `if` as passing. Like a station gate that waves you through because your ticket was already checked at the last gate, and nothing changed since.
  - **Fail-open**, explained: the `if` says "run unless `changes` said `false`", so if `changes` itself breaks, everything runs. `!cancelled()` is needed for that: an `if` with no status function gets an invisible `success()` in front, which would skip (and so pass) the six jobs whenever `changes` failed.
- **An npm audit exception** (`main.yml:77-117`): the audit step lets through the GitHub advisory IDs in `ACCEPTED` until `ACCEPTED_UNTIL`, and fails on any other high or critical advisory. After the date it runs the plain audit again.
  - **Why:** GHSA-fc5q-39xc-vjpf, a high ReDoS in picomatch with no fixed version (every version up to 4.0.7, the latest), fails every PR's `web` job. picomatch reaches only build and test tools (Vite, Vitest, the router plugin, typescript-eslint, the Astryx CLI).
  - **ReDoS**, explained: a regular expression that takes exponentially long on certain text, so one crafted input ties up the process. Like a lock that takes an hour to reject one particular wrong key.
  - **An advisory ID (GHSA)**, explained: GitHub's catalogue number for one published vulnerability. Accepting by ID means one known problem, not a whole package: a new advisory against picomatch would still fail.
- **`AGENTS.md`:**
  - the CI section describes `changes` and what makes it run everything (`AGENTS.md:538-546`)
  - the npm supply chain rule describes `ACCEPTED` and `ACCEPTED_UNTIL`, in place of `--omit=dev` (`:571`)
  - a CI watcher waits for one PR each, and expects about 7 minutes, or under one for a docs-only push (`:588-589`)
  - **small changes are lighter** (`:621-625`): a fix or tweak in a few files, with nothing new and nothing sensitive, is made by the main session itself and reviewed at high rather than xhigh. The merge flow says so (`:636, 645`).
- **`docs/todo.md`:** the to-do the exception expires with (`:148`), and an unrelated one for a light demo on GitHub Pages (`:135`).

## Design choices

- **Compare the push, not the PR:**
  - **Instead:** `paths-ignore` on the workflow, or a path filter action from the marketplace.
  - **Why not:** `paths-ignore` looks at the whole PR, which always contains code. And a workflow skipped that way never reports its required checks, so they stay "Expected" and block the merge. A third-party filter action is a supply chain risk the repository's rules avoid where a few lines of shell do the job.
  - **Worse at:** it trusts the previous run's result, so it's only as good as what that run tested (finding 1).
- **Skipped jobs pass the required checks, rather than one gate job:**
  - **Instead:** a final job (`if: always()`) that needs every other job and fails unless each passed, or was skipped because `changes` said `false`. That gate becomes the only required check. Many large projects do this (nuxt's `ci-ok`, for one).
  - **Chosen because:** no change to the ruleset, and GitHub's rule for skipped jobs does the work.
  - **Worse at:** the rule that makes skipping safe is copied into six `if` lines. A new job that copies the `if` without `!cancelled()` would pass whenever `changes` fails. A gate keeps the rule in one place.
- **A dated allowlist in the workflow, rather than `--omit=dev`:**
  - **Instead:** `npm audit --omit=dev`, as the old rule said.
  - **Chosen because:** `--omit=dev` would stop checking every build and test tool, for good, to accept one advisory. The allowlist keeps every other advisory failing, and lapses by itself.
  - **Worse at:** 30 lines of shell and Node in the workflow instead of one flag, and the date isn't checked (finding 2).
- **Small changes made by the main session, reviewed at high:**
  - **Instead:** the three stages for every change, as before.
  - **Chosen because:** a subagent's cold start (reading the rules and the code afresh) costs more than a change this size, and the xhigh review was the longest stage. Those measurements are the PR's; they weren't repeated here.
  - **Worse at:** whoever makes the change also decides it's small (finding 3).

## How it was checked

- **Read:** the diff, the PR description, the whole of `main.yml`, `publish.yml`, `.dockerignore` and the Dockerfile's copies, `AGENTS.md` around every change, and `docs/reviews/README.md`'s grading.
- **What reads `docs/`:** searched every `.cs`, `.csproj`, `.ts`, `.tsx`, `.mjs`, `.js`, `.json`, `.yml`, `.sh` and `.ps1` file for `docs/` and for a quoted `docs`. Only comments mention it, and `.dockerignore` leaves `docs/` out of the image. So "no build or test reads `docs/`" holds today.
- **The `changes` script, run locally:** extracted from the workflow and run under `bash -e`, with the environment it gets, against real commits.
  - **30 pairs of consecutive PR runs whose first passed:** 28 would have run everything (code changed, `main` merged in, or a Dependabot rebase). 2 would have skipped: #86's `1eae28c` to `4eb478e` and #64's `6aa1e55` to `19e45c8`, both review documents only.
  - **Edge cases:** this PR's own push (`18b1180` to `f241c5e`: two files outside `docs/`, and the run before failed), a new PR (`opened`), a before commit that doesn't exist (the comparison fails: `unknown`), a push back to an older commit (`behind`), and a docs-only push on a commit with no run. All ran everything.
- **The compare API's limits:** for `nuxt/nuxt` v3.0.0 to v3.10.0, and Linux v6.0 to v6.1, it returned exactly 300 files, with `files` present. So "fewer than 300" is the right test.
- **Cancelling:** `nuxt/nuxt` uses the same `!cancelled() && needs.changes…` pattern. Its run 44140547551, cancelled while `changes` ran, left every dependent job `cancelled`, not `skipped`. So cancelling a run can't turn the required checks green.
- **The audit step, run locally** on a copy of `MedApp.Web`'s lockfile, against the real npm registry:
  - accepted and in date: passes ("No high or critical advisories but GHSA-fc5q-39xc-vjpf")
  - a different ID accepted: fails, naming GHSA-fc5q-39xc-vjpf
  - the date passed: fails, with the warning; nothing accepted: a plain audit, which fails
  - on the last day itself: passes (it lapses the day after)
  - the registry unreachable: fails ("npm audit gave no report")
  - `ACCEPTED_UNTIL` as `never`, or as `2027-1-1`: passes (finding 2)
- **CI:** run 44181414187 at `f241c5e` passed all 9 jobs in 8 min 50 s. `changes` took 5 s, so the six jobs started about 8 s later than they would have. The `web` job's log shows the exception at work.
  - **The shell:** those steps ran under `/usr/bin/bash -e {0}`. GitHub adds `pipefail` only when a step says `shell: bash`. Neither script relies on a pipe, so it makes no difference.
- **What a run tests:** the `web` job's checkout says "HEAD is now at d44de24 Merge f241c5e… into 894c1fe…". A `pull_request` run tests the branch merged into `main` as `main` was at that moment (finding 1).
- **GitHub API:**
  - `main`'s `Protect` ruleset: six required checks, `strict_required_status_checks_policy: false`, no bypass actors
  - the advisory: high, published 19 October 2026, updated 17 November, vulnerable `<= 4.0.7`, no patched version
  - npm: picomatch's latest is 4.0.7; `MedApp.Web/package-lock.json`'s only picomatch is `dev: true`, and the image's last stage copies only built output, never `node_modules`
- **Not done:** no test suite, Docker, AppHost or browser. The skip itself hasn't happened in CI yet; this review's own push will be its first.

## Findings

### 1. A skipped run vouches for the branch as it was tested against an older `main`

- **Severity:** medium. **Confidence:** high on the mechanism, medium on how often it bites.
- **What could go wrong:** a `pull_request` run never tests the branch alone. It tests the branch merged into `main` as `main` was when the run started (seen: `Merge f241c5e into 894c1fe`). So a passing run on the before commit vouches for "before, plus `main` back then", not for "before".
  - **A scenario:** PR A's code push passes at 10:00. At 10:20, PR B merges and renames a method that A now calls. Git merges the two without a conflict. At 10:40, A's review document is pushed: `changes` sees only `docs/` and a passing run, and skips. All six required checks show green, and A merges at 10:45.
  - **The result:** `main` no longer compiles. Publish can't build the image, and every other PR's CI fails until someone fixes it. Had it compiled but broken a test instead, the image would have deployed to kind.
  - **Before this PR,** the review document's push re-ran everything against `main` as it then was. So the untested window ran from the last push to the merge, a few minutes. Now it runs from the last code push to the merge: the whole review, which is when other PRs merge. On 17 November, 8 PRs merged, 3 of them within 17 minutes.
  - **Nothing requires the branch to be up to date:** the ruleset's `strict_required_status_checks_policy` is `false`. The #100 review noted this gap in its Remaining risks; this PR widens it.
  - **What makes it rarer in practice:** every shipped PR adds a row at the end of `docs/reviews/README.md`'s table, so two shipped PRs usually conflict there, and the second must merge `main` in (a change outside `docs/`, so everything runs). Recent PRs did merge `main` in before merging (#64, #98, #99). But neither is a rule, and a sibling merged without a row (a Dependabot PR, a revert made on GitHub's page) doesn't conflict.
  - **The stated reason is wrong too:** the workflow comment and `AGENTS.md` say a docs-only push "has exactly the code that passed".
  - **Re-testing doesn't help:** re-running the skipped run skips again, and so does an empty commit ("ahead", with 0 files).
- **Evidence:** `.github/workflows/main.yml:9-11, 42-47`; `AGENTS.md:539, 645`; the `Protect` ruleset (`gh api repos/Aliator/MedApp/rulesets/12421699`); the `web` job's log in run 44181414187, line 121; `docs/reviews/pr-100-lighter-dev.md:219`.
- **Fix:**
  - **Best:** turn on "Require branches to be up to date before merging" in the `Protect` ruleset (`strict_required_status_checks_policy: true`). A PR whose base has moved must then merge `main` in, which brings code, so `changes` runs everything. Recent PRs already do that by hand, so it costs little. Record it in `docs/todo.md`'s repository settings line.
  - **Or, in the workflow:** have `changes` also check that `main` hasn't changed outside `docs/` since the passing run started (`gh api "repos/$REPO/commits?sha=main&until=$started&per_page=1"`, then compare that commit with `main` now, as the push is compared). A run's own record can't say which base it tested: its `pull_requests[].base.sha` is the PR's current base.
  - **Either way:** correct the wording in `main.yml:9-11` and `AGENTS.md:539` ("the same branch, tested against `main` as it was then"), and add to step 4 of the merge flow: "If `main` has moved since the last full run, merge it in first; re-running doesn't re-test."
- **What happened to it:** fixed in the PR (`46a581d`). `changes` now also lists the base branch's commits since the passing run started (`commits?sha=$BASE&since=$started`) and skips only when there are none, so a skipped run tests exactly what the passing one did. The wording in `main.yml` and `AGENTS.md` now says so. Checked locally: #86's docs-only pair, which would have skipped, now runs everything, as `main` has moved since. Turning on the ruleset's "up to date" rule wasn't needed for this.

### 2. The exception's end date isn't checked, so a typo can keep it open

- **Severity:** low. **Confidence:** high (run locally).
- **What could go wrong:** the step compares today's date with `ACCEPTED_UNTIL` as text (`[[ today > until ]]`). That's right only for `YYYY-MM-DD`.
  - **`never`,** or any value starting with a letter, sorts after every date, so the advisory is accepted for good, silently.
  - **`2027-1-1`** (no zero padding) stays open until 1 October 2027.
  - **The 30-day limit** in the rule (`AGENTS.md:571`) isn't checked at all: `2027-06-01` gets through unless a reviewer does the sum.
  - Wrong formats that sort before today (`17 December 2026`, or empty) lapse at once, so the failure is one-sided: towards accepting.
- **Evidence:** `.github/workflows/main.yml:88, 92`; `AGENTS.md:571`; the local runs (How it was checked).
- **Fix:** check the date first, at the top of the step. Tested: it allows `2026-12-17` and `2026-12-18`, and refuses `2026-12-19`, `never`, `2027-1-1` and an empty date:

  ```bash
  if [[ -n "$ACCEPTED" ]] && { ! [[ "$ACCEPTED_UNTIL" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]] || [[ "$ACCEPTED_UNTIL" > "$(date -u -d '+31 days' +%F)" ]]; }; then
    echo "::error::ACCEPTED_UNTIL must be a date (YYYY-MM-DD) at most 31 days ahead, not '$ACCEPTED_UNTIL'."
    exit 1
  fi
  ```
- **What happened to it:** fixed in the PR (`46a581d`), with the check suggested above: a non-date or a date more than 31 days ahead is an error. Checked locally: `never`, `2027-1-1` and `2026-12-31` fail; `2026-12-17` passes.

### 3. Whoever makes a change also decides it's small

- **Severity:** low. **Confidence:** medium (a process risk; nothing measured).
- **What could go wrong:** the main session classifies its own change, makes it, and asks for a high review instead of xhigh. The review brief doesn't ask the reviewer to check that call, so a wrong one isn't caught.
  - **A scenario:** a "one-line tweak" adds a field to `TimelineMapper`'s `PublicValueFields`, the allowlist of what staff see on a patient's timeline. That's a privacy rule, so the change isn't small, but it looks like a tweak, and it gets the lighter review.
  - **The table above the new rule still says xhigh for every merge** (`AGENTS.md:619`). And "need no stages at all until they're shipped" (`:625`) doesn't say what happens when they are (presumably the high review).
- **Evidence:** `AGENTS.md:619, 621-625, 636`.
- **Fix:**
  - in step 3 of the merge flow: "A high review first checks the change meets What counts, says so in the review, and stops for an xhigh review if it doesn't."
  - in the table's Review row: "(high for a small change)"
  - in `:625`: "a typo or one-line edit still gets the high review when it's shipped"
- **What happened to it:** fixed in the PR (`46a581d`). Step 3 of the merge flow now has a review at high start by saying whether the change meets the definition of small, and review it again at xhigh if not; the Effort table's review row says high for a small change.

### 4. Dates and timings in the new text are slightly off

- **Severity:** low. **Confidence:** high.
- **What's wrong:**
  - **The advisory's date:** the workflow comment says "published 17 November 2026" (`main.yml:82`), and the PR description "published today". GitHub's record says published 19 October, updated 17 November (perhaps when it began to cover 4.0.7).
  - **When it lapses:** `docs/todo.md:148` says "On 17 December 2026 the exception lapses". It holds through the 17th (UTC), and lapses on the 18th.
  - **How long CI takes:** "about 7 minutes" (`AGENTS.md:588`) and "about 8 minutes" (`:44`). This PR's run took 8 min 50 s, set by `integration-tests`.
  - **The `web` bullet** still says a high or critical advisory fails it (`AGENTS.md:547`), with no word of the dated exception.
- **Fix:** "published 19 October 2026; npm audit reports it from 17 November"; "after 17 December"; one figure, "about 9 minutes", in both places; "(a high or critical advisory fails it, unless accepted for a dated time: see Supply chain rules)".
- **What happened to it:** fixed in the PR (`46a581d`). The step says the advisory fails the audit from 17 November and is accepted to the end of 17 December; the to-do says it fails again from 18 December; CI's time is "about 9 minutes" in both places; the `web` job's bullet mentions the accepted IDs.

### 5. An unrelated to-do rides along, and its options leave out the main obstacles

- **Severity:** low. **Confidence:** medium.
- **What's wrong:** the GitHub Pages demo to-do (`docs/todo.md:135`) has nothing to do with CI or the audit, so it's easy to miss in this PR's history. And its two static options understate the work:
  - **Option 1, the Application layer on SQLite in the browser:** Infrastructure relies on SQL Server features SQLite doesn't have as they are: calendar locks with `sp_getapplock` (`CalendarLock`), Insights' `JSON_VALUE` and `DATEDIFF_BIG` (`InsightsDbFunctions`), and `rowversion` concurrency tokens on patients, appointments, meetings and notes.
  - **Option 2, mocking `/api` over sql.js:** every rule the API enforces (procedure workflow, calendar clashes, stage visits, the timeline's privacy trimming) would be written again in TypeScript, or the demo is read-only.
- **Fix:** name it in the PR's title or move it to a PR of its own, and add those two points to the to-do's research list.
- **What happened to it:** documented. The to-do stays in this PR (it's the to-do list, which any PR may add to), and now names the obstacles: what only SQL Server does (`sp_getapplock`, `JSON_VALUE` and `DATEDIFF_BIG`, `rowversion`) and that a mock would mean writing the rules again in TypeScript.

## Remaining risks

- **A new file under `docs/` that a build or test reads would be skipped silently.** `AGENTS.md:546` says not to add one; nothing enforces it.
- **A new job that copies the `if` without `!cancelled()`** would be skipped, and so pass, whenever `changes` fails. A single gate job as the required check would remove that trap (Design choices).
- **The decision is only in the run's summary,** not its log, so `gh run view --log` doesn't show why a run skipped. One `echo` of the same line would.
- **The skip hasn't run in CI yet.** The first docs-only push after a passing run will be its first real test.
- **The picomatch exception lapses on 18 December** whether or not a fix exists. If none does, every PR's `web` job fails again that day, and the choice comes back.

## Say it like this

"PR #112 stops CI re-running the whole build and test suite when a push only adds documentation, like a review write-up, on top of code that already passed: a small job compares the push with the commit before it, and skips the six required jobs, which GitHub counts as passing. The trade-off is that the earlier pass was against `main` as it was then, so it's only airtight if a branch must be up to date with `main` before it merges, which the review recommends switching on. It also lets one build-tool advisory with no fix yet through the npm audit until a set date, while any other still fails."

## Detail

### How `changes` decides

| Situation | What the comparison says | Passing run on the before commit | Result |
|---|---|---|---|
| A new or reopened PR | not compared (no before commit) | not asked | everything runs |
| Only `docs/` changed, after a pass | `ahead`, fewer than 300 files, 0 outside `docs/` | yes | **skipped** |
| Only `docs/` changed, run before failed, cancelled or unfinished | `ahead`, 0 outside | no | everything runs |
| A file moved into or out of `docs/` | its old name counts too | either | everything runs |
| A merge from `main` | `main`'s files are in the comparison | either | everything runs, unless `main` changed only `docs/` |
| A force push (rebase, amend) | `diverged` or `behind` | either | everything runs |
| An empty commit, after a pass | `ahead`, 0 files | yes | skipped |
| 300 files or more | the API lists only 300 | either | everything runs |
| The API fails | `unknown`, or 0 passing runs | | everything runs |
| `changes` itself fails | no output | | everything runs (the run is marked failed) |

- **The `jq` expression** counts, for each file, its name and any old name that don't start with `docs/`. The comparison is three-dot (`before...after`), which for an "ahead" comparison is exactly the change between the two.
- **A hardening point, never seen:** `(.files // [])` treats a missing `files` list as no files. Every comparison tried had the list, even Linux's, but `.files == null` giving `unknown` would make that fail safe too.
- **A wording point:** for a newly opened PR, the summary says "not a push to an open pull request".

### Why `!cancelled()` and not `always()`

- **No status function:** GitHub silently adds `success()`. If `changes` failed, the six jobs would be skipped, and a skip counts as passing.
- **`always()`:** the jobs would also run in a cancelled run, so a run couldn't be stopped.
- **`!cancelled()`:** runs after a failed `changes`, and stops when the run is cancelled; the cancelled jobs then report `cancelled`, which blocks a merge.

### How the audit filter matches `npm audit`

- **`npm audit --json`** lists each vulnerable package under `vulnerabilities`, and in its `via` either an advisory (an object with `url`, `severity` and so on) or the name of a vulnerable package it depends on (a string).
- **The script** collects every advisory object rated high or critical, takes the ID from the end of its URL, and fails if any isn't in `ACCEPTED`. A package rated high only through a dependency gets that rating from an advisory somewhere down the chain, so the two checks fail on the same trees.
- **Failing closed:** an error report (no `vulnerabilities`), an empty file (`JSON.parse` throws), an advisory with no URL (its numeric source isn't a GHSA ID) or a list written with commas all fail the step.
