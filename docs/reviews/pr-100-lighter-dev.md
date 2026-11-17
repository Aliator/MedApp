# Review: PR #100, targeted local testing and a lighter dev setup

**PR:** [#100](https://github.com/Aliator/MedApp/pull/100), branch `feature/lighter-dev`. Reviewed at `f8a3ce3`, compared with `main` at `77c24fb` (also the merge base).

**Reviewed:** 17 November 2026, as an independent, adversarial review, without the author's conclusions. Each claim below was checked against the code, the SDK's and Aspire's own files, Microsoft's documentation, CI's results or read-only Docker state. Nothing was built from the solution, and no container, AppHost, cluster or browser was started (the rules this PR introduces). **No finding is high or critical:** 5 are medium and 3 are low.

## Summary

The PR tells agents to test only what a change touches on the developer's PC and to leave the full suites to CI. It also caps SQL Server's memory at 2 GB in the AppHost and the integration tests, and documents how to keep Docker's memory down. The cap is sound and costs nothing measurable. But the new rules drop the only checks that ever looked at a change in a browser, ran the accessibility audit, built the whole solution or watched the kind deploy, and CI doesn't do any of those.

## What changed, and why

6 files, 65 lines added and 10 removed.

- **Testing a change** (`AGENTS.md:44-58`, `MedApp.Web/AGENTS.md:95-99`, `docs/architecture.md:1290-1291`):
  - **Locally:** a build, the unit tests for the code changed (a `--filter`), the changed files' Vitest tests, `npm run typecheck`, ESLint on the changed files, and one integration fixture when unit tests can't reach the change.
  - **Not locally,** unless asked: the full suites, E2E, the whole UI parity set, accessibility audits and the kind cluster.
  - **Why:** the full runs are slow and heavy (Docker, browsers), and CI already runs every suite on every PR.
- **The merge flow** (`AGENTS.md:595-614`):
  - no full UI parity run before merging
  - the reviewer checks by reading code and with the narrowest tests, and starts no Docker, AppHost, kind cluster or browser
  - the deploy check looks at Argo CD only if the kind cluster is already running
- **SQL Server capped at 2 GB** (`MedApp.AppHost/AppHost.cs:10-13`, `MedApp.IntegrationTests/Common/TestEnvironment.cs:55-56`), with the `MSSQL_MEMORY_LIMIT_MB` environment variable.
  - **What it does:** SQL Server on Linux reads that variable as `memory.memorylimitmb`, the most physical memory its whole process may use. Without it, the limit is 80% of the memory it can see.
  - **What it can see in Docker Desktop:** the WSL virtual machine's memory, which by default is half the PC's.
  - **The buffer pool**, explained: SQL Server keeps the data pages it has read in memory, so the next read is fast. That cache grows as data is read and gives memory back only when something asks for it. Like a desk you keep piling books on and clear only when told to.
- **Keeping memory down** (`AGENTS.md:462-472`): the AppHost is the light setup, kind the heavy one; most changes need no containers; and a `.wslconfig` cap for WSL itself.
  - **WSL 2**, explained: Windows runs a small Linux virtual machine, and Docker Desktop runs every container inside it. Like a flat inside the house with its own meter: whatever it takes, Windows can't use.
- **A build gotcha** (`AGENTS.md:449`): while `medapp-dev` runs, `dotnet build MedApp.sln` fails with "file is locked by: MedApp.DevMcp".
  - **Why it happens:** the unit and integration tests reference the tool (since #97), the tool isn't in the solution, and a solution build always builds it as Debug, into the folder the running tool was started from.
  - **A file lock**, explained: Windows won't let a program's loaded files be overwritten while it runs. Like trying to reprint a page someone is still reading.
- **kind: stopping and starting** (`deploy/kind/README.md:54-64`): `docker stop` / `docker start` the single node container, and `docker update --restart=no` so it doesn't come back by itself.
- **Smaller wording changes:** CI failures are reproduced with only the failing tests (`AGENTS.md:566`), the whole parity set is only for many-screen changes (`AGENTS.md:429`), and axe runs on the touched screens (`MedApp.Web/AGENTS.md:77`).

## Design choices

- **Targeted local runs, complete CI:**
  - **Instead:** keep the full local runs before calling a change done, as before.
  - **Chosen because:** CI runs every suite on GitHub's machines in about 8 minutes, and the jobs that run them are required checks, so the bar for `main` holds for what CI checks.
  - **Worse at:** anything CI doesn't check now goes unchecked (findings 2 to 5). Failures also surface later, after a push.
- **SQL Server's own cap, rather than a container memory limit:**
  - **Instead:** give the container a Docker memory limit (`--memory 2g`).
  - **A container memory limit**, explained: Docker asks the Linux kernel to fence the container's processes in a *cgroup* (control group), a kernel feature that meters and limits a group of processes. Go over its memory limit and the kernel kills a process (an *OOM kill*, for out of memory). It's a hard wall: hit it and you're thrown out.
  - **SQL Server's own cap** is a budget it plans within: it sizes its caches to fit and trims them under pressure, so it slows down rather than dies.
  - **Chosen because:** a database killed mid-write is worse than one that runs a little slower.
  - **Worse at:** it bounds SQL Server only. And it's a ceiling, not a saving: the dev database is about 11 MB (`tools/MedApp.DevMcp/.backups`), so SQL Server was unlikely to grow anywhere near 23 GB anyway (not measured here). The measured "23.7 GB uncapped, 1.4 GB capped" is SQL Server's *target*, the most it would take, not what it used.
- **2 GB, rather than more:**
  - **Chosen because:** 2 GB is SQL Server on Linux's minimum, and is about 100 times the dev database.
  - **Worse at:** a large synthetic database for timing queries would no longer fit in memory. Today's Insights timings were taken on LocalDB, not this container (`docs/reviews/pr-66-admin-insights.md:216`), so they're unaffected.
- **The WSL cap is advice, not a file in the repo:**
  - **Chosen because:** `.wslconfig` lives in each person's Windows profile and applies to every WSL distribution, so the repo can't set it.
  - **Worse at:** one number for every PC (finding 7).
- **Stop the kind cluster with `docker stop`, rather than delete it with `down.sh`:**
  - **Chosen because:** it keeps the cluster's database, secrets and Argo CD set-up, and a start takes minutes, not a rebuild.
  - **Worse at:** a plain `docker stop` gives the node only 10 seconds to shut down (finding 8).
- **Document the build lock, rather than remove it:**
  - **Instead:** have `.mcp.json` run a published copy of the tool, from a folder no build writes to.
  - **Chosen because:** a note is cheaper.
  - **Worse at:** the lock still breaks the commands the new rules recommend (finding 1).
- **The deploy check only when the cluster is running:**
  - **Chosen because:** starting kind for every merge costs gigabytes of memory, and Argo CD catches up when the cluster next starts.
  - **Worse at:** kind is the only place MedApp is deployed today, so a broken deploy is found late (finding 5).

## How it was checked

- **Read:** the diff and the PR description; `AGENTS.md`, `MedApp.Web/AGENTS.md`, `deploy/kind/README.md` and `docs/architecture.md` around the changes; `.github/workflows/main.yml` and `codeql.yml`; `.mcp.json`; the test projects' references; `TestEnvironment.cs`; the chart's `values.yaml` and kind's values; the accessibility statement page.
- **CI:** all 13 checks pass at `f8a3ce3` (`main.yml` run 44015341143, CodeQL run 44015341207).
  - **The cap didn't slow the integration suite:** its job took 7 min 28 s here, against 6 min 51 s and 7 min 23 s for the two uncapped runs of #97, the last branch that added integration tests (`c2264d6`, `3bf74f1`).
- **Required checks** (`main`'s `Protect` ruleset, read through the GitHub API): `web`, `helm`, `unit-tests`, `image`, `integration-tests` and `e2e`. No bypass actors. Branches needn't be up to date with `main` (`strict_required_status_checks_policy: false`).
- **The build note:**
  - `dotnet sln MedApp.sln list`: 11 projects, without `tools/MedApp.DevMcp`.
  - `MedApp.UnitTests.csproj:44` and `MedApp.IntegrationTests.csproj:45` reference it.
  - **Why "always Debug" is true, even with `-c Release`:** building a solution file sets `ShouldUnsetParentConfigurationAndPlatform` (SDK 10.0.404, `Microsoft.Common.CurrentVersion.targets:1583-1585`). A reference outside the solution is then built *without* the solution's configuration, so it falls back to its own default, Debug.
  - **The running tool:** `tools/MedApp.DevMcp/bin/Debug/net10.0/MedApp.DevMcp.exe`, with its own copies of `MedApp.Application`, `Infrastructure`, `Domain` and `Contracts` DLLs in that folder.
- **Aspire's persistent containers** (`Aspire.Hosting` 13.6.5's own XML docs, `ContainerLifetime.Persistent`): when the configuration no longer matches, "the existing container will be stopped and a new container created"; a named volume "keeps the same attached volume". So the AppHost comment is right.
  - **Not exercised here:** the dev SQL container (`sql-6aca4589`) was created on 14 November and has no `MSSQL_MEMORY_LIMIT_MB`, so the AppHost hasn't run since the change.
- **SQL Server's documentation** (mssql-conf, updated October 2026): `memory.memorylimitmb` limits the physical memory available to the SQL Server process, and defaults to 80% of physical memory.
- **WSL's documentation** (`wsl-config`, updated 16 October 2026): `memory` defaults to 50% of Windows' memory; `autoMemoryReclaim` is under `[experimental]`, takes `disabled`, `gradual` or `dropCache`, and **defaults to `dropCache`**. This PC runs WSL 2.5.9.
- **Docker state, read only:**
  - **The kind node** (`medapp-control-plane`, `kindest/node:v1.37.0`): one node; restart policy already `no`; stop signal `SIGRTMIN+3` (systemd's "shut down"), no stop timeout of its own. It last stopped at 04:03:22, seven minutes before the commit, with **exit code 137**: Docker killed it after the 10-second grace.
  - **It has come back from a restart before:** it started at 23:26 on 16 November, five minutes after Docker went down (the time the test containers above stopped), and ran until that stop.
  - **Two integration runs left their containers behind** (16 November, 14:52 and 23:06): SQL Server and Azurite, labelled by Testcontainers, still running hours or minutes after a run would have ended, then stopped (exit codes 0, 137 and 255) when Docker went down, and never removed.
- **The kind cluster's size:** the chart's app pods request about 3 GiB and may use up to about 6.75 GiB with kind's two API copies (Detail), before Argo CD, cert-manager, External Secrets, Traefik, the secret store and Kubernetes itself.
- **Leftovers:** searched the repo for the old rules (`npm test`, `full set`, `when merging`, `a11y`, `Synced and Healthy`, the old "before calling a change done").
- **Not done:** no solution build (the running tool locks it), so the lock with a filtered `dotnet test` is reasoned from MSBuild's rules and the running process, not reproduced. No AppHost run, so the container's recreation wasn't seen. kind wasn't started.

## Findings

### 1. The test commands the new rules recommend hit the same lock while `medapp-dev` runs

- **Severity:** medium. **Confidence:** high (MSBuild's rules and the running process; not reproduced, as that needs the build this review mustn't run).
- **What could go wrong:** an agent changes `MedApp.Application`, then follows "Testing a change" to the letter: `dotnet test MedApp.UnitTests --filter "FullyQualifiedName~UpdatePatient"`.
  - **That builds in Debug,** because no `-c` is given. The unit tests reference the tool, so MSBuild builds the tool in Debug too, and copies the new `MedApp.Application.dll` into `tools/MedApp.DevMcp/bin/Debug/net10.0/`.
  - **That's the folder the running tool loaded it from.** The copy retries ten times, then fails with the same "file is locked by: MedApp.DevMcp".
  - **The integration fixture** (`--filter "FullyQualifiedName~<Fixture>"`, `AGENTS.md:51`) does the same.
  - **When:** whenever the change touched `Domain`, `Contracts`, `Application` or `Infrastructure` and the tool is running. In a Claude Code session in this repo, it usually is: `.mcp.json` starts it.
  - **The note's own advice is half the answer:** it builds in Release (`dotnet build MedApp.UnitTests -c Release`, `AGENTS.md:449`), but the test commands above still build in Debug.
  - **The tool goes stale too:** after a Release build, the running tool still runs the code it started with. `reset_demo_data` replays history through shared code (`ProcedureChange`, `AppointmentChange`), so after a change to it, a reset uses the old rules until the tool is rebuilt in Debug and restarted. "Restart the tool afterwards with `/mcp`" doesn't say to rebuild it, or that it must be stopped before the build, not after.
- **Evidence:** `AGENTS.md:16, 46, 51, 449`; `.mcp.json:6` (`dotnet run --no-build`, so Debug); `MedApp.UnitTests/MedApp.UnitTests.csproj:44`; `MedApp.IntegrationTests/MedApp.IntegrationTests.csproj:45`; `tools/MedApp.DevMcp/MedApp.DevMcp.csproj:27-28`.
- **Fix:**
  - **The commands:** add `-c Release` to every local test command in "Testing a change" and Commands (`dotnet test MedApp.UnitTests -c Release --filter …`), as CI runs them.
  - **The note:** "While it runs, a Debug build of the unit or integration tests fails, and so does `dotnet build MedApp.sln` whatever its `-c`. Build and test in Release. The tool keeps running the code it started with: after changing code it uses, stop it in `/mcp`, run `dotnet build tools/MedApp.DevMcp`, then start it again."
  - **Better, remove the lock:** have `.mcp.json` run a copy no build writes to. For example, `dotnet publish tools/MedApp.DevMcp -c Release -o tools/MedApp.DevMcp/.run` (git-ignored) as the "Build it first" step, and `"args": ["tools/MedApp.DevMcp/.run/MedApp.DevMcp.dll"]`.
- **What happened to it:** fixed in the PR. The rules say to build and test in Release locally, with the reason, and the examples use `-c Release`. The Dev MCP section says how to give the running tool new code (stop it in `/mcp`, build, start it).

### 2. Nothing required builds the whole solution any more

- **Severity:** medium. **Confidence:** high on the mechanics, medium on how soon it bites.
- **What could go wrong:** the rules now say to build only the changed projects while `medapp-dev` runs, which is most of the time. So the solution build, the only local check that compiles the AppHost and `MedApp.Client.Deprecated`, rarely runs.
  - **CI doesn't make up for it.** The required jobs build only what they test: the Dockerfile builds the API and its references, `unit-tests` and `integration-tests` build their projects and the tool, and `e2e` builds `MedApp.E2ETests`.
  - **The one CI build of `MedApp.sln` is CodeQL's** C# analysis, which isn't a required check. The merge flow waits only for `main.yml`'s run (`AGENTS.md:606`).
  - **A scenario:** a PR renames a property on a `Contracts` DTO. The deprecated Blazor client uses it. The agent builds the API and the unit tests in Release, the filtered tests pass, and the six required checks pass. "Analyze (csharp)" fails, but nothing waits for it, so the PR merges.
  - **The result:** `dotnet build MedApp.sln` fails on `main` for everyone, and CodeQL's C# analysis of `main` stops producing results until someone fixes it. An AppHost change, like this PR's, would break `dotnet run --project MedApp.AppHost` the same way.
- **Evidence:** `AGENTS.md:45`; `.github/workflows/main.yml:472-480` (`unit-tests` builds only the tool, then the tests); `.github/workflows/codeql.yml:54-61` (the only `dotnet build MedApp.sln`); `MedApp.Client.Deprecated/MedApp.Client.Deprecated.csproj:29` (references `Contracts`); the ruleset's required checks (How it was checked).
- **Fix:** build the solution in a required job. In `main.yml`'s `unit-tests` job, the step that builds the tool becomes:

  ```yaml
        # The whole solution (the AppHost and the deprecated client are built nowhere else that's required), then
        # the dev MCP tool, which isn't in it.
        - name: Build the solution and the dev MCP tool
          run: |
            dotnet build MedApp.sln --configuration Release
            dotnet build tools/MedApp.DevMcp --configuration Release
  ```

  It adds about a minute, as the test step's own build is then mostly up to date. Making "Analyze (csharp)" required would also work, but ties merging to CodeQL's own failures.
- **What happened to it:** fixed in the PR. `unit-tests` now builds `MedApp.sln` in Release before the tool and the tests, so a required check builds the whole solution.

### 3. A visible change can merge without anyone seeing it in a browser

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** CI has no visual check. Vitest runs in jsdom, a simulated browser with no layout, and the E2E journeys find elements by role and name, wherever they sit on the page.
  - **Before this PR,** two things looked: the author captured the changed screens, unconditionally (the old `MedApp.Web/AGENTS.md`), and the full parity set ran before every merge of a UI change.
  - **Now, both can be skipped:**
    - the author's screenshots are only "if the AppHost is already running" (`MedApp.Web/AGENTS.md:99`), and the root rules don't mention them at all (`AGENTS.md:44-58`)
    - the whole set "isn't part of merging" (`AGENTS.md:429`)
    - the reviewer "starts no … AppHost … or browser" (`AGENTS.md:603`)
  - **A scenario:** a change to a shared component's spacing (`Page`, `DataTable`) passes Vitest and E2E. The AppHost isn't running, so no screenshot is taken, and the reviewer only reads code. It merges with the page out of line on every screen that uses it.
  - **This has happened:** the review of #96 found the calendar's menu opening in the window's top-left corner only by driving the app in a browser. All 13 checks had passed on that commit (`docs/reviews/pr-96-calendar-drag-block.md`, finding 1). Under the new rules, that review couldn't have found it.
- **Evidence:** `MedApp.Web/AGENTS.md:99`; `AGENTS.md:44-58, 429, 595, 600-603`.
- **Fix:**
  - **For a visible change, make the targeted screenshots unconditional:** "start the AppHost if it isn't running; it's the light setup". Add a bullet to the root "Testing a change" list too.
  - **Let the reviewer look** at the screens a PR changes when it touches the UI (the AppHost, Chromium, a few screens by name), and keep banning only the heavy runs: kind, full suites, the whole parity set.
  - **Optionally,** keep the whole parity set for a theme, layout or shared component change, as `AGENTS.md:429` already suggests, and say so in the merge flow.
- **What happened to it:** fixed in the PR. A visible change is looked at in a browser: the AppHost is started if needed, and the touched screens are captured and looked at (root and web `AGENTS.md`).

### 4. The accessibility statement promises checks the rules no longer run, and the two AGENTS files disagree

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the app's own accessibility statement tells users: "The checks run again whenever the app's screens change."
  - **What backs it:** the axe audit (`npm run a11y` in `tools/ui-parity`). *axe* is an automated checker for WCAG problems: missing labels, low contrast, wrong roles. It isn't in CI (`docs/architecture.md:698`).
  - **The two rule files now disagree:**
    - the root rules list "accessibility audits" under **Not locally** (`AGENTS.md:56`), with no "whole", unlike the parity line above it
    - `MedApp.Web/AGENTS.md:77` says to run it on the screens a visible change touches, and the whole run only when the user asks
    - neither file's "before calling a change done" list includes it (`MedApp.Web/AGENTS.md:95-99`)
  - **A scenario:** a theme token change lowers the contrast of secondary text on every screen. Every screen is "touched", so the targeted rule means the whole ten-minute run, which is only for when the user asks; the root rule says not to run it. It merges, and the statement still says the checks ran again.
  - **Why it matters more than a doc:** UK public sector customers, the NHS among them, must publish an accessibility statement (the Public Sector Bodies Accessibility Regulations 2018), and it's expected to say how the service was checked. A customer would build theirs on this one. `docs/architecture.md:1245` and `docs/todo.md:7` also say every screen passes.
- **Evidence:** `MedApp.Web/src/pages/accessibility/AccessibilityStatementPage.tsx:56-58`; `AGENTS.md:52-57`; `MedApp.Web/AGENTS.md:77, 95-99`; `docs/architecture.md:698, 1245, 1339`.
- **Fix:**
  - **Reconcile the rules:** in `AGENTS.md:56`, write "the whole accessibility audit". Add axe on the touched screens (`npm run a11y -- -g "<screen>"`) to the visible-change bullet of both files. Say a theme, layout or shared component change runs the whole audit.
  - **Or change the statement** to say when the audit runs, if it won't run on every screen change.
  - **Longer term,** run axe in CI: `Deque.AxeCore.Playwright` in the E2E suite, over the pages the journeys already open, against the production image.
- **What happened to it:** fixed in the PR. A visible change also gets the axe audit of the screens it touches, so the statement stays true, and the two `AGENTS.md` files say the same. Only the run of every screen is left for when the user asks.

### 5. The deploy check is skipped whenever the kind cluster is stopped, and kind is the only deployment

- **Severity:** medium. **Confidence:** medium (depends on how often changes break a deploy without failing CI).
- **What could go wrong:** step 8 now checks Argo CD only "if the kind cluster is running" and says not to start it for this. The new advice is to stop the cluster after chart work, so after most merges it's stopped, and nobody checks that the deploy took.
  - **What CI doesn't catch:** the `helm` job lints and renders the chart; `e2e` runs the image with the chart's container restrictions. Neither installs the chart, so neither sees its network policies, External Secrets, the migrations Job, the ingress or Argo CD's sync.
  - **A scenario:** a PR adds a setting the API checks at startup, or a call to a new outside service, but no matching chart change. `AGENTS.md:40` warns that a new connection needs one, but it's easy to miss. CI passes and Publish moves `env/kind`.
  - **What follows:** the cluster is stopped, so nothing happens. Weeks and several merges later, someone starts kind for chart work, and Argo CD syncs the latest commit: the API crash-loops, or the feature is blocked. Which merge caused it is now hard to tell.
  - **Azure isn't deployed** (`AGENTS.md:42`), so kind is the only deployment that ever runs.
- **Evidence:** `AGENTS.md:40, 42, 467, 611-614`; `.github/workflows/main.yml` (the `helm` and `e2e` jobs).
- **Fix:**
  - **Say when the cluster must be started for the check:** a change to the chart, the Dockerfile, settings the API reads at startup, secrets, ports or outbound connections. Otherwise, as the PR says, don't start it just for this.
  - **Better, close the gap in CI:** a job that creates a kind cluster on the runner (`helm/kind-action`), installs the chart with kind's values (plain Secrets instead of External Secrets, no Argo CD), and waits for `kubectl rollout status`. That finds a crash loop on the PR, at no cost to the developer's PC.
- **What happened to it:** fixed in the PR. The rules name the changes that must be tried on kind before merging: the chart, the Dockerfile or the image, `deploy/kind` or `deploy/argocd`, and a new setting, secret, port, container or connection. The deploy check refers to it.

### 6. Other docs still state the old rules

- **Severity:** low. **Confidence:** high.
- **What's out of date:**
  - **`docs/architecture.md:130`:** `AGENTS.md` "requires a build with no new warnings, passing tests, and for web changes `npm run lint`, `npm run typecheck` and `npm test`".
  - **`docs/architecture.md:1048-1051`:** "Before calling a change done: `dotnet build MedApp.sln && dotnet test MedApp.UnitTests`" and the full web scripts. That's the old rule, and its solution build fails while `medapp-dev` runs.
  - **`docs/architecture.md:1053`:** kind needs "about 4 GB of memory". The PR calls it "much the heaviest", and its app pods alone may use about 6.75 GiB (Detail).
  - **Older drift the PR now leans on:** `docs/architecture.md:132, 825, 1015-1021, 1451` say the required checks are `web`, `image`, `helm` and `unit-tests`, and the CI table has no rows for `integration-tests`, `e2e` or `dependency-review`. The ruleset also requires `integration-tests` and `e2e`. The new paragraph at `:1290` rests on "CI runs every suite"; the same document's CI table doesn't show it.
- **Fix:** bring each passage in line with `AGENTS.md`'s "Testing a change", and add the two jobs to the CI table as required.
- **What happened to it:** fixed in the PR. `docs/architecture.md`'s constraints, quick start, kind memory, required checks (all six, from the ruleset) and CI table (with `integration-tests` and `e2e`) match.

### 7. The WSL advice: one number for every PC, a setting that's already the default, and a restart that kills containers

- **Severity:** low. **Confidence:** high on the facts, medium on the sizing.
- **What's wrong with it:**
  - **12 GB isn't always a cap.** WSL's default is half the PC's memory, so `memory=12GB` lowers it only on a PC with more than 24 GB. On a 16 GB laptop, it raises WSL's ceiling from 8 GB to 12 GB and leaves Windows, the IDE, the browser and Node 4 GB.
  - **12 GB is tight with kind.** kind's app pods may use about 6.75 GiB, plus Argo CD, cert-manager, External Secrets, Traefik and Kubernetes itself. Add the AppHost's SQL Server (up to 2 GB), an integration run's (another 2 GB) and `up.sh`'s image build (`docker build`, inside the same machine), and it's over. Past the cap, WSL swaps, then its kernel kills the largest process, probably a SQL Server.
  - **`autoMemoryReclaim=dropCache` is already the default** in current WSL (Microsoft's `wsl-config` page). So "it keeps what it used as cache" (`AGENTS.md:462`) overstates the default, and the line changes nothing there. It does no harm, and helps on an older WSL.
  - **"It applies after `wsl --shutdown`"** leaves out quitting Docker Desktop first. With Docker Desktop running, `wsl --shutdown` pulls the machine out from under every container: the persistent dev SQL Server stops without a clean shutdown, and a test run in progress leaves its containers behind. This PC has two such sets from 16 November (How it was checked), so "remove them afterwards" (`AGENTS.md:471`) holds only for runs that finish.
- **Evidence:** `AGENTS.md:462, 471, 472`; Microsoft's `wsl-config` page (How it was checked); `deploy/helm/medapp/values.yaml:19-21, 37-39, 299-301`; `deploy/kind/values.yaml:9`; `deploy/kind/up.sh:201`.
- **Fix:** reword `AGENTS.md:472` along these lines:
  - "WSL may take half the PC's memory. On a PC with 32 GB or more, `[wsl2] memory=12GB` in `%UserProfile%\.wslconfig` caps it; raise it to 16GB when you run the kind cluster. Leave the default on a smaller PC."
  - "Current WSL already gives cached memory back when idle (`autoMemoryReclaim` defaults to `dropCache`)."
  - "To apply it: quit Docker Desktop, run `wsl --shutdown`, then start Docker Desktop."
  - "An interrupted integration run leaves its containers: `docker container prune --filter label=org.testcontainers=true` removes them."
- **What happened to it:** fixed in the PR. The advice says when 12 GB lowers the limit, to raise it to 16 GB for kind and the AppHost together, that `dropCache` is the default, and to quit Docker Desktop before `wsl --shutdown`.

### 8. `docker stop medapp-control-plane` kills the cluster after 10 seconds

- **Severity:** low. **Confidence:** high on the kill, medium on the harm.
- **What could go wrong:** the node runs systemd, and Docker's stop signal asks it to shut everything down: Kubernetes, etcd (its database), the cluster's SQL Server and Valkey. Docker waits only 10 seconds, then kills the whole node.
  - **Seen here:** the node's last stop, seven minutes before this commit, ended with exit code 137 (killed).
  - **What it risks:** SQL Server then recovers on its next start, and etcd is built to survive this, so usually nothing is lost. But a stop during a migration Job or an image load is a hard kill mid-write, for no reason.
  - **A smaller point:** `docker update --restart=no` "once" holds only until `down.sh` and `up.sh` make a new node; kind sets its own restart policy on each new node.
- **Evidence:** `deploy/kind/README.md:59, 63`; the node's state (How it was checked).
- **Fix:** `docker stop --time 120 medapp-control-plane` in the README, with "gives the cluster two minutes to shut down cleanly"; and "after `up.sh` makes a new cluster, run it again" beside `docker update`.
- **What happened to it:** fixed in the PR. `docker stop --time 120`, with the reason, and `docker update` again after `up.sh` makes a new cluster.

## Remaining risks

- **CI checks each PR against `main` as it was when CI ran.** The ruleset doesn't require a branch to be up to date, so two PRs green on their own can break `main` together. The old full local runs didn't cover this either.
- **The development path is checked by nobody:** CI runs E2E against the production image, so the Vite dev server, the AppHost's wiring and attached files (no storage in CI's `e2e` job) are only ever seen locally. That was true before; now it's less likely to be seen at all.
- **The in-cluster SQL Server has a 3 GiB container limit but no SQL Server cap** (`deploy/helm/medapp/values.yaml:296-301`). Whether SQL Server sizes itself to the container's limit depends on its version; if it doesn't, it aims for 80% of WSL's memory and is killed at 3 GiB. Setting `MSSQL_MEMORY_LIMIT_MB` to about 80% of the limit (2457) in the chart would make it certain. Not checked here: the cluster was stopped.
- **The AppHost's container hasn't been recreated yet.** Aspire documents that it will be, with the data kept; the first AppHost run after merging is when that happens, and `medapp-dev` loses its connection for a moment.
- **"About 8 minutes" depends on the slowest job.** The integration suite is now 7 to 7.5 minutes of it, and grows with each feature.

## Say it like this

"PR #100 moved the heavy testing off the developer's PC: locally you run only the tests for what you changed, and CI runs every suite on every pull request before it can merge. It also capped SQL Server at 2 GB, its own budget rather than a hard container limit, so it can't take most of the machine. The trade-off is that a few checks CI never did, like looking at a change in a browser, the accessibility audit and watching the deploy, now happen only when someone chooses to, so the review recommends moving them into CI."

## Detail

### Why a solution build always builds the tool as Debug

From `Microsoft.Common.CurrentVersion.targets` in SDK 10.0.404, target `AssignProjectConfiguration`:

```xml
<ShouldUnsetParentConfigurationAndPlatform Condition="'$(ShouldUnsetParentConfigurationAndPlatform)' == '' and ('$(BuildingInsideVisualStudio)' == 'true' or '$(BuildingSolutionFile)' == 'true')">true</ShouldUnsetParentConfigurationAndPlatform>
```

- **In a solution build,** each reference is given the configuration the solution maps it to. A reference that isn't in the solution has no mapping, and with this property set, MSBuild removes `Configuration` and `Platform` before building it. The tool's project has no default of its own, so the SDK's applies: Debug.
- **In a project build** (`dotnet build MedApp.UnitTests -c Release`), `Configuration=Release` is a global property and flows to every reference, the tool included. So a Release project build writes to `bin/Release` and doesn't touch the running Debug tool.
- **Without `-c`,** a project build is Debug all the way down, and copies the changed app DLLs into the tool's `bin/Debug` (finding 1).
- CodeQL's workflow builds the solution, then the tool again in Release (`codeql.yml:60-61`), for this reason.

### Docker state on 17 November 2026

| Container | Image | State | What it shows |
|---|---|---|---|
| `sql-6aca4589` | SQL Server 2022 (16.0.4325.4) | Exited | The AppHost's persistent database. Created 14 November; environment has no `MSSQL_MEMORY_LIMIT_MB`. |
| `medapp-control-plane` | `kindest/node:v1.37.0` | Exited (137), 04:03:22 | The kind cluster's one node. Restart policy `no`; stop signal `SIGRTMIN+3`; default 10-second timeout. |
| `musing_kilby`, `practical_dhawan` | SQL Server, Azurite 3.37.0 | Exited (255), 19:32 on 16 November | An integration run from 14:52 that never cleaned up. Testcontainers labels. |
| `wizardly_wozniak`, `nostalgic_brahmagupta` | SQL Server, Azurite 3.37.0 | Exited (137 and 0), 23:21 on 16 November | Another, from 23:06. |

### The integration suite with and without the cap

| Commit | Branch | SQL Server | `integration-tests` job |
|---|---|---|---|
| `c2264d6` | `feature/demo-data` (#97) | Uncapped | 6 min 51 s |
| `3bf74f1` | `feature/demo-data` (#97, with its review) | Uncapped | 7 min 23 s |
| `f8a3ce3` | `feature/lighter-dev` (this PR) | 2 GB | 7 min 28 s |

The job includes restoring and building; the difference is within the run-to-run spread.

### What the kind cluster's app asks for

From `deploy/helm/medapp/values.yaml`, with kind's two API copies (`deploy/kind/values.yaml:9`):

| Pod | Request | Limit |
|---|---|---|
| API, 2 copies | 512 MiB | 2 GiB |
| Page server, 2 copies | 256 MiB | 1 GiB |
| SQL Server | 2 GiB | 3 GiB |
| Azurite | 128 MiB | 512 MiB |
| Valkey | 32 MiB | 128 MiB |
| Mailpit | 32 MiB | 128 MiB |
| **Total** | **about 3 GiB** | **about 6.75 GiB** |

Not counted: the migrations Job (up to 512 MiB while it runs), Argo CD, cert-manager, External Secrets, the secret store, Traefik and Kubernetes' own components.

### WSL's documented settings (October 2026)

| Setting | Section | Default | Values |
|---|---|---|---|
| `memory` | `[wsl2]` | 50% of Windows' memory | a size, such as `12GB` |
| `swap` | `[wsl2]` | 25% of Windows' memory | a size; `0` for none |
| `autoMemoryReclaim` | `[experimental]` | `dropCache` | `disabled`, `gradual`, `dropCache` |
