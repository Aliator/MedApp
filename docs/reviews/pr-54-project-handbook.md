# Review: PR #54, the architecture handbook and the record of choices

**PR:** [#54](https://github.com/Aliator/MedApp/pull/54), branch `feature/project-handbook` at `2ea8d0b`. The branch starts from `main` at `6774269` (the merge of #52). `main` has since moved to `eef92dd` (the merge of #53).

**Reviewed:** 15 November 2026, against `main` at `eef92dd`. Every claim below was checked against the code, git history or the GitHub API, unless it's marked unverified.

## 1. What the PR adds

Two documents, and no code:
- **`docs/architecture.md`, the architecture handbook** (about 2,500 lines). It follows arc42:
  - goals, constraints, context and the solution strategy, with a technology inventory
  - building blocks, runtime flows and deployment
  - cross-cutting concepts, 22 decision records, quality requirements, and risks with the status of the 31 earlier review findings
  - a glossary, and an appendix of where the docs and the code disagree
  - 10 Mermaid diagrams
- **The roadmap, inside the handbook:** "the next 10 steps" to make MedApp compliant and saleable, with a ranked feature list, a compliance map and 139 sources.
- **`docs/choices-made-2026-11-15.md`:** the decisions made for the owner while they were away, each with its reason and how to undo it.

**The problem it had:** the handbook was written while #52 was in review and before #53, so parts of it described #52 before its review fixes, and the shared default password as still in place.

## 2. How it was checked

**The target:** `main` at `eef92dd`, which is `origin/main` plus #53 (its tree is identical to #53's head, `25fc798`). Exported to a scratch folder and read there.

**Against the code:**
- **#52's review fixes**, one by one: `deploy-to-env.sh` (kept workflows, parent order, the backwards guard), the three Publish jobs, the Argo CD project and Applications, the Bicep `environmentName` check, `deploy.sh`'s placeholder check, `bootstrap.sh`'s wait, and CI building `tools/MedApp.DevMcp`.
- **#53:** the generator, the create-user endpoint and its tests, the dev tool, `AGENTS.md` and `docs/todo.md`, and its review document.
- **The technology inventory:** every version, against the `.csproj` files, both `package.json` files, `deploy/versions.sh`, the chart's `values.yaml`, the Dockerfile and the workflows. All match.
- **Counts:**
  - 51 handlers, 29 validators, and 55 endpoints in six controllers
  - nine migrations and six repositories
  - 22 decision records, 10 diagrams, 139 sources and 139 unique source URLs
- **Every cited line number:** for example `Program.cs:18-19`, `ApplicationBuilderExtensions.cs:19` and `:38`, Infrastructure's `ServiceExtensions.cs:40-46`, `values.yaml:126-128` and `LiveNotifications.tsx:33`. All are right at `eef92dd`.
- **Spot checks** of security claims: the chart's pod settings, the network policy, sessions, the health port, `localStorage`, and sign-out.

**Against GitHub** (the API and `gh`):
- **The rulesets:**
  - `Protect` requires `web`, `helm`, `unit-tests` and `image`, and no approvals.
  - `Environment branches` blocks deletion and non-fast-forward pushes on `env/*`, with no bypass list.
- **The deploy after #52:** Publish run 43768949982 on `6774269` succeeded (`image`, then `deploy-kind`; `azure` skipped). Its deploy commit, `429d41d`, changes no workflow file against its first parent.
- **The test counts:** CI run 43771881138 on #53's head: 768 .NET tests, and 677 web tests in 82 files.
- **The merge dates** of #22 to #53, and which pull request descriptions are marked as generated (#24 onwards).
- **The history:** 312 commits on `main`, 49 with a Claude co-author line. That's every non-merge commit since 13 November.

**Links and anchors:** a script (in the appendix below) checks every relative link and `#anchor` the way GitHub renders headings.
- It catches a broken anchor, a repeated heading's anchor (`-1`, `-2`) and a missing file, tested on a sample file.
- **Before this pass:** 61 links, none broken against `main` plus this branch.
- **After it:** 62 links, none broken.
- **On the branch alone,** the choices document's link to `reviews/pr-53-generated-first-password.md` is broken, because that file arrived with #53 (finding 13).

**Spelling:** both documents were searched for American spellings. Only proper names and code remain, such as "Remix Icon License" and `[Authorize]`.

**Not done:**
- The roadmap's external sources weren't fetched, and its legal, regulatory and market claims weren't checked. Several are dated after mid-2026.
- The Mermaid diagrams weren't rendered. I read them for syntax only.
- Nothing was run against a database, Docker, a cluster or Azure. Nothing was committed or pushed.

## 3. Findings, most severe first

Each finding describes the documents as the PR had them. Section 4 lists what this pass changed.

### 1. The security section described the default password as still in place, and missed what #53 left

- **Severity:** high: materially misleading about security. **Status:** fixed in this pass.
- **Evidence:**
  - Section 8.1 said "New accounts all get the same password, `DefaultPassword1`", citing `MedApp.Web/src/api/users.ts:9`. That line no longer exists: since #53, `POST api/auth/users` takes only a username, and the server generates the password (`IdentityUserService.cs:15-25`).
  - Risk S1 rated it high and open.
  - Roadmap step 2 called #53 "the in-progress `feature/generated-first-password` branch".
  - Nothing mentioned what #53 left: accounts created before it may still have `DefaultPassword1` (`AGENTS.md`, Auth; `docs/todo.md`, Features), and nothing forces a change at first sign-in.
- **Why it matters:** a security assessor would have reported a fixed high risk as open. Worse, they'd have missed the real remaining one: any account created before #53 may still sign in with a password published in the repository.
- **Fix:** rewrite 8.1's password bullets, mark S1 as fixed by #53, and list its leftovers:
  - the old accounts (medium, #53's review finding 1)
  - no forced change
  - a password lost if the admin leaves mid-request
  - the Blazor client not showing it

  Also update 5.5, 5.7, 1.1 and roadmap step 2.

### 2. The deploy pipeline was described as it was before #52's review fixes

- **Severity:** medium. **Status:** fixed in this pass.
- **Evidence:**
  - **Section 6.5:**
    - It described one `image` job that ended with the kind deploy, with `azure` after it.
    - It said a deploy commit is "exactly `main` at that commit plus" the image file.
    - It had no backwards guard.
  - **What the code does:**
    - `publish.yml:28-105` has three jobs: `image`, then `deploy-kind` and `azure`, which each need only `image`.
    - `deploy-to-env.sh:65-77` keeps the branch's own `.github/workflows`.
    - `:39-56` refuses to move a branch back to an older commit of `main`.
  - **Sections 7.7 and 8.1** said Argo CD creates the namespace with its `restricted` labels. `project.yaml:22-25` now allows nothing cluster-wide, and `up.sh:60-62` and `bootstrap.sh:84-86` make the namespace.
  - **Sections 7.6 and 7.8** didn't mention:
    - `bootstrap.sh`'s wait for the cluster role (`:41-54`)
    - its `kubelogin --context`
    - `deploy.sh`'s placeholder check (`:23-28`)
    - the `environmentName` rule (`main.bicep:12-15`, `:61-65`)
    - starting the first Azure deploy by hand
  - **The risk tables:**
    - D5 (Azure waits for kind) was fixed by #52.
    - Review finding 20 (Bicep names) is now fully fixed.
    - R6's first-run list was out of date: `kubelogin` was fixed, and Let's Encrypt through the redirect was checked.
- **Why it matters:** an operator debugging a deploy would expect the environment branch's workflow files to match `main`'s, and would expect a re-run of an old Publish run to roll back. Neither holds. Someone matching the Applications to the handbook would put back `CreateNamespace`, which #52 removed so that a commit on `env/*` can't relabel the namespace to allow privileged pods.
- **Fix:** describe the three jobs, the kept workflows, the parent order and the guard, and the scripts making the namespace. Add the new checks to 7.6 and 7.8. Update ADR-18, D5, R6 and finding 20, and add R15 (#52's review finding 5, a retry in `IdentityTransactions`, documented but not in the risk tables).

### 3. The `image` check was described as not required

- **Severity:** medium. **Status:** fixed in this pass.
- **Evidence:**
  - **The handbook said so in seven places:**
    - section 2.2 ("but not `image`")
    - 6.5 and the 7.7 table ("No")
    - D1 ("the `image` check isn't required")
    - review finding 7 ("`image` not")
    - roadmap step 1 ("require the `image` job too")
    - Appendix A
  - **GitHub:** the `Protect` ruleset (ID 12421699) requires `image`.
- **Fix:** say `web`, `image`, `helm` and `unit-tests` are required everywhere, mark finding 7 as fixed, and list the other docs that still say otherwise in Appendix A (finding 12).

### 4. The privacy section said the browser never holds patient data

- **Severity:** medium. **Status:** fixed in this pass.
- **Evidence:**
  - **The claim:** section 8.2 said "The browser keeps the account's settings in `localStorage`, never patient data".
  - **Saved searches:** they're settings (`app/favourites.ts:21`, `defineSetting('medapp.favourites')`), kept in `localStorage` and with the account. Their terms can be patient names, as the roadmap's own data inventory says (step 3).
  - **Pins:** pinned patients and procedures are kept in `localStorage` as IDs (`components/navigation/pins.ts:12-30`).
  - **Sign-out:** it clears the query cache only (`auth/auth.ts:70-80`), not `localStorage`.
- **Why it matters:** a DPIA built on 8.2 would leave out browser storage on shared clinic computers.
- **Fix:** say what's there (saved search terms, pinned IDs), that no records are, and that sign-out doesn't clear it.

### 5. The status lines, scope and summary numbers were stale

- **Severity:** medium. **Status:** fixed in this pass.
- **Evidence:**
  - **The status line:** "as it will be once pull request #52 … is merged", and "About" said `main` at `0f4f474` plus #52.
  - **Section 1.5:**
    - "PRs #22 to #52"
    - 747 and 666 tests, where CI on #53 ran 768 and 677
    - #51's review "five of them fixed". Its own resolution fixes four (3.1, 3.3, 3.4 and 3.6's constants) and documents two (3.2, 3.5).
    - no mention of #52's or #53's reviews
  - **The roadmap:**
    - "739 .NET at the last review", a number no run produced
    - "47 of its 308 commits"
  - **Elsewhere:**
    - `server.mjs` "226 lines": it was already 232 at `0f4f474`
    - "about 47" parity screens: there are 46
    - FluentAssertions in "117 files": 119 at `6774269`, and 122 now
    - demo data with "10 stage definitions, 3 templates": the tool makes 11 and 4, one of each archived
- **Fix:** describe `main` at `eef92dd`, and correct each number.

### 6. The record of choices said both of #53's medium findings were fixed

- **Severity:** medium. **Status:** fixed in this pass.
- **Evidence:**
  - **The heading:** item 6 said "fixed both medium findings".
  - **#53's resolution:** finding 2 (announcing and copying the password) was fixed, but finding 1 (accounts that may still have `DefaultPassword1`) was "documented and deferred".
  - **The body text** said so, but the heading is what an owner skimming the list reads.
- **Why it matters:** the deferred one is the remaining security exposure from finding 1 above.
- **Fix:** "fixed one medium finding, documented the other, and fixed most lows".

### 7. `MedApp.Contracts`' Identity package was called unused

- **Severity:** low. **Status:** fixed in this pass.
- **Evidence:**
  - **What the handbook said:** section 5.2 and debt C3 called the package unused.
  - **What the code does:** `MedApp.Contracts/Authentication/Responses/ResetUserPasswordResponse.cs` uses `IdentityError` from it, both before and after #53.
- **Fix:** say it's used for that one type, so the API's public shapes depend on Identity. That's the real debt.

### 8. Authorisation tests were described as covering one endpoint

- **Severity:** low. **Status:** fixed in this pass.
- **Evidence:** `AuthControllerTests.cs:141-163` now also evaluates `CreateUser`'s `[Authorize]` attributes (#53). Sections 8.1, 8.5 and 10.1 and risk S7 said only the own-password endpoint was covered.
- **Fix:** say "the own-password and create-user endpoints".

### 9. Roadmap step 2 said the "get everything" endpoints are used only by the deprecated client

- **Severity:** low. **Status:** fixed in this pass.
- **Evidence:** `tools/ui-parity/auth.setup.ts:22` calls `GET api/patients`. Section 5.7 was loose the same way: it said both endpoints are used by the parity tool, which uses only one.
- **Fix:** the parity tool's sign-in check uses `GET api/patients`, so removing it means moving that check to a search endpoint.

### 10. Small slips in the record of choices

- **Severity:** low. **Status:** fixed in this pass.
- **Each slip:**
  - **Item 11:** "each pull request merged today has its own" review. #39 to #50 also merged on 15 November without one. #42 to #50 are covered by the review notes instead.
  - **Item 4:** "CI now builds it, which is how its break went unnoticed" says the opposite of what's meant.
  - **Item 4's heading,** "#52's low findings, also fixed", left out the two that were only documented: findings 5 and 8.
  - **Item 3's "To undo"** missed `managedNamespaceMetadata`, which carried the Pod Security labels (`313c5ec:deploy/argocd/medapp-kind.yaml`).
  - **Items 10 and 14:** MediatR "needs a paid licence for commercial use". The roadmap itself says the Community licence is free under its limits. FluentAssertions 8 has no free commercial use.
  - **Item 13:** making the image private affects the Publish `azure` job too, which reads GHCR anonymously (`docs/reviews/azure.md`, open question 19), not just kind.

### 11. Appendix A was out of date

- **Severity:** low. **Status:** fixed in this pass.
- **Evidence:**
  - **Fixed by #52,** so no longer disagreements:
    - `deploy/kind/up.sh:198`
    - `traefik-values.yaml:2-4`
    - `secret.yaml:12`
  - **Changed by #52:** `overview.md` now says the image check "isn't required yet", which is wrong the other way.
  - **Missing:** the drift that #52 and #53 left (finding 12).
- **Fix:** remove the fixed rows, update the changed ones, and add the new ones.

### 12. Other documents still disagree with the code or the settings

- **Severity:** low. **Status:** recorded in Appendix A. Not fixed: outside this PR's three documents.
- **The documents:**
  - **`AGENTS.md`, CI (line 229):** says `image` "isn't a required check".
  - **`docs/todo.md`, Deployment:** lists requiring `image` as still to do.
  - **`docs/reviews/overview.md:108` and `:111`:** gives the old stage efforts, and says `image` isn't required.
  - **`docs/reviews/README.md`:** its index has no row for `pr-53-generated-first-password.md`.
  - **`deploy/argocd/medapp-kind.yaml:5`:** "exactly main's plus the tag", with no word of the kept workflows.
  - **`AGENTS.md:206`:** the SQL volume "only opens with" the `sa` password. That's the same slip #52 fixed in `secret.yaml`.
  - **`AGENTS.md`, Solution layout:** the Domain list is still incomplete.
- **Fix:** a small follow-up PR that corrects them, and adds this review to `docs/reviews/README.md`.

### 13. The choices document links to a file the branch doesn't have

- **Severity:** low. **Status:** not changed (nothing to edit).
- **Evidence:** the choices document's item 6 links to `reviews/pr-53-generated-first-password.md`, which is on `main` since `eef92dd` but not on this branch.
- **Fix:** merge `main` into the branch before merging the PR. That also lets CI run on the combined tree. The link resolves once both are on `main`.

### 14. Roadmap recommendations worth a second look

- **Severity:** low. **Status:** not changed: these are judgements, and the brief was to leave the research and recommendations alone.
- **Step 7, an append-only ledger audit and retention:**
  - Step 7 makes the audit table an append-only ledger table. In SQL Server, append-only ledger tables refuse `UPDATE` and `DELETE`. An updatable ledger table keeps deleted rows in its history table.
  - The same step's retention engine and erasure workflow are meant to destroy or anonymise audit details for records past retention.
  - The design needs to say which wins. For example, keep clinical text out of the ledger (the step's note-version table does part of this), and accept that ledger rows outlive the record. Or use an updatable ledger table, and state that its history keeps deleted rows.
- **Step 4, passkeys:** .NET 10's Identity passkey support is built around `SignInManager`, as far as I know. MedApp uses `AddIdentityCore` with its own session scheme, and registers no `SignInManager` (`ServiceExtensions.cs:73-77`). Check how much of the built-in flow applies before trusting the L estimate. Unverified.
- **"Pilots" and step 8's dedicated option:**
  - The roadmap says "the Helm chart already runs one organisation per namespace". The chart itself takes any namespace.
  - But the Argo CD project (`project.yaml:19-21`), both `ClusterSecretStore`s (`conditions: namespaces: [medapp]`), the scripts and the Key Vault secret names all assume the single `medapp` namespace.
  - Today, a dedicated deployment means a cluster per customer, at the README's £150 to £200 a month. A second namespace on one cluster needs those generalised first.

### 15. Two statements in the record of choices can't be checked

- **Severity:** low. **Status:** not changed.
- **Item 8: "GitHub only allows that for organisation-owned repositories".**
  - **The claim:** making the Publish workflow the only pusher to `env/*` can't be done in a personal repository.
  - **What I found:** GitHub's ruleset docs list GitHub Apps among eligible bypass actors, and don't say which actors a personal repository can use. Nothing contradicts the claim, and `deploy/argocd/README.md` and `docs/todo.md` say the same.
  - **Left as it is.**
- **Item 15:** it refers to checks reported in a conversation, so it can't be verified from the repository.

### 16. The handbook's shape makes it go stale fast

- **Severity:** low. **Status:** not changed. Suggestions only.
- **Evidence:** the same volatile facts were stated in many places, which is why the handbook drifted within hours of being written:
  - test counts in three places
  - required checks in seven
  - whether Argo CD makes the namespace in three
- **Suggestions:**
  - State each volatile fact once (section 1.5 is the natural home), and refer to it elsewhere.
  - Cite files more than line numbers.
  - Consider moving the roadmap (about 960 lines with its sources) into its own `docs/roadmap.md`, linked from the handbook. It's a plan that will date faster than the architecture, and the handbook would read as the reference it's meant to be. The owner asked for it inside the handbook (choices, item 9), so this is for them to decide.

## 4. What I corrected in this pass

In `docs/architecture.md`:
- **Status and scope:** `main` at `eef92dd`, with #51, #52 and #53. Section 1.5: PRs #22 to #53, the co-author lines (merge commits excepted), 768 and 677 tests, and a summary of each PR review's findings (finding 5).
- **Required checks:** `image` is required, in sections 2.2, 6.5 and 7.7, D1, finding 7 and roadmap step 1 (finding 3).
- **Building blocks:**
  - 5.2: the Contracts dependency (finding 7), and CI building the dev tool
  - 5.5 and 5.7: generated passwords, and what `POST api/auth/users` takes and returns
  - 5.7: which tools use the "get everything" endpoints (finding 9)
  - 5.9 and 5.10: the line count, the demo data and the screen count (finding 5)
- **Deployment:**
  - 6.5: the three Publish jobs, the kept workflows, the parent order and the backwards guard
  - 6.6: Bicep writes the secrets
  - 7.5 and 7.6: the scripts make the namespace; `deploy.sh`'s and the Bicep's checks; `bootstrap.sh`'s wait and its own context
  - 7.7: the CI table, the environment branches and the Argo CD project
  - 7.8: the Azure steps
  - ADR-18
  - (finding 2)
- **Security and privacy:**
  - 8.1: generated passwords and what #53 left (finding 1); two endpoints' attributes tested (finding 8); the Argo CD project
  - 8.2: browser storage (finding 4)
  - 8.5 and 10.1: the test numbers and the tested endpoints
- **Risks:**
  - S1 marked fixed by #53, with its leftovers and their severities
  - S7, R6, D1, C3 and C5 updated
  - D5 marked fixed by #52
  - R15 added
  - review findings 7, 8 and 20 updated
  - the to-do summary updated
- **Roadmap:**
  - its basis now includes #53
  - the test and commit counts, and the FluentAssertions file count
  - the repository settings item
  - #53 is merged, not "in progress"
  - the "get everything" endpoints' users

  Nothing in its research or recommendations was changed.
- **Appendix A:** rebuilt for `eef92dd` (findings 11 and 12). **Appendix B:** the reviews of #52 and #53, and this one.

In `docs/choices-made-2026-11-15.md`, factual corrections only, with no decision changed:
- item 3's "To undo"
- item 4's wording, and the two documented low findings
- item 6's heading
- MediatR's licence in items 10 and 14
- item 11's claim about review documents
- item 13's note on the private image

(Findings 6 and 10.)

## 5. What's worth keeping

- **The structure:** arc42 is the right frame for a due diligence reader. "Read this before anything else" (1.5) says plainly that it has never run in production, never held real data, and has one maintainer.
- **It was checked against the code, not the docs.** I found the technology inventory exact: every version matched its source. So did the counts and every cited line number I checked.
- **Honesty about inference:** "(inferred)" marks reasons nobody wrote down, so the decision records don't invent history.
- **The risk register:**
  - every item has a source
  - it separates "fixed", "documented" and "open"
  - it tracks all 31 earlier findings
  - Appendix A turns doc drift into a list someone can work through
- **The runtime views:**
  - **Server rendering** (6.2): with its fallback and its limits, including what isn't timed out.
  - **The procedure audit** (6.3): step by step, with each step's entries.
  - **The notification across two copies** (6.4): with the measured 8 of 8 against 4 of 8.
- **The security section names its own gaps:**
  - one endpoint's attributes tested (now two)
  - Node's allowlist isn't isolation
  - the Windows path forms
  - the admin login on Azure SQL
- **The roadmap:**
  - each step maps its scope to real files, with done-when criteria
  - the compliance map is a useful starting checklist
  - 139 unique sources, every one listed

## 6. Verdict

**Nothing critical or high remains.**
- **The one high finding** was the security section still describing the shared default password, while missing the accounts that may still have it. It's fixed in this pass, along with the medium ones: the pre-fix deploy pipeline, the `image` check, browser storage, the stale status and numbers, and the choices document's #53 heading.
- **What's left is low:**
  - other documents that still disagree with the code (finding 12, best fixed in a small follow-up PR)
  - the branch needing `main` merged in (finding 13)
  - three roadmap recommendations worth a second look (finding 14)
  - two unverifiable statements (finding 15)
  - the handbook's shape (finding 16)
- **Not verified:** the roadmap's external research and dated legal claims.

## Appendix: the link and anchor check

Run from anywhere:

```bash
python -I check_links.py <repo root> docs/architecture.md docs/choices-made-2026-11-15.md
```

Against `main` at `eef92dd` plus this branch's two documents, after this pass:

```text
docs/architecture.md: 57 relative links and anchors checked, 0 broken
docs/choices-made-2026-11-15.md: 5 relative links and anchors checked, 0 broken
```

The script:

```python
"""Checks the relative links and #anchors in Markdown files, the way GitHub renders them.

    python check_links.py <repo root> <file.md> [<file.md> ...]

For each link that isn't http(s) or mailto, it checks that the target file exists (relative to the linking file),
and, when there's a #fragment, that the target file has a heading with that anchor. Anchors follow GitHub's rules:
lower case, punctuation other than hyphens and underscores dropped, spaces to hyphens, and -1, -2... for repeats.
Links inside fenced code blocks and inline code are ignored. Exits 1 if anything is broken.
"""
import os
import re
import sys

FENCE = re.compile(r"^\s*(```|~~~)")
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
LINK = re.compile(r"(?<!!)\[(?:[^\[\]]|\[[^\]]*\])*\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
INLINE_CODE = re.compile(r"`[^`]*`")


def slug(text):
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)  # a link in a heading keeps only its text
    text = text.replace("`", "").replace("*", "")
    text = text.strip().lower()
    text = re.sub(r"[^\w\- ]", "", text)  # \w keeps Unicode letters, digits and _
    return text.replace(" ", "-")


def lines_outside_code(path):
    in_fence = False
    with open(path, encoding="utf-8") as f:
        for number, line in enumerate(f, 1):
            if FENCE.match(line):
                in_fence = not in_fence
                continue
            if not in_fence:
                yield number, line


_anchor_cache = {}


def anchors(path):
    if path not in _anchor_cache:
        seen, result = {}, set()
        for _, line in lines_outside_code(path):
            m = HEADING.match(line)
            if not m:
                continue
            base = slug(m.group(2))
            count = seen.get(base, 0)
            result.add(base if count == 0 else f"{base}-{count}")
            seen[base] = count + 1
        _anchor_cache[path] = result
    return _anchor_cache[path]


def check(root, path):
    problems, total = [], 0
    for number, line in lines_outside_code(path):
        for target in LINK.findall(INLINE_CODE.sub("", line)):
            if re.match(r"^(https?:|mailto:)", target):
                continue
            total += 1
            file_part, _, fragment = target.partition("#")
            target_path = os.path.normpath(os.path.join(os.path.dirname(path), file_part)) if file_part else path
            if not os.path.exists(target_path):
                problems.append(f"{path}:{number}: missing file {target}")
                continue
            if fragment:
                if not target_path.endswith(".md"):
                    problems.append(f"{path}:{number}: anchor on a non-Markdown file {target}")
                elif fragment not in anchors(target_path):
                    problems.append(f"{path}:{number}: no heading for #{fragment} in {os.path.relpath(target_path, root)}")
    return problems, total


def main():
    root, files = sys.argv[1], sys.argv[2:]
    all_problems = []
    for name in files:
        path = os.path.join(root, name)
        problems, total = check(root, path)
        print(f"{name}: {total} relative links and anchors checked, {len(problems)} broken")
        all_problems += problems
    for p in all_problems:
        print("  " + p)
    sys.exit(1 if all_problems else 0)


if __name__ == "__main__":
    main()
```

## Resolution

Fixed in this PR after the review:
- **Finding 12** (docs outside the three reviewed files): `AGENTS.md` (the `image` check is required; the `sa` password wording), `docs/todo.md`, `docs/reviews/overview.md` (the effort levels and the required checks), `docs/reviews/README.md` (rows for #53's and #54's reviews) and the comment in `deploy/argocd/medapp-kind.yaml` (the kept workflow files). Their rows are removed from the handbook's Appendix A.
- **Finding 13:** `main` is merged into the branch, so the choices document's link to #53's review resolves.
- **Finding 15:** the choices document's last section now records the checks themselves, not a pointer to the conversation: development on the merged `main`, the kind cluster deployed by Argo CD from `env/kind`, the deploy pipeline and Azure.

Left as recorded: finding 14 (three roadmap recommendations worth a second look), finding 16 (restructuring the volatile facts), and the remaining Appendix A rows, which are low.
