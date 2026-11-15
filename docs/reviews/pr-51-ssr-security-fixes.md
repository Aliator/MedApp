# Review: PR #51, "Closed an open proxy in server-side rendering and hardened its fallback"

Branch `feature/ssr-security-fixes`, commit `c29b690`, against `main` at `6cd0aa3`. Reviewed 15 November 2026.

## 1. What the PR changes, and why

It fixes five findings from the post-SSR review:

1. **Open proxy (high).** `SsrPages` built the Node URL with `new Uri(address, Path + QueryString)`. A path such as `//evil.example/x` is a network-path reference, so the API fetched `http://evil.example/x` with the caller's cookie and served the reply from MedApp's origin. The PR fixes this in two independent ways:
   - The URL is now built with `UriBuilder`, from the renderer's own scheme, host and port plus the request's path and query.
   - `PageServerFiles.IsBlocked` returns 404 for any path containing `//`, as well as for `/ssr` and `/.vite`.
2. **Fallback under failure (high).** The Aspire service defaults gave the `ssr` client the standard resilience handler. Failed renders were retried, and once the circuit breaker opened, its `BrokenCircuitException` slipped past the catch and pages returned 500. The PR changes four things:
   - It calls `RemoveAllResilienceHandlers()` on that client.
   - The catch now takes every failure except the browser going away.
   - A 5xx from Node now falls back to the client-rendered app.
   - A failure after streaming has started now aborts the connection.
3. **Caching.** Server-rendered responses get `Cache-Control: no-store`.
4. **Node's environment** is now an allowlist, so Node no longer inherits the API's connection strings.
5. **First admin.** The first admin gets both the Admin and User roles, so they can change their own password.

## 2. How it was checked

**The PR's own checks:** the Release build has no new warnings, and 739 tests pass, 31 of them new. I counted the new tests per fixture: 14 + 12 + 3 + 1 + 1 = 31, which matches.

**What I ran:**

