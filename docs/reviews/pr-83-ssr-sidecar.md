# Review: PR #83, the page server in a container of its own, out of `wwwroot`

**PR:** [#83](https://github.com/Aliator/MedApp/pull/83), branch `feature/ssr-sidecar`, reviewed at `977f4ed` on top of `main` at `5c6a0c5` (the merge of #67). `main` moved to `2ab00d4` (the merge of #68) during the review. It merges cleanly, and this review is committed on top of that merge (`e75338d`).

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, a render of the chart, a run of the built page server in a container, or the GitHub API. **No finding is high or critical;** 2 are medium and 5 are low.

## Summary

The PR moves the Node page server (the program that renders MedApp's pages on the server) out of the public web folder, adds an `Ssr:ServerUrl` setting so the API can use a page server it didn't start, and, in the Helm chart, runs that page server as a second container (`pages`) in each API pod, under its own user and with none of the API's secrets. The isolation is real and the image still works on its own as before. The weak point is what happens when the page server fails: it now takes its copy of the API out of service with it, where before the app carried on without server rendering.

## What changed, and why

34 files, 731 lines added and 152 removed.

- **Why:** the review of #51 deferred two findings. **3.2:** the page server's files sat in `wwwroot`, and on a Windows host `\`, trailing-dot and 8.3 short-name paths could still fetch them. **3.5:** Node ran in the API's container as the same user, so code running in Node could read the API's connection strings from `/proc`. `docs/architecture.md` carried them as risks S17 and S18, with R2 (Node is never restarted).
- **The files** (`MedApp.Web/package.json`, `vite.config.ts`, `MedApp.API/Dockerfile`):
  - The build copies the client build's manifest into `dist/ssr/` as `client-manifest.json`. The server entry's own build no longer writes a manifest.
  - The Dockerfile splits the build: `index.html` and `assets/` go to `/app/wwwroot` (what browsers may fetch), and `dist/ssr` (the page server, its bundled entry and the manifest) goes to `/app/ssr`. `dist/.vite` is deleted.
  - `server.mjs` reads the entry and the manifest from beside itself.
- **The API** (`MedApp.API/ServerRendering`):
  - `Ssr:ScriptPath` is now relative to the content root (`/app`), not the web root.
  - **`Ssr:ServerUrl`:** an http or https address with no path, query, fragment or user name. When it's set, the API forwards pages there and starts nothing. A value that isn't a server's address stops start-up (`ValidateOnStart`).
  - `PageServerFiles` still refuses `/ssr` and `/.vite`, as defence in depth.
  - The https redirect is unchanged: only a Node the API starts has the key that skips it. A page server of its own has none.
- **The page server** (`MedApp.Web/server/server.mjs`):
  - **`HEALTH_PORT`:** a second listener, on every interface, that answers `GET /alive` with 200 once the page listener is up.
  - **SIGTERM** (outside development): stop taking requests, finish the pages being sent, exit with code 0. As the first process in a container, Node would otherwise ignore SIGTERM.
- **The chart** (`deploy/helm/medapp`), with `api.pageServer.enabled` (on by default, and set in kind's and Azure's values):
  - **The `pages` container:** the same image, run as `node /app/ssr/server.mjs`, as user and group 1655 (the API is 1654), read-only root, no capabilities, no volumes.
  - **Its environment:** only `HOST=127.0.0.1`, `PORT=8082`, `HEALTH_PORT=8083`, `MEDAPP_API_URL=http://127.0.0.1:8080` and `NODE_ENV`.
  - **Probes:** startup, readiness and liveness, all on `/alive` on port 8083. A 5-second `preStop` sleep, as the API has.
  - **Resources:** 100m CPU and 128 MiB requested, a 512 MiB limit.
  - **The API container** gets `Ssr__ServerUrl=http://127.0.0.1:8082`. The pod gets `enableServiceLinks: false`, so neither container is handed every Service's address.
  - With the page server turned off, the API starts Node itself, as before.
- **CI:** one more `helm lint` step, with the page server turned off.
- **Tests:**
  - `SsrOptionsTests`: which `ServerUrl` values are accepted.
  - `SsrStartupTests`: the start-up check through the real `AddApi`.
  - `SsrServerTests`: the content root, an external page server, and no key for it.
  - `PageServerTests` (integration): a stand-in page server behind the full Production pipeline. It checks the nonce, the cookie, the forwarded host and `no-store`, the fallback when the page server is down, and that `/api` and `/ssr` paths never reach it.
  - `ContentSecurityPolicyTests` (E2E): seven pages rendered on the server with their own response's nonce.
- **Docs:** `AGENTS.md`, `docs/architecture.md`, the threat model and the ASVS checklist, the chart's, kind's, Azure's and the E2E suite's READMEs, the to-do list and the review index.

## Design choices

- **A second container in the API's pod, not a Deployment of its own:**
  - **Instead:** a `pages` Deployment and Service, which the API calls over the pod network.
  - **Chosen because:** the pages port stays on loopback, with no Service or network policy to add. Each page server is always the build of the API beside it, which matters because its manifest names hashed asset files that only that build's `wwwroot` has. A separate Deployment would, during a rollout, let a new API forward to an old page server.
  - **Worse at:** failure isolation. The two stand or fall together (finding 1). Network policies apply to whole pods, so Node can reach whatever the API can (the database, Valkey, DNS). Like two tenants behind one front door: separate flats, but one lock and one fire alarm.
- **The same image, with another command:**
  - **Instead:** a small Node-only image holding `/app/ssr`.
  - **Chosen because:** one build, one tag, and no way for the page server and the app's files to come from different builds.
  - **Worse at:** each container carries what it doesn't use. The `pages` container has the .NET runtime and the API's assemblies, and in Kubernetes the API container still has Node and `/app/ssr`.
- **An ordinary container, not a native sidecar** (an init container with `restartPolicy: Always`):
  - **Instead:** a native sidecar, which Kubernetes starts before the API and stops after it.
  - **Chosen because:** the pod isn't ready, and gets no traffic, until both containers are up, so start order doesn't matter. It's also simpler.
  - **Worse at:** both containers get SIGTERM at the same moment. A page arriving in the last seconds of a shutdown can find Node gone and fall back. A native sidecar wouldn't help with finding 1: its readiness counts towards the pod's too.
- **No key for the page server of its own:**
  - **Instead:** give it a key, read from a Secret by both containers.
  - **Chosen because:** the key only skips the https redirect, and behind the ingress the API has no https port. A Secret in the `pages` container would undo the point of the change.
  - **Worse at:** server rendering now depends on the API never having an https port (finding 3).
- **A health listener of its own, on every interface:**
  - **Instead:** an exec probe (`node -e "fetch(…)"`) or a TCP check.
  - **Chosen because:** the kubelet probes the pod's address, not its loopback, so the pages port itself can't be probed. An exec probe would start a whole Node process every few seconds.
  - **Worse at:** one more open port, though it answers nothing but `/alive`. It shares the page server's event loop, so it shows a stuck server. That's also why it can fail under load (finding 1).
- **Moving the files, rather than filtering paths:**
  - **Instead:** a file provider that refuses the folders (the #51 review's other option).
  - **Chosen because:** no path trick can reach a file that isn't in the web root, on any host.
  - **Worse at:** the script is found relative to the working directory. A web root filled by hand from a whole `dist/` folder now runs without server rendering, silently.
- **Failing start-up on a bad `Ssr:ServerUrl`:**
  - **Instead:** log a warning and render in the browser.
  - **Chosen because:** a typo stops the rollout (the old pods keep serving) instead of quietly turning server rendering off everywhere.
  - **Worse at:** a fresh install with a bad value doesn't start at all. The check also lets through `https`, which the page server can't serve (finding 3).

## How it was checked

- **Read:**
  - the whole diff, the PR description, and `AGENTS.md` (server rendering, the image, the chart, the security headers)
  - `MedApp.API/ServerRendering`, the pipeline (`ApplicationBuilderExtensions`, `ServiceExtensions`), `server.mjs`, and the web app's request context, API client and sign-in guard
  - the Dockerfile, the chart (`api.yaml`, `networkpolicy.yaml`, `_helpers.tpl`, `values.yaml`), kind's and Azure's values, the Argo CD application and `deploy/kind/up.sh`
  - the threat model, the ASVS checklist, the governance pack, and the #51 review's findings 3.2 and 3.5
- **Build:** `dotnet build MedApp.sln --configuration Release` before and after merging `main`: 0 errors, 1 warning (CS0414 in `MedApp.Client.Deprecated`, which the PR doesn't touch).
- **.NET tests:**
  - Unit tests: 1,152 passed at `977f4ed`, and 1,400 after merging `main`.
  - Integration tests, filtered to server rendering, security headers and pages: 9 passed. They ran against SQL Server in a Testcontainers container, removed afterwards.
- **Web:** `npm run lint` and `npm run typecheck` clean. `npm test`: 900 tests in 96 files passed.
- **The build's layout** (`npm run build`):
  - `dist/ssr` holds `server.mjs`, `entry-server.js`, `client-manifest.json` and `assets/` (the server bundle's chunks), 11 MB.
  - The rest of `dist` is `index.html`, `assets/` (3.8 MB) and `.vite`, which the Dockerfile deletes.
  - No source maps, and no public folder.
- **Helm** (v4.3.2): `helm lint --strict` in all six configurations CI uses, and `helm template` with kind's and Azure's values. The rendered Deployment was read through.
- **The page server, run as the chart runs it:** the built `dist/ssr`, mounted read-only at `/app/ssr` in `node:22-bookworm-slim` (the Node the image copies), with a read-only root, every capability dropped, `no-new-privileges`, user `1655:1655` and the chart's four variables.
  - `GET /alive` on 8083: 200. Any other path there: 404.
  - A page with no API to call: 500, so the API would fall back.
  - A stand-in API in the same network namespace that redirected every call to `https://127.0.0.1`, as `UseHttpsRedirection` does once there's an https port: the page server called `whoami` twice, with the cookie, then answered 500 (finding 3).
  - `docker stop`: exit code 0, so the SIGTERM handler ran with Node as the first process. The containers were removed.
- **CI:** run [43854156208](https://github.com/Aliator/MedApp/actions/runs/43854156208) on `977f4ed`, tested merged with `main` at `2ab00d4`, passed all eight jobs: `web`, `image`, `dependency-review`, `e2e` (18 of 18, the seven new CSP cases among them, with the API starting Node), `helm`, `integration-tests`, `unit-tests` and `coverage-comment`. Coverage: `SsrServer` and `SsrOptions` 100% of lines, `SsrPages` 96.9%.
- **Not done:**
  - Nothing ran on the kind cluster or on Azure, so what happens to pod readiness when `pages` fails comes from Kubernetes' rules, not from watching it.
  - The AppHost wasn't run. The development path was read instead: `HEALTH_PORT` is unset there, and `--dev` skips the SIGTERM handler.

## Findings

Most severe first. None is high or critical. All seven were open when this review was written; all seven were then fixed on the branch (each one's outcome is below).

### 1. A page server that stops now takes its copy of the API out of service, instead of the pages falling back

- **Severity:** medium. **Confidence:** high (Kubernetes' readiness rules and the chart's values; not observed on a cluster).
- **What could go wrong:** Kubernetes counts a pod as ready only while every container in it is ready, and a container that isn't running is never ready.
  - **With one copy** (the chart's default): the page server crashes, is killed at its 512 MiB limit, or fails its liveness probe. For the whole restart, the API pod drops out of the Service. Traefik has nowhere to send anything, so pages, `/api`, sign-in and the notifications hub all get an error from Traefik instead of the app.
  - **A crash loop** makes it minutes, not seconds: Kubernetes waits 10 seconds before the next restart, doubling up to 5 minutes. Before the PR, the same Node failure left the app working, rendered in the browser.
  - **With two copies** (kind and Azure), one failing copy is covered by the other. A failure that hits both at once takes both out:
    - a memory leak in rendering
    - a load spike that stalls Node's event loop past the probe's 1-second timeout three times running, so readiness fails on the busiest pod first, its traffic moves to the other, and that one fails next
  - **A stuck event loop** is replaced slowly: liveness fails after 60 seconds, the `preStop` hook sleeps 5, and a stuck Node can't run its SIGTERM handler, so it's killed only at the 30-second grace period. Meanwhile each page sent to that pod waits 15 seconds (`Ssr:RenderTimeout`) before falling back.
  - Like a shop that locks its doors whenever the window display breaks, although the counter inside still works.
- **Evidence:**
  - `deploy/helm/medapp/templates/api.yaml:173-191`: the `pages` container's probes. The comment assumes "another copy" takes the traffic.
  - `deploy/helm/medapp/values.yaml:15`: `replicas: 1`. `values.yaml:27`: "meanwhile, pages are rendered in the browser".
  - `deploy/helm/medapp/README.md:78`: "a page that reaches the API while the page server is down is rendered in the browser instead". With one copy, no page reaches it.
  - `docs/architecture.md:1263`: "Graceful degradation: With Node gone, pages still load."
- **Fix:**
  - Drop the `pages` readiness probe and keep startup and liveness, so a slow page server never pulls the API out. A restarting one still does; within one pod there's no way round that.
  - Give liveness `timeoutSeconds: 5` and a probe-level `terminationGracePeriodSeconds: 5`, so a stuck Node is replaced in about 70 seconds rather than about 95. The diff is under Detail.
  - Say it in `values.yaml`, the chart's README and the graceful degradation row: with one copy, a page server restart is a short outage of the whole app, and two copies avoid it.
  - If the API must stay up whatever Node does, run the page server as its own Deployment. That would need a way to keep it on the API's build during a rollout.
- **What happened to it:** fixed, further than the suggested fix. Dropping the readiness probe alone wasn't enough: a container that isn't running is never ready, so each crash, out-of-memory kill or liveness restart would still have taken the pod out, for minutes in a crash loop.
  - **`server.mjs`:** run as a service of its own (`HEALTH_PORT`), its first process now only supervises (`supervise`, with `node:cluster`). It runs the page server in a worker and starts a new one whenever the worker exits or misses its heartbeat for 15 s, after 1 s, doubling up to 30 s while it keeps failing soon after starting. It answers `/alive` itself. So the container keeps running while the renderer fails.
  - **The chart:** the `pages` container has no readiness or startup probe (a startup probe also gates readiness), and liveness has `timeoutSeconds: 5` and `terminationGracePeriodSeconds: 5`, as suggested. `values.yaml`, the chart's README and `docs/architecture.md` (graceful degradation, R2, R3) say so.
  - **Proved on kind,** in a throwaway namespace with the chart's defaults (one copy), sampling each second:
    - **The worker killed 6 times, 3 s apart:** the pod Ready and its endpoint ready throughout, `pages` never restarted, `/api/auth/whoami` 200 every time, and pages back to server rendering after each restart (backoff 1, 1, 2, 4, 8 s).
    - **The worker frozen with SIGSTOP:** replaced after 15 s of missed heartbeats. Signing in during the freeze answered 200, and a page asked for then waited 8.6 s and got the client-rendered app.
    - **The supervisor itself stopped:** the container restarted, and the pod was not ready for about 0.3 s. That's what's left, and only a supervisor that stops answering triggers it.
    - The namespace, its volumes and the image were removed afterwards.

### 2. The threat model rates code in Node as low, but leaves out what Node still holds

- **Severity:** medium. **Confidence:** high for what Node can reach (read from the code and the chart). Medium for the rating. The DNS route was reasoned from the network policy, not tried.
- **What could go wrong:** the PR rates T-23 ("Code in Node", a compromised npm package or a rendering flaw) **Low**. The only thing left, it says, is that Node can reach the database and Valkey without their passwords. That's true of the connection strings, but Node still holds what matters more.
  - **Every visitor's session:** every page request carries the visitor's session cookie, and sessions are "as good as a password" for up to 24 hours (threat model, section 1).
  - **The API on loopback:** compromised code could call the API there as any admin who opens a page. It could make an admin account, read every patient, or export records. Those calls would be logged from `127.0.0.1`, like every call made while rendering, so they wouldn't stand out.
  - **A way out:** the pod's policy allows DNS, so the results can leave as DNS lookups.
  - The sidecar doesn't change any of this. It's the same in every design where Node renders pages with the user's session.
  - In an incident, the threat model would say the blast radius is small when it's every account in use. Like moving the safe out of the shop: the takings are safer, but the till still sees every customer's card.
- **Evidence:**
  - `docs/security/threat-model.md:110` (T-23, now Low), `:60` (B3) and `:152` (section 7, item 5)
  - `MedApp.API/ServerRendering/SsrPages.cs:25` (the cookie is forwarded) and `MedApp.Web/src/api/client.ts:31` (Node sends it to the API)
  - `deploy/helm/medapp/templates/networkpolicy.yaml:29-30` (DNS egress for the whole pod)
- **Fix:**
  - Keep T-23 at medium. Add to it that a compromised page server can act as anyone who opens a page while it's compromised, admins included, and can send data out over DNS.
  - Name the controls that bear on it: the lockfile, `npm audit` in CI and Dependabot.
  - In section 7, item 5, say what would contain it further: a page server with no route out, such as a Deployment of its own whose policy allows only the API, or a DNS policy (Cilium can filter names).
- **What happened to it:** fixed. T-23 is **Medium** again. It now says what Node still holds: every visitor's session, so it can act on the API on loopback as anyone who opens a page while it's compromised, admins included, logged from `127.0.0.1`; and DNS, the database and Valkey on the pod's network, without credentials. It names the controls against a compromised package (the lockfile, `npm audit`, Dependabot, dependency review). B3 and section 7, item 5 say the same, with what would contain it further (a page server with no route out, or a DNS policy).

### 3. An https port on the API would quietly switch server rendering off in Kubernetes

- **Severity:** low. **Confidence:** high (simulated with the built page server; the sign-in loop below was reasoned, not run).
- **What could go wrong:** the `pages` container calls the API on `http://127.0.0.1:8080` with no key, so its calls pass through `UseHttpsRedirection`. Today the API has no https port in the chart, so the middleware has nowhere to redirect to and lets them through.
  - **The trigger:** set `ASPNETCORE_HTTPS_PORT` or `HTTPS_PORT` through `api.env` (the usual way to make an app behind a TLS proxy redirect to 443), or give the API an https listener for TLS inside the cluster.
  - **What follows:** every one of the page server's calls gets a 307 to `https://127.0.0.1/...`. Nothing answers there, the render fails with a 500, and the API sends the client-rendered app. That happens for every page, and the only sign is a warning per page in the API's log.
  - **The worse case:** if something did answer there with a certificate Node trusted, the cookie would be dropped on that cross-origin redirect. `whoami` would then say "signed out", and the page server would send every signed-in user to `/login`, over and over.
  - **The same with `https` in `Ssr:ServerUrl`:** the start-up check accepts it, but the page server only speaks http, so every page would fall back the same way.
- **Evidence:**
  - `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:27-31` (the redirect is skipped only with the key)
  - `MedApp.API/ServerRendering/SsrServer.cs:80-86` (the key exists only for a Node the API starts)
  - `deploy/helm/medapp/templates/api.yaml:99-101` (`api.env` can add a port)
  - `MedApp.API/ServerRendering/SsrOptions.cs:48` (https accepted) and `MedApp.Web/server/server.mjs:27,44` (http only)
- **Fix:**
  - Don't redirect a request the API received on a loopback address: add `!IPAddress.IsLoopback(context.Connection.LocalIpAddress)` beside the key check. Only something inside the pod or container can connect to 127.0.0.1, and forwarded headers don't change the local address. The alternative is to fail start-up when `Ssr:ServerUrl` and an https port are both set.
  - Accept only `http` in `ServerUrl` until the page server serves https.
  - Add an integration test: with `HTTPS_PORT` and `Ssr:ServerUrl` set, a loopback call to `/api/auth/whoami` isn't redirected.
- **What happened to it:** fixed, both ways. The API doesn't apply the https redirect to a request it received on a loopback address (`ApplicationBuilderExtensions.UseApi`, IPv4-mapped addresses included), and `Ssr:ServerUrl` accepts only `http` (`SsrOptions.ExternalServer`). New integration tests (`PageServerTests.ApiCall_HttpsPortSet_IsRedirectedOnlyIfNotReceivedOnLoopback`): with `https_port` and `Ssr:ServerUrl` set, `/api/auth/whoami` received on `127.0.0.1`, `::1` or `::ffff:127.0.0.1` isn't redirected, and received on a pod address it is (307). `SsrOptionsTests` refuses `https`. The renderer's key now only duplicates the loopback rule for a Node the API starts; it was kept.

### 4. Nothing in CI runs the page server the way the chart does

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the code that only the chart uses runs only on kind, by hand:
  - the `HEALTH_PORT` listener and the SIGTERM handler
  - the `pages` container's command, user and variables
  - `Ssr:ServerUrl` pointing at the real `server.mjs`

  CI's `e2e` job runs the image on its own, where the API starts Node. The `helm` job only lints and renders. The integration test's page server is a stand-in written in C#. So a change that breaks the sidecar passes every check: a renamed variable, a moved file, or the API's port changed in one place but not the other (`MEDAPP_API_URL`'s 8080 is typed separately from `ASPNETCORE_HTTP_PORTS`).
  - **If Node can't start:** after merge, the new pods never become ready. The rollout stalls on kind, with the old pods serving, while Publish shows green.
  - **If Node starts but can't reach the API:** every page silently falls back, and nothing fails at all.
- **Evidence:**
  - `.github/workflows/main.yml:236-256` (the API on its own) and `:301-303` (lint only)
  - `MedApp.IntegrationTests/Api/PageServerTests.cs:32-47` (the stand-in)
  - `MedApp.Web/server/server.mjs:255-277` (no test)
  - `deploy/helm/medapp/templates/api.yaml:87-88` and `:160-161` (8080 in two places)
- **Fix:**
  - In the `e2e` job, after the current run, run the pair the way the chart does: the image as `pages` (`--user 1655:1655 --read-only --cap-drop ALL`, `node /app/ssr/server.mjs` and the chart's four variables), and the API with `Ssr__ServerUrl=http://127.0.0.1:8082`, both on the host network.
  - Check `/alive` on 8083, then run `ContentSecurityPolicyTests` again. They fail on a fallback, because `index.html` has no nonce.
  - At the least, add a `node:test` for `server.mjs`'s health listener and SIGTERM.
- **What happened to it:** fixed. After its first run, CI's `e2e` job runs the image as the chart does: the API with `Ssr__ServerUrl=http://127.0.0.1:8082`, and `pages` with `--network container:medapp-api`, user `1655:1655`, read-only, no capabilities and the chart's variables. It waits for `/alive` on 8083 and a server-rendered `/login`, then runs `ContentSecurityPolicyTests` again, which fail on a fallback. Run the same way locally, the whole E2E suite passed (18 of 18). The chart now types the API's ports once (`$apiPort`, `$apiHealthPort`), for `ASPNETCORE_HTTP_PORTS`, the container ports and `MEDAPP_API_URL`. No `node:test` was added: the CI run covers the health listener, and the supervisor was proved on kind.

### 5. The DPIA and the roadmap still say the page server is in the API's container, and the sidecar is planned

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the DPIA is the record that information governance decisions rest on.
  - **Line 39:** it describes "a Node page server inside the API's container".
  - **Risk 12 (High):** part of its reason is that "Node runs in the API's container and could read its environment", which is now true only outside Kubernetes.
  - **The actions:** "move the page server into a sidecar" is still listed as planned.
  - **The roadmap:** `docs/architecture.md` still lists "Node in a sidecar" as to do in step 2.

  So the documents disagree both ways: they claim a gap this PR closed, and they leave out the risk it leaves (finding 2).
- **Evidence:** `docs/governance/dpia.md:39`, `:123` and `:286`; `docs/architecture.md:1541`; `docs/governance/data-inventory.md:61` ("with the Node page server inside", true of the pod but read as the container).
- **Fix:**
  - **DPIA, line 39:** "a Node page server, in Kubernetes a container of its own in the API's pod".
  - **DPIA, risk 12:** in Kubernetes Node no longer sees the API's environment; run on its own, it still could.
  - **DPIA, actions:** mark the sidecar done in Kubernetes, and keep hashing session tokens as planned.
  - **Roadmap:** take "Node in a sidecar" off step 2's list.
- **What happened to it:** fixed. The DPIA's line 39 and risk 12 say the page server is a container of its own in Kubernetes (and inside the API's container only when the image runs on its own), and that Node sees every visitor's session. Its actions mark the sidecar done in Kubernetes, with hashing session tokens still planned. The roadmap drops "Node in a sidecar" from step 2, and the data inventory's diagram says the page server is a container of its own.

### 6. Node's log moved to the `pages` container, and nothing says where to find it

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** before, the API copied Node's output into its own log as `SSR: …` lines, render errors and stack traces included. In Kubernetes they're now the `pages` container's own output.
  - `kubectl logs deploy/medapp-api` picks the first container, `api`.
  - The API's own warning gives only the path and the status ("Couldn't render /patients on the server (500)"), not the cause.
  - So the log shown by default has the symptom of a failed render, not its cause, which only `-c pages` shows.
- **Evidence:** `MedApp.API/ServerRendering/SsrServer.cs:135-145` (the old copying, now used only with `docker run`); `MedApp.API/ServerRendering/SsrPages.cs:114`; `MedApp.Web/server/server.mjs:205` (`console.error`); `deploy/helm/medapp/README.md` (the page server section doesn't mention logs).
- **Fix:**
  - Add the `kubectl.kubernetes.io/default-container: api` annotation to the pod template.
  - Add a line to the chart's README: Node's own log is `kubectl logs deploy/medapp-api -c pages`.
- **What happened to it:** fixed. The pod template has `kubectl.kubernetes.io/default-container: api`, and the chart's README says the page server's log is `kubectl logs deploy/medapp-api -c pages`. AGENTS.md says it too. The pod also has `azure.workload.identity/skip-containers: pages` now (from the remaining risks below), harmless until the API has a workload identity.

### 7. The Dockerfile's split lost its line breaks, and nothing checks what's left in `wwwroot`

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the step that decides what browsers can fetch is one line with runs of spaces where the backslash line continuations were: `RUN npm run build     && mkdir /out     && mv dist/ssr /out/ssr     && rm -r dist/.vite     && mv dist /out/wwwroot`.
  - It works, but it reads like a mistake, so the next edit may "fix" it wrongly.
  - Anything a future build adds to `dist/`, a new hidden folder for example, lands in `wwwroot` without a word.
- **Evidence:** `MedApp.API/Dockerfile:26`.
- **Fix:**
  - Put back the `\` continuations, one command per line.
  - End the step with a guard such as `test ! -e /out/wwwroot/ssr && test ! -e /out/wwwroot/.vite`.
  - Add a comment that everything in `/out/wwwroot` is public.
- **What happened to it:** fixed. The step is one command per line again, with a comment that everything in `/out/wwwroot` is public, and it ends with a guard: `test ! -e` for `/out/wwwroot/ssr`, `/out/wwwroot/.vite` and `/out/wwwroot/server.mjs`. The image was rebuilt with it.

## Remaining risks

- **Network policies can't tell the two containers apart.** The page server can open connections to SQL Server, Valkey and, on Azure, the SQL private endpoint, though it has no credentials for them (documented).
- **One network namespace.** Node can listen on any free port on the pod's address. If it were compromised, it could grab 8080 or 8081 while the API container restarts, so the API couldn't start again. That's a denial of service, not interception: Kestrel would exit, and the pod would stay unready.
- **Workload identity would undo the isolation on Azure** (now prevented: the pod is annotated `azure.workload.identity/skip-containers: pages`). The planned passwordless Azure SQL (`deploy/azure/README.md:127`) gives the API a federated identity. Azure's webhook puts that token into every container in the pod unless the pod is annotated `azure.workload.identity/skip-containers: pages`. Without that, the step that removes the password would hand Node the database identity.
- **Run on its own** (`docker run`, CI's `e2e` job), Node is still in the API's container as the same user (S17), and isn't restarted (R2).
- **Readiness still ignores the database and Valkey** (R3).
- **A page that stalls part-way still isn't timed out.**
- **API calls made while rendering are logged from `127.0.0.1`,** not the browser's address (documented in `HttpActivityContext`).
- **More resources:** each API pod's requests rose by 100m CPU and 128 MiB, and its limits from 1 GiB to 1.5 GiB. The API no longer shares its memory with Node, which is the point. Node hasn't been load-tested under 512 MiB.
- **Not observed on a cluster** in this review: readiness, restarts and rolling updates with the sidecar were reasoned, not watched.

## Say it like this

"The Node program that draws MedApp's pages on the server has moved out of the public web folder. In Kubernetes it now runs in a container of its own next to the API, under its own user and with none of the API's passwords. That closes the two leftovers from the #51 review: no web address can reach its files, and code running in Node can no longer read the database password. It also gets health checks, so Kubernetes restarts it if it stops. Run on its own, the image works as before. The catch is that the two containers now stand or fall together. When the page server restarts, Kubernetes takes that whole copy of the API out of service, so with a single copy the app is briefly down, where before it carried on without server rendering. The security write-up also gives the change too much credit: Node still sees every visitor's session, so a compromised Node package could still act as an admin. Smaller points: an https port on the API would quietly switch server rendering off, nothing in CI runs the page server the way Kubernetes does, and the DPIA still says the separation is only planned."

## Detail

### Why a stopped `pages` container makes the whole pod unready

The kubelet counts a container as ready only if all of these hold:
- it's running
- its startup probe has passed
- its readiness probe passes, or it has none

The pod's `Ready` condition needs every container ready. A not-ready pod is marked not ready in its EndpointSlice, and Traefik sends nothing to it. So:

| What the page server does | `pages` ready? | Pod in service? |
|---|---|---|
| Serving | Yes | Yes |
| Slow: `/alive` misses its 1-second timeout 3 times in a row | No, after about 30 s | No |
| Crashed or killed, restarting | No, until it's running and its startup probe passes | No |
| Crash loop | No, for 10 s, 20 s, 40 s … up to 5 minutes between tries | No |

Dropping the readiness probe removes only the second row. The other rows hold for any container in the pod, a native sidecar included.

### A stuck event loop, from stall to restart, with the chart as it is

| Time | What happens |
|---|---|
| 0 s | The event loop stalls. Each page sent to this pod waits 15 s (`Ssr:RenderTimeout`), then falls back. |
| About 30 s | The third readiness failure: the pod leaves the Service. |
| About 60 s | The third liveness failure: the kubelet starts stopping `pages`. |
| About 65 s | The `preStop` sleep ends, and SIGTERM is sent. The stuck loop can't run the handler. |
| About 95 s | The 30 s grace period ends, and SIGKILL. Then the restart, the startup probe, and the pod is back. |

### The probe change proposed in finding 1

```diff
           startupProbe:
             httpGet:
               path: /alive
               port: pages-health
             periodSeconds: 2
             failureThreshold: 30
-          readinessProbe:
-            httpGet:
-              path: /alive
-              port: pages-health
-            periodSeconds: 10
           livenessProbe:
             httpGet:
               path: /alive
               port: pages-health
             periodSeconds: 20
+            timeoutSeconds: 5
+            # A stuck event loop can't run the SIGTERM handler: don't wait the pod's 30 seconds for it.
+            terminationGracePeriodSeconds: 5
```

### Who can reach which port in an API pod

| Port | Listener | Bound to | Reachable by |
|---|---|---|---|
| 8080 | The API | Every interface | The ingress controller (network policy), and `pages` on loopback |
| 8081 | The API's health checks | Every interface | The kubelet, and `pages` on loopback |
| 8082 | The page server's pages | `127.0.0.1` | Only the two containers in the pod |
| 8083 | The page server's health | Every interface | The kubelet. The policy blocks everything else, and it answers only `/alive`. |

### What the `pages` container has, and doesn't

- **Its environment:** the chart's five variables, the image's own (`PATH`, `ASPNETCORE_HTTP_PORTS`, `APP_UID`, the .NET version variables), and the `KUBERNETES_SERVICE_*` pair every container gets. No connection strings, and no Service links (`enableServiceLinks: false`).
- **No Kubernetes token:** `automountServiceAccountToken: false` is set for the whole pod.
- **Its own processes:** `shareProcessNamespace` isn't set. Even if it were, another user can't read the API's `/proc/<pid>/environ`.
- **Its own files:** no volumes. The API's `/tmp`, where .NET keeps its diagnostics socket, isn't mounted in `pages`.
- **Shared with the API:** the network (loopback, the ports above, the pod's egress) and, as in every pod, the IPC namespace.

### The https redirect, simulated

The built `dist/ssr` ran in one container, as the chart runs `pages`. A second container shared its network (`--network container:`) and answered every request on `127.0.0.1:8080` with `307 Location: https://127.0.0.1<path>`. A page request for `/patients` with a synthetic session cookie gave:
- **The stand-in API's log:** `GET /api/auth/whoami __Host-medapp.session=synthetic`, twice.
- **The page server's answer:** `500`, with its error page. The API treats a 5xx as a failed render and sends `index.html`.
