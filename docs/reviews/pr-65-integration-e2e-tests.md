# Review: PR #65, integration and end-to-end tests, run in CI

**PR:** [#65](https://github.com/Aliator/MedApp/pull/65), branch `feature/integration-e2e-tests`, reviewed at `f755147` against `main` at `a506079` (the merge of #61). The branch started from `ea827ef` (the merge of #59); `main` has since gained only #61's documents, which merged into the branch cleanly before this review was committed. Line numbers below are for the branch after that merge.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a local build and test run, two mutation runs in a scratch copy, the CI logs or the GitHub API. **No finding is high or critical;** three are medium and seven are low.

## Summary

The PR fills the two empty test projects: 182 integration tests that run the real API against a real SQL Server, and 11 Playwright journeys that drive the React app in a browser, plus two CI jobs that run them on every pull request. The suites are well built, isolated and green in CI; the gaps are a few tests that pass for a reason other than the one their names give, a browser suite that reports success when it can't sign in, and new jobs that don't yet block a merge.

## What changed, and why

34 files, 3,500 lines added and 45 removed. Until now nothing tested the assembled app: unit tests call one class at a time with fakes around it, and the handbook listed "no integration or end-to-end tests" as debt C1.

- **Integration tests (`MedApp.IntegrationTests`):** the real API, started inside the test process, talking HTTP to itself, against SQL Server 2022 in Docker.
  - **`WebApplicationFactory`** runs `Program` in memory, in the Production environment (so the session cookie is `Secure` and HSTS is on), with server-side rendering off (`MedAppApiFactory`). Each test gets HTTP clients with their own cookie jars, like separate browsers.
  - **Testcontainers** starts a throwaway SQL Server container once per run, applies every migration, and removes the container afterwards (`TestEnvironment`, a `[SetUpFixture]`).
  - **Respawn** empties every table before each test, except the migrations history and the seeded "Definition missing" stage definition, which a fix-up puts back as the migration left it. Then the deploy's own `FirstAdminSetup` makes the admin. Like wiping a whiteboard between lessons but keeping the printed heading.
  - **What they cover:** sign-in, sessions, expiry, revocation and lockout; `SessionService` and `IdentityTransactions`, which unit tests can't reach; an authorisation matrix (20 admin-only and 6 staff endpoints, by role and signed out); the forced password change from #59, including the pipeline test its review asked for; users and roles; patients' search as real SQL (filters, sorting, paging without repeats); the procedure workflow, audit, due dates in summer and winter, and archiving; notifications, including a live push through the SignalR hub; error responses; and every migration up, down and up again, with no model changes left over.
- **End-to-end tests (`MedApp.E2ETests`):** Playwright drives Chromium through the main journeys: sign in and out, a wrong password, patients (add, find, edit, delete), a procedure worked to "In review", My Tasks, a new user's first sign-in with the forced password change, deleting a user, and the notification bell updating live.
  - **Setup through the API:** `E2EApi` makes what a journey needs (patients, stage definitions, templates, procedures, accounts) with unique synthetic names, and `E2EData` deletes it all afterwards, newest first, pass or fail.
  - **Waiting for the page to come alive:** pages are rendered on the server and then taken over by React in the browser (hydration). `OpenAsync` waits until every control carries React's event props, then until the network is quiet, because a click before then is lost.
- **CI (`.github/workflows/main.yml`):**
  - **`integration-tests`:** `dotnet test MedApp.IntegrationTests` in Release. GitHub's Ubuntu runners have Docker, so Testcontainers works there.
  - **`e2e`:** builds the production image (reading the shared GitHub Actions build cache), runs SQL Server as a service container, migrates with the image's own `efbundle`, makes the first admin with `create-first-admin` and a password generated and masked for the run, starts the API on `http://localhost:8080`, installs Chromium and runs the 11 journeys.
- **Docs:** `AGENTS.md` (commands, conventions for both suites, CI), `MedApp.E2ETests/README.md`, `docs/architecture.md` (test counts, the quality table, debt C1 lowered to low, the roadmap), and the comment in `.github/workflows/e2e.yml`, the by-hand workflow that runs the same journeys against a deployed URL.

## Design choices

- **The API in memory, not the container, for integration tests:**
  - **Instead:** run the built image and test it over the network, as the `e2e` job does.
  - **Chosen because:** in memory is fast (about a minute for 182 tests), debuggable, and lets a test reach the API's own services (`RunInScopeAsync`) for what HTTP can't do, such as expiring a session.
  - **Worse at:** it isn't Kestrel or the image. The test server has no sockets, so the hub test uses long polling where browsers use WebSockets, and there's no web root, so the page fallback is never exercised (finding 5).
- **A real SQL Server in Docker, not SQLite or EF's in-memory provider:**
  - **Chosen because:** the point is real SQL translation, real Identity stores and real transactions.
  - **Worse at:** Docker is required locally, and every CI run pulls a 1.6 GB image (about 30 seconds on the runner).
- **One database per run, reset by Respawn before each test:**
  - **Instead:** a database per test, or each test in a transaction rolled back at the end.
  - **Chosen because:** one database is quick to reset, and real commits happen (a rolled-back transaction would hide `IdentityTransactions`' own).
  - **Worse at:** tests can't run in parallel, and the reset keeps seeded data only by a hand-kept list (Remaining risks).
- **The production image in CI for end-to-end tests, not the dev server:**
  - **Chosen because:** it tests what ships: the built React app, the Node page server, the migrations bundle and `create-first-admin`.
  - **Worse at:** the job builds the image itself, at the same time as the `image` job, so when the cache is cold (most UI or API changes) the image is built twice in parallel. And one writable container on plain http isn't how the chart runs it (finding 7).
- **Test data made through the API, not a seeded database:**
  - **Chosen because:** the same tests can run against any environment, including a deployed one through `e2e.yml`.
  - **Worse at:** what the app can't delete stays (audit entries, a read notification, session rows), and accounts the tests make carry a fixed password (finding 4).
- **One sign-in per run, saved and reused:**
  - **Chosen because:** it's quicker, and the sign-in journey is tested on its own with a separate session.
  - **Worse at:** if credentials are missing, the whole run is skipped rather than failed (finding 2).
- **Hydration detected through React's internal props (`__reactProps$`), not a marker the app sets:**
  - **Chosen because:** no app change is needed, and it checks every control rather than one flag.
  - **Worse at:** it depends on a name React doesn't promise to keep (Remaining risks).

## How it was checked

- **Read:** the whole diff, the PR description, `AGENTS.md`, and the code each test's claim depends on: the middleware order (`ApplicationBuilderExtensions`), the policies, `PasswordChangeRequired`, `SessionService`, `SsrPages`' fallback, the seeded stage definition and its migrations, the password generator, `DeleteUserHandler`, and the chart's security context.
- **Build:** `dotnet build MedApp.sln --configuration Release`: 0 warnings, 0 errors.
- **Integration tests locally:** `dotnet test MedApp.IntegrationTests --configuration Release` on Windows with Docker Desktop: 182 passed in 1 minute 7 seconds. Testcontainers' cleanup container (Ryuk) removed the SQL Server afterwards; no container was left behind.
- **Mutation runs:** a mutation test breaks the code on purpose to see whether a test notices, like unplugging a sensor to check its alarm sounds. In a scratch copy outside the repository (`git archive` of the branch):
  - the `Staff` policy weakened to "any signed-in user": 5 of the 6 `StaffEndpoint_NoRoles_Returns403` rows still passed (finding 1)
  - the `/api/{**path}` 404 route removed: all four "matching no endpoint" tests still passed (finding 5)
- **End-to-end tests:** not run against any app. With no credentials and an unreachable base URL, the suite reported 11 skipped and exited with code 0 (finding 2). The Playwright NUnit assembly carries no `Parallelizable` attribute, so the journeys run one at a time, as the CI timestamps show.
- **CI:** run [43832645185](https://github.com/Aliator/MedApp/actions/runs/43832645185) on `f755147` (merged with `main` at `a506079`): all seven jobs passed. `integration-tests` 182 of 182 (the job took 2 minutes 12 seconds); `e2e` 11 of 11, none skipped (2 minutes 30 seconds); `unit-tests` 815; `web` 713. The `e2e` log was read for secrets: the first admin's password shows as `***` everywhere; the SQL Server password is printed in clear (finding 8).
- **GitHub API:** `main`'s "Protect" ruleset requires `web`, `helm`, `unit-tests` and `image` only (finding 3). The repository has no Actions secrets, and `e2e.yml` has never run (finding 2).
- **Data clean-up, by reading:** every thing a journey makes is registered for deletion, and the order is right in every test (a procedure before its patient and template, a template before its stages). What stays is listed in finding 10.
- **Not done:** the E2E journeys weren't run locally (no app was started for them), and nothing was run against a deployed environment.

## Findings

Most severe first. None is high or critical. None was fixed at the time of this review; each one's "What happened to it" says how it was dealt with afterwards.

### 1. Five of the six "no roles" authorisation rows are passed by the forced password change, not by the Staff policy

- **Severity:** medium. **Confidence:** high (mutation-checked).
- **What could go wrong:** `StaffEndpoint_NoRoles_Returns403` makes its account with `CreateStaffAsync()`, which changes the generated password only when there are roles. An account with none can't change it (the change needs the Staff policy), so it stays held by `PasswordChangeRequired`, the middleware that answers every other `/api` request with a 403 before authorisation even runs. Five of the six rows get that 403; only `GET /api/auth/me/settings`, which the middleware lets through, reaches the Staff policy. So if an endpoint's `[Authorize(Policy = AuthPolicies.Staff)]` became a bare `[Authorize]`, a staff member whose roles an admin had removed (a leaver, say: their password is their own, so nothing holds them) could call it, and the matrix would stay green. It's like testing a locked office door while the building's front gate is also shut: the test passes, but the gate is doing the work. With the policy weakened to "any signed-in user" in a scratch copy, 5 of 6 rows still passed. One other test (`UserTests.SetUserRoles_BothRolesThenNone_AddsAndRemovesThem`, whose account chose its own password before losing its roles) would catch that mutation for one endpoint (by reading; not run against the mutation), so the suite isn't blind, but the matrix the handbook cites as the evidence for confidentiality is weaker than it reads.
- **Evidence:**
  - `MedApp.IntegrationTests/Common/IntegrationTest.cs:98` and `:107`: the password is changed only `if (roles.Length > 0)`
  - `MedApp.IntegrationTests/Auth/AuthorisationTests.cs:89-95`: `CreateStaffAsync()` with no roles, then any 403 passes
  - `MedApp.API/Controllers/AuthController.cs:152-154`: changing a password needs the Staff policy
  - `MedApp.Infrastructure/Common/Identity/IdentityUserService.cs:25`: every new account must change its password
  - `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:47-48`: the middleware runs before `UseAuthorization`
  - `MedApp.API/Common/Authentication/PasswordChangeRequired.cs:50-51`: `me/settings` is allowed through, which is why that one row works
  - `MedApp.IntegrationTests/Auth/UserTests.cs:100`: the one other test that would notice
  - `docs/architecture.md:1248`: the matrix as the measure of confidentiality; `AGENTS.md:237` says `CreateStaffAsync` always changes the password
- **Fix:** test the account the policy actually guards: create it with `User`, let it choose its password (as `CreateStaffAsync(UserRole)` does), then remove its roles through `PUT /api/auth/users/{username}/roles` with `[]`, and assert the 403 isn't the `password_change_required` problem. Keep the forced case, if wanted, under its own name in `PasswordChangeRequiredTests`. Make `AGENTS.md:237` say no-role accounts keep their generated password. (Code in Detail.)
- **What happened to it:** fixed. `StaffEndpoint_NoRoles_Returns403` became `StaffEndpoint_RolesRemoved_Returns403FromTheStaffPolicy`: the account gets `User`, chooses its own password, then an admin removes its roles, and the test asserts the 403 isn't `password_change_required`. The held case moved to `PasswordChangeRequiredTests.ApiRequest_AccountWithNoRoleYet_IsHeldByTheForcedChange`. Mutation-checked: with the Staff policy weakened to "any signed-in user", all 6 rows now fail. `AGENTS.md` and `CreateStaffAsync`'s comment say that a no-role account keeps its generated password.

### 2. The end-to-end suite reports success when it can't sign in, and the by-hand workflow's credentials don't exist

- **Severity:** medium. **Confidence:** high (run locally; secrets checked through the API).
- **What could go wrong:** with `E2E_USERNAME` or `E2E_PASSWORD` empty, `E2ESession` calls `Assert.Ignore`, so every journey is skipped and `dotnet test` exits with 0. In GitHub that's a green tick for a run that tested nothing, like a smoke alarm with a flat battery whose light still shows green. Two ways in:
  - **Today:** `e2e.yml` reads both from repository secrets, and the repository has none. Anyone who runs "E2E tests" by hand against a deployment gets a passing run that never opened a page.
  - **Later:** in `main.yml`, the password reaches the tests through `$GITHUB_ENV` from an earlier step. If that step is renamed, reordered or made conditional, the `e2e` check stays green with 11 skipped.
- **Evidence:**
  - `MedApp.E2ETests/Common/E2ESession.cs:19-21`: skip, not fail
  - `.github/workflows/e2e.yml:33-34`: the secrets; `GET /repos/Aliator/MedApp/actions/secrets` returns `total_count: 0`
  - `.github/workflows/main.yml:121-126`: the password handed over through `$GITHUB_ENV`
  - a local run with no credentials: 11 skipped, exit code 0
  - `AGENTS.md:261` and `MedApp.E2ETests/README.md:73` describe the by-hand run as using those secrets
- **Fix:** fail instead of skipping when running in CI (GitHub sets `CI=true`), so a green run always ran the journeys; keep the skip for a local run without credentials. Then either create the two secrets for a known deployment, or say in `e2e.yml` and the README that they must be created first. (Code in Detail.)
- **What happened to it:** fixed. `E2ESession` fails when `CI=true` and a credential is missing, and still skips locally. `e2e.yml`, the README and `AGENTS.md` say the `E2E_USERNAME` and `E2E_PASSWORD` repository secrets don't exist yet and must be created for a deployment before the by-hand workflow is used. No secrets were created.

### 3. Neither new job is a required check, so a red one doesn't stop a merge

- **Severity:** medium. **Confidence:** high (ruleset read through the API).
- **What could go wrong:** `main`'s ruleset requires `web`, `helm`, `unit-tests` and `image`. A pull request that breaks sign-in, the forced password change or a migration turns `integration-tests` or `e2e` red, and GitHub still offers the merge button; the merge then deploys to kind through Publish. The documented merge flow in `AGENTS.md` waits for the whole run and stops on any failure, so a merge done that way is safe, but a merge from the web page isn't. Meanwhile the handbook lowers debt C1 to low because the suites "run on every pull request": they run, but they don't guard.
- **Evidence:**
  - ruleset 12421699 ("Protect"): `required_status_checks` lists `web`, `helm`, `unit-tests`, `image`
  - `AGENTS.md:249`: the same four named as required
  - `docs/architecture.md:98` and `:1333`: "both run on every pull request"; C1 lowered to low
  - `docs/governance/security-policies.md:52`: the policy lists the four required checks
- **Fix:** add `integration-tests` and `e2e` to the ruleset's required checks once they've proved stable over a few runs, then update `AGENTS.md:249` and `security-policies.md:52`. Until then, say in `docs/architecture.md` that they report but don't block.
- **What happened to it:** decided by the owner: `integration-tests` and `e2e` will be added to `main`'s required checks once this merges and runs green on `main`. The docs say "required on `main`" now (`AGENTS.md`, `docs/governance/security-policies.md`, `docs/architecture.md`), so they're true once that's done.

### 4. Accounts the journeys make, an admin among them, all get the same password, which is in the public repository

- **Severity:** low. **Confidence:** high on the mechanism; the exposure needs several things to line up.
- **What could go wrong:** the bell journey makes a second **admin**, and the new-user journey a staff account, and both set the password `E2e-Chosen-Passw0rd`, written in the source. They're deleted when the test ends, but a clean-up runs only if the process lives that long: a cancelled run, a job timeout or a crash leaves the account. Against CI's throwaway database that doesn't matter. Against a deployment, through `e2e.yml`, it leaves an admin account whose password anyone can read on GitHub. The username has 8 random hex characters, so it isn't guessable, but it's printed in the public run log whenever a clean-up fails (`Couldn't clean up after the test: user e2e-admin-…`).
- **Evidence:**
  - `MedApp.E2ETests/Notifications/NotificationBellTests.cs:11` and `:68-73`: an Admin and User account set to the fixed password
  - `MedApp.E2ETests/Users/UserTests.cs:15`, `:71-72` and `:82`: the same password for the new user
  - `MedApp.E2ETests/Common/E2EData.cs:13` and `:33`: the 8-character suffix, and the clean-up failure message that names the account
- **Fix:** generate the chosen password per run, for example `$"E2e-{Guid.NewGuid():N}"` (upper and lower case, a digit and a symbol, as the rules need), and keep it out of failure messages. Mention in the README that a cancelled run against a deployment can leave `e2e-…` accounts to delete by hand.
- **What happened to it:** fixed. The accounts the journeys make get a password from `E2EData.NewPassword()`, new each run; none is in the source, and none goes into a message. The README says a cancelled run against a deployment can leave `e2e-…` accounts to delete by hand.

### 5. The "no endpoint gives 404, not the app" tests can't fail, because the tests have no app to fall back to

- **Severity:** low. **Confidence:** high (mutation-checked).
- **What could go wrong:** `ApiPath_MatchingNoEndpoint_Returns404NotTheApp` is meant to prove that an unknown `/api` path gets a 404 rather than the React app's `index.html`. But `MedApp.API/wwwroot` is git-ignored and only the image fills it, so in tests the page fallback finds no `index.html` and answers 404 as well. With the `/api/{**path}` route removed in a scratch copy, all four tests still passed. If that route were lost, an API client would get HTML with a 200 in production and these tests would stay green.
- **Evidence:**
  - `MedApp.IntegrationTests/Api/ErrorTests.cs:57-72`: the tests
  - `MedApp.IntegrationTests/Common/MedAppApiFactory.cs:15-22`: no web root is set
  - `MedApp.API/ServerRendering/SsrPages.cs:130-137`: no `index.html`, so 404
  - `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:54`: the route under test; `.gitignore:16`
- **Fix:** give the factory a temporary web root holding a stub `index.html`, and add the positive case: a page path such as `/patients/anything` returns the stub, while the `/api` paths still get 404 without it. (Code in Detail.)
- **What happened to it:** fixed. `MedAppApiFactory` gives the API a temporary web root with a stub `index.html` (deleted with the factory), the `/api` 404 tests check that the stub isn't in the answer, and `PagePath_AnyRoute_ReturnsTheApp` shows a page path gets it. Mutation-checked: with the `/api/{**path}` route removed, all four `/api` tests now fail.

### 6. Two session-expiry tests compare SQL Server's clock with the test machine's

- **Severity:** low. **Confidence:** medium (not reproduced; depends on clock drift).
- **What could go wrong:** the tests expire a session by setting `ExpiresAtUtc` from `SYSUTCDATETIME()`, the database container's clock, but `SessionService` checks it against `DateTime.UtcNow`, the test machine's. On Linux CI they're the same clock. On Docker Desktop (Windows or macOS) the container runs in a virtual machine whose clock can drift; if it runs more than a second ahead, `ValidateSessionAsync_ExpiredSession_ReturnsNull` sees a session that hasn't expired yet and fails, only on that machine, only sometimes.
- **Evidence:**
  - `MedApp.IntegrationTests/Auth/SessionServiceTests.cs:82`: one second in the past, by the database's clock
  - `MedApp.IntegrationTests/Auth/SignInTests.cs:120`: one minute, the same mix
  - `MedApp.Infrastructure/Common/Authentication/SessionService.cs:44`: compared with the app's clock
- **Fix:** pass the time from .NET, with a margin: `{DateTime.UtcNow.AddMinutes(-5)}` as the interpolated parameter in both statements.
- **What happened to it:** fixed. Both expiry statements, the revocation and the overdue due date pass the time from .NET (`DateTime.UtcNow`, five minutes or a day back) instead of `SYSUTCDATETIME()`.

### 7. The `e2e` job runs the image "as a deploy runs it", but not under the chart's restrictions

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** the chart runs the API with a read-only root filesystem, a writable `/tmp` only and all Linux capabilities dropped, and on kind as two copies with the Valkey backplane. The job runs one writable container with default capabilities and no backplane. A change that writes outside `/tmp` (a cache file, a temporary upload) passes every journey in CI and fails on kind after the merge. The job's own log already shows the image storing its data-protection keys in `/home/app/.aspnet` (the "Make the first admin" step), a folder that is read-only under the chart.
- **Evidence:**
  - `.github/workflows/main.yml:71` and `AGENTS.md:253`: "as a deploy runs it"
  - `.github/workflows/main.yml:144-147`: `docker run -d … --network host` with no restrictions
  - `deploy/helm/medapp/templates/_helpers.tpl:70-75` and `deploy/helm/medapp/templates/api.yaml:118-124`: the chart's read-only filesystem and `/tmp` volume
- **Fix:** start the container closer to the chart: `--read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges`. Or change the wording to "the image a deploy runs", and leave the restrictions to kind.
- **What happened to it:** fixed. Every `docker run` of the image in the `e2e` job (the bundle, `create-first-admin` and the API) has `--read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges`. The bundle unpacks into `/tmp` (`DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp`), as the chart's migrations Job sets it. The job's comment and `AGENTS.md` say it runs one copy without kind's Valkey backplane.

### 8. CI hardening: a predictable SQL Server password printed in a public log, the port open on every interface, and write permission the new jobs don't need

- **Severity:** low. **Confidence:** high on the facts; the risk is small.
- **What could go wrong:**
  - **The password:** `Ci-<run id>-<attempt>-Sql` is built from the public run ID and appears in clear in the public log: in the service container's `docker create` line and in every step's environment block. The comment says the server is the run's own, on a runner nothing outside reaches, which is true of GitHub's hosted runners today and keeps the risk low; it holds synthetic data for about three minutes.
  - **The port:** `1433:1433` publishes SQL Server on all of the runner's interfaces, where `127.0.0.1` would do, so the "nothing outside reaches it" assumption is carried by GitHub's network rules alone.
  - **Permissions:** the workflow grants `pull-requests: write` to every job, and both new jobs build and run the pull request's own code. A same-repository branch already has that access, and forks get a read-only token, so this is defence in depth.
- **Evidence:**
  - `.github/workflows/main.yml:81` and `:89`: the password; `:91`: the port; `:5-7`: the permissions
  - the `e2e` log of run 43832645185: "Initialize containers" (`-e "MSSQL_SA_PASSWORD=Ci-43832645185-1-Sql"`) and each step's `SQL_PASSWORD:` line
- **Fix:** bind the port to `127.0.0.1:1433:1433` if the runner accepts it, or start SQL Server in a step with a random password masked by `::add-mask::` (losing the service container's built-in health wait, so poll `sqlcmd` instead). Give `e2e` and `integration-tests` their own `permissions: contents: read`. Reword the comment to say the password is public and why that's acceptable.
- **What happened to it:** fixed. SQL Server starts in a step, published on `127.0.0.1:1433` only, with a random password masked by `::add-mask::` and passed to containers through the environment (by name), with a `sqlcmd` wait. `integration-tests` and `e2e` have `permissions: contents: read`. Checked with `actionlint` 1.7.7; not run in CI before the push.

### 9. A journey that fails only in CI leaves nothing to look at but the console and the API's log

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** a CI-only failure (a slower runner, a hydration race) shows a Playwright timeout message and `docker logs medapp-api`. There's no trace, screenshot or page HTML, so the cause has to be guessed and the run repeated.
- **Evidence:** `.github/workflows/main.yml:160-165` (only the API's log on failure); `MedApp.E2ETests/Common/E2ETest.cs:41-66` (no tracing).
- **Fix:** start Playwright tracing in `StartApiAsync`, save it only for a failed test, and upload it with `actions/upload-artifact` under `if: failure()` in `main.yml` only. A trace records typed values, the E2E admin's password included; in `main.yml` that password dies with the run's database, but in `e2e.yml` it's a deployment's real credential, so don't upload traces there.
- **What happened to it:** fixed. With `E2E_TRACE_DIR` set, `E2ETest` records a Playwright trace and keeps it only for a failed test. `main.yml` sets it and uploads `e2e-traces` on failure; `e2e.yml` doesn't, so no trace of a deployment's real credentials is kept.

### 10. Docs that don't match the code or each other

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** small drifts that send a later change in the wrong direction:
  - **Web test count:** `docs/architecture.md:98` and `:1153` say 677 web tests; CI ran 713 on this branch.
  - **"The gap" is now wrong:** `docs/architecture.md:1156` still says nothing tests the assembled pipeline or the authorisation attributes and nothing runs against a real database.
  - **The cache:** `AGENTS.md:253` says the `e2e` job builds "from the `image` job's cache"; the two run at the same time, so it reads what earlier runs left, and on a cold cache both build from scratch.
  - **"And delete it":** `AGENTS.md:19-20` and `.github/workflows/e2e.yml:2-4` say the journeys delete what they make. `MedApp.E2ETests/README.md:44` is accurate that audit entries and a notification stay; session rows of the deleted test accounts stay too (`UserSessions` has no foreign key to users, and deleting a user doesn't revoke them).
  - **Findings 1 and 3:** `docs/architecture.md:1248` and `:1333` lean on the matrix and on the suites guarding every pull request.
- **Fix:** correct each line; point `AGENTS.md` and `e2e.yml` at the README's list of what stays, adding session rows to it.
- **What happened to it:** fixed. Web tests are 713 in `docs/architecture.md`, with integration tests at 185; "the gap" now says what the integration tests do and don't reach; the confidentiality row and C1 are updated. `AGENTS.md` explains the `e2e` job's cache. `AGENTS.md` and `e2e.yml` point to the README's list of what stays, which now includes the session rows.

## Remaining risks

- **The reset keeps seeded data only by name:** Respawn ignores `PatientProcedureStageDefinitions` and a fix-up restores its one seeded row. A future `HasData` seed in any other table would be wiped before every test, and the suite would run against a database production never has, silently.
- **Hydration depends on a React internal:** `WaitUntilInteractiveAsync` looks for `__reactProps$` on every control (`MedApp.E2ETests/Common/E2ETest.cs:151-158`). A React upgrade that renames it makes every journey time out at once; easy to diagnose, but a whole-suite outage.
- **A fixed wait tied to the UI:** the new-user journey sleeps 1.1 seconds because the dialog's Done button ignores clicks for one second (`MedApp.E2ETests/Users/UserTests.cs:12` and `:40`, `MedApp.Web/src/pages/users/UsersPage.tsx:38`). Lengthen the hold and the journey fails with "dialog still visible".
- **Migrations are round-tripped empty:** `Migrate_DownToNothingAndUpAgain` proves every `Down` runs, not that it keeps data; a rename scaffolded as drop and create still passes.
- **A moving image tag:** CI's service container and Testcontainers both use `mssql/server:2022-latest`, so a new SQL Server build can change results between two runs of the same commit.
- **The UK midnight edge:** tests compute "today in the UK" a moment before the API does; a test that runs across midnight can see a due date of "today" refused as yesterday. The window is milliseconds.
- **The hub's live push is tested by long polling, in one process, without the backplane.** The WebSocket path is covered only by the bell journey, against one container; delivery across two copies through Valkey is still covered only by the manual check in #50.
- **The matrix is a sample:** 26 of the API's endpoints, as `docs/architecture.md:1248` says. A new endpoint isn't in it until someone adds it.
- **Coverage:** the integration tests don't feed the .NET coverage report, so `SessionService` and `IdentityTransactions` stay excluded from it, and a gap in what the integration tests reach doesn't show anywhere. `AGENTS.md` says so.

## Say it like this

"PR #65 gives MedApp the tests it was missing: 182 that run the real API against a real SQL Server, from sign-in and lockout to the procedure workflow, the audit log and live notifications, and 11 that drive the app in a real browser against the production image. Both run in CI and passed. They're well isolated and clean up after themselves. Three things need fixing before they can be fully trusted: most of the 'no roles' authorisation checks pass because of the forced password change rather than the role check they're named for; the browser suite reports success when it can't sign in, and the by-hand workflow has no credentials set, so today it would pass without testing anything; and neither new job is required, so a red one doesn't stop a merge. The rest is hardening: a fixed password for the accounts the tests create, a 404 test with nothing to fall back to, and docs that drifted."

## Detail

### The mutation runs

In a scratch copy (`git archive f755147`), outside the repository:

```diff
--- MedApp.API/Common/Extensions/ServiceExtensions.cs
-            options.AddPolicy(AuthPolicies.Staff, policy => policy.RequireRole("Admin", "User"));
+            options.AddPolicy(AuthPolicies.Staff, policy => policy.RequireAuthenticatedUser());
--- MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs
-        app.Map("/api/{**path}", () => Results.NotFound());
+        // removed
```

`dotnet test MedApp.IntegrationTests -c Release --filter "FullyQualifiedName~AuthorisationTests.StaffEndpoint_NoRoles|FullyQualifiedName~ErrorTests.ApiPath"`:

```
Passed ApiPath_MatchingNoEndpoint_Returns404NotTheApp("/api/no-such-endpoint")
Passed ApiPath_MatchingNoEndpoint_Returns404NotTheApp("/api/patients/not-a-guid")
Passed ApiPath_MatchingNoEndpoint_Returns404NotTheApp("/api/procedures/nothing/here")
Passed ApiPath_MatchingNoEndpointSignedOut_Returns404
Passed StaffEndpoint_NoRoles_Returns403(GET, /api/patients/search)
Passed StaffEndpoint_NoRoles_Returns403(GET, /api/procedures/patient-procedure-stage/definitions)
Passed StaffEndpoint_NoRoles_Returns403(GET, /api/procedures/patient-procedure-stage/templates)
Passed StaffEndpoint_NoRoles_Returns403(GET, /api/procedures/patient-procedures/search)
Passed StaffEndpoint_NoRoles_Returns403(GET, /api/notifications)
Failed StaffEndpoint_NoRoles_Returns403(GET, /api/auth/me/settings)
```

### Finding 1: testing the account the policy guards

```csharp
[TestCaseSource(nameof(StaffEndpoints))]
public async Task StaffEndpoint_RolesRemoved_Returns403(string method, string url)
{
    // Chose their own password, then lost their roles (a leaver, say): nothing but the Staff policy stops them.
    var staff = await CreateStaffAsync(UserRole);
    var admin = await SignInAsAdminAsync();
    await (await admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest([])))
        .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

    var response = await SendAsync(staff.Client, method, url);

    await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    (await response.Content.ReadAsStringAsync()).Should().NotContain(PasswordChangeRequired.Code);
}
```

### Finding 2: fail in CI, skip locally

```diff
 if (string.IsNullOrEmpty(E2ESettings.Username) || string.IsNullOrEmpty(E2ESettings.Password))
 {
-    Assert.Ignore("Set E2E_USERNAME and E2E_PASSWORD to run the end-to-end tests (see MedApp.E2ETests/README.md).");
+    const string message = "Set E2E_USERNAME and E2E_PASSWORD to run the end-to-end tests (see MedApp.E2ETests/README.md).";
+    // In CI a missing credential is a broken job, not a reason to skip: a green run must have run the journeys.
+    if (Environment.GetEnvironmentVariable("CI") == "true")
+        Assert.Fail(message);
+    Assert.Ignore(message);
 }
```

The local check, with no credentials and an unreachable base URL so nothing could be contacted:

```
$ env -u E2E_USERNAME -u E2E_PASSWORD E2E_BASE_URL=http://127.0.0.1:9 dotnet test MedApp.E2ETests -c Release --no-build
  Skipped SignIn_RightPassword_OpensTheAppAndSignOutReturnsToLogin
  Skipped SignIn_WrongPassword_SaysInvalidCredentialsAndStaysOnLogin
  ... (11 skipped)
exit=0
```

### Finding 3: the ruleset

From `GET /repos/Aliator/MedApp/rulesets/12421699`:

```json
{"type": "required_status_checks", "parameters": {"required_status_checks": [
  {"context": "web"}, {"context": "helm"}, {"context": "unit-tests"}, {"context": "image"}]}}
```

### Finding 5: a web root for the tests

A sketch (the web root could equally be set with `builder.UseWebRoot`):

```csharp
// MedAppApiFactory.ConfigureWebHost
var webRoot = Directory.CreateTempSubdirectory("medapp-it-wwwroot").FullName;
File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><html><body>stub</body></html>");
builder.UseSetting(WebHostDefaults.WebRootKey, webRoot);
```

Then `ApiPath_MatchingNoEndpoint_Returns404NotTheApp` means what it says, and a new `PagePath_AnyRoute_ReturnsTheApp` (for example `GET /patients/anything` returns the stub with `text/html`) proves the fallback is there to be avoided.

### Finding 8: what the public log shows

From the `e2e` job of run 43832645185:

```
Initialize containers   /usr/bin/docker create ... -e "MSSQL_SA_PASSWORD=Ci-43832645185-1-Sql" ... mcr.microsoft.com/mssql/server:2022-latest
Run actions/checkout@v4   SQL_PASSWORD: Ci-43832645185-1-Sql
Make the first admin      E2E_PASSWORD: ***   FirstAdmin__Password: ***
```

The connection strings in the scripts show `***` where the script says `Password=$SQL_PASSWORD`. That looks like the runner masking text shaped like `Password=…`, not the value being registered as a secret: the value itself is in clear in the lines above.

### What the end-to-end journeys leave behind

Against a shared database (locally, or through `e2e.yml`), each run leaves:
- **Audit entries** for every procedure a journey made (created, worked, cancelled, deleted). By design: the audit log has no foreign keys and outlives deletes.
- **One read notification** for the E2E account, from the bell journey, pointing at a deleted procedure.
- **Session rows** of the accounts the journeys made and deleted. Those signed out through the API are revoked; the new user's browser session isn't. None can be used: validating a session looks the user up, and they're gone.
