# Review: PR #53, a generated first password for new accounts

**PR:** [#53](https://github.com/Aliator/MedApp/pull/53), branch `feature/generated-first-password` at `f86d277`. The branch starts from `main` at `0f4f474`. `main` has since moved to `6774269` (PR #52).

**Reviewed:** 15 November 2026, independently of the author's notes. Every claim below was checked against the code, a tool or the GitHub API, unless it's marked suspected.

## What the PR changes, and why

**Why:** every new account was created with the same password, `DefaultPassword1`:
- It was hard-coded in `MedApp.Web/src/api/users.ts` and shown in the create dialog.
- It was public in the repository.
- Nothing made anyone change it.

So every account made from the Users page started with a password anyone could know.

**What changes:**
- **API:** `POST api/auth/users` now takes only `{ username }`.
  - It used to return 204. It now returns `200 CreateUserResponse { username, password }`, carrying a password the server generated.
  - Identity's errors are still a 400 with `IdentityError[]`.
  - The endpoint is still `[Authorize(Roles = "Admin")]` (`AuthController.cs:90`).
- **Application:**
  - `CreateUserCommand(Username)` returns `CreateUserResult(IdentityResult Result, string? GeneratedPassword)`.
  - The validator's password rule is gone.
- **Infrastructure:**
  - `IdentityUserService.CreateUserAsync(username)` makes the password with the same private `GeneratePassword` that resets use.
  - The generator now makes at least 16 characters, or the configured minimum if that's longer. Before, it used the configured minimum, which is 8.
  - The branch that grew the length to fit the required character kinds is gone.
- **Web:**
  - `useCreateUser` sends only the username and returns `{ username, password }`.
  - The create dialog shows the password in a new shared `OneTimePassword` component, which the reset dialog now uses too.
  - `DEFAULT_NEW_USER_PASSWORD` is gone, and `schema.d.ts` is regenerated.
- **`medapp-dev`:**
  - Without a password, `create_user` returns the generated one.
  - With a chosen password, it checks it against the rules, creates the account, then sets the chosen password with a reset token.
  - Demo staff accounts get generated passwords.
  - `delete_user` calls the user service directly, the same change #52 made.
- **Blazor client:** sends only the username, and the "Default password will be" hint is gone.
- **Docs:**
  - a new Auth bullet in `AGENTS.md`
  - a to-do entry for forcing a password change at first sign-in

## How it was checked

**CI:** run [43768892287](https://github.com/Aliator/MedApp/actions/runs/43768892287) on `f86d277` passed. It ran the branch's own, older workflow, which doesn't build the dev tool (see finding 6).

**What I ran**, in the worktree or in a scratch folder:
- **.NET:**
  - `dotnet build MedApp.sln -c Release`, also with `--no-incremental`, succeeds with one warning: CS0414 in `MedApp.Client.Deprecated/Pages/Admin/Users/UsersTable.razor.cs:29`. That warning is already on `main`.
  - `dotnet test MedApp.UnitTests -c Release`: 751 passed.
  - `dotnet build tools/MedApp.DevMcp -c Release` succeeds, with no warnings.
- **Web** (`MedApp.Web`):
  - `npm run lint` and `npm run typecheck` are clean.
  - `npm test`: 82 files and 668 tests passed.
  - The test run rewrote `src/routeTree.gen.ts` with line-ending changes only, and I restored it.
- **Merging with `main`:**
  - `git merge-tree` against `origin/main` gives one conflict (finding 6).
  - I exported the merged tree to a scratch folder, with the conflict resolved to `main`'s side.
  - There, the solution builds (the same one warning), the dev tool builds, and 751 tests pass.
- **The generator, sampled:**
  - A scratch file-based app called the private `GeneratePassword` by reflection, 200,000 times, with the rules in `MedApp.API/appsettings.json`.
  - **Every password:** 16 characters long, all different, and each meets the rules.
  - **The alphabet:** 57 characters, with no `I`, `O`, `l`, `0` or `1`.
  - **The bias:** digits make up 17.65% of characters against 14.04% for a uniform pick. That's because one digit is always guaranteed. It costs a fraction of a bit.
  - **Strength:** about 88 to 93 bits.
  - With `RequireNonAlphanumeric` and a minimum of 20, it gave 20 characters with a symbol.
- **Searches across the repository** (outside `bin`, `obj` and `node_modules`):
  - `DefaultPassword`, "default password" and other fixed passwords
  - every caller of `POST api/auth/users`, `CreateUserRequest` and `CreateUserCommand`, including the Blazor client, the dev tool, `tools/ui-parity` and `MedApp.E2ETests`
  - HTTP body logging, MediatR behaviours and telemetry hooks
  - React Query persistence and devtools, and browser error reporting
  - how the dev server proxies `/api`
- **Reading:** Astryx's `Button`, `Text`, `Dialog` and `CodeBlock` sources in `node_modules`, for focus, live regions and copy support.

**Not done:** nothing against a running app, a database, Docker or a cluster. No screen reader or browser. Nothing committed or pushed.

### The brief's questions, point by point

- **Is the password strong?** Yes:
  - It uses the crypto RNG through `RandomNumberGenerator.GetInt32`, which rejects samples to avoid bias.
  - It's shuffled by Fisher–Yates with the same RNG.
  - It has at least 16 characters from a 57-character alphabet.
  - The only bias is the guaranteed character of each kind, which doesn't matter.
  - `RequiredUniqueChars` above 57 would make the generator loop for ever (`IdentityUserService.cs:167-172`), but that setting isn't configurable here (`IdentityOptionsConfiguration` doesn't map it), so the risk is theoretical.
- **Is it returned only to the admin, and never logged?** Yes:
  - It's in the 200 response of an Admin-only action and nowhere else, and it's never stored in plain text.
  - **Server:** no HTTP body logging. The only MediatR behaviour is validation. OpenTelemetry records no bodies.
  - **Browser:** the web app has no browser telemetry and no `console` calls on this path. React Query has no persistence or devtools.
  - **Rendering:** the page server renders only GET pages, so the password is never in server-rendered HTML.
  - Finding 8 covers how that could change later.
- **Is it cleared from UI state when the dialog closes?** No. It goes when the dialog is next opened (finding 4).
- **Could it leak through the dev tool's output?** `create_user` and `reset_demo_data` return passwords, as they did before, on purpose and as documented. `DevGuard` refuses any non-local database, so these are local test accounts.
- **Is the endpoint still Admin-only?** Yes, though no test pins it (finding 10).
- **Is any fixed or default password left?**
  - `medapp-dev`'s local `admin` / `admin` is by design (`TestAdminTools.cs:17`).
  - That account also reaches kind through `up.sh --demo-data` snapshots, which are local and documented.
  - The accounts already made with `DefaultPassword1` are finding 1.
  - The stale plan line is finding 11.
  - No seed, no Blazor code and no other source still uses `DefaultPassword1`.

## Findings, most severe first

### 1. Accounts made before this PR keep the published password

- **Severity:** medium. **Confidence:** confirmed that nothing checks or changes them. Whether a database that matters has any is unknown.
- **Evidence:**
  - **Before this PR,** every account made from the React Users page or the Blazor client got `DefaultPassword1`:
    - React: `DEFAULT_NEW_USER_PASSWORD` in `users.ts` on `main`
    - Blazor: `UsersTable.razor.cs:506` on `main`
  - **The password is public:** it stays in git history, and `docs/react-migration-plan.md:319` names it.
  - **Nothing in the PR finds those accounts:**
    - no migration
    - no startup check
    - no command beside `create-first-admin`
    - no `medapp-dev` tool
  - **The to-do** (`docs/todo.md:17`) tracks only a forced change at first sign-in.
  - **SQL can't find them:** passwords are salted hashes, so only Identity's hasher can check them.
  - **They travel:** local databases carry such accounts into dev snapshots, and from there into kind (`deploy/kind/up.sh --demo-data`).
- **Failure scenario:**
  1. An admin created a staff account from the Users page before this PR.
  2. Its owner never changed the password.
  3. After the merge, it still signs in with `DefaultPassword1`, which anyone who has read the repository knows.
  4. The lockout after 5 tries doesn't help, because the first guess is right.
- **Fix:** a one-off check that an admin runs:
  - For each account, test `PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, "DefaultPassword1")`. Calling the hasher directly has no side effects: no rehash and no failed-attempt count.
  - For each match, set a generated password (or a lockout far in the future), revoke its sessions, and list the usernames, so the admin can pass on new passwords.
  - It fits as an API command like `create-first-admin`, for the Helm Job, and as a `medapp-dev` tool for local databases.
  - At the least, add it to `docs/todo.md` and the PR description.

### 2. The password isn't announced, focused or easy to copy

- **Severity:** medium. **Confidence:** confirmed by reading the code and Astryx's source. Not tried with a screen reader.
- **Evidence:**
  - **Nothing announces it:**
    - When the request succeeds, the dialog swaps the username field for `OneTimePassword`, and its message for "Account created for …" (`UsersPage.tsx:176-184`).
    - Neither is in a live region. `OneTimePassword` is three Astryx `Text`s (`OneTimePassword.tsx:19-29`), and `Text` adds no `aria-live`.
    - `UsersPage` never calls `useAnnounce()`. `MedApp.Web/AGENTS.md` asks for it for "changes people see but a screen reader wouldn't mention".
  - **Focus stays on the button just pressed,** which now says "Done" (`UsersPage.tsx:178`). The password sits above it, unannounced.
  - **No copy control:** the only way to take the password is to select it by hand. Astryx has what's needed:
    - `CodeBlock`, whose copy button is on by default and announces "Copied"
    - `useClipboard`, in `@astryxdesign/core/hooks`
  - **The reset dialog** had the same gaps before. The shared component now carries them into account creation, where the password is the flow's only result.
- **Failure scenario:**
  1. An admin using NVDA creates an account.
  2. The screen reader says nothing about the result, and focus is on "Done".
  3. They press it, taking it for a confirmation. The dialog closes and the password is gone.
  4. They have to reset it (finding 3), and meet the same problem again.
- **Fix,** in `OneTimePassword`:
  - Show the password in an Astryx `CodeBlock`, or a `Text` with an icon button that uses `useClipboard`, labelled "Copy password".
  - Move focus to that button when the component mounts.
  - Announce the result with `useAnnounce()`: "Account created for X. Its first password is shown, with a copy button." Don't announce the password itself, so it isn't spoken aloud in a shared room.
  - Test that focus lands on the copy button, that the announcer gets the message, and that copying writes the password to a stubbed clipboard.

### 3. The password can be lost before the admin has read it

- **Severity:** low. **Confidence:** Escape, the close button and Cancel are confirmed by reading. The double-click is suspected, because it depends on timing.
- **Evidence:**
  - **"Done" takes "Create User"'s place:** it's the same button, in the same place (`UsersPage.tsx:178`, `:181`). Its loading state lasts only while the request runs.
  - **Once the account exists, the dialog can be dismissed by mistake:**
    - Its `purpose` goes back to `form` (`ConfirmDialog.tsx:62`), so Escape and the header's close button dismiss it (`DialogFrame.tsx:16-18`).
    - "Cancel" still shows (`ConfirmDialog.tsx:65`), though there's nothing left to cancel.
    - All three just close the dialog.
  - **Leaving mid-request:** if the admin leaves the page while the request runs, the `onSuccess` passed to `mutate` (`UsersPage.tsx:181`) doesn't run after the page unmounts. The account is created, and its password is never shown.
- **Failure scenario:** either way, the account exists with a password nobody has, and the admin has to reset it.
  - An admin double-clicks "Create User". On a quick local API, the response arrives between the clicks, so the second click lands on "Done", and the dialog closes before the password is drawn.
  - Or the admin presses Escape to dismiss what looks like a notice.
- **Fix:**
  - Once `created` is set, let only "Done" close the dialog:
    - Add a prop to `ConfirmDialog` that uses `purpose='required'`, which blocks Escape and hides the close button.
    - Let `cancelText={null}` hide "Cancel".
  - Ignore the confirm button for a moment after the password appears, or move focus to the copy button (finding 2), so a stray click or Enter doesn't land on "Done".
  - Do the same for the reset dialog.

### 4. The password stays in memory after the dialog closes

- **Severity:** low. **Confidence:** confirmed.
- **Evidence:**
  - **Create:** `close` only clears `dialog` (`UsersPage.tsx:109`). `created` (`:91`) and the mutation's `data` stay until the next `open()` (`:102`, `:105`) or until the page unmounts. The comment at `:90` says it's "gone when the dialog closes", which isn't so.
  - **Reset:** `reset.reset()` is never called. The last reset password stays in TanStack Query's mutation cache until the page unmounts and the default `gcTime` (5 minutes) passes. This was already so before this PR.
  - **Only in memory:** nothing persists it and there are no devtools, so it stays in the tab's memory only.
- **Failure scenario:** an admin creates an account, closes the dialog and leaves the Users page open. Anyone with access to that tab can still read the password: React DevTools, a heap snapshot, or an extension with page access. It's low, because that person already has the admin's session.
- **Fix:**
  - **Clear it on close:** `setCreated(null)`, `setNewPassword(null)`, `create.reset()` and `reset.reset()` in `close`. If the content shouldn't change while the dialog fades out, clear them when the fade ends.
  - Give the two password mutations `gcTime: 0`.
  - Correct the comment.

### 5. `medapp-dev`'s `create_user` with a chosen password can leave a half-made account

- **Severity:** low. **Confidence:** confirmed by reading. The failure needs a database error or a cancellation between the steps.
- **Evidence:** `AccountTools.cs:70-88` does four things in turn:
  1. checks the chosen password
  2. creates the account with a generated password (`:74`)
  3. resets it to the chosen password (`:82-84`)
  4. assigns the roles (`:87-88`)

  If anything after `:74` throws, the account is left with a password that was thrown away, and no roles. That could be a dropped connection, or a cancelled call: `ct` isn't passed to the Identity calls, so a cancellation lands at `:88`.
- **Failure scenario:**
  1. An agent calls `create_user` with a chosen password while the SQL container restarts.
  2. The tool fails after the account is created.
  3. The next call says the name is already taken.
  4. The account can't sign in until someone calls `set_password` or `delete_user`.
- **Fix:** either of these:
  - When anything after the create fails, delete the account before rethrowing.
  - For a chosen password, run `CreateUserValidator`, then `users.CreateAsync(user, password)`, in one step.

  The role assignment could already fail like this before the PR, and the first fix covers it too.

### 6. The branch conflicts with `main`

- **Severity:** low. **Confidence:** confirmed.
- **Evidence:**
  - **The conflict:** `git merge-tree --write-tree HEAD origin/main` reports one, in `tools/MedApp.DevMcp/Tools/AccountTools.cs`. Both sides rewrote the comment above `delete_user`'s call (the PR's is at `:159`). The code lines are the same.
  - **Clean merges:** `AGENTS.md`, `docs/todo.md` and `DemoDataTools.cs`.
  - **What `main` has added:**
    - EF Core's retries on transient errors (`EnableRetryOnFailure`)
    - a CI step that builds `medapp-dev`
  - **The merge works:** resolved to `main`'s comment in a scratch copy, the solution and the dev tool build, and 751 tests pass.
