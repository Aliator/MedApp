# Review: PR #59, a forced password change at first sign-in

**PR:** [#59](https://github.com/Aliator/MedApp/pull/59), branch `feature/force-password-change`, reviewed at `13a26b0` on top of `main` at `88dc1fa` (the merge of #56). `main` has since moved to `864c900` (the merges of #57 and #58), and the branch no longer merges into it cleanly (finding 3).

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, a scratch probe of the real API pipeline, or the GitHub API. **No finding is high or critical;** two are medium and five are low.

## Summary

A password an admin generates (a new account, or a reset) now has to be replaced by its user before they can do anything else: a flag on the account becomes a claim on the session, a middleware refuses every other `/api` request, and the web app holds them on a new `/change-password` page. The server side holds up against every path trick tried, but a page that's already open when the flag is set isn't sent to change the password, and signing in again from a browser that still holds such a session is refused as "Invalid credentials". The PR also fixes `SetRolesAsync`'s retries and makes medapp-dev's `create_user` all-or-nothing.

## What changed, and why

54 files, 2,031 lines added and 197 removed; 786 of the added lines are the migration's generated designer file.

- **Why:** since #53 each new account gets a generated password, shown once to the admin, but it worked until its user changed it, and nothing made them. A password that has passed through an admin's hands (and perhaps a chat message or a sticky note) should be swapped for one only its user knows.
- **The flag on the account:** `ApplicationUser.MustChangePassword`, a new `bit NOT NULL` column on `AspNetUsers` with default 0 (migration `AddedMustChangePassword`). Think of a hotel key card stamped "temporary".
  - **Set** when an admin's action produces a password: creating an account (`IdentityUserService.CreateUserAsync`, in the same insert) and resetting one (`ResetUserPasswordAsync`, in the same update as the new password hash).
  - **Cleared** when the user changes their own password (`UpdateUserPasswordAsync`, in the same update). If a reset or change fails, the flag is put back in memory, so nothing saves it later.
  - **Not set** for the first admin (`FirstAdminSetup`) or for accounts medapp-dev makes or sets a password on.
- **From the account to each request:** `SessionService.ValidateSessionAsync` already loads the user on every request (for the roles). It now also returns the flag, and `SessionAuthenticationHandler` adds the claim `medapp:must_change_password` with the value `true`. `GET api/auth/whoami` reports it as `mustChangePassword`.
- **The gatekeeper:** `PasswordChangeRequired`, a middleware between authentication and authorisation. For a signed-in user with the claim, every `/api` request gets a 403 problem response (title "Password change required", `code: "password_change_required"`), except four:
  - `GET api/auth/whoami` and `GET api/auth/me/settings` (so the page shows in their theme)
  - `POST api/auth/logout`
  - `PUT api/auth/users/{username}/update-password` for their own username

  Pages and static files pass, so the web app can show its own page. It works like a visitor's pass that opens only the door to the desk where it's swapped for a staff pass, and the exit.
- **One comparison, shared:** the `SelfUserOnly` policy's check (the route's `{username}` is the caller's own, exactly) moves to `AuthPolicies.IsOwnUsername`, which the middleware uses too, so both decide "own password" the same way.
- **A real change:** `UpdateUserValidator` refuses a new password equal to the current one (an exact, case-sensitive comparison), so the given password can't be "changed" to itself to clear the flag.
- **In the web app:**
  - `whoamiQuery` carries `mustChangePassword`. The signed-in layout's guard (`requireSignedIn`, in `_app`) sends such a user to `/change-password` before it loads anything the API would refuse.
  - `/change-password` (`RequiredPasswordChangePage`) sits outside the layout, like `/login`, with a guard of its own (`requirePasswordChange`): only someone who must change gets in; anyone signed out goes to sign in, and anyone else goes home. It asks for the "Password you were given" and a new one, then forgets the cached whoami and opens the start page (`useEnterApp`, which sign-in now uses too). It has a Sign out button.
  - The three inputs and the form's logic move out of Account Settings' Change Password page into `PasswordChangeFields` and `usePasswordChangeForm`, which both pages use.
  - Rendered on the server, a page under the layout answers with a 307 redirect to `/change-password` after asking only for whoami.
- **Bug fix, `SetRolesAsync`:** it loaded the user before `IdentityTransactions.RunAsync`, which repeats the whole transaction after a transient database error (an Azure SQL failover, say). The failed attempt had already given that user a new concurrency stamp in memory, so the retry's save failed with a false concurrency error. Now the user and the roles are loaded inside the retried work. Like restarting a spoiled form from a fresh copy, not from the one with the last attempt's crossings-out. This was finding 5 of the #52 review.
- **Bug fix, medapp-dev's `create_user`:** creating the account, setting a chosen password, clearing the flag and adding the roles now run in one `IdentityTransactions` transaction (`AccountTools.CreateAccount`), so a failure part-way leaves no half-made account. This was finding 5 of the #53 review. `reset_demo_data`'s staff go through the same helper; `set_password` and `ensure_test_admin` clear the flag.
- **Docs:** `AGENTS.md` (Auth), `docs/architecture.md` (routes, the password section, risks S1 and R15), `docs/todo.md` (both items done), `MedApp.Web/AGENTS.md` and `README.md`, medapp-dev's `README.md`, and `deploy/azure/README.md` (the retry item leaves "Not yet").

## Design choices

- **One gatekeeper middleware with an allowlist, not a rule on each endpoint:**
  - **Instead:** an authorisation requirement on every policy (and on the hub) that fails while the claim is set, with an attribute on the endpoints that may still run; or the same middleware reading that attribute from the matched endpoint's metadata instead of comparing path strings.
  - **Chosen because:** one place covers every controller, the hub and the `/api` catch-all, and a new endpoint is refused by default. A single checkpoint at the building's entrance, rather than a lock on each door.
  - **Worse at:**
    - the allowlist is path strings kept apart from the routes, so renaming one of the four endpoints silently refuses it
    - the own-password exception depends on routing having already run, which no committed test checks (finding 4)
    - an endpoint the list leaves out is refused, and sign-in is one of them (finding 2)

    Endpoint metadata would keep the list beside the actions, at the cost of trusting each new endpoint to be marked sparingly.
- **The flag read from the account on every request, not copied onto the session:**
  - **Instead:** copy it onto the `UserSession` row at sign-in.
  - **Chosen because:** the account is already loaded for its roles, and a reset applies on the very next request, even to a session that was already open.
  - **Worse at:** the server knows at once, but a page that's already open doesn't (finding 1).
- **A page of its own outside the signed-in layout, not a dialog inside it:**
  - **Instead:** keep the layout and show a blocking dialog.
  - **Chosen because:** the layout's parts (the bell, live notifications, settings sync) all call endpoints the API refuses; outside it, nothing does. A server-rendering test shows whoami is the only request made before the redirect.
  - **Worse at:** a second password form to keep in step with Account Settings (the shared fields help), and it only catches someone who loads a page (finding 1).
- **Existing accounts left unflagged (the column defaults to 0):**
  - **Instead:** flag accounts that may still have a generated password or `DefaultPassword1`.
  - **Chosen because:** deploying doesn't suddenly hold everyone on the change page, and the migration stays a single column added.
  - **Worse at:** accounts created since #53 whose generated password was never changed, and those from before #53, aren't forced (Remaining risks).
- **The first admin and medapp-dev's accounts exempt:**
  - **Instead:** flag them too.
  - **Chosen because:** the deploy's first sign-in and local dev work straight away, and the parity tool's account signs in unattended.
  - **Worse at:** on kind, the first admin's password is generated by a script and stays readable in the cluster's secret store (finding 5); and medapp-dev can't make a forced account to try the flow, which needs the Users page or a reset.
- **"Different from the current one" as the only new rule:**
  - **Instead:** a password history.
  - **Chosen because:** one validator rule closes the obvious loophole of choosing the given password again.
  - **Worse at:** a later change can go back to the given password. A change of case alone counts as different, deliberately (passwords are case-sensitive, and a test covers it).

## How it was checked

- **Read:** the whole diff (54 files), the PR description, and the code around it: `Program.cs` and `ApplicationBuilderExtensions` (the pipeline), `AuthController`, `SessionService`, `IdentityTransactions` and its callers, the Users page's create flow, the login page, the API client, `LiveNotifications`, the notifications hub, the Helm chart's migrations Job, and medapp-dev's host and DI setup. Identity's `ChangePasswordAsync` and `ResetPasswordAsync` save the user only after the new hash is accepted and return before changing anything on a validation failure, so the flag really is saved with the password or not at all.
- **Builds:** `dotnet build MedApp.sln --configuration Release`, 0 warnings and 0 errors; `dotnet build tools/MedApp.DevMcp --configuration Release`, clean.
- **.NET tests:** `dotnet test MedApp.UnitTests --configuration Release`, 813 passed.
- **Web:** `npm run lint` and `npm run typecheck` clean; `npx vitest run`, 699 tests in 84 files passed.
- **The migration, offline:** `dotnet ef migrations has-pending-model-changes` reports no changes. `dotnet ef migrations script`, both ways, shows `Up` as one `ALTER TABLE [AspNetUsers] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit)`, and `Down` dropping that default constraint, then the column. No database was touched. The chart runs the migrations Job before the API's pods (a pre-upgrade hook, and sync wave 1 against the API's 2 under Argo CD), and old pods ignore the new column, so a rolling deploy is safe.
- **A scratch probe of the real pipeline,** outside the repository: `WebApplicationFactory` over `MedApp.API` (its real `Program`, services and middleware), on real Kestrel, with only the session lookup (`ISessionService`) and the user service (`IIdentityUserService`) faked, and a connection string that points nowhere. Requests were sent as raw HTTP over a socket, so each request line reached Kestrel exactly as written, plus WebSockets to the hub. Results are under Detail. In short:
  - **Routing does run before the middleware.** The app never calls `UseRouting()`, so ASP.NET Core adds it at the start of the pipeline. A forced user's own `PUT …/update-password` went through the middleware, both policies and the controller (204), which can only happen if the route's `username` was already set.
  - **No way round it was found:** case changes, trailing slashes, `//`, encoded slashes, dot segments (plain and encoded), another user's name and a case-changed own name, the hub's negotiate, its WebSocket, `HEAD` and unknown `/api` paths were all refused or never reached a controller.
  - **Two gaps:** `POST api/auth/login` with a forced session's cookie is refused (finding 2), and a hub WebSocket opened before the flag was set kept receiving notifications after it (finding 1).