- **Build.** `dotnet build MedApp.sln -c Release` in the worktree, from fresh, succeeded with 1 warning. That warning is CS0414 in `MedApp.Client.Deprecated/Pages/Admin/Users/UsersTable.razor.cs:29`, which this PR doesn't touch.
- **Tests.** `dotnet test MedApp.UnitTests -c Release`: 739 passed, 0 failed, 0 skipped, in 4 s. Node 26 was on the PATH, so the real-Node tests ran.
- **End to end, against the PR's own Release build.** I ran `MedApp.API.dll` on Windows as follows:
  - in Production, on `127.0.0.1`
  - with `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, as the Helm chart sets it
  - with `Ssr:RenderTimeout` at 3 s
  - with a synthetic connection string and `FirstAdmin__Password` in its environment
  - with a scratch web root holding `index.html`, `.vite/manifest.json` and a stand-in `ssr/server.mjs`. The stand-in echoes the URL it receives, records its environment, and has endpoints that fail in different ways.

  I sent raw request targets with `curl --path-as-is --request-target`. The results:

  | Request target | API's answer | What Node received |
  |---|---|---|
  | `//evil.example/x` | 404 (blocked) | nothing |
  | `/a/..//evil.example` | 404 (Kestrel removes the dot segment, then the block applies) | nothing |
  | `/%2F%2Fevil.example/x` | 200 | `/%2F%2Fevil.example/x`, on 127.0.0.1 |
  | `/\evil.example/x` | 200 | `/%5Cevil.example/x`, on 127.0.0.1 |
  | `/%5C%5Cevil.example/x` | 200 | `/%5C%5Cevil.example/x`, on 127.0.0.1 |
  | `http://evil.example/x` (absolute form) | 400 from Kestrel (the Host header doesn't match) | nothing |
  | `/p%C3%A9ople`, `/x?q=%23frag&r=a/b` | 200 | unchanged |
  | `/api/notifications/hub/negotiate?negotiateVersion=1` | 401 (reaches SignalR) | not sent to Node |
  | `/assets/a.js`, `/assets//a.js` | 200, 404 | not sent to Node |

  In every case that reached Node, Node's own `new URL(req.url, origin)` (`server.mjs:147`) kept MedApp's origin.

  How the fallback behaved:
  - **Node answers 500:** the API sent `index.html` with status 200. HSTS was kept and no Node headers leaked.
  - **Node answers 404 or 302:** passed on as it was, with Node's `Cache-Control: public, max-age=600` replaced by `no-store`.
  - **Headers slower than the timeout:** `index.html` after 3.06 s, but without HSTS (see finding 3.1).
  - **Node closes the connection part-way through a page:** the browser got the part already sent, then a reset (curl exit 56), and the API logged "failed part-way".
  - **Node's environment** held only these names: `APPDATA`, `COMSPEC`, `HOME`, `HOST`, `LOCALAPPDATA`, `MEDAPP_API_URL`, `MEDAPP_SSR_KEY`, `NODE_ENV`, `NODE_OPTIONS`, `PATH`, `PATHEXT`, `PORT`, `ProgramData`, `SYSTEMROOT`, `TEMP`, `TMP`, `USERPROFILE` and `WINDIR`. There was no connection string and no `FirstAdmin__Password`.
- **The resilience handler, in a scratch app.** I registered `Microsoft.Extensions.Http.Resilience` 10.10.0 the way the service defaults do, against a server that always returns 500:
  - **Without the fix:** each call made 4 requests and took 6.5 to 11 s. Forty parallel calls (101 requests) opened the circuit breaker. After that, callers got `BrokenCircuitException`, which the old filter (`HttpRequestException or TaskCanceledException`) doesn't catch, so those pages became 500s.
  - **With `RemoveAllResilienceHandlers()`:** 1 request per call.
  - **Without the `#pragma`:** the call is a build error (EXTEXP0001), so the suppression is needed.

  The PR's root-cause claims hold.
- **Reading.**
  - `MedApp.Web/server/server.mjs`.
  - TanStack Router's `createRequestHandler` and `resolveRedirect`. Redirects are built as relative `publicHref`s, and protocol-relative ones are refused.
  - The authorisation on `update-password`.
  - The Helm `api.yaml` and the `Dockerfile`.
  - The test project, which has no `[Parallelizable]` or `LevelOfParallelism`, so tests run one at a time.

## 3. Findings

There are **no critical, high or medium findings.** The six below are all low.

### 3.1 `Response.Clear()` drops headers set by earlier middleware, including HSTS

**Severity:** low. **Confidence:** confirmed.

**Evidence:**
- `MedApp.API/ServerRendering/SsrPages.cs:52-54` calls `context.Response.Clear()` on the fallback path, which clears every response header, not just the ones copied from Node.
- `UseHsts()` (`MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:19`) sets `Strict-Transport-Security` on the response before calling the next middleware.

**Failure scenario:**
- In the cluster, forwarded headers make the request https, so HSTS adds its header. Then a render times out, or Node refuses the connection.
- The client-rendered app goes out without `Strict-Transport-Security`. I reproduced this: the timeout fallback had no HSTS header, while the 5xx fallback, which doesn't call `Clear()`, kept it.
- The direct impact is small, because assets and API responses still carry HSTS. But any header that a later change adds earlier in the pipeline (CSP, other security headers, a correlation ID) would also be stripped on this path only, which is hard to notice.

**Suggested fix:** undo only what `Forward` did. Take a snapshot of the response headers before calling `Forward`, and in the catch restore it and set `StatusCode = 200`, instead of calling `Clear()`. Alternatively, have `Forward` record the header names it copied, and remove just those. Add a test that sets a header before `Handle` and checks that it survives a fallback.

### 3.2 On a Windows host, `wwwroot/ssr` and `wwwroot/.vite` can still be fetched

**Severity:** low. **Confidence:** confirmed on Windows.

This is not new, but the PR rewrites the check, and AGENTS.md now says these folders "are never served".

**Evidence:**
- `MedApp.API/ServerRendering/PageServerFiles.cs:10-13` compares the request path as a string.
- `UseStaticFiles()` (`ApplicationBuilderExtensions.cs:40`) then maps the path onto the file system. On Windows, the file system treats `\` as a separator, drops trailing dots from folder names, and resolves 8.3 short names.
- The claim is at `AGENTS.md:205`.

**Failure scenario:** I ran these against the PR's Release build on Windows:
- `/ssr\server.mjs`, `/ssr%5Cserver.mjs` and `/ssr./server.mjs` returned 200 with `server.mjs`.
- `/.vite\manifest.json`, `/.vite./manifest.json` and `/VITE~1/manifest.json` returned 200 with the manifest.

On Linux (the production image) none of these should reach the files: `\` is an ordinary character in a Linux file name, and nothing strips dots or resolves short names. I reasoned that from how Linux file systems behave; I didn't run it. What's exposed is the page server's code and the asset manifest, not secrets. So this matters only if the published API runs on Windows.

**Suggested fix:**
- **Most robust:** move the page server and the manifest out of the web root, for example to `/app/ssr/` beside `wwwroot`. Point `Ssr:ScriptPath` and `server.mjs`'s `dist` lookup at that folder. Then no path can reach them through static files.
- **If they stay in `wwwroot`:** give `UseStaticFiles` a file provider that refuses anything whose resolved full path is under those folders.
- **Either way:** fix or narrow the AGENTS.md sentence.

### 3.3 A stream error in the page server kills Node, and SSR stays off until the API restarts

**Severity:** low. **Confidence:** confirmed with a stand-in. The cause is outside the diff.

**Evidence:**
- `MedApp.Web/server/server.mjs:164`, `Readable.fromWeb(response.body).pipe(res)`, has no error handling.
- `MedApp.API/ServerRendering/SsrServer.cs:116-129` doesn't restart Node. That's by design: see `ExecuteAsync_ServerExits_StaysOff` at `SsrServerProcessTests.cs:154`.

**Failure scenario:**
- If the rendered stream errors after the start of the page has gone, `pipe` raises an unhandled `'error'` event, and the whole Node process exits.
- I reproduced this with the same `Readable.fromWeb(...).pipe(res)` pattern. Node printed "Unhandled 'error' event" and exited with code 1.
- The API handled that one page correctly: it cut the page short and logged "failed part-way", so the PR's part works. But every later page in that pod was client-rendered until the API restarted, and nothing reports it, because `/health` and `/alive` don't look at SSR.
- The app keeps working. But this is the failure the PR set out to make graceful: one bad page switches SSR off for the pod.

**Suggested fix:**
- In `server.mjs`, use `pipeline(Readable.fromWeb(response.body), res, (error) => { if (error) { console.error(error); res.destroy(error); } })` from `node:stream`. Then a bad page ends only its own response, and the API sees a broken connection and aborts as it does now.
- As a follow-up, consider restarting Node with a back-off, or reporting SSR as degraded in `/health`.

### 3.4 The first-admin fix treats the symptom, not the cause

**Severity:** low. **Confidence:** confirmed by reading the code.

**Evidence:**
- `MedApp.API/Controllers/AuthController.cs:144-145` has `[Authorize(Roles = "User", Policy = "SelfUserOnly")]` on `update-password`.
- `MedApp.Infrastructure/Common/Identity/FirstAdminSetup.cs:27,76`.
- `MedApp.Application/Authentication/Roles/Commands/SetUserRoles/SetUserRolesHandler.cs:16-17`: no one can change their own roles.
- The Users page lets an admin give any combination of roles.
- `MedApp.Web/src/pages/account/AccountPage.tsx:61` offers Change Password to everyone.

**Failure scenario:**
- An admin made in the UI with only the Admin role clicks Change Password and gets a 403.
- A first admin who was created by #48 (merged today, Admin only) still can't change their own password. They can't add the User role to themselves either, so they have to create a second admin to do it.
- AGENTS.md describes staff as "either role", and every other staff endpoint uses `AuthPolicies.Staff`.

**Suggested fix:**
- Authorise the endpoint on who the caller is, not on the User role: use `[Authorize(Policy = AuthPolicies.Staff)]` together with `[Authorize(Policy = "SelfUserOnly")]`. With two attributes, both must pass.
- The `FirstAdminSetup` change can stay. It matches the dev `admin` account, which `TestAdminTools` gives both roles.

### 3.5 The allowlist keeps secrets out of Node's environment, but not out of Node's reach

**Severity:** low. **Confidence:** suspected. I didn't run the container, by the rules of this review.

**Evidence:**
- `MedApp.API/ServerRendering/SsrServer.cs:42-51,155-163`.
- `MedApp.API/Dockerfile:55,58`: one non-root user, and `dotnet` is the container's entry point.
- `deploy/helm/medapp/templates/api.yaml` puts the connection strings into the API's environment from Secrets.

**Failure scenario:**
- Node runs as the same user, in the same container, as the API. On Linux, a process can read `/proc/<pid>/environ` of another process owned by the same user.
- So code running inside Node (a flaw in rendering, or a compromised npm package) can read `/proc/1/environ` and get `ConnectionStrings__DefaultConnection`.
- The allowlist does stop accidental leaks, such as a library logging `process.env` or a crash report that includes it, and that's worth having. But AGENTS.md's "never the API's connection strings" overstates what it does.

**Suggested fix:**
- In AGENTS.md and the comment on `InheritedVariables`, say what the allowlist protects against.
- If real separation is wanted, run the page server as a sidecar container in the pod, with no secrets in its environment, calling the API over localhost.
- This PR needs no code change for it.

### 3.6 Smaller test and documentation gaps

**Severity:** low. **Confidence:** confirmed.

- **Untested URL forms.** No test covers the encoded and backslash forms (`/%2F%2Fevil.example/x`, `/\evil.example/x`). They're safe because `PathString.ToUriComponent` escapes `\` to `%5C`, which I verified end to end. But Node's `new URL()` would treat a raw `\` as `/`, so this is worth pinning with test cases on `Handle_PathNamesAnotherHost_StillAsksTheRenderer` (`SsrPagesTests.cs:263`).
- **Untested middleware order.** Nothing tests that `UseApi` puts the block before `UseStaticFiles` and the fallback, because wiring is excluded from coverage. A refactor that moved it would reopen `//ssr/server.mjs` without any test noticing. A small `WebApplication` test on port 0, like `SsrClientTests`, asking for `//ssr/server.mjs` and `//evil.example/x`, would cover it.
- **The timeout note in AGENTS.md is inaccurate.** `AGENTS.md:205` says "`Ssr:RenderTimeout` is its only timeout". The `HttpClient.Timeout` default of 100 s also applies. Neither timeout covers the body once it's streaming, so a render that stalls part-way holds the browser's request until the browser gives up. That isn't new: the old resilience handler didn't cover the body either.
- **Inline test values.** `SsrServerTests.cs:93,95` inlines `"synthetic-password"` and `"--require=/tmp/synthetic.js"`. AGENTS.md asks for such values to live in `SsrTestConstants`.
- **Missing reference.** The PR description cites `docs/reviews/flaws-review.md`, which isn't committed on any branch.

### Checked, and not findings

- **A literal `%` before two hex digits is decoded twice.** For example, `/a%25ab` reaches Node as `/a%ab`, because `PathString.ToUriComponent` keeps `%XX` sequences. The old code did exactly the same, the host can't change, and the app's paths are IDs. No action needed.
- **Host header tricks.**
  - An absolute-form target whose authority doesn't match the Host header gets a 400 from Kestrel.
  - The client's Host is passed to Node as `X-Forwarded-Host`, and only sets the origin of Node's `Request`.
  - TanStack builds redirects as relative paths and refuses protocol-relative ones, so I found no way to turn that into anything.
  - `AllowedHosts: "*"` (`appsettings.json:8`) is still worth narrowing in deployments, but that predates this PR.
- **Blocking `//` breaks nothing legitimate.** SignalR builds `/api/notifications/hub/negotiate`. Assets and API routes have no empty segments. Kestrel keeps `%2F` encoded, so it isn't turned into `//`.
- **Test flakiness.**
  - The Kestrel test server binds port 0.
  - The real-Node tests skip when Node is missing; CI's ubuntu runners have Node.
  - The process test that sets `ConnectionStrings__DefaultConnection` restores it in `finally` (`SsrServerProcessTests.cs:124`). Since the project doesn't run tests in parallel, it can't leak into another test.

## 4. What's worth keeping

- **The URL fix.** Building the target from the renderer's own scheme, host and port, with only the path and query from the request, is the right fix. It held for every form I tried: encoded slashes, backslashes, absolute form, dot segments and unicode. The extra host check after it can't fail, as the PR says, and that's fine as a second line of defence.
- **The `//` block.** As a second layer, it also closes the `//ssr/server.mjs` bypass, at no cost to real traffic.
- **Removing the resilience handler.** Replacing a failed render with the client-rendered app is cheap. Retrying cost seconds per page, and the circuit breaker turned a slow fallback into a 500. The experimental API is genuinely needed, is scoped to one call, and is pinned by `SsrClientTests`, which uses the real `AddServiceDefaults` + `AddApi` registration. (A plain `HttpClient` over `SocketsHttpHandler`, kept outside the factory, would avoid the experimental API, but I don't think that's worth the churn.)
- **The fallback rules:**
  - every failure falls back, except the browser going away
  - once the response has started, the connection is aborted
  - a 5xx falls back to the client-rendered app
  - 3xx and 4xx pass through

  Under real Kestrel there was no case where a half-written page was followed by `index.html`.
- **`Cache-Control: no-store`** on rendered pages, redirects and not-found pages is right for pages with patient data. The `index.html` fallback holds no user data, so leaving it cacheable is fine.
- **The extracted `SsrServer.StartInfo`.** It's tested both with a synthetic environment (Linux and Windows rules) and with a real Node process.

## 5. Verdict

**Nothing high or critical. Nothing blocks merging.** The open proxy is closed, in two independent ways, and the fallback now behaves under failure as described. I checked both claims against the PR's own build, not just its tests.

I'd fix 3.1 in this PR (it's small and in the new code), and correct the AGENTS.md wording behind 3.2, 3.5 and 3.6. 3.3 and 3.4 are good follow-ups.