- **Failure scenario:** GitHub won't merge the PR until the conflict is resolved. And CI hasn't yet built the dev tool with this change.
- **Fix:** merge `main` into the branch, keep `main`'s comment, push, and let CI run on the merge.

### 7. The deprecated Blazor client now makes accounts whose password nobody sees

- **Severity:** low. **Confidence:** confirmed.
- **Evidence:**
  - `UsersTable.razor.cs:506-512` posts only the username. On success it closes the dialog and reloads, ignoring the response body.
  - The hint about the default password is gone (`UsersTable.razor:244`).
- **Failure scenario:** someone runs the Blazor client by hand and creates an account, for example to capture parity screenshots again, as `AGENTS.md` describes. Nobody can sign in to that account until an admin resets its password.
- **Fix:**
  - The client is frozen to "no changes beyond keeping it compiling". So say in `MedApp.Client.Deprecated/README.md` that creating a user there doesn't show the generated password, and to reset it from the React app.
  - Otherwise, read `CreateUserResponse` and show the password the way its reset modal does.

### 8. Nothing stops the password being logged or cached later

- **Severity:** low. **Confidence:** suspected. Nothing logs or caches it today.
- **Evidence:**
  - **Records print their passwords:** `CreateUserResult` (`CreateUserResult.cs:9-11`), `CreateUserResponse` (`CreateUserResponse.cs:7-9`) and `ResetUserPasswordResponse` are records. A record's generated `ToString()` prints every property, the password included.
    - Nothing logs MediatR responses or HTTP bodies today.
    - But a logging behaviour is a common MediatR addition, and it would log every generated password.
  - **No `no-store`:** the 200 that carries the password (`AuthController.cs:102`) has no `Cache-Control: no-store`.
    - Browsers don't cache POST responses without freshness headers, so this is defence in depth.
    - The reset endpoint is the same.
