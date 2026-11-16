# Review: PR #67, a security baseline

**PR:** [#67](https://github.com/Aliator/MedApp/pull/67), branch `feature/security-baseline`, reviewed at `49b06a0` on top of `main` at `0c86f70` (the merge of #65). `main` has since moved to `29d2d50` (the merge of #66); it was merged into the branch for this review's index row (one conflict, in `AGENTS.md`'s Auth section, resolved by keeping both sides), and the merged branch was built and tested again.

**Reviewed:** 16 November 2026, as an adversarial security review, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, a SQL Server container, a scratch program or the GitHub API. **One finding is high:** the new `dependency-review` job fails on every pull request, because the repository's dependency graph is switched off, so a control the documents count as in place does nothing. Two findings are medium and six are low.

## Summary

The PR adds the first part of roadmap step 2: browser security headers and a Content Security Policy with a fresh nonce for every server-rendered page, a `__Host-` session cookie, sign-in throttling, sessions that end when a password, a reset or roles change (and an admin action to sign someone out everywhere), a list of common passwords to refuse, supply chain checks in CI, a STRIDE threat model and an ASVS 5.0 Level 2 checklist. The browser controls are carefully built and well tested, and the pinned actions all match their tags. The weak spots are around the edges: one CI gate can't run on this repository yet, the per-username sign-in counter can be fed enormous names until a server runs out of memory, and a role change now signs the user out, which the stated scope said wouldn't happen.

## What changed, and why

94 files, 13,353 lines added and 295 removed (`git diff --stat 0c86f70 49b06a0`); 10,000 of the added lines are the password list and 289 changed lines are the generated `schema.d.ts`.

- **Why:** roadmap step 2 in `docs/architecture.md` asks for an ASVS Level 2 baseline. This first part is scoped so that no existing feature, permission, login status or session lifetime changes.
- **Security headers** (`MedApp.API/Middleware/SecurityHeaders.cs`, first in the pipeline): `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY` and a `Permissions-Policy` on every response. Every `/api` response also gets `Cache-Control: no-store` and `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`, so an API answer opened as a page can run nothing.
- **A Content Security Policy (CSP) on pages, outside Development.** A CSP is a header that tells the browser what a page may load and run, like a guest list. A nonce is a random value printed on the page's own inline scripts and on that night's guest list, new for every response:
  - the API makes 16 random bytes per page request and puts `script-src 'self' 'nonce-…'` in the header;
  - `SsrPages` sends the nonce to the Node page server in `X-MedApp-Csp-Nonce`; the browser's own copy of that header is never forwarded (`ForwardedRequestHeaders` is a fixed list);
  - `server.mjs` accepts it only if it looks like base64, and `createSsrRouter` passes it to TanStack Router (`ssr.nonce`), which puts it on every script it writes, passes it to React's streaming, and writes a `csp-nonce` meta tag for the browser's router;
  - styles allow `'unsafe-inline'`, because the server writes `style` attributes, which a nonce can't cover;
  - `SecurityHeaders:PageCspReportOnly` switches the page policy to report-only without a new build.
- **The session cookie** is `__Host-medapp.session` outside Development: `Secure`, `Path=/`, no `Domain`, so only this host over https can set it. Only `ISessionCookieService` knows the name. Deploying it signs everyone out once.
- **Sign-in throttling** (`MedApp.API/Common/RateLimiting`), in front of Identity's lockout (5 wrong passwords, 5 minutes):
  - 30 attempts a minute per client address (an IPv6 address counts by its /64), with ASP.NET Core's rate limiter (`[EnableRateLimiting("login")]`);
  - 5 a minute per username from anywhere, with an action filter (`[ThrottleLoginByUsername]`), because the username is in the body;
  - over either, a 429 `ProblemDetails` with `Retry-After`, which the login page shows. Counts are in each API copy's memory.
- **Ending sessions** (`ISessionService.RevokeUserSessionsAsync`, `UserSessions.EndSessionsAsync`): a password change ends the user's other sessions (the session's ID is now a claim, `medapp:session_id`); an admin's reset ends all of them; a role change (`SetUserRoles`, and the deprecated `roles/assign` and `roles/revoke`) ends them; and `POST api/auth/users/{username}/sign-out-everywhere` (admin only) ends every session a user has, with a confirmed action on the Users page.
- **A common-password deny list** (`CommonPasswordValidator`): the first 10,000 lines of the NCSC's list of the passwords most used in breaches, embedded, compared ignoring case, whenever a password is set (never at sign-in).
- **Dependency audits as errors:** every `csproj` treats NuGet's NU1903 (high) and NU1904 (critical) as errors, transitive packages included, and the `web` job runs `npm audit --audit-level=high`.
- **CI supply chain:**
  - every action pinned to a full commit SHA, with its tag in a comment;
  - Dependabot (`.github/dependabot.yml`): weekly, grouped, with a 7-day cooldown, for actions, NuGet, npm and Docker;
  - CodeQL (`codeql.yml`): C# built for real, JavaScript and TypeScript, and the workflows;
  - a `dependency-review` job on pull requests;
  - Trivy scans the built image from an image pinned by digest (not `trivy-action`, whose tags were hijacked in March 2026): a SARIF report for code scanning, and a gate that fails on a critical vulnerability with a fix;
  - Publish makes an SPDX SBOM of the pushed image and attests the build provenance and the SBOM with `actions/attest`, pushed to GHCR;
  - permissions set per job.