## Resolution

**Fixed in this PR:**

- **3.1, the fallback dropping the pipeline's headers.** `SsrPages.Handle` takes a snapshot of the response headers before forwarding. On every fallback (a timeout or other exception, or a 5xx from Node), it restores exactly that snapshot and sets the status back to 200, instead of calling `Response.Clear()`. So `Strict-Transport-Security` and anything else set earlier survive, and only what Node sent is dropped. New tests in `SsrPagesTests` cover the timeout, a failure after Node's headers were copied, and the 5xx path.
- **3.3, a stream error crashing Node.** `server.mjs` now sends the page with `stream.pipeline`. An error part-way destroys only that response, which the API sees as a broken connection and aborts as before, and is logged; the process keeps running. The page server has no automated tests (it's a script that starts listening when imported), so this was checked by hand against a stand-in build: a page whose stream errors was cut short, and the next request was served. With the old `pipe`, Node exited on the same page. Restarting Node, or reporting SSR in `/health`, is still a possible follow-up.
- **3.4, admins without the User role.** `update-password` now takes `[Authorize(Policy = AuthPolicies.Staff)]` and `[Authorize(Policy = "SelfUserOnly")]`, so any staff member (either role) can change their own password, and only their own. New tests in `AuthControllerTests` evaluate the action's attributes against the API's registered policies. The first-admin change (both roles) stays.
- **3.6, inline test values.** `SsrServerTests` takes the first admin's password and the API's `NODE_OPTIONS` from `SsrTestConstants`.

**Documented, and deferred:**

- **3.2, `wwwroot/ssr` and `wwwroot/.vite` on a Windows host.** AGENTS.md now says the block holds on Linux, where the image runs, and which forms get past it on Windows. The robust fix, moving the page server out of `wwwroot`, is on the to-do list (`docs/todo.md`, Deployment).
- **3.5, the environment allowlist.** AGENTS.md and the comment on `SsrServer.InheritedVariables` now say it keeps the API's secrets out of Node's environment, so they can't leak by accident, but isn't isolation: Node could still read them from `/proc`. Real separation would need a sidecar container; not planned.
- **3.6, the timeout note.** AGENTS.md now says `Ssr:RenderTimeout` covers only getting the page started, that `HttpClient`'s default 100 s timeout applies too, and that nothing times out a page that's still streaming.
- **3.6, the untested URL forms and middleware order.** Not added in this PR.
- **3.6, the missing reference.** `docs/reviews/flaws-review.md`, cited in the PR description, arrives with PR #52.

After these changes: the Release build has no new warnings, and 747 unit tests pass (8 new). In MedApp.Web, lint, type check and Vitest (666 tests) pass.