- **Failure scenario:** someone adds a `LoggingBehaviour` that logs `{@Response}`. From then on, every new account's password lands in the API's logs and its OpenTelemetry export.
- **Fix:**
  - Override `PrintMembers` on the three records to print `Password = ***`, and pin that with a unit test.
  - Add `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` to `CreateUser` and `ResetUserPassword`.

### 9. `CreateUserResult` can say "created" with no password

- **Severity:** low. **Confidence:** confirmed by reading. It isn't reachable today.
- **Evidence:**
  - **The record allows it:** `Result.Succeeded` with `GeneratedPassword == null` (`CreateUserResult.cs:9-11`).
  - **The controller hides it:** it uses `!` (`AuthController.cs:102`), so such a result would send `"password": null` in a 200.
  - **The client gets it wrong:** it throws `ApiError(500)` (`users.ts:31`), and the dialog says "The user could not be created. Please try again." But the account exists, so trying again fails with "already taken".
- **Failure scenario:** a later change to `IdentityUserService`, or a test double, returns success without a password. The admin is told to try again, and gets a duplicate-name error.
- **Fix:**
  - Give the record a private constructor and two factories, `Created(string password)` and `Failed(IdentityResult result)`, so success always carries a password. Then drop the `!`.
  - In the client, word the fallback as "The account may have been created, but no password came back. Reset its password from the list."