- **Docs:** `docs/security/threat-model.md` (STRIDE), `docs/security/asvs.md` (all 253 Level 1 and 2 requirements), and updates to `AGENTS.md`, the handbook, the to-do list, the governance pack and the clinical safety file.
- **Tests:** unit tests for the headers, throttling, session ending, the deny list, the nonce hand-off and the cookie; integration tests for the headers through the real pipeline, throttling, Sign out everywhere and the password change; web tests for the nonce on every script, the 429 message and the new Users page action.

## Design choices

- **A nonce passed from the API to Node, rather than hashes or a policy written by Node:**
  - **Instead:** hash each inline script (the hydration data changes per page, so hashes can't be precomputed), or let Node write the whole header.
  - **Chosen because:** the API owns every response's headers, including the fallback to `index.html`, so one place decides the policy and Node only applies it.
  - **Worse at:** two processes must agree on every page; if the router stops applying the nonce after an upgrade, pages render but never come alive (hydration is refused), and only the E2E suite would notice.
- **`'unsafe-inline'` for styles:**
  - **Instead:** move every server-written `style` attribute into classes and drop it.
  - **Chosen because:** Astryx and the text-size setting write `style` attributes on the server.
  - **Worse at:** injected CSS isn't blocked. With `img-src`, `font-src` and `connect-src` limited to the app's own origin, CSS can't send data anywhere, so the remaining risk is defacement through an HTML injection, which React's escaping and the Markdown renderer already prevent. Acceptable, and on the to-do list.
- **No page policy in Development:**
  - **Instead:** have the dev server send the same policy, with Vite's inline scripts given the nonce.
  - **Chosen because:** Vite's client and React Fast Refresh write inline scripts of their own, and Swagger UI needs them too.
  - **Worse at:** a change that breaks the policy works locally and fails only in the production image (finding 8).
- **Throttling in each copy's memory, in the app:**
  - **Instead:** share the counts through Valkey, or rate-limit at Traefik.
  - **Chosen because:** no new moving part, and the app knows the username; Traefik doesn't.
  - **Worse at:** two copies allow twice as many attempts, every deploy resets the counts, and what the counter keeps is attacker-controlled (finding 2).
- **A per-username limit equal to the lockout count:**
  - **Instead:** a lower limit, or none (the lockout already caps guessing at 5 per 5 minutes per account).
  - **Chosen because:** it locks out no one the lockout wouldn't.
  - **Worse at:** it adds little against guessing that the lockout doesn't already stop, and keeps the lockout's flaw: anyone can block a known username's sign-in (ASVS 6.1.1, which the checklist records honestly).
- **Ending sessions in each handler, rather than a stamp checked on every request:**
  - **Instead:** keep Identity's security stamp on the session and refuse a session whose stamp no longer matches the user's. Identity changes the stamp itself on a password change or reset, in the same save, so every path is covered at once.
  - **Chosen because:** no migration, and each handler states what it ends.
  - **Worse at:** every new account action must remember to call it, and the change and the ending are two saves (finding 5).
- **The NCSC's top 10,000, embedded:**
  - **Instead:** Have I Been Pwned's range API (a network call per password set), or the whole 100,000.
  - **Chosen because:** offline, fast, no new egress through the network policies, and ASVS 6.2.4 asks for at least 3,000.
  - **Worse at:** only 2,223 of the 10,000 could ever pass today's composition rules (8 characters or more, with a letter and a digit, and a capital added); the composition rules already refuse the other 7,777, so the list does less than its size suggests until those rules go (step 2). Context words such as "medapp" or the username aren't on it.
- **Trivy run with `docker run` from a digest:**
  - **Instead:** `aquasecurity/trivy-action` pinned by SHA.
  - **Chosen because:** the action's tags and Trivy's images were hijacked in March 2026; a digest can't be moved, and the scan reads a tarball, never the Docker socket.
  - **Worse at:** Dependabot can't update it, so it's bumped by hand.
- **NuGet audit in every `csproj`, not a `Directory.Build.props`:**
  - **Chosen because:** the Dockerfile copies only the `csproj` files before restoring.
  - **Worse at:** twelve copies of the same block; a new project must remember it.

## How it was checked

- **Read:** the whole diff, the PR description, `threat-model.md`, `asvs.md`, `AGENTS.md`'s Auth and CI sections, and the code around the changes: the session handler and service, Identity's user and role services, the login path down to `UserManager.FindByNameAsync`, `SsrPages`, `server.mjs`, TanStack Router's stream renderer (`renderRouterToStream.js`, which passes `ssr.nonce` to React), the notifications hub, the session timeout component, the chart's network policies and Traefik's values for kind and Azure.
- **.NET, at `49b06a0`:** `dotnet build MedApp.sln -c Release`, 0 errors and 1 warning, in the deprecated Blazor client (`UsersTable.razor.cs:29`), which this PR doesn't touch. `dotnet test MedApp.UnitTests -c Release`: 1,024 passed. `dotnet test MedApp.IntegrationTests -c Release`, filtered to sign-in, throttling, session ending, headers, users and the forced password change: 55 passed, against SQL Server in Docker.
- **Web, at `49b06a0`:** `npm run lint` and `npm run typecheck` clean; `npm test`: 840 passed; `npm audit --audit-level=high` passes (7 moderate); `npm run build` succeeds, and the built `index.html` (the fallback page) has one external module script and no inline script.
- **After merging `main` (`29d2d50`, as `73cc2c3`):** the build is unchanged (0 errors, the same warning); 1,109 unit tests and all 203 integration tests passed; after `npm ci` (for #66's Recharts), lint and typecheck are clean and 900 web tests passed. The merge brings no new workflow or action.
- **CI on `49b06a0`:** run [43841531538](https://github.com/Aliator/MedApp/actions/runs/43841531538): `web`, `image` (with both Trivy steps and the SARIF upload), `unit-tests`, `integration-tests`, `e2e`, `helm` and `coverage-comment` passed; `dependency-review` failed (finding 1). CodeQL run [43841531553](https://github.com/Aliator/MedApp/actions/runs/43841531553): C#, JavaScript and TypeScript, and actions all passed. The `e2e` job ran the production image (so the page policy was enforced) over `http://localhost:8080` in Chromium, which accepted the `__Host-` cookie there.
- **The pinned actions:** all 15 SHAs resolved from their tags through the GitHub API (annotated tags dereferenced) and every one matched. Trivy's digest matches `docker buildx imagetools inspect aquasec/trivy:0.76.2`, and v0.76.2 is an immutable GitHub release. `actions/attest` at its pinned SHA has the `create-storage-record` input the workflow sets. `actionlint` 1.7.7 (in Docker): one style note (SC2129), in the coverage comment step, which predates this PR.
- **Repository settings, through the GitHub API:** the dependency graph, Dependabot alerts and Dependabot security updates are off; CodeQL's default setup is off (so this advanced setup can upload); `main`'s required checks are `web`, `helm`, `unit-tests`, `image`, `integration-tests` and `e2e`.
- **The deny list's source:** downloaded the SecLists file at the commit `NOTICE.txt` names. Its SHA-256 matches the notice, and its first 10,000 lines are identical to `common-passwords.txt`.
- **A SQL Server probe** (a throwaway SQL Server 2022 container, default collation `SQL_Latin1_General_CP1_CI_AS`, removed afterwards): which usernames the database treats as `ADMIN` (finding 4).
- **A scratch program** (outside the repository) built the same partitioned limiter as `LoginUsernameLimiter` and measured the memory it keeps for long names (finding 2).
- **Not done:** no browser was opened by hand, nothing was deployed to kind or Azure, the Publish workflow (SBOM and attestations) can't run until the merge, and Dependabot's config can't be validated by GitHub until it's on `main`.

## Findings

Most severe first. One is high, two are medium and six are low. All nine were open when this review was written. Line numbers are the merged branch's (`73cc2c3`); only `docs/architecture.md`'s differ from `49b06a0`, by one.

### 1. `dependency-review` fails on every pull request: the repository's dependency graph is off

- **Severity:** high. **Confidence:** high.
- **What could go wrong:** the job ends in "Dependency review is not supported on this repository. Please ensure that Dependency graph is enabled". It fails on this PR, and will fail on every pull request after the merge, so every workflow run is red. The merge flow in `AGENTS.md` stops on a failed run, and once a red job is normal, a real failure in the same run is easy to miss. Meanwhile the control the documents count as in place does nothing: a pull request that adds a package with a critical advisory passes. The same setting being off means Dependabot alerts and security updates are off too, so the claim in `dependabot.yml` that security updates "come as soon as there's an advisory" isn't true either. Like a smoke alarm fitted without its battery: it's on the ceiling, and on the inspection form.
- **Evidence:**
  - `.github/workflows/main.yml:124-137`: the job
  - run 43841531538, job `dependency-review`: the error above, at `actions/dependency-review-action@a1d282b…`
  - `gh api repos/Aliator/MedApp`: `dependabot_security_updates: disabled`; `gh api repos/Aliator/MedApp/vulnerability-alerts`: 404 (alerts off)
  - `.github/dependabot.yml:1-4`; `docs/security/threat-model.md:130` (T-41); `docs/security/asvs.md:422` (15.2.1); `docs/governance/security-policies.md:59` ("In place")
- **Fix:** in the repository's Settings, Advanced Security: turn on the dependency graph, Dependabot alerts and Dependabot security updates (free for a public repository), then re-run the job. Record the three settings beside the required checks in `docs/todo.md`'s "Repository settings" line, so a new fork or a restored repository gets them. Consider making `dependency-review` a required check once it's green.
- **What happened to it:** open.

### 2. The per-username counter keeps every name it's given, of any length, for about a minute

- **Severity:** medium. **Confidence:** high on the mechanism (measured); the end-to-end crash wasn't run against the API.
- **What could go wrong:** the action filter takes the username from the request body before anything checks its length, and `LoginUsernameLimiter` keeps an upper-cased copy as the key of a fixed-window limiter. The limiter is only dropped once its window has ended and it has been idle for 10 more seconds, so each name is held for about 70 seconds. Kestrel accepts bodies up to 30 MB, and the per-address limit lets 30 sign-ins a minute through. One client sending 30 sign-ins a minute, each with a 10-million-character username, keeps about 600 MB alive; with 29-million-character names it keeps about 1.7 GB, past the API pod's 1 GiB limit. The pod is killed for memory, restarts, and the attack repeats; with two addresses, both copies go down. Before this PR the same requests cost memory only while they ran. Like a doorman who writes every refused name on a pad with no page limit, and keeps the pad for a minute.
- **Evidence:**
  - `MedApp.API/Common/RateLimiting/ThrottleLoginByUsernameAttribute.cs:18` and `:26`: the name from the body, straight to the limiter
  - `MedApp.API/Common/RateLimiting/LoginUsernameLimiter.cs:30`: `_limiter.AttemptAcquire(username.Trim().ToUpperInvariant())`
  - `MedApp.Contracts/Authentication/Requests/LoginRequest.cs`: no length limit; `MedApp.Application/Authentication/Sessions/Commands/Login/LoginValidator.cs:10-12`: only `NotEmpty`, and it runs after the filter
  - `MedApp.API/Common/RateLimiting/LoginThrottlingOptions.cs:15`: 30 a minute per address
  - `deploy/helm/medapp/values.yaml:20-21`: the API's 1 GiB memory limit
  - measured (Detail): 30 keys of 10,000,000 characters kept 610 MB after a full garbage collection, and 572 MB 15 seconds later
- **Fix:** bound it in three places (diff under Detail): `[RequestSizeLimit(16 * 1024)]` on `AuthController.Login`, so a huge body is refused before it's read; `MaximumLength(256)` on `LoginValidator`'s username (the column's length); and in the filter, leave a name over 256 characters to validation instead of counting it. Add a test that a 257-character name isn't kept by the limiter.
- **What happened to it:** open.

### 3. Changing someone's roles now signs them out, which the stated scope said wouldn't change

- **Severity:** medium. **Confidence:** high that the behaviour changed; medium that it's unwanted (the PR description lists it, but also says no login status changes).
- **What could go wrong:** before this PR, a role change took effect on the user's next request: roles are read from the database on every request. Now, when an admin ticks Admin for a nurse in Edit Roles, every session the nurse has ends. Their next save shows "Your session has ended. Please sign in again." and they sign in again mid-shift. The Edit Roles dialog doesn't say this will happen, while the new Sign out everywhere dialog does. The security gain is small: roles already apply at once, and ending a session doesn't close its live notifications connection, which is the one thing that keeps old access (T-14). It also isn't applied consistently: deleting a role (`DELETE api/auth/roles/{roleName}`) removes it from everyone who had it and ends nothing. The integration tests were changed to match: `UserTests` used to say "the session picks the new roles up on its next request".
- **Evidence:**
  - `MedApp.Application/Authentication/Roles/Commands/SetUserRoles/SetUserRolesHandler.cs:28-29`; `AssignRole/AssignRoleHandler.cs:24-25`; `RevokeRole/RevokeRoleHandler.cs:24-25`
  - `MedApp.Infrastructure/Common/Authentication/SessionService.cs:49`: roles read on every request
  - `MedApp.Web/src/pages/users/UsersPage.tsx:250-252`: the Edit Roles dialog's text
  - `MedApp.IntegrationTests/Auth/UserTests.cs:82-87`: the changed test
  - `MedApp.Application/Authentication/Roles/Commands/DeleteRole/DeleteRoleHandler.cs`: no session ending
- **Fix:** a decision either way:
  - **keep the old behaviour** (preferred, given the scope): remove the three `EndSessions`/`RevokeUserSessions` calls from the role handlers and restore `UserTests`' "picks the new roles up on its next request". Password changes, resets and Sign out everywhere keep ending sessions; those are what ASVS 7.4.3 and 7.4.5 ask for.
  - **or keep it as a deliberate exception:** say so in the PR's scope statement and in the hazard log, add "Saving signs them out everywhere if their roles change" to the Edit Roles dialog, and end sessions on `DeleteRole` too.
- **What happened to it:** open.

### 4. The per-username limit can be dodged with characters the database ignores

- **Severity:** low. **Confidence:** high (measured on SQL Server 2022).
- **What could go wrong:** the limiter keys on `Trim().ToUpperInvariant()`, and its comment says usernames are compared "as Identity compares them". But Identity's lookup ends in SQL Server, whose default collation treats more names as equal: `ADMIN` with a byte order mark (U+FEFF) in front, with a NUL after it, or with full-width letters (`ＡDMIN`) all find the `admin` row, while .NET's `Trim` keeps U+FEFF and NUL. So `\uFEFFadmin`, `\uFEFF\uFEFFadmin`, `admin\u0000` and so on each get a fresh allowance of 5, while every wrong guess still counts against the real account. Today the lockout (5 wrong, 5 minutes) still caps guessing per account, so the practical effect is small; it's the documents' "5 a minute per username from anywhere" that overstates. Like a doorman who counts "Smith" and "Smith with an invisible dot" as two names, when the records office treats them as one.
- **Evidence:**
  - `MedApp.API/Common/RateLimiting/LoginUsernameLimiter.cs:9-10` and `:30`
  - `MedApp.Infrastructure/Common/Authentication/IdentityAuthenticationService.cs:21`: `FindByNameAsync(username)` with the name as sent
  - the probe (Detail): `NCHAR(65279) + N'ADMIN'`, `N'ADMIN' + NCHAR(0)` and `NCHAR(65313) + N'DMIN'` all equal `N'ADMIN'`; zero-width space, zero-width non-joiner and soft hyphen don't
- **Fix:** treat a username with any character outside Identity's `User.AllowedUserNameCharacters` (letters, digits and `-._@+`, which every account already meets) as unknown, with the usual 401, before the lookup in `IdentityAuthenticationService` and before the limiter counts it, so these variants never reach the database and the login's answers don't change. At the least, normalise the key the way the database compares: `Normalize(NormalizationForm.FormKC)`, then drop control and format characters (`UnicodeCategory.Control`, `UnicodeCategory.Format`), then upper-case.
- **What happened to it:** open.

### 5. Changing an account and ending its sessions are two saves

- **Severity:** low. **Confidence:** high on the code; a failure between the two saves is unlikely.
- **What could go wrong:** each handler saves the change, then ends the sessions in a second `SaveChangesAsync`. If the second fails (a database blip, a timeout), the request becomes a 500 after the change was saved. For a reset, the new password is set but never shown, so the admin can't pass it on and resets again; the user's old sessions also stay alive. For a password change, the page says it failed though it didn't, and the user's next sign-in with the old password is refused. The PR records this honestly in ASVS 2.3.3, but not in `docs/todo.md`.
- **Evidence:**
  - `MedApp.Application/Authentication/Users/Commands/ResetUserPassword/ResetUserPasswordHandler.cs:18-25`
  - `MedApp.Application/Authentication/Users/Commands/UpdateUserPassword/UpdateUserPasswordHandler.cs:18-26`
  - `MedApp.Application/Authentication/Roles/Commands/SetUserRoles/SetUserRolesHandler.cs:24-29`
  - `MedApp.Infrastructure/Common/Authentication/SessionService.cs:101-122`
  - `docs/security/asvs.md:120` (2.3.3)
- **Fix:** run both steps in one transaction with `IIdentityTransactions.RunAsync` (both use the request's `MedAppDbContext`), or, for the reset, catch and log a failure to end sessions and still return the password, so the admin can use Sign out everywhere. Add it to `docs/todo.md` if it's left.
- **What happened to it:** documented in the PR (ASVS 2.3.3); not on the to-do list.

### 6. Three new CI gates can turn every pull request, and Publish, red overnight, with no documented way through

- **Severity:** low. **Confidence:** high on the behaviour; when it happens depends on advisories.
- **What could go wrong:** a high advisory published tonight against any NuGet package, direct or transitive, fails every build tomorrow, including the Dockerfile's restore, so Publish can't deploy even an urgent fix. `npm audit --audit-level=high` fails on a high advisory even when no fixed version exists yet, in a dev dependency too. Trivy's gate fails when Debian has fixed a critical before Microsoft has rebuilt the `aspnet` image. Each is the point of the gate, but nothing says how to accept a known issue for a while, with a review date, so the first time it happens the quickest way through is to switch the gate off.
- **Evidence:**
  - `MedApp.API/MedApp.API.csproj:3-9` and the same block in every `csproj`
  - `.github/workflows/main.yml:28-31` (`npm audit`) and `:107-115` (Trivy's gate)
  - `AGENTS.md`, CI: "a newly published advisory fails the build until the package is updated"
- **Fix:** document the escape hatches in `AGENTS.md`'s supply chain rules: `<NuGetAuditSuppress Include="<advisory URL>" />` in the one affected `csproj`, with a comment and a date; an npm `overrides` entry or a documented exception; and a `.trivyignore` entry with `exp:<date>`. Each one should name the to-do it expires with.
- **What happened to it:** open.

### 7. A whole site behind one address shares 30 sign-ins a minute, and refusals leave no trace

- **Severity:** low. **Confidence:** medium (depends on how a customer's network looks).
- **What could go wrong:** hospitals often reach the internet through one address (NAT, or the HSCN's proxies). At a shift change, a few hundred staff sign in within minutes; with typing mistakes counted too, 30 attempts a minute per copy (60 with two) is reachable, and the rest see "Too many sign-in attempts" for up to a minute. Anyone else behind the same address can also use up the allowance on purpose. Neither the rate limiter's `OnRejected` nor the filter logs anything, so nothing shows that staff are being throttled, or from which address.
- **Evidence:**
  - `MedApp.API/Common/RateLimiting/LoginThrottlingOptions.cs:12-15`: the comment expects shared addresses, but the default is 30
  - `MedApp.API/Common/RateLimiting/LoginThrottling.cs:36` and `:97-105`: the refusal, not logged
  - `MedApp.API/Common/RateLimiting/ThrottleLoginByUsernameAttribute.cs:33-40`: the same
- **Fix:** log each refusal at Warning with which limit refused it and the client key (never the username), so it's visible and alertable; and add sizing guidance where the setting is documented (for example, raise `LoginThrottling:PerClientPermitLimit` for a site with many staff behind one address), with the step to take if staff report the message.
- **What happened to it:** open.

### 8. The tests don't pin some of what the security rests on

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the code is right today, but nothing would catch these going wrong:
  - **a browser's own nonce header:** `Handle_NoNonce_SendsNone` sends no `X-MedApp-Csp-Nonce` at all, so a change that forwarded the browser's copy would pass;
  - **a policy violation in a real browser:** Development has no page policy, and the E2E suite (which runs the production image, with the policy on) checks behaviour, not the console, so a blocked script that doesn't break a journey ships unnoticed;
  - **the client address behind Traefik:** nothing tests that the per-address key comes from the forwarded headers rather than Traefik's address;
  - **usernames:** only case and spaces are tested, not length or the characters in finding 4.
- **Evidence:**
  - `MedApp.UnitTests/Api/ServerRendering/SsrPagesTests.cs:114-120`
  - `MedApp.E2ETests` (no `securitypolicyviolation` or console check anywhere in it)
  - `MedApp.UnitTests/Api/RateLimiting/LoginThrottlingTests.cs:108-116` (`ClientKey` only) and `:171-185`
- **Fix:** add a browser-sent `X-MedApp-Csp-Nonce` to `Handle_NoNonce_SendsNone` and assert it isn't forwarded; in the E2E base fixture, listen for `securitypolicyviolation` events (or CSP console errors) on every page and fail the test on one; add an integration test with `ForwardedHeaders` on and an `X-Forwarded-For` header; add the long-name and variant cases with findings 2 and 4.
- **What happened to it:** open.

### 9. Docs: stale rows, now better after #65, and a few slips

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the checklist and threat model are the evidence for a DSPT submission and a penetration test. Where they're out of date, they either undersell or oversell:
  - **Now better than the documents say** (the checklist predates #65's integration tests): `asvs.md:270` (8.2.1, "only two endpoints' attributes are tested"), `:268` (8.1.1), `:483` (gap 6, "tests for only two endpoints"), `threat-model.md:99` (T-13, "most endpoints' attributes aren't tested") and `:153`, and the handbook's S7. `AuthorisationTests` now checks 27 admin and staff endpoints for each role and for signed-out requests. And `asvs.md:35` ("nothing was run for this checklist") is now backed by integration tests for the headers, cookie flags, throttling and session ending (`SecurityHeadersTests`, `SignInTests`, `LoginThrottlingTests`, `SessionEndingTests`); those rows could cite them.
  - **Oversold:** dependency review and Dependabot security updates (finding 1); "5 a minute per username" (finding 4).
  - **Slips:** `docs/architecture.md:644` and `:656` still name the cookie `medapp.session`, and `:657` says a role change or a reset "applies at once" without saying either now ends sessions; `asvs.md:36` says nine pages were checked in Chromium, the PR description eleven; `asvs.md:270` lists sign-in, sign-out and who-am-I as the only actions without a role, but the session endpoints from #63 are `[Authorize]` only too.
- **Evidence:** as listed.
- **Fix:** update those rows: cite `AuthorisationTests` and the new integration tests, lower T-13's rating, and correct the slips.
- **What happened to it:** open.

## Remaining risks

- **Open live connections:** ending a session doesn't close its notifications WebSocket, which keeps receiving procedure titles until the page reloads (T-14, ASVS 7.4.1). Documented, and on the to-do list.
- **Counts per copy:** two copies allow twice as many attempts, and every deploy (each merge) starts the counts again. Sharing them through Valkey is on the to-do list.
- **Malicious lockout:** anyone can still block a known username's sign-in for 5 minutes, through the lockout or the per-username limit (ASVS 6.1.1).
- **Address trust:** the per-address limit is only as good as Traefik's address. On Azure that holds (`externalTrafficPolicy: Local`); on kind every browser arrives from one address; a proxy put in front of Traefik later (a gateway or a CDN) would make every user one address unless the forwarded-header settings change with it. An IPv6 client with a /56 gets 256 allowances.
- **The deploy itself:** the cookie rename signs everyone out once, and during the rollout the old and new copies read different cookies for a minute or so.
- **Publish is unproven** until the first merge: the SBOM and the two attestations run only on `main`. If Sigstore or the attestation API is down, the image job fails and nothing deploys (failing closed, but blocking). Nothing checks the attestations before deploying yet (T-43).
- **The CI token:** `pull-requests: write` is still granted to the whole PR workflow, including the jobs that run `npm ci` and the image build (T-40, on the to-do list), against `AGENTS.md`'s new rule to set permissions per job.
- **Base images by tag,** and the CSP's report-only switch has no reporting endpoint, so turning it on hides problems as well as fixing them.
- **No audit** of Sign out everywhere or of the sessions a change ends (T-15).

## Say it like this

"This change puts a standard set of locks on MedApp without changing how it's used day to day. Browsers are told to run only what MedApp wrote itself, the sign-in cookie can only come from MedApp's own address over https, repeated sign-in attempts are slowed down, sessions end when a password is changed or reset, and an admin can sign someone out everywhere. The build now checks every dependency and the finished image for known flaws, and records how each image was made. Three things need attention before relying on it: one of the new checks can't run until a repository setting is switched on, so today it fails every pull request and protects nothing; the new per-name sign-in counter can be fed enormous names until a server runs out of memory; and changing someone's roles now signs them out, which is a change in behaviour the scope said it wouldn't make."

## Detail

### Finding 1: the job's error and the settings

```
##[error]Dependency review is not supported on this repository. Please ensure that Dependency graph is enabled,
see https://github.com/Aliator/MedApp/settings/security_analysis
```

`gh api repos/Aliator/MedApp --jq .security_and_analysis` returned `dependabot_security_updates: disabled` (secret scanning and push protection are on). `GET /repos/Aliator/MedApp/vulnerability-alerts` returned 404, which means Dependabot alerts are off. `GET /repos/Aliator/MedApp/code-scanning/default-setup` returned `not-configured`, which this PR needs (an advanced CodeQL setup can't upload while the default one is on).

### Finding 2: the measurement

A console program (net10.0, the ASP.NET Core shared framework) built the limiter exactly as `LoginUsernameLimiter` does, then made 30 attempts, each with a different 10,000,000-character name, keeping no other reference to the names:

```csharp
var limiter = PartitionedRateLimiter.Create<string, string>(username =>
    RateLimitPartition.GetFixedWindowLimiter(username, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true,
    }));
for (var i = 0; i < 30; i++)
{
    var username = new string((char)('a' + i % 26), 10_000_000 - 1) + i;
    using var lease = limiter.AttemptAcquire(username.Trim().ToUpperInvariant());
}
```

```
before: 0 MB
after 30 attempts, nothing else referencing the names: 610 MB
15 s later (inside the window): 572 MB
```

The fix, in outline:

```diff
 // AuthController.cs
     [HttpPost("login")]
     [Tags("Login")]
+    [RequestSizeLimit(16 * 1024)]
     [EnableRateLimiting(LoginThrottling.Policy)]
     [ThrottleLoginByUsername]

 // LoginValidator.cs
         RuleFor(x => x.Username)
             .NotEmpty()
-            .WithMessage("Username must not be empty.");
+            .WithMessage("Username must not be empty.")
+            .MaximumLength(256)
+            .WithMessage("Username must not exceed 256 characters.");

 // ThrottleLoginByUsernameAttribute.cs
+    /// <summary>Longer than any account's name (Identity's column): left to validation, never counted.</summary>
+    private const int MaxUsernameLength = 256;
+
     public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
     {
         var username = context.ActionArguments.Values.OfType<LoginRequest>().FirstOrDefault()?.Username;
-        if (string.IsNullOrWhiteSpace(username))
+        if (string.IsNullOrWhiteSpace(username) || username.Length > MaxUsernameLength)
         {
             await next();
             return;
         }
```

### Finding 4: the probe

In a throwaway `mcr.microsoft.com/mssql/server:2022-latest` container (server collation `SQL_Latin1_General_CP1_CI_AS`, the default here and on Azure SQL), a table with one `nvarchar(256)` row, `N'ADMIN'`:

| Compared with `N'ADMIN'` | Equal? |
|---|---|
| `NCHAR(65279) + N'ADMIN'` (byte order mark first) | Yes |
| `N'ADMIN' + NCHAR(0)` (NUL last) | Yes |
| `NCHAR(65313) + N'DMIN'` (full-width A) | Yes |
| `N'ADMIN   '` (trailing spaces; the limiter trims these) | Yes |
| `N'ADMIN' + NCHAR(8203)` (zero-width space) | No |
| `N'ADMIN' + NCHAR(8204)` (zero-width non-joiner) | No |
| `N'AD' + NCHAR(173) + N'MIN'` (soft hyphen) | No |

In .NET, `char.IsWhiteSpace('\uFEFF')` is false, `"\uFEFFadmin\0".Trim()` keeps both characters, and `"ａdmin".ToUpperInvariant()` is `"ＡDMIN"`.

### The pinned actions

| Action | Tag | SHA (first 12) | Matches |
|---|---|---|---|
| `actions/checkout` | v4.4.0 | `11d5960a3267` | Yes |
| `actions/setup-dotnet` | v4.3.1 | `67a3573c9a98` | Yes |
| `actions/setup-node` | v4.4.0 | `49933ea5288c` | Yes |
| `actions/upload-artifact` | v4.6.2 | `ea165f8d65b6` | Yes |
| `actions/download-artifact` | v4.3.0 | `d3f86a106a0b` | Yes |
| `actions/dependency-review-action` | v5.0.0 | `a1d282b36b6f` | Yes |
| `actions/attest` | v4.2.2 | `1e69f48acb82` | Yes |
| `docker/setup-buildx-action` | v3.12.0 | `8d2750c68a42` | Yes |
| `docker/build-push-action` | v6.19.2 | `10e90e3645ea` | Yes |
| `docker/login-action` | v3.7.0 | `c94ce9fb4685` | Yes |
| `github/codeql-action` | v4.39.3 | `33d7b85da1a0` | Yes |
| `anchore/sbom-action` | v0.24.3 | `66cbf4bc1f1c` | Yes |
| `azure/login` | v2.3.1 | `7184910d9eb2` | Yes |
| `marocchino/sticky-pull-request-comment` | v2.9.4 | `773744901bac` | Yes |
| `danielpalme/ReportGenerator-GitHub-Action` | v5.6.0 | `5f3701a41d14` | Yes |

### Questions asked of the PR, and the answers

- **Can the nonce leak or be reused?** It's 128 random bits, new for every response, and pages and `/api` answers are `no-store`. It's visible in the page (the meta tag and script attributes), as with any nonce; reading it needs script already running, and CSS can't send it anywhere, since images, fonts and connections are limited to the app's own origin. No reuse path found.
- **Is `X-MedApp-Csp-Nonce` trusted only from the API?** The API never forwards the browser's copy (a fixed list of forwarded headers), and in production Node listens only on loopback. In development a browser can send its own, which affects only its own page, and there's no policy there. `server.mjs` refuses anything that isn't base64, so it can't break out of the attribute.
- **What if the page server or the router fails to apply it?** If Node fails before sending anything, the API sends `index.html`, which has no inline script (checked in the build), so the policy doesn't get in the way. If the router stopped writing the nonce, the page would render but not come alive; the web test that checks every script carries it, and the E2E suite on the production image, would catch that before a merge.
- **Is `'unsafe-inline'` for styles acceptable?** Yes, for now (Design choices): nothing can be sent out through CSS, and no HTML injection point was found.
- **Does it work in Development and with Vite?** Yes: no page policy there, the nonce is simply absent, and the cookie keeps its old name over plain http. That's also why a violation would first show in production (finding 8).
- **The cookie rename:** everyone is signed out once; nothing outside `ISessionCookieService` reads the cookie (searched the code, tests, tools and the page server; only the handbook's section 6.1 still names the old cookie, finding 9). The `__Host-` rules hold: `Secure`, `Path=/`, no `Domain`, tested in `SignInTests`. Chromium keeps it over `http://localhost`, as the `e2e` job shows.
- **Header spoofing to dodge the per-address limit:** ASP.NET Core takes only the last `X-Forwarded-For` entry (the one Traefik writes), and the network policy lets only the ingress namespace reach the API's port. Not bypassable from outside; from inside the cluster only by something already in the ingress namespace (T-46).
- **Is the per-username key normalised?** For case and spaces, yes; not for the characters SQL Server ignores (finding 4), and not for length (finding 2).
- **Is per-copy memory a real weakness?** A modest one: twice the attempts with two copies, and counts reset on every deploy. The lockout, which is in the database, still caps guessing per account.
- **Can the per-username limit be used to lock someone out?** Yes, as the lockout already can: 5 requests a minute at a known name keep it refused. It adds no new way to do it.
- **Does every path that should end sessions do so?** Password change, reset, Sign out everywhere and the three role commands do; deleting a user makes their sessions fail. Not ended: a lockout (documented), and deleting a role (finding 3).
- **Does it ever end the wrong ones?** No. Each call is by the target's user ID. Only the password change and the deprecated assign and revoke commands keep the caller's own session, and an admin's session ID never matches another user's sessions.
- **Open SignalR connections:** not closed (Remaining risks); documented in T-14 and ASVS 7.4.1.
- **Order and atomicity:** the change is saved first, then the sessions are ended in a second save (finding 5).
- **The deny list:** the source is right and verified (SHA-256 and content); OGL v3 for the NCSC's list and MIT for the SecLists copy, both named in `NOTICE.txt`; compared ignoring case; generated passwords are 16 characters from a 57-character alphabet, so a match is practically impossible, and a test makes 500.
- **CI:** the SHAs match their tags; Trivy fails closed (a failed scan or database download fails the step, and `--ignore-unfixed` is a stated choice); permissions are per job except the PR workflow's top-level `pull-requests: write`; attestations work for a public, user-owned repository, and `create-storage-record: false` is a real input; the Dependabot config uses current keys (`cooldown`, `directories`, grouped `update-types`), though GitHub only validates it on `main`; CodeQL's manual C# build ran and passed; every job but `dependency-review` passed.
- **The ASVS and threat-model docs:** generally honest, with "Partly met" used freely. The overclaims are dependency review and Dependabot security updates (finding 1) and the per-username limit (finding 4); the rows now better after #65 are in finding 9.
- **Do the tests really test all this?** Mostly, and well: the integration tests run the real pipeline for headers, cookie flags, throttling and session ending. The gaps are in finding 8.