- **Do the tests test the change?** Mostly, yes. The middleware's 35 test cases cover the allowlist and 19 refusals, but set the route value by hand (finding 4). The Identity tests capture the flag at the moment of the saving call, which really shows "the same save". The `SetRolesAsync` test runs the work twice and checks the second run uses a freshly loaded user. The web tests cover both guards, the server-rendered redirect and the page's flows, but not a 403 `password_change_required` arriving at an open page (finding 1). medapp-dev has no tests, so `create_user`'s rollback is reasoned, not tested: the tool resolves its services per call, so `IMediator`'s handlers share the transaction's `DbContext`, and `AssignRoleAsync` opens no transaction of its own, so nothing nests.
- **CI:** run [43826547440](https://github.com/Aliator/MedApp/actions/runs/43826547440) on `13a26b0` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`). The coverage comment has `PasswordChangeRequired` at 100% of lines and 93.7% of branches.
- **Merging:** `git merge-tree` of the branch with `origin/main` (`864c900`) reports a conflict in `docs/todo.md` (finding 3).
- **Not done:** the migration against a throwaway LocalDB database (the PR says it was run), medapp-dev against a database, UI parity screenshots, the axe audit, and a real browser.

## Findings

Most severe first. None is high or critical. All seven were open when this review was written; each says below what happened to it.

### 1. A page already open when its user's password is reset isn't sent to change it, and its live connection stays open

- **Severity:** medium. **Confidence:** high (the API side shown by the probe; the web side read from the code).
- **What could go wrong:** an admin resets the password of someone who's signed in, for example one of the accounts that may still have `DefaultPassword1`, as `AGENTS.md` advises. The API applies the flag on that session's next request, but the open app cached whoami for good (`staleTime: Infinity`), so its guard keeps passing. Every list then says it "could not be loaded", and every save says "You do not have permission to change …", until the page is fully reloaded or they sign out. Nothing tells them a new password is needed. Meanwhile the notifications hub's WebSocket, authorised once when it opened, keeps receiving: the probe sent a notification to a user after the flag was set, and it arrived on the open socket while their next HTTP request got a 403. The page announces it to screen readers ("New notification: …"), and a notification's title is a procedure's title. Before this PR, a reset left such a session fully working, so this is still an improvement; it's the open page that's left behind.
- **Evidence:**
  - `MedApp.Web/src/auth/auth.ts:35` (`staleTime: Infinity`) and `auth.ts:42-46` (`requireSignedIn` reads the cached whoami)
  - `MedApp.Web/src/api/client.ts:46-51`: `unwrap` turns the 403 into a plain `ApiError`; nothing looks for `password_change_required`
  - `MedApp.Web/src/pages/saveProblem.ts:22-23` and `MedApp.Web/src/pages/patients/PatientsListPage.tsx:130`: the messages shown instead
  - `MedApp.API/Common/Authentication/PasswordChangeRequired.cs:20-29`: checks HTTP requests only; `MedApp.API/Notifications/NotificationsHub.cs:12-13`: authorised at connect
  - `MedApp.Web/src/app/LiveNotifications.tsx:38-42`: an arriving notification refreshes the bell and is announced to screen readers
- **Fix:** in the browser, one response middleware on the API client: a 403 whose body has `code: "password_change_required"` sends the page to `/change-password` with a full load (sketch under Detail). The server then renders it with a fresh whoami, and the old page's hub connection closes with it. Add a test in `client.test.ts`. For a client that ignores it, the open socket remains (Remaining risks); closing it would need the API to track each user's hub connections and abort them on a reset.
- **What happened to it:** **Fixed.** The API client (`MedApp.Web/src/api/client.ts`) has a response middleware, in the browser only: a 403 whose problem `code` is `password_change_required` sends the page to `/change-password` with a full load (not when it's already there). Server rendering is unaffected. `ApiError` now carries the problem's `code` (`problemCode`). Tested in `client.test.ts` and `errors.test.ts`. A client that ignores it keeps an open hub socket (Remaining risks).

### 2. Signing in again from a browser that still holds a forced session is refused, as "Invalid credentials"

- **Severity:** medium. **Confidence:** high (the 403 shown by the probe).
- **What could go wrong:** someone signs in for the first time with the password they were given, reaches "Choose your password", and closes the browser. The session cookie lasts 8 hours. Later that day they open the bookmarked sign-in page (their first visit was redirected there, so it's the page they saved) and type the same, correct password. The middleware sees the old cookie's claim and refuses `POST api/auth/login` with a 403, and the login page shows any failure other than a locked account (423) or field errors (400) as "Invalid credentials". They conclude the password is wrong and ask for another reset. The same happens to anyone else signing in from that browser. Nothing gets past this but signing out, which the page doesn't offer, or opening the app's home address, which isn't obvious.
- **Evidence:**
  - `MedApp.API/Common/Authentication/PasswordChangeRequired.cs:53-54`: the only `POST` allowed is logout
  - `MedApp.API/Controllers/AuthController.cs:40-59`: login checks the posted credentials and replaces the cookie; it needs no signed-in user
  - `MedApp.API/Common/Authentication/SessionCookieService.cs:34`: the cookie expires with the session, so it outlives the browser
  - `MedApp.Web/src/routes/login.tsx`: no guard, so the page shows with the cookie present
  - `MedApp.Web/src/pages/login/LoginPage.tsx:33-35`: any other failure reads "Invalid credentials"
  - the probe: `POST /api/auth/login` with the forced cookie, 403 `password_change_required`
- **Fix:** allow `POST api/auth/login` in `IsAllowed` (diff under Detail), with a test case in `PasswordChangeRequiredTests`, and add it to the lists in `AGENTS.md:156`, `docs/architecture.md:1006` and the class's summary. Signing in proves the password afresh and replaces the session, so it gives a forced user nothing new. Optionally, `/login` could also send a signed-in visitor on to `/`.
- **What happened to it:** **Fixed.** `PasswordChangeRequired.IsAllowed` allows `POST api/auth/login`, with a test case (and `GET api/auth/login` among the refusals); `AGENTS.md`, `docs/architecture.md` and the class's summary list it.

### 3. The branch no longer merges cleanly into `main`

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** #57 and #58 were merged after this branch was cut. #57 rewrote the Features list in `docs/todo.md` around the two items this PR deletes, so the PR can't be merged until that's resolved. This review's row in `docs/reviews/README.md` will collide the same way with the #57 and #58 rows `main` added below the #56 row.
- **Evidence:** `git merge-tree --write-tree HEAD origin/main` reports `CONFLICT (content): Merge conflict in docs/todo.md`; `git diff 88dc1fa origin/main -- docs/todo.md docs/reviews/README.md`.
- **Fix:** merge `origin/main` into the branch. In `docs/todo.md`, keep `main`'s new Features items and drop the two this PR finishes ("Force a password change…" and "`medapp-dev`'s `create_user`…"). In `docs/reviews/README.md`, keep the #57 and #58 rows, then this one. Then let CI run again on the merge.
- **What happened to it:** **Fixed.** `origin/main` (#57 and #58) merged into the branch. `docs/todo.md` keeps `main`'s new Features items and drops the two this PR finishes; `docs/reviews/README.md` lists #57, #58, then #59.

### 4. Nothing committed tests the pipeline order the own-password exception depends on

- **Severity:** low. **Confidence:** high that it's untested; low that it will break soon.
- **What could go wrong:** `IsOwnPasswordChange` reads the route's `username`, which is set only because ASP.NET Core adds routing at the start of the pipeline when the app doesn't call `UseRouting()` itself. The unit tests set the route value by hand. If someone later adds `app.UseRouting()` below the middleware (to put something between routing and the endpoints, say), the route value is empty when the middleware runs, every forced user's own change is refused, and nobody can leave the change page. It fails closed, so nothing leaks, but no test would notice; only a person trying it would.
- **Evidence:**
  - `MedApp.API/Common/Authentication/PasswordChangeRequired.cs:59-74`, whose comment says routing "has already matched by now"
  - `MedApp.UnitTests/Api/Authentication/PasswordChangeRequiredTests.cs:22-24`: the route value set by hand
  - `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:45-50`: no `UseRouting()`
  - `docs/architecture.md:1153` already records that nothing tests the assembled pipeline
- **Fix:** commit a pipeline test like this review's probe, in `MedApp.IntegrationTests` (empty today): `WebApplicationFactory` with `ISessionService` and `IIdentityUserService` faked, and two raw requests, a forced user's own `PUT …/update-password` (expect 204) and another user's (expect 403). It would also guard finding 2's fix and the hub's refusal.
- **What happened to it:** **Deferred.** The integration test suite is being built on another branch; the pipeline test (a forced user's own `PUT …/update-password` gets 204, another user's 403, plus sign-in and the hub) will be added there after this merges.

### 5. On kind, the exempt first admin's password is generated by a script and stays in the secret store

- **Severity:** low. **Confidence:** high on the facts; whether to exempt is a judgement call.
- **What could go wrong:** the exemption rests on the first admin's password being "chosen by whoever deploys and kept in the secret store" (`FirstAdminSetup.cs:70-71`, `AGENTS.md:157`). On kind it isn't chosen: `up.sh` generates it and prints the `kubectl` command that reads it back. It's handed on like any generated password, and since nothing makes the admin change it, it keeps working for anyone who can read that secret, for as long as the deployment lives. That's the very risk this PR removes for every other account.
- **Evidence:** `deploy/kind/up.sh:104-107` and `:173`; `MedApp.Infrastructure/Common/Identity/FirstAdminSetup.cs:70-78`; `AGENTS.md:157`.
- **Fix:** either set `MustChangePassword = true` for the first admin too, so the stored password only gets them to the change page (the secret then no longer signs anyone in, and re-running `create-first-admin` is unaffected, as it only acts on an empty database); or keep the exemption and reword the justification to say the password sits in the secret store until changed, adding it to Remaining risks in `docs/architecture.md`.
- **What happened to it:** **Kept, and the reason reworded.** The first admin stays exempt. `FirstAdminSetup.cs` and `AGENTS.md` now say its password comes from the secret store, set by whoever deploys, and that on kind `up.sh` generates it and it can be read back from the cluster's store, which is acceptable for a local cluster. The risk is recorded under Remaining risks.

### 6. A new account has no role, and without one the change page can't succeed

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the Users page creates an account from a username alone; roles are a separate "Edit roles" step. If the admin passes on the password before giving a role, the new user reaches "Choose your password", fills it in, and the API's `Staff` policy refuses the change. The page says "You do not have permission to update this password", which doesn't say what's missing. A role-less account couldn't use any page before this PR either, so it isn't a regression, but the forced change makes this the first thing such a user sees.
- **Evidence:**
  - `MedApp.Web/src/pages/users/UsersPage.tsx:196`: `create.mutate(newUsername.trim(), …)`, a username only
  - `MedApp.API/Controllers/AuthController.cs:152-154`: `Staff` and `SelfUserOnly` on the own-password change
  - `MedApp.Web/src/components/forms/PasswordChangeFields.tsx:17-18`: the 403 message
- **Fix:** have the create dialog take the roles (User ticked by default) and set them straight after creating, or, at least, word a 403 on this page as "Your account has no role yet. Ask an admin to give it one."
- **What happened to it:** **Mitigated, behaviour unchanged.** A new account still has no role until an admin gives it one. On the change-password page, a 403 that isn't `password_change_required` now reads "This account has no role yet. Ask an admin to give it one, then sign in again." (`usePasswordChangeForm`'s `forbiddenMessage`), with tests on the page and the shared form.

### 7. Docs and the parity screens lag the change

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the architecture handbook's pipeline table, which lists every step in order, has no step for `PasswordChangeRequired`, and its walk-through of the session check doesn't mention the claim or the middleware, so anyone reasoning about the pipeline from the handbook misses it. The new page has no UI parity screen, though `AGENTS.md` asks for one for each new screen. Smaller: the given-password field's placeholder still reads "Enter your current password", and `deploy/azure/README.md` lost its final newline.
- **Evidence:**
  - `docs/architecture.md:413` (step 8, "Authentication, authorisation") and `:653-655` (steps 4 to 6)
  - `tools/ui-parity/screens.ts:287` has `account-password` but nothing for `/change-password`
  - `MedApp.Web/src/components/forms/PasswordChangeFields.tsx:100` and `MedApp.Web/src/pages/account/RequiredPasswordChangePage.tsx:60`
- **Fix:**
  - in the table, make step 8 "Authentication, password change, authorisation", noting that `PasswordChangeRequired` refuses a forced user everything under `/api` but its allowlist
  - in step 4, add that the claim comes from `MustChangePassword`
  - in step 6, add that whoami reports it and the guard redirects
  - add a parity screen for `/change-password` (it needs a forced account of its own, made from the Users page)
  - let `PasswordChangeFields` take the placeholder with the label
- **What happened to it:** **Fixed, except the parity screen.** `docs/architecture.md`'s pipeline table (step 8 is now "Authentication, password change, authorisation") and the session walk-through (steps 4 and 6) describe the claim, the middleware and the redirect. `PasswordChangeFields` takes a `currentPlaceholder` ("Enter the password you were given" on the change page), and `deploy/azure/README.md` has its final newline back. **To do:** a UI parity screen for `/change-password`, which needs a forced account of its own.

## Remaining risks

- **Hub connections outlive any change to the session:** they're authorised once, when they open, so a reset, a role change, a revoked session or a deleted account all leave an open socket receiving until it drops. This PR's flag joins that list (finding 1).
- **Accounts the migration doesn't flag:** those created since #53 whose generated password was never changed, and those from before #53 that may still have `DefaultPassword1`. A reset now flags an account, so resetting them covers both.
- **A reset or a change still ends no sessions,** as `docs/architecture.md` (6.1) already says.
- **Two tabs:** a tab opened before the change keeps its cached whoami, so its change page still asks for the given password, now wrong, until it's reloaded.
- **No password history:** a later change can go back to the given password.
- **medapp-dev's all-or-nothing `create_user` is reasoned, not tested:** the tool has no tests and wasn't run against a database here.
- **The first admin on kind keeps a readable password:** it's exempt from the forced change, and on kind `up.sh` generates its password, which stays readable in the cluster's secret store and keeps working until the admin changes it. Accepted for a local cluster (finding 5); a deployment whose secret store is shared more widely should have the admin change it after the first sign-in.
- **No UI parity screen for `/change-password`** yet: it needs a forced account of its own (finding 7).
- **Not seen in a browser:** no UI parity screenshots or axe audit were run for this review, and the PR doesn't say they were.

## Say it like this

"Any password an admin hands out now has to be swapped for the user's own before they can do anything: the account carries a flag, the API turns away every request except signing out and changing that password, and the web app holds them on a change-password page. The main choice was one gatekeeper in front of the whole API rather than a rule on every endpoint, so a new endpoint or the live notifications can't be forgotten. The trade-off is that the gatekeeper matches a short list of paths and checks only new requests, so it also turns away a fresh sign-in, and a page that was already open isn't told."

## Detail

- **The probe's requests,** each with a forced user's session cookie (user `test.staff`, role User) unless noted:

  | Request | Result |
  |---|---|
  | `GET /api/auth/whoami`, `GET /API/Auth/WhoAmI` | 200 |
  | `PUT /api/auth/users/test.staff/update-password` (own name) | 204: through the middleware, both policies and the controller |
  | `PUT …/Test.Staff/update-password`, `…/test.other/update-password`, own name with a trailing `/` | 403 `password_change_required`; the user service is never called |
  | `PUT …/test%2Estaff/update-password` | 204: Kestrel decodes `%2E`, so this is the own name |
  | `GET /api/patients/search`, `/API/PATIENTS/SEARCH`, `/api/patients/search/` | 403 `password_change_required` |
  | `GET /api/auth/whoami/../../patients/search`, and with `%2E%2E` | 403: Kestrel removes the dot segments first |
  | `GET /api/auth/whoami%2F..%2F..%2Fpatients%2Fsearch` | 403 |
  | `GET //api/patients/search` | 404 (a blocked path) |
  | `GET /api%2Fpatients%2Fsearch` | 404: one segment, so neither `/api` nor any controller |
  | `GET /api/auth/whoami/`, `HEAD /api/auth/whoami` | 403: stricter than routing, and harmless |
  | `GET /api/notifications`, `GET /api/notifications/hub`, `POST /api/notifications/hub/negotiate` | 403 `password_change_required` |
  | WebSocket to `/api/notifications/hub` | refused with 403 |
  | `PUT /api/auth/me/settings`, `GET /api/no-such-endpoint` | 403 `password_change_required` |
  | `POST /api/auth/login` | 403 `password_change_required` (finding 2) |
  | A user without the flag: whoami, and a hub WebSocket | 200 with `mustChangePassword: false`; the socket opens |
  | A hub WebSocket opened without the flag, then the flag set | the next HTTP request gets 403, but a notification sent to that user arrives on the open socket (finding 1) |

- **A sketch of the fix for finding 1,** in `MedApp.Web/src/api/client.ts`, below the server-rendering middleware:

  ```ts
  /** The 403's `code` when the signed-in user must change a password an admin gave them (PasswordChangeRequired). */
  export const PASSWORD_CHANGE_REQUIRED = 'password_change_required';

  api.use({
    async onResponse({response}) {
      if (typeof window === 'undefined' || response.status !== 403) return;
      if (window.location.pathname === '/change-password') return;
      const body: unknown = await response.clone().json().catch(() => null);
      // A full load: the server renders the page with a fresh whoami, and this page's hub connection closes.
      if (typeof body === 'object' && body !== null && 'code' in body && body.code === PASSWORD_CHANGE_REQUIRED) {
        window.location.assign('/change-password');
      }
    },
  });
  ```

- **The fix for finding 2,** in `MedApp.API/Common/Authentication/PasswordChangeRequired.cs`:

  ```diff
           if (HttpMethods.IsPost(request.Method))
  -            return Is(path, "/api/auth/logout");
  +            // Signing in (as anyone) checks the posted password and replaces this session; signing out ends it.
  +            return Is(path, "/api/auth/login") || Is(path, "/api/auth/logout");
  ```

  and `[TestCase("POST", "/api/auth/login")]` on `InvokeAsync_MustChangePassword_AllowsTheAllowlist`.
- **The migration's SQL** (`dotnet ef migrations script`, offline):

  ```sql
  -- Up
  ALTER TABLE [AspNetUsers] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);
  -- Down: the default constraint first, found by name, then the column
  IF @var IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var + ';');
  ALTER TABLE [AspNetUsers] DROP COLUMN [MustChangePassword];
  ```

  Rolling back loses the flags: anyone held on the change page is released. That's acceptable, as the code that reads them goes with it.
- **The probe's setup,** for repeating it as finding 4's test: a `WebApplicationFactory<PasswordChangeRequired>` (any public type in `MedApp.API` names the assembly), `UseKestrel(0)` and `StartServer()`, the setting `ConnectionStrings:DefaultConnection` pointed at `127.0.0.1,1`, and `ConfigureTestServices` replacing `ISessionService` (sessions by fixed GUIDs, one with `MustChangePassword: true`) and `IIdentityUserService` (a password change that counts its calls). Raw requests go over a `TcpClient` with `Connection: close`; WebSockets use `ClientWebSocket` with the cookie header and `CollectHttpResponseDetails`. The open-socket check sends the SignalR handshake, flips the fake's flag, then pushes through `IHubContext<NotificationsHub>.Clients.User(id)`.
- **`SetRolesAsync` before and after:** before, a retry reused the user from the failed attempt, whose concurrency stamp the failed save had already moved on in memory, while the rollback put the database's back. `IdentityTransactions` clears the change tracker before a retry, but re-attaching that same object made EF check the stale stamp, so the update matched no row and Identity reported a concurrency failure. Now the user is loaded inside the work, after the tracker is cleared, with the database's own stamp. The callers of `RunAsync` are `SetRolesAsync`, `FirstAdminSetup` and medapp-dev's `CreateAccount`; all three load or create everything inside the work.