### 10. Gaps in the tests

- **Severity:** low. **Confidence:** confirmed.
- **Evidence:**
  - **Admin-only isn't pinned:** nothing tests that `POST api/auth/users` is Admin-only, though it now hands out a working password. `AuthControllerTests.cs:449-460` already has the pattern (`MayUpdatePassword`) for another action.
  - **The handler tests only check the mock:** `CreateUserHandlerTests.cs:52-66` checks that the handler passes the mock's result through. Its failure case uses `UserNotFound`, which creating an account can't return.
  - **Only "Done" is tested:** the Users page tests close the create dialog only with "Done" (`UsersPage.test.tsx:69`). Two things are untested:
    - Cancel, Escape and the close button once the account exists
    - the state being cleared on close (finding 4)
  - **The dev tool has no tests,** including the chosen-password sequence.
- **Fix:**
  - Add `MayCreateUser`: Admin allowed, User refused, anonymous refused.
  - Add Users page tests for dismissing the dialog once the account exists.
  - Use `DuplicateUserName` in the handler's failure test.

### 11. Docs: "one-time", and a stale line in the migration plan

- **Severity:** low. **Confidence:** confirmed.
- **Evidence:**
  - **"One-time":**
    - `AGENTS.md:152` says the account gets "a one-time password", and the component is `OneTimePassword`.
    - A one-time password usually means one that works once. This one works until the user changes it, and nothing makes them (`docs/todo.md:17`).
    - What's meant is that it's shown once.
  - **The stale line:** `docs/react-migration-plan.md:319` still says new users get `DefaultPassword1`.
