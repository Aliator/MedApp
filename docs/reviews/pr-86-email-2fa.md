# Review: PR #86, staff email, password reset by email and two-factor sign-in

**PR:** [#86](https://github.com/Aliator/MedApp/pull/86), branch `feature/email-2fa`, reviewed at `1eae28c` on top of `main` at `173f7fe` (the merge of #84), which hadn't moved when this review was committed.

**Reviewed:** 16 November 2026, as an adversarial security review, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, a probe against the real API and SQL Server, a rendered chart, or the GitHub API. **Two findings are high:** wrong authenticator codes sent at the same moment escape both the challenge's five-try limit and the lockout (measured: 145 codes checked before one lockout, where 5 should be), and a stolen session can be turned into permanent ownership of the account without its password. 2 findings are medium and 8 are low.

## Summary

MedApp can now email staff (a link to confirm an address, a link to reset a forgotten password, and notices of changes to how they sign in), keeps its data protection keys in the database (wrapped by Key Vault on Azure), and offers optional two-factor sign-in with an authenticator app, recovery codes, an admin reset and an admin-only requirement. Most of it is careful: tokens travel in the link's fragment, the reset form answers the same whatever matched, challenges are 256-bit and stored hashed, codes are compared in constant time and accepted once, and no clinical text reaches an email. The weak points are where requests race and where a change to the account trusts the session alone: parallel wrong codes aren't counted (high), a session holder can swap in their own email address and reset the password (high), a confirmation link proves only a mailbox (medium), and on Azure the pod that holds the page server can now reach the whole internet (medium).

## What changed, and why

282 files, 16,315 lines added and 1,098 removed (15,827 and 610 ignoring line endings: `AGENTS.md` alone is rewritten from LF to CRLF, finding 11). About 2,400 of the added lines are generated: the migration's designer file (1,028), the snapshot (77), the API types (1,121) and the route tree (147).

- **Why:** roadmap step 4 (multi-factor authentication) and the to-do list's email and reset items. The old sign-in page's "Forgot password?" said a link had been sent when nothing was (ASVS 6.4.3, previously partly met).
- **Sending email:** `IEmailSender` puts each message on an in-memory queue (`EmailQueue`), and `EmailDispatcher` sends it over SMTP with MailKit in the background, best effort, no retries. Settings are under `Email` (`LinkBaseUrl`, `FromAddress`, `Smtp:*`). Mailpit catches mail in development (AppHost) and kind (chart); Azure Communication Services' SMTP relay sends it on Azure (Bicep, not deployed).
- **Templates** (`EmailTemplates`): fixed subjects, HTML-encoded values, the username, a link or a short notice; never clinical text or a password.
- **Staff email addresses:** Identity's `Email` and `EmailConfirmed`, plus a new `PendingEmail` column. Set by an admin at account creation, or by the person in Account Settings (`PUT`/`DELETE api/auth/me/email`); confirmed by an anonymous `POST api/auth/email/confirm` with Identity's change-email token (24 hours).
- **Forgot your password:** `POST api/auth/password/forgot` always answers 202 and emails a 30-minute, single-use reset link to each matching account with a confirmed address; throttled per client address and per identifier (3 in 15 minutes). `POST api/auth/password/reset` sets the new password, ends every session, clears a forced change and a lockout, and emails a notice.
- **Data protection keys** (which seal those tokens) are in the database (`DataProtectionKeys`, shared by every pod), wrapped with a Key Vault key on Azure (`DataProtection:KeyVaultKeyUri`, the API's workload identity).
- **Two-factor sign-in:** a right password for an account with two-factor now answers **202** with a challenge and no session; `POST api/auth/login/two-factor` with the challenge and a code (or a recovery code) makes the session. Codes are checked by MedApp's own `Totp` (RFC 6238, one step either side) and accepted once (the last step is kept as a user token). Wrong codes count towards Identity's lockout, and a right password no longer clears that count for these accounts.
- **Setting it up and changing it:** a QR code drawn on the server, ten recovery codes, regenerate (with a code), turn off (with the password and a code). Turning it on ends the account's other sessions.
- **Admins:** reset someone else's two-factor (ends their sessions, emails them), and a setting that requires two-factor for admin accounts, enforced by a new hold, `TwoFactorSetupRequired`, which shares a new base class (`AccountHold`) with `PasswordChangeRequired`.
- **Activity log:** actions 28 to 39 for each step, without addresses, tokens or codes.
- **Infrastructure:** Mailpit in the AppHost and the chart (SMTP only from the API, no egress); chart settings for an external mail server and its Secret; a service account for workload identity; egress rules to the internet on the SMTP port and 443; on Azure, Communication Services with an Azure-managed domain, a custom role for the hand-made Entra app the relay signs in as, a Key Vault key and an identity that can only wrap and unwrap with it; Argo CD's project now allows a ServiceAccount.
- **Web:** the code step on the login page, forgot, reset, confirm-email and set-up pages, Account Settings' Email and Security cards, the Users page's email and two-factor columns and reset action, and Admin Settings.
- **Migration** `AddedStaffEmailAndTwoFactorSignIn`: one column and three tables, additions only.
- **Tests and docs:** unit, integration, web and one E2E journey; `AGENTS.md`, the ASVS checklist, the threat model (T-16 to T-19, T-35), the governance pack and the hazard log.

## Design choices

- **MedApp checks codes itself, not with Identity's authenticator token provider:**
  - **Instead:** `UserManager.VerifyTwoFactorTokenAsync`.
  - **Chosen because:** the clock comes from `TimeProvider` (testable), and knowing the step a code matched allows "each code once".
  - **Worse at:** more code that must be exactly right. It is (RFC 6238 test values pass, constant-time compare), but the replay and lockout guards now depend on Identity's concurrency stamp behaving as hoped under load (finding 1).
- **The password step answers 202 with a bearer challenge in the body:**
  - **Instead:** Identity's sign-in manager with its short-lived two-factor cookie, or asking for the code before saying whether the password was right.
  - **Chosen because:** no extra cookie, and a clean API for the React app.
  - **Worse at:** a 202 tells anyone guessing passwords that this one is right (normal for two-step sign-in; the per-username limit slows the guessing). The challenge is bound to the user but not to the browser that asked for it; at 256 bits that is fine.
- **One lockout counter for passwords and codes, with a five-minute lockout that ends by itself:**
  - **Instead:** a separate, stricter budget for codes, or a lockout that grows.
  - **Chosen because:** Identity already has it, and the account holder isn't stuck for long.
  - **Worse at:** codes can be guessed for ever at five per five minutes (about 1 in 230 a day), even before the race in finding 1. A six-digit code needs a tighter budget than a password does.
- **Changes to the account need only the session** (a new email address and turning two-factor on; new recovery codes need a code, but not the password):
  - **Instead:** asking for the password again, as `DisableTwoFactor` and `DeleteUser` do.
  - **Chosen because:** fewer prompts.
  - **Worse at:** a borrowed session becomes a permanent account (finding 2).
- **Anonymous email confirmation:**
  - **Instead:** confirm only while signed in as that account.
  - **Chosen because:** the link opens in whatever browser the mail app uses.
  - **Worse at:** the link proves someone reads that mailbox, not that they're the person the account is for (finding 3).
- **Keys in the database, wrapped by Key Vault only on Azure:**
  - **Instead:** a certificate from a Secret, Azure Blob storage, or Redis.
  - **Chosen because:** the database is already shared by every pod and backed up.
  - **Worse at:** outside Azure the keys sit unwrapped beside the security stamps they protect (finding 9).
- **An in-memory queue, best effort:**
  - **Instead:** an outbox table and a worker.
  - **Chosen because:** simple, and a request that sends mail takes no longer than one that doesn't (so timing doesn't reveal accounts).
  - **Worse at:** mail queued at shutdown is lost, nothing retries, and nothing keeps the total under the provider's quota (finding 6).
- **The SMTP relay, signed in to as an Entra app with a client secret:**
  - **Instead:** Communication Services' email SDK with the API's workload identity (no secret at all).
  - **Chosen because:** SMTP works the same with Mailpit and any other mail server.
  - **Worse at:** a client secret that expires (the README says so) and that is a full Entra credential, usable outside SMTP with the app's role.
- **Two-factor optional, required only for admins and only when an admin turns that on:**
  - **Instead:** compulsory at go-live.
  - **Chosen because:** the PR's constraint that accounts without email or two-factor sign in exactly as before.
  - **Worse at:** most accounts stay password-only (DPIA residual risk "high", recorded), and an admin who hasn't enrolled is enrolled by whoever signs in first (finding 5).
- **Recovery codes stored as Identity stores them, in clear:**
  - **Instead:** a custom store with salted hashes.
  - **Chosen because:** time; it's recorded (ASVS 6.5.2 planned, `docs/todo.md`, the data inventory, T-18).
  - **Worse at:** a copy of the database gives every unused recovery code.

## How it was checked

- **Read:** the whole diff, the PR description, `AGENTS.md` (Auth and the new Email section), `docs/security/asvs.md`, `docs/security/threat-model.md`, `docs/governance/`, the hazard log, and the code around the change: `IdentityAuthenticationService`, `SessionService`, the holds, the rate limiting, `LoginHandler`, `UserStore`'s concurrency handling, the activity log's repository, the chart's templates and Azure's Bicep.
- **Build:** `dotnet build MedApp.sln --configuration Release`: 0 errors, 1 warning (CS0414 in `MedApp.Client.Deprecated`, not touched by the PR).
- **.NET tests:** `dotnet test MedApp.UnitTests --configuration Release`: 2,081 passed. `dotnet test MedApp.IntegrationTests --configuration Release` filtered to the `Auth` namespace: 180 passed, against SQL Server 2022 in its own Testcontainers container, removed afterwards.
- **The migration:** `dotnet ef migrations has-pending-model-changes`: no changes. Read by hand: one `AddColumn`, three `CreateTable`, three indexes; `Down` drops them. Not run against a database here.
- **Probes:** a scratch test project outside the repository, compiling the integration tests' own `Common` helpers (so the real API in memory, against SQL Server in Docker, removed afterwards), with seven probes: wrong codes in parallel on one challenge (30 and 100 at once), waves of 29 until the lockout, 100 wrong codes at once to regenerate recovery codes from a session, four address shapes, a stolen session's takeover, and a mistyped address's takeover. Results in [Detail](#the-probes).
- **Chart:** rendered with Azure's values filled in (as CI's new lint step fills them), with `alpine/helm:3.22.1` in Docker, to read the API's NetworkPolicy.
- **CI** at `1eae28c`, run [43868311606](https://github.com/Aliator/MedApp/actions/runs/43868311606): all eight jobs passed (`web`, `helm`, `unit-tests`, `image`, `integration-tests`, `e2e`, `dependency-review`, `coverage-comment`), so every required check passes. The CodeQL workflow passed, but its code scanning check, **CodeQL, failed** with 8 new alerts (finding 10); the PR shows as "unstable". Coverage: two new classes under 80% lines (`PasswordResetTokenProvider` 0%, a constructor-only subclass covered by integration tests; `SmtpOptions` 67%, a settings class).
- **Not done:** the web checks locally (CI's `web` job ran lint, type check, Vitest with coverage and the build), a real browser, kind, and Azure (never deployed). The dev database on port 14330 wasn't touched.

## Findings

Most severe first. Two are high. All twelve were open at the reviewed commit; each one's outcome is under "What happened to it".

### 1. Wrong codes sent at the same moment aren't limited, counted or logged

- **Severity:** high. **Confidence:** high (measured through the real pipeline and SQL Server; see [the probes](#the-probes)).
- **What could go wrong:** someone has a member of staff's password (phished, reused, watched) and the account has two-factor on, so they need a six-digit code. The design allows 5 wrong codes per challenge and locks the account after 5 wrong attempts. Both limits are "read, then check, then write" in separate steps, so requests that arrive together all pass the check before any of them writes. Sending 29 wrong codes at once (one minute's allowance from one address) gets all 29 checked; the account's failure count rises by 1, because Identity's update of the count loses to the concurrency stamp in all but one request; and 28 of the 29 end in a 500, because the failed update stays in the request's database context and makes the activity log's save throw. In waves of 29, **145 codes were checked before one lockout, where 5 should be, and only 5 reached the activity log.** That turns the chance of a right guess from about 1 in 230 a day into about 1 in 8 a day, better than even within a week, while the activity log shows five failures and a lockout. The same race lets a stolen session check about 40 codes per lockout against "new recovery codes" (100 sent at once: 40 checked before the lockout), an endpoint with no rate limit at all; a right one there hands over a fresh set of recovery codes. Like a bouncer who counts people one at a time at the door: send thirty together and they're all inside before the count reaches five.
- **Evidence:**
  - `MedApp.Application/Authentication/Sessions/Commands/LoginTwoFactor/LoginTwoFactorHandler.cs:37` (reads the challenge, untracked), `:45` (checks its attempts), `:72` (counts the failure only after the code is checked), `:74` (the lockout count), `:81` (the activity write that throws)
  - `MedApp.Infrastructure/Data/Repositories/SignInChallengeRepository.cs:19-27` (read and increment are separate statements)
  - `MedApp.Domain/Authentication/SignInChallenge.cs:41-42` (`IsUsable`)
  - `MedApp.Infrastructure/Common/Identity/TwoFactorService.cs:170-177` (`AccessFailedAsync`'s result ignored; on a concurrency failure the count isn't saved and the user stays modified in the context)
  - `MedApp.Application/Authentication/TwoFactor/Common/TwoFactorCodes.cs:37-50` (the same pattern for regenerate and disable)
  - `MedApp.API/Controllers/AccountSecurityController.cs:52` (the second step is limited only per client address, 30 a minute per copy; `:220-228`, regenerate, has no limit)
  - The tests send one request at a time: `MedApp.IntegrationTests/Auth/TwoFactorTests.cs:91-171`; the replay guard's race is unit-tested with a mock (`TwoFactorServiceTests.cs:87`), never through the database.
  - The docs rate it low: `docs/security/threat-model.md:106` (T-18), `docs/security/asvs.md:197` (6.1.1, "codes can't be guessed by re-entering the password").
- **Fix:**
  - **Reserve an attempt before checking:** one `ExecuteUpdateAsync` that adds 1 to `FailedAttempts` only `WHERE Id = @id AND FailedAttempts < 5 AND ExpiresAtUtc > @now`, refusing the request when it changes no row; a right code then deletes the challenge as now.
  - **Count failures in one statement:** increment `AccessFailedCount` (and set `LockoutEnd` at the limit) with `ExecuteUpdateAsync`, or retry `AccessFailedAsync` on `ConcurrencyFailure` after reloading the user. Never ignore its result.
  - **Don't leave a failed update in the context:** reload or detach the user after a failed Identity update, so the activity entry always saves and no wrong code becomes a 500.
  - **Add a per-account limit on code attempts** (as sign-in has per username), on `login/two-factor` and the three signed-in code checks.
  - **Bound guessing over time:** a lockout that grows with each lockout from codes (or a daily cap that needs a reset by email or an admin), and an email to the account holder after the first such lockout ("someone has your password").
  - **Tests:** an integration test that sends 30 wrong codes at once on one challenge and asserts at most 5 are checked, none is a 500, the count rises by exactly the number checked, and each is in the activity log; the same for regenerate.
- **What happened to it:** fixed. Each challenge attempt is reserved in one statement before the code is checked (`SignInChallengeRepository.TryReserveAttemptAsync`, `WHERE Id AND FailedAttempts < 5 AND ExpiresAtUtc > now`; no row means the same 401 `sign_in_expired`). Wrong codes and wrong passwords are counted in one statement, with the lockout applied by a second conditional one so only one request applies it (`AccountLockoutRepository`; `TwoFactorService`, `IdentityAuthenticationService` and `IdentityUserService.VerifyPasswordAsync` no longer use `AccessFailedAsync`); after a failed Identity update the user is detached (`Forget`), so the activity entry always saves. Each account gets at most 10 code checks in 15 minutes from any address, across sign-in and the signed-in checks (`AccountAttemptLimiter`, a 429 with `Retry-After`, logged as `TooManyCodes`), and `enable`, `disable` and `recovery-codes` are limited per address like sign-in. A lockout caused by codes doubles each time, to a day at most, until a complete sign-in (`CodeLockouts`, `ApplicationUser.TwoFactorLockouts`, in the migration), and emails the holder ("Wrong sign-in codes locked your MedApp account"). New recovery codes need the password as well as a code. `ParallelCodeTests` sends 30 wrong codes at once on one challenge (exactly 4 wrong-code answers and one lockout, the rest refused, no 500, the lockout and the email once), 30 right codes at once (one sign-in), and 30 wrong recovery-code requests with the real limit (9 checked after the sign-in's one, 21 refused with 429); `CodeLockouts_GrowEachTime…` checks the growth against SQL Server. One race remains, bounded: requests already past the lockout check when another one locks the account are still checked, within the per-account limit (seen: 9 checks, one lockout, with the limit at 10).

### 2. A session alone can take the account for good: change the address, confirm it, reset the password

- **Severity:** high. **Confidence:** high (the whole chain run end to end through the API).
- **What could go wrong:** a member of staff leaves a shared ward computer signed in. In the time it takes to open Account Settings, someone else puts their own email address on the account (`PUT api/auth/me/email` needs no password), opens the confirmation link on their phone, and leaves. Later, from anywhere, they use "Forgot your password?", get the link at their address, and set a new password: the account is theirs, with every patient record, and everything they do is recorded as the member of staff. They can then turn on two-factor with their own app (`enable` needs no password either), which ends every other session and locks the real holder out until an admin resets it. The notice of the change goes only to a previously confirmed address; every account that exists today has none, and in the probe **no email went to anyone but the attacker**. An admin who notices can reset the password and two-factor, but **can't take the address off the account** (there is no admin endpoint for it), so the next "Forgot your password?" gives it back. Before this PR a borrowed session lasted at most 24 hours and couldn't change the password without the current one. For an account with two-factor, the same session can guess codes against "new recovery codes" (finding 1), then use one to sign in after the reset. Like a hotel that lets anyone standing in a guest's room change the phone number the front desk rings to reissue that guest's key.
- **Evidence:**
  - `MedApp.API/Controllers/AccountSecurityController.cs:145-152` (change address, session only), `:189-196` (turn on two-factor, a code from the new app only), `:220-228` (new recovery codes, a code only)
  - `MedApp.Application/Authentication/Emails/Commands/ChangeMyEmail/ChangeMyEmailHandler.cs:29` (no re-authentication); `MedApp.Application/Authentication/TwoFactor/Commands/EnableTwoFactor/EnableTwoFactorHandler.cs:36` and `:47` (code only; other sessions end)
  - `MedApp.Application/Authentication/Emails/Commands/ConfirmEmail/ConfirmEmailHandler.cs:30-31` (the notice goes only to a previous confirmed address)
  - No admin endpoint changes or removes another account's address (`AuthController` and `AccountSecurityController` have only `me/email` and `CreateUserRequest.Email`)
  - `docs/security/asvs.md:255` (7.5.1, "Met", because "accounts have no email, phone or MFA settings yet"); `docs/security/threat-model.md:106` (T-18 says a stolen session "can't regenerate recovery codes ... without the password"; it can, with a code)
- **Fix:**
  - **Re-authenticate for changes that affect how someone signs in:** the current password for `PUT` and `DELETE me/email` and for `two-factor/enable`, and the password as well as a code for `recovery-codes`; or a "recently signed in" window (say 5 minutes) checked on those endpoints.
  - **Tell the account when a change is asked for, not only when it's confirmed:** an email to the current confirmed address and an in-app notification; with no confirmed address, an activity entry admins are told about.
  - **Give admins a way to remove an account's address** (and have the Users page's "reset password" offer it), so a takeover can be undone.
  - Correct ASVS 7.5.1 and T-18 until then. Tests: each endpoint refuses without the password.
- **What happened to it:** fixed. Changing or removing the address and turning two-factor on need the current password; new recovery codes and turning two-factor off need the password and a code (`PasswordConfirmation`; a wrong password is a 400 on `Password` and counts towards the lockout). Asking for a new address emails the confirmed one at once ("Someone asked to change your MedApp email address"), and changes are limited to 3 per account in 15 minutes. Admins can remove any account's address and any pending one (`DELETE api/auth/users/{username}/email`, `RemoveUserEmailHandler`, the Users page's "Remove email address"), recorded with the admin and told to the removed address. ASVS 7.5.1 and T-18 are corrected. Tests: `EmailAddressTests` (refused without the password, the notice of a request, the admin removal), `ParallelCodeTests` (enable and recovery codes without the password), and the handlers' unit tests. With no confirmed address, a request is in the activity log but no one is emailed: an in-app notice is on `docs/todo.md`'s list only for admins' two-factor.

### 3. A confirmation link proves a mailbox, not a person: a mistyped or shared address hands over the account

- **Severity:** medium. **Confidence:** high on the mechanism (run end to end); how often it happens depends on typing and on how customers use team mailboxes.
- **What could go wrong:** an admin creates an account for a new nurse and types the address with one letter wrong. The stranger who owns that address gets "Someone asked to use this email address for the MedApp account …", clicks "Confirm email address" (the page needs no sign-in), then uses "Forgot your password?" with that address, sets a password and signs in: in the probe, the new account searched patients at once, and the generated password the admin passed on stopped working. The same happens if staff mistype their own address. And because several accounts may share an address, a ward's team mailbox given to several accounts lets anyone who reads it reset any of them. Like a bank that sends a new card to whatever address it was given and activates it for whoever opens the envelope.
- **Evidence:**
  - `MedApp.API/Controllers/AccountSecurityController.cs:121-131` (anonymous confirm); `MedApp.Infrastructure/Common/Identity/AccountEmailService.cs:50-71` (checks only the token against the pending address)
  - `MedApp.Application/Authentication/Users/Commands/CreateUser/CreateUserHandler.cs:46-53` (the admin's typed address gets a link at once)
  - `MedApp.Infrastructure/Common/Identity/PasswordResetService.cs:79` (a reset also clears the forced change of the admin's generated password)
  - `AGENTS.md:295` ("anonymous: the link proves the address"); `docs/security/asvs.md:218` (6.4.3, "Met")
- **Fix:** make confirmation prove the account as well: `POST api/auth/email/confirm` requires the session of the account in the link (the page sends a signed-out visitor to sign in, then returns), so a stranger can never confirm someone else's address. For an admin-given address this means it's confirmed only after the person has signed in with the password the admin gave them. Say in `AGENTS.md` and the staff privacy notice that each account needs a personal address, not a team mailbox. Tests: a signed-out confirm, and a confirm from another account's session, are refused.
- **What happened to it:** fixed. `POST api/auth/email/confirm` needs a session of the account the link is for (`[Authorize]`; another account is a 400 `WrongAccount`, `ConfirmEmailHandler`). Signed out, `/confirm-email` keeps the link in `sessionStorage` and sends the person to sign in, then back (a same-origin redirect only, `safeRedirectPath`). Every hold allows the confirmation, so someone held to choose their first password can confirm an address an admin gave them; an admin-typed address is confirmed only by the person who knows the password they were given. `AGENTS.md` says each account needs an address of its own. Tests: `EmailAddressTests` (signed out is 401, another account's session is refused, the right one confirms) and `ConfirmEmailPage.test.tsx`.

### 4. On Azure, the pod with the page server can now reach every public address on 443 and 587

- **Severity:** medium. **Confidence:** high (rendered with Azure's values).
- **What could go wrong:** network policies apply to a pod, not a container, and the page server runs in the API's pod. With a Key Vault key and an external mail server set, as Azure's values will be, the API's policy allows any public address on 443 and on 587. #83 boxed the page server in because code running in Node sees every visitor's session cookie; until now it could leak them only slowly, as DNS lookups. A compromised npm package in the page server can now post each session cookie to its own server over https as pages are rendered, and use them from anywhere for up to 24 hours. The Azure README records "egress limited by name" as not yet done, but the threat model still says the page server "can reach DNS" and nothing more.
- **Evidence:**
  - `deploy/helm/medapp/templates/networkpolicy.yaml:59-74` (both rules), `deploy/helm/medapp/values.yaml:213-221` (`0.0.0.0/0` minus private ranges); the rendered policy in [Detail](#the-apis-network-policy-on-azure)
  - `deploy/helm/medapp/templates/api.yaml:237-241` (the page server in the same pod)
  - `docs/security/threat-model.md:117` (T-23) and `:161` (risk 5) unchanged; `deploy/azure/README.md:198`
  - `deploy/helm/medapp/templates/api.yaml:78-80` still says the API's workload identity is hypothetical ("Harmless until then")
- **Fix:** before the first Azure deploy, either run the page server as its own Deployment whose policy allows only the API (risk 5's own proposal), or limit egress by name (Cilium FQDN policies, or Azure Firewall). Meanwhile, update T-23 and risk 5 to say the pod reaches the internet on 443 and 587 on Azure, and fix the stale comment.
- **What happened to it:** fixed for Azure, as decided. A `CiliumNetworkPolicy` (`templates/fqdnpolicy.yaml`, behind `networkPolicy.fqdnEgress.enabled`, on only in `deploy/azure/values.yaml`) allows the API pod `smtp.azurecomm.net:587`, the vault named in `dataProtection.keyVaultKeyUri` and `login.microsoftonline.com` on 443, with a DNS rule for Cilium's proxy; with it on, the standard NetworkPolicy drops its `0.0.0.0/0` rules (CI's helm job now renders Azure's values and fails on any `0.0.0.0/0`). `main.bicep` turns on Advanced Container Networking Services' FQDN policies (API 2025-08-01; its cost is in `deploy/azure/README.md`), and Argo CD's project allows the kind. The stale comment in `api.yaml` is corrected, and T-23 and risk 5 say what the pod reaches. Not tried on a real AKS cluster. What remains: the page server shares the pod, so it can reach the same shared services (the relay with an attacker's own credentials, another vault behind the same addresses); the page server in its own Deployment is on `docs/todo.md`.

### 5. Under the admin requirement, whoever signs in first enrols the admin's two-factor

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** admins are required to use two-factor, and one admin hasn't set it up yet. Someone with that admin's password signs in, is held to set it up, enrols their own phone, and the enable call ends the real admin's sessions. The real admin is now locked out of their own admin account and needs another admin. That's inherent in enrolling at next sign-in, but nothing narrows it: no notice to other admins, and the only email goes to the admin's address if they confirmed one.
- **Evidence:** `MedApp.API/Common/Authentication/TwoFactorSetupRequired.cs:26-34` (the hold allows enrolment); `MedApp.Application/Authentication/TwoFactor/Commands/EnableTwoFactor/EnableTwoFactorHandler.cs:47`; `docs/security/threat-model.md:107` (T-19).
- **Fix:** notify every admin (in-app, and by email where confirmed) when an admin enrols or is reset; show in Admin Settings which admins haven't enrolled; and say in T-19 that enrolment under the hold trusts the password alone.
- **What happened to it:** fixed as far as decided. Every other admin with a confirmed address is emailed when an admin turns two-factor on, off or has it reset (`ISecurityNotices.SendToOtherAdminsAsync`), Admin Settings names the admins who haven't set it up (`SecuritySettingsResponse.AdminsWithoutTwoFactorUsernames`), turning it on needs the password, and T-19 says enrolment under the hold trusts the password alone. In-app notices to admins are on `docs/todo.md`.

### 6. Nothing caps the email MedApp sends

- **Severity:** low. **Confidence:** high on the code; the quota figures are the Azure README's.
- **What could go wrong:**
  - **Using up the quota:** the Azure-managed domain allows 10 emails an hour for the whole subscription (a custom domain, 100). Anyone, signed out, can ask for resets for a handful of known usernames whose accounts have confirmed addresses and use it up, so for the rest of the hour no reset links and **no security notices** go out, including the notice that would reveal finding 2.
  - **A relay for anyone signed in:** `PUT me/email` has no limit, so any member of staff can make MedApp send its confirmation email, from MedApp's sender address, to as many addresses as they like.
  - **Addresses with a display name** (`"Payroll team" <x@y.test>`) are accepted, stored and shown as typed; mail goes to `x@y.test`, and a reset by that address never matches. CR/LF and lists are refused (probe).
- **Evidence:** `MedApp.Infrastructure/Common/Email/EmailQueue.cs:41-46` (no overall limit); `MedApp.API/Controllers/AccountSecurityController.cs:145-152` (no rate limiting); `MedApp.Application/Authentication/Emails/Common/EmailAddressRules.cs:13-17`; `deploy/azure/README.md:153`.
- **Fix:** an hourly cap below the provider's quota, with notices sent ahead of reset links; a per-account limit on `PUT me/email` like the reset one (3 in 15 minutes); parse addresses with MimeKit's `MailboxAddress.TryParse` and store only the bare address.
- **What happened to it:** fixed. Each copy sends at most `Email:MaxPerHour` emails a clock hour (`EmailBudget`; default 100, `email.maxPerHour: 8` in Azure's values, under the managed domain's 10), a fifth kept for notices, and notices are queued and sent before links (`EmailCategory`, two queues in `EmailQueue`); over the cap a message is dropped and logged by category. `PUT me/email` is limited to 3 per account in 15 minutes and needs the password. Addresses are parsed (`EmailAddressRules.Normalize`) and only the bare address is stored and used (display names dropped; lists and line breaks refused), for staff and for admins creating accounts. Each copy counts on its own (on `docs/todo.md`).

### 7. The activity log credits some actions to someone who didn't do them

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** when an admin gives a new account an address, the log records `EmailChangeRequested` **by the new account**, from the admin's address. Anonymous requests are recorded the same way: a reset request, a confirmation and a reset by link all appear as done by the account, from whoever's address sent them. In finding 3's scenario the log reads as if the new nurse confirmed the address, asked for a reset and chose a password.
- **Evidence:** `MedApp.Application/Authentication/Users/Commands/CreateUser/CreateUserHandler.cs:50-52` (no actor passed); `MedApp.Application/Common/Security/ActivitySecurityEventLog.cs:22-25` (no actor means "by the account").
- **Fix:** pass the admin as the actor in `CreateUserHandler`; record anonymous events with no actor and the account as the target (`targetUsername`), as an admin's action on someone else is.
- **What happened to it:** fixed. `CreateUserHandler` passes the admin as the actor, so the address an admin gives is recorded by the admin with the account as its target; events from a request nobody was signed in for (a reset link asked for or used) are recorded by nobody, from the request's address, with the account as its target (`ActivitySecurityEventLog`, `ActivitySecurityEventLogTests`). A confirmation is now always by the signed-in account (finding 3).

### 8. Email transport: opportunistic TLS by default, and addresses in failure logs

- **Severity:** low. **Confidence:** high on the default; medium on the log text (it's the mail server's own wording).
- **What could go wrong:** the app's default `Email:Smtp:Security` is `Auto`, which on port 587 means "TLS if the server offers it": anyone on the network path can strip the offer and read the SMTP password (on Azure, an Entra client secret) and every reset link. The chart and Azure's values set `StartTls`, so this bites only a deployment configured some other way. Separately, a failed send is logged with its exception, and a refused recipient's message usually quotes the address (`5.1.1 <name@example.org>: Recipient address rejected`), against "never the address".
- **Evidence:** `MedApp.Infrastructure/Common/Email/EmailDeliveryOptions.cs:43`; `MedApp.Infrastructure/Common/Email/SmtpEmailTransport.cs:50-57`; `MedApp.Infrastructure/Common/Email/EmailDispatcher.cs:30`.
- **Fix:** default to `StartTls`, and allow `None` or `Auto` only for a host on loopback or the chart's Mailpit; log the exception's type and SMTP status code, not its message.
- **What happened to it:** fixed. `Email:Smtp:Security` defaults to `StartTls`, and an unknown value means `StartTls` too (`SmtpEmailTransport.SecurityFor`); `Auto` and `None` must be asked for. `EmailDispatcher` logs a failure with the email's category, the exception's type and the SMTP status code only (`SmtpStatus`), never the exception's message.

### 9. Outside Azure, the keys sit unwrapped beside the security stamps they protect

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** T-35 calls unwrapped keys acceptable for kind and development, but the chart stores them unwrapped in any cluster that isn't given a Key Vault key, such as a customer's own. Anyone with a copy of the database (a backup, a DBA) has both the keys and each account's security stamp, so they can mint a reset token for any account and post it to `password/reset` directly, with no email involved; with the authenticator keys also in clear, they can pass two-factor too. Nothing at deploy time says so.
- **Evidence:** `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs:57-69`; `deploy/helm/medapp/values.yaml:146`; `docs/security/threat-model.md:131` (T-35).
- **Fix:** support wrapping without Azure (`ProtectKeysWithCertificate`, from a Secret) in the chart, and have `NOTES.txt` warn (or the chart fail) when neither is set outside a Mailpit cluster; say so in T-35.
- **What happened to it:** partly fixed, as decided. The API logs a warning at startup outside Development when the keys aren't wrapped (`DataProtectionKeyCheck`), and the chart's `NOTES.txt` says the same after an install without a Key Vault key; T-35 now rates it medium elsewhere with real data. Wrapping with a certificate from a Secret is on `docs/todo.md`.

### 10. The CodeQL check is red, with eight false positives

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the PR's code scanning check fails with 4 high and 4 medium "clear text storage" and "exposure of private information" alerts. Each is a log line whose value is an enum name or constant subject containing "Password" or "Email" (`PasswordReset`, `ResetPasswordSubject`, `EmailChanged`), not a secret. CodeQL isn't a required check, so the PR can merge, but a check that is always red teaches everyone to ignore it, and the next real alert hides among these.
- **Evidence:** alerts 23 to 30 at `MedApp.Application/Common/Email/ISecurityNotices.cs:43`, `MedApp.Application/Common/Security/LoggingSecurityEventLog.cs:16`, `MedApp.Infrastructure/Common/Email/EmailQueue.cs:41` and `:46`; the check run [133302833321](https://github.com/Aliator/MedApp/runs/133302833321).
- **Fix:** dismiss them as false positives with that reason, or log those values under names CodeQL's heuristic doesn't match (for example the notice's number).
- **What happened to it:** fixed in the code, not dismissed. The logged values were renamed so CodeQL's heuristic no longer matches them (`SecurityEventKind.ResetLinkRequested`, `ResetByLink`, `AddressChangeRequested`, `AddressConfirmed`, `AddressRemoved`; `SecurityNotice.ResetByLink`, `AddressChanged`, `AddressRemoved`; `EmailTemplates.ResetLinkSubject`, `ConfirmAddressSubject`), the notice's debug log no longer names it, and the email queue and dispatcher log only the email's category, never its subject. The stored `ActivityAction` names are unchanged. To be confirmed by the next CodeQL run on the PR.

### 11. `AGENTS.md` is rewritten from LF to CRLF

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** `main` has `AGENTS.md` with LF line endings; the branch has CRLF, so its 25 changed lines show as 1,001, and one stray carriage return sits in the middle of line 111, in the list of mappers just before `EmailMapper`, which CommonMark reads as a line break. Every other branch that edits `AGENTS.md` (most do) will conflict on the whole file after this merges, and `git blame` loses its history.
- **Evidence:** `git diff --stat origin/main...HEAD -- AGENTS.md` (1,001 lines) against `--ignore-cr-at-eol` (29); `.gitattributes` fixes only `*.sh`. Two more files flip too (`CreateUserCommand.cs`, `CreateUserValidator.cs`).
- **Fix:** convert them back to LF before merging (and take out the stray carriage return), and add `*.md text eol=lf` (or `* text=auto`) to `.gitattributes`.
- **What happened to it:** fixed. `AGENTS.md` is back to LF, with the stray carriage return in the list of mappers removed, and `.gitattributes` has `*.md text eol=lf` (every `.md` file on `main` is already LF, so nothing else is renormalised). `CreateUserCommand.cs` and `CreateUserValidator.cs` are LF in the branch, as on `main`.

### 12. Docs that claim more than the code does

- **Severity:** low. **Confidence:** high.
- **The claims:**
  - `docs/security/asvs.md:255` (7.5.1, Met): accounts now have email and two-factor settings, changed without the password (finding 2).
  - `docs/security/asvs.md:197` (6.1.1) and `docs/security/threat-model.md:106` (T-18, "Low"): the code limits hold one request at a time only (finding 1); T-18 also says a stolen session can't regenerate recovery codes without the password, but a code is enough.
  - `docs/security/asvs.md:218` (6.4.3, Met) and `AGENTS.md:295` ("the link proves the address"): the link proves a mailbox (finding 3).
  - `docs/security/threat-model.md:117` and `:161` (T-23, risk 5): the page server's pod reaches the internet on Azure (finding 4).
  - `docs/security/asvs.md:447-449` (16.3.1 to 16.3.3, "Planned: not logged"): the activity log has recorded sign-ins since #68, and now every step here. Older than this PR, but the PR's own claims rest on it.
- **Fix:** correct each with its finding.
- **What happened to it:** fixed. ASVS 6.1.1, 6.3.1, 6.4.3, 6.4.4, 7.5.1 and 16.3.1 to 16.3.3, threat model T-16 to T-19, T-23, T-35 and risk 5, and `AGENTS.md` (confirmation needs the account signed in, password re-checks, the limits, the growing lockout, the email cap) now say what the code does.

## Remaining risks

- **Two-factor is optional:** most accounts still have only a password (DPIA residual risk high until it's required).
- **Recovery codes and authenticator keys are in clear** in `AspNetUserTokens` (recorded: ASVS 6.5.2, `docs/todo.md`).
- **A rollback silently drops two-factor:** the previous image ignores `TwoFactorEnabled`, so after a revert every account signs in with the password alone, with nothing to say so. Worth a line in the rollback notes.
- **An admin's response to a takeover** is now a password reset, a two-factor reset and removing the address (finding 2); nothing yet lists an account's recent security events for them in one place, beyond the activity log.
- **The SMTP password is an Entra client secret** that expires and works beyond SMTP; the Communication Services email SDK with the API's workload identity would need none.
- **Two pods starting together on an empty key table** may each make a key; not tested (the integration test starts its two copies one after the other).
- **Each copy counts its own limits**, so two copies allow twice the sign-in, code, address-change and reset attempts and emails an hour; the challenge's attempts and the lockout are in the database and shared.
- **Mail queued when the API stops is lost,** and nothing retries.
- **Not seen in a browser, on kind or on Azure** by this review.

## Say it like this

"MedApp can now email staff: a link to confirm their address, a link to reset a forgotten password, and a short notice whenever how they sign in changes. Staff can also add a second step at sign-in, a six-digit code from an authenticator app, with ten spare one-time codes, and admins can require it for admin accounts. Most of it is careful: the secret in each link never reaches a server log, every answer to 'forgot your password' looks the same, and nothing clinical is ever emailed. Two holes need closing first. The limit of five wrong codes is like a bouncer counting people one at a time: send thirty at once and they're all inside before the count reaches five, so someone who already has a password can guess codes about thirty times faster than designed, and most of their tries never reach the log. And anyone who gets a minute at a signed-in screen can put their own email address on that account, confirm it, and use 'forgot your password' to make the account theirs for good, with no password and, for today's accounts, no warning to anyone. Two smaller ones: a confirmation link proves only that someone reads that mailbox, so a mistyped address hands the account to a stranger; and on Azure the page server, deliberately boxed in last time, can now reach the whole internet."

## Detail

### The probes

A scratch NUnit project outside the repository, compiling `MedApp.IntegrationTests/Common/**` as it is on the branch (the real API in memory through `MedAppApiFactory`, SQL Server 2022 in Testcontainers, the database reset before each probe), with the integration tests' sign-in throttling set out of reach as they set it, which stands for an attacker with several addresses. A request the app didn't handle comes back from the test server as an exception; the probes count those as the 500s production would send.

The core of the race probe:

```csharp
var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
var codes = WrongCodes(key, 30, seed);                 // none valid in the current window
using var gate = new SemaphoreSlim(0);
var tasks = codes.Select(async (code, i) =>
{
    await gate.WaitAsync();
    return await clients[i].PostAsJsonAsync("/api/auth/login/two-factor",
        new LoginTwoFactorRequest(challenge.Challenge, code));
}).ToList();
gate.Release(codes.Count);                              // all at once
```

| Probe | Result |
|---|---|
| 30 wrong codes at once on one challenge | 30 checked (2 answered 401, 28 ended in a 500 from the activity log's save); none refused as `sign_in_expired`; failure count 2; 2 activity entries |
| 100 wrong codes at once on one challenge | 34 checked (2 × 401, 32 × 500), 66 refused as `sign_in_expired`; failure count 2; 2 activity entries |
| Waves of 29, a new challenge each, until the password step is refused | 29 checked in every wave, the count rising by 1 a wave; locked after wave 5: **145 codes checked, 5 activity entries** |
| 100 wrong codes at once to `me/two-factor/recovery-codes`, from the account's session | 40 answered 400 (checked) and 60 answered 423 once the lockout landed |
| Addresses for `PUT me/email` | `a@b.test\r\nBcc: c@d.test` 400; `x@y.test, z@w.test` 400; `"Payroll team" <x@y.test>` 202, stored as typed, mail to `x@y.test`; `<x@y.test>` 202 |
| A stolen session, no password | change address 202, confirm 204, forgot 202, reset 204, sign in with the new password 200, two-factor turned on with the attacker's app; the old password then 401; emails to anyone but the attacker: **0** |
| An admin-typed address owned by a stranger | the stranger confirms (signed out), asks for a reset by that address, resets (204), signs in (200, no forced change), searches patients (200); the admin's generated password then 401 |

Why the 500s: `AccessFailedAsync` saves through `UserStore.UpdateAsync`, which catches the concurrency failure and returns it as a result, leaving the user modified in the request's `DbContext`. The next `SaveChangesAsync` in the same request, the activity log's (`ActivityRepository.AddAsync`), sends that update again and throws `DbUpdateConcurrencyException`, which `ExceptionHandling` doesn't catch.

### What 145 codes a lockout means

Three codes are valid at any moment (the current step and one either side), so each guess has a 3 in 1,000,000 chance.

| | Codes checked per lockout | A day | Chance of a right guess a day |
|---|---:|---:|---:|
| As designed (5, then 5 minutes locked) | 5 | about 1,440 | about 0.4% (1 in 230); about 12% a month |
| One address, the per-address allowance (a wave a minute per copy, so about 10 minutes per lockout) | 145 | about 21,000 per copy, 42,000 with two copies | about 6% per copy, 12% with two; better than even within a week |
| Several addresses (no wait between waves) | 145 or more | 40,000 or more | 12% or more |

Two things make it worse for someone who also controls the account's mailbox: a reset by link clears the lockout (`PasswordResetService.cs:93-94`), and it can be asked for six times in 15 minutes (three by username, three by address).

### The API's network policy on Azure

Rendered with `deploy/azure/values.yaml` and the three values `deploy.sh` prints (as CI's new lint step sets them):

```yaml
    # The mail server, by name, so anywhere on the internet (networkPolicy.internet), but on its port only.
    - to:
        - ipBlock:
            cidr: 0.0.0.0/0
            except: [10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 100.64.0.0/10, 169.254.0.0/16]
      ports:
        - port: 587
    # Key Vault (the data protection key) and Microsoft Entra ID (signing in to it), over https ...
    - to:
        - ipBlock:
            cidr: 0.0.0.0/0
            except: [10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 100.64.0.0/10, 169.254.0.0/16]
      ports:
        - port: 443
```

The policy's `podSelector` is the API's pod, which holds both the `api` and `pages` containers.

### The CodeQL alerts

| Alert | Rule | Where | Logged value |
|---|---|---|---|
| 23, 27 | clear text storage (high), private information (medium) | `ISecurityNotices.cs:43` | the notice's enum name (`PasswordReset`, `EmailChanged`) |
| 24, 28 | the same | `LoggingSecurityEventLog.cs:16` | the event kind's enum name (`PasswordResetRequested`, `EmailConfirmed`) |
| 25, 26, 29, 30 | the same | `EmailQueue.cs:41`, `:46` | the constant subjects (`ResetPasswordSubject`, `ConfirmEmailSubject`) |

The other open alerts on the PR (2 to 14) are Trivy's, for the base image's `libc6` and `perl-base`, from before this PR.

### Questions asked of the PR, and the answers

- **Can two-factor be bypassed by a path that makes a session?** No. Only `LoginHandler` (for an account without two-factor) and `LoginTwoFactorHandler` call `CreateSessionAsync`; a reset, an email confirmation and the forced change create none; `create-first-admin` and `medapp-dev` make accounts, not sessions; session extension only extends. The ways around it are guessing (finding 1) and a session's changes (finding 2).
- **The challenge:** 256 bits from `RandomNumberGenerator`, stored only as SHA-256 (enough at that length), found through a unique index (no useful timing), bound to the user (not the browser), 5 minutes, deleted when used (`ConsumeAsync` checks the rows affected, so two right codes can't both sign in), replaced by the next password step. Attempts: finding 1.
- **The TOTP:** RFC 6238 with HMAC-SHA1, six digits, 30-second steps, one step either side; each candidate compared in constant time; replay refused by keeping the last step as a user token, saved with the user under the concurrency stamp, so two requests with the same right code can't both pass. The key is Identity's (160 bits), in clear.
- **Recovery codes:** Identity's (about 47 bits each), used up on redemption, wrong ones counted; stored in clear, recorded in ASVS 6.5.2 and `docs/todo.md`.
- **Can someone with the password lock the account?** Yes, as anyone could before by guessing passwords; challenges are replaced, so their password step also cancels a sign-in in progress.
- **The reset flow:** the same 202 and empty body whatever matched; the work differs by one token's creation and a queue write (time differences far below network noise), and each request writes one activity row, matched or not. Tokens are in the fragment, cleared from the address bar, posted in a body, never logged (records redact them), and Communication Services' click tracking is off. Links come from `Email:LinkBaseUrl`, never the Host header (the chart derives it from `ingress.host`). Single use and invalidation: the security stamp changes on any password change, email change and two-factor change. Throttle keys are trimmed, NFKC-normalised and upper-cased, per copy. A shared address gets one link per account (finding 3). The reset clears the forced change and the lockout, and ends every session.
- **Email change:** the old address stays until the link is opened, and the token is bound to the pending address, so an older link can't confirm a newer address; confirmation needs a click, so link scanners don't confirm. No pending-address takeover was found (several accounts may share an address, so there's no uniqueness to race). The weak points are findings 2 and 3.
- **The admin requirement and reset:** turning the requirement on needs the admin's own two-factor; a held admin can still enrol, so nobody is locked out; turning it off needs an admin who isn't held; a reset of one's own two-factor is refused. No escalation was found beyond finding 5.
- **Data protection keys:** readable by anyone who can read the database (the app's login, backups); on Azure wrapped by a Key Vault RSA key that only the API's identity can use, and only to wrap and unwrap, on that key alone; rotation makes new data protection keys every 90 days in the shared table, and new Key Vault key versions wrap new keys while old versions unwrap old ones; every pod shares the ring. If Key Vault is unreachable, the code shows only reset and confirmation links depending on these keys (sessions don't use data protection); not tried.
- **Emails:** no clinical text and no passwords; subjects are constants; recipients go through `MailboxAddress.Parse`, which with the validator refuses CR/LF and lists (probe); every value in the HTML is encoded.
- **Infrastructure:** Mailpit accepts SMTP only from the API's pods, has no egress, and its inbox is reachable only by port-forward. The Key Vault role is the narrowest built-in one, on one key. The custom SMTP role is the three actions Microsoft documents for the relay, assignable only in the resource group and assigned on the Communication Services resource. Egress: finding 4.
- **Accounts without email or two-factor:** sign in as before (200, the same body; `AccountWithoutTwoFactor_SignsInAsBefore`). The login page's only visible change for them is that "Forgot your password?" now shows only where email can be sent, instead of a dialog that claimed a link was sent.
- **The activity log:** no codes, tokens or addresses (checked in code and by `ActivityLog_RecordsTheChallengeTheRefusedCodeTheSignInAndTheChanges_NeverACode`); attribution: finding 7.
- **The migration:** additive and in step with the model; `Down` drops the key table (outstanding links stop working) but leaves `TwoFactorEnabled` and the tokens.
- **The tests:** thorough one request at a time; none sends requests together, none has a stranger confirm an address, and none checks that a change needs the password (findings 1 to 3).