- **Failure scenario:** a reader, or an agent, takes "one-time" at its word. They assume a leaked first password stops working after one use, so they don't treat the leak as urgent.
- **Fix:**
  - In `AGENTS.md`, say "a generated first password, shown once, which works until the user changes it".
  - Consider `GeneratedPassword` as the component's name.
  - Add "replaced by a generated password in #53" to the plan's line.

## What's worth keeping

- **The server makes the password,** and it's strong:
  - `RandomNumberGenerator.GetInt32`, which avoids bias, and a Fisher–Yates shuffle with the same RNG
  - a 16-character floor
  - no characters that look alike, which helps when the password is read out or copied by hand
- **It goes only to an Admin, once,** in the 200 response:
  - It's never stored in plain text.
  - It isn't logged, persisted in the browser, or written into server-rendered HTML.
- **The contract change is complete:**
  - No caller sends a password any more: not the web app, the Blazor client, the dev tool, `tools/ui-parity` or the E2E project.
  - `schema.d.ts` matches the controller's response attributes exactly.
- **The tests that matter most:**
  - `CreateUserAsync_Succeeds_CreatesTheUserWithTheReturnedPassword` checks that the password returned is the one Identity was given. That's the bug that would hurt most.
  - Another test checks the generated password against Identity's real `PasswordValidator`.
  - The page test checks that the request body is only `{ username }`, and that the password is gone when the dialog opens again.
- **The removed branch really was dead:**
  - At most 4 kinds of character are required, and the length is now at least 16.
  - `Math.Max` also covers a minimum of 0 or less, which the old `< 1 ? 8` handled.
- **`OneTimePassword` is one shared component** for both dialogs, as `MedApp.Web/AGENTS.md` asks, and is built only from Astryx.
- **The dev tool:**
  - It checks a chosen password before creating anything, so a weak one leaves nothing behind.
  - Demo staff get passwords from the app's own generator.
- **The to-do entry** for forcing a change at first sign-in is the right next step. It needs the remediation in finding 1 beside it.

## Verdict

Nothing critical or high, so nothing blocks merging on severity:
- **Before merging:** resolve the conflict with `main` (finding 6), since GitHub can't merge the PR until then. Then let CI run, including the dev tool build that `main` added.
- **Soon after, the two mediums:**
  - finding 1: find and fix the accounts that still have `DefaultPassword1`
  - finding 2: announce the password, focus it, and make it easy to copy
- **The lows** can go in follow-ups.

## Resolution

Fixed on `feature/generated-first-password`, after `main` was merged in. Each finding:

1. **Accounts made before this PR keep the published password: documented and deferred.**
   - `AGENTS.md` (Auth) says accounts created before #53 may still have `DefaultPassword1`, and that an admin should reset their passwords or ask their users to change them.
   - `docs/todo.md` (Features) tracks the one-off check that finds them with `PasswordHasher.VerifyHashedPassword`, as a `medapp-dev` tool and an admin report or command. It isn't built.
2. **The password isn't announced, focused or easy to copy: fixed.** `OneTimePassword`:
   - announces "Account created for X." (or "Password reset for X.") with `useAnnounce()`, once per password, and never the password itself
   - moves focus to a new "Copy password" button, built on Astryx's `useClipboard`, which shows "Copied" with a tick (and Astryx announces it)
   - is tested for all three, in its own tests and on the Users page.
3. **The password can be lost before it's read: fixed for the double-click.**
   - `ConfirmDialog` has `confirmHoldMs`: the confirm button ignores clicks for that long after its text changes. The Users page holds "Done" for a second in both the create and the reset dialogs, so the second click of a double-click can't close them. Tested in both.
   - Focus moving to the copy button (finding 2) keeps a stray Enter off "Done".
   - Escape, the close button and Cancel still close the dialog once the password shows, as the brief asked; they now clear it too (finding 4). Leaving the page mid-request isn't handled.
4. **The password stays in memory: fixed.** Closing either dialog by any route clears `created` and `newPassword` and calls `create.reset()` and `reset.reset()`. The two password mutations also have `gcTime: 0`, so they leave the mutation cache at once. The username field renders only while the dialog is open. Tests check that closing by Done, Escape, the close button or Cancel leaves the password neither in the page nor in the mutation cache. The misleading comment is corrected.
5. **`medapp-dev`'s `create_user` can leave a half-made account: deferred** to `docs/todo.md` (Features).
6. **The branch conflicts with `main`: fixed** by the merge of `main` into the branch, with `main`'s comment kept.
7. **The Blazor client makes accounts whose password nobody sees: documented.** Its create dialog now says the password isn't shown and to reset it from MedApp.Web's Users page, and its `README.md` says the same. No other change, since the client is frozen.
8. **Nothing stops the password being logged or cached: fixed.**
   - `CreateUserResult`, `CreateUserResponse` and `ResetUserPasswordResponse` override `PrintMembers`, so `ToString()` prints `***` for the password. Unit tests pin it.
   - `CreateUser` and `ResetUserPassword` send `Cache-Control: no-store` with the password. Controller tests check the header.
9. **`CreateUserResult` can say "created" with no password: fixed.** It has a private constructor and two factories, `Created(password)` and `Failed(result)`, which refuse an empty password or a successful "failure". `Succeeded` is true exactly when there's a password (`[MemberNotNullWhen]`), so the controller's `!` is gone. Tests updated, with new `CreateUserResultTests`.
10. **Gaps in the tests: fixed, except the dev tool.**
    - `AuthControllerTests` checks that `CreateUser`'s `[Authorize]` attributes let an Admin in and refuse a User, a signed-in account with no role, and an anonymous user.
    - The handler test now returns the password only for the username the handler passes, and its failure case uses `DuplicateUserName`.
    - The Users page tests close the dialog after creation by Escape, the close button and Cancel.
    - The dev tool still has no tests.
11. **"One-time", and the stale plan line: fixed.**
    - `AGENTS.md` now says a generated first password, shown once, which works until the user changes it. The UI note says "It's shown only this once, and works until X changes it." The component keeps the name `OneTimePassword`, and its doc comment says what the name means.
    - `docs/react-migration-plan.md:319` notes that #53 replaced the fixed password.
