# Review: PR #118, two-factor codes by email

**PR:** [#118](https://github.com/Aliator/MedApp/pull/118), branch `feature/email-2fa-codes`, reviewed at `fdc1bec` against `main` at `1e1a33a` (the merge of #114).

**Reviewed:** 18 November 2026, as an adversarial security review, from the diff, the code around it, filtered test runs, small probes against the Release build, and the PR's CI run, independently of the PR description's conclusions.

**One finding is high:** CI's required `integration-tests` job fails on this commit, in 4 of the 18 new tests, so the PR can't merge as it is, and turning two-factor off or getting new recovery codes with an emailed code has never passed against a real database (finding 1). Three findings are medium and four are low. All eight have since been dealt with in the PR (each finding's "What happened to it"): seven fixed, one (finding 5) documented.

## Summary

Two-factor sign-in can now use a 6-digit code that MedApp emails, as a weaker option beside the authenticator app. The code handling itself is careful: codes come from a cryptographic generator, are stored only as a hash, are compared in constant time, work once, expire in 10 minutes, and wrong ones count exactly as wrong app codes do, decided in single SQL statements. The weak points are around the code rather than in it: the new integration tests fail in CI, the second factor can be swapped with only a session and the password, anyone can use up the hour's email allowance with reset links so no codes go out, and a resend after five wrong tries sends a code that can never work.

## What changed, and why

133 files, 7,060 lines added and 343 removed. About 2,400 of the added lines are generated (the migration's designer file, the snapshot and `schema.d.ts`, edited by hand), and about 1,900 are .NET tests.

- **Why:** an authenticator app on a personal phone isn't possible for every member of staff. Email codes give those people some second factor rather than none. The PR is explicit that they're weaker: whoever can read the mailbox has the code.
- **One method per account:** a new column, `AspNetUsers.TwoFactorMethod` (`None = 0`, `Authenticator = 1`, `Email = 2`). The migration `AddedEmailTwoFactorCodes` marks every account that already had two-factor on as `Authenticator`, and adds the `EmailCodes` table. `EffectiveTwoFactorMethod` treats "on, but no method recorded" as the app.
- **The codes** (`EmailCode`, `EmailCodes`, `EmailCodeService`, `EmailCodeRepository`):
  - 6 digits from `RandomNumberGenerator`, valid 10 minutes, stored as SHA-256 of the row's ID plus the code (a fingerprint, not the code), compared in constant time, deleted when used.
  - Three purposes: signing in (tied to one sign-in challenge), turning codes on, and confirming a change (turning two-factor off, new recovery codes).
  - Sending again: at most once a minute and three times per code, decided by one SQL `UPDATE`; at most 10 emailed codes per account in 15 minutes (`AccountAttempt.EmailCode`).
  - Tries: a sign-in code uses its challenge's five tries; the other two purposes have five tries of their own.
- **Signing in:** for an email-codes account, the password step answers 202 as before, but also emails a code and says where it went, masked (`j•••@example.com`). The challenge lasts 10 minutes instead of 5. `POST api/auth/login/two-factor/resend` sends a new code, and every account hold allows it.
- **Turning on:** the password emails a code (`POST …/me/two-factor/email`), and the code turns it on (`…/email/enable`), returns recovery codes once, ends other sessions and sends notices.
- **While on:** `POST …/email/code` emails a confirm code, which turning off and new recovery codes accept in place of an app code.
- **Switching to the app:** setting up an authenticator app is now allowed while email codes are on, and replaces them in one step, so two-factor never goes off.
- **The admin requirement now needs the app.** An admin with email codes counts as "without two-factor": they're held by `TwoFactorSetupRequired`, and they can't switch the requirement on.
- **The address:** it can't be removed while it's used for codes (a 409, for the owner and for admins).
- **Email:** a new template, `TwoFactorCode`, holds only the code, what it's for, how long it lasts and what to do if you didn't ask (no username, no link, not in the subject). A new category, `EmailCategory.Code`, goes in the notices queue but is counted against the links' share of the hourly limit.
- **Activity log:** actions 40 (`EmailCodesEnabled`) and 41 (`EmailCodesDisabled`); `TwoFactorChallenged` records `method: "email"`; `SignedIn` records `secondStep: "email"`; a switch is `TwoFactorEnabled` with `switchedFrom: "email"`. No code or address in any of them.
- **Web:** a choice between app and email on Account Settings' Security page, `EmailCodesSetup`, `EmailCodeNote` (where the code went, with a Resend countdown from the server's seconds), the email path in `TwoFactorStep`, "Email me a code" in the turn-off and new-codes dialogs, and an explanation on the held admin's page.
- **Docs:** `AGENTS.md`, the threat model (DF14, DF15, T-16, T-18, T-19, new T-52 and T-53), the ASVS checklist (6.4.3 to partly met, 6.6.2 and 6.6.3 to met), the data inventory, the DPIA, the staff privacy notice and the to-do list.

## Design choices

- **MedApp's own codes, not Identity's email token provider:**
  - **Instead:** Identity's built-in `EmailTokenProvider` (`GenerateTwoFactorTokenAsync`).
  - **Chosen because:** that provider's codes are derived from the security stamp and time, so they can't be "used once", resent as new codes, or have their tries reserved in one SQL statement.
  - **Worse at:** more hand-written rules that must agree with each other. Finding 4 is one place where they don't.
- **Six digits, hashed with SHA-256 bound to the row:**
  - **Instead:** a keyed hash (HMAC with a server secret), or a longer code.
  - **Chosen because:** six digits are easy to type from a phone's mail app; the short life and tries do the protecting.
  - **Worse at:** anyone with a copy of the database can work out a live code in a moment (one million guesses). The docs say so honestly.
- **Resend makes a new code (the old one stops working):**
  - **Instead:** send the same code again.
  - **Chosen because:** a code is never in two emails, so a stale email is worth less.
  - **Worse at:** people who type the code from the older email get "wrong code", and those wrong tries count.
- **Codes share the links' share of the hourly email limit:**
  - **Instead:** a share of their own, as notices have.
  - **Chosen because:** notices (the sign of a takeover) keep their reserve; codes go out before links in the queue.
  - **Worse at:** whatever uses up the links' share, including anonymous reset requests, also stops codes (finding 3).
- **Switching from email codes to the app in one step:**
  - **Instead:** turn email codes off (password and emailed code), then set up the app.
  - **Chosen because:** two-factor is never off in between, and a held admin can switch from the page they're sent to.
  - **Worse at:** the switch asks for less proof than turning off does: the password and a code from the new app, but nothing from the current factor (finding 2).
- **The admin requirement needs the app:**
  - **Instead:** accept email codes for admins too.
  - **Chosen because:** NIST SP 800-63B doesn't accept email as an out-of-band factor, and admins are the most valuable accounts.
  - **Worse at:** turning the requirement on holds every admin who chose email codes at once, until they set up an app.
- **Refusing to remove an address used for codes, rather than turning codes off with it:**
  - **Chosen because:** an account never quietly loses its second factor.
  - **Worse at:** in an incident (an address known to be compromised), an admin must reset the account's two-factor first, then remove the address: two steps, in that order.
- **Showing the masked address after the password:**
  - **Chosen because:** people with several mailboxes know where to look.
  - **Worse at:** someone who has only the password learns the mailbox's domain and first letter (and the whole name, for a one-letter name such as `j@x.org`).

## How it was checked

- **Read:** the whole diff, and the handlers, repositories, holds, limiter, email queue and budget, migration and web pages it touches.
- **.NET unit tests (Release, filtered):** the two-factor, email code, sign-in, limiter, email delivery, security settings and controller tests: 375 passed. `DevMcpServicesTests` and the convention tests: 8 passed.
- **Web:** Vitest on the 7 changed or new test files (`EmailCodeNote`, `EmailCodesSetup`, `TwoFactorStep`, `SecurityPage`, `RequiredTwoFactorSetupPage`, `AdminSettingsPage`, `activity`): 105 passed.
- **Migration:** `dotnet ef migrations has-pending-model-changes` (Release, no build): no changes, so the snapshot matches the model.
- **Probes** (a scratch console program against the Release build, not committed): a resend after five wrong tries (finding 4), the hourly budget (finding 3), and the integration tests' code-finding pattern (finding 1). Their output is under Detail.
- **CI on `fdc1bec`:** every job passed except `integration-tests`: 4 of 550 failed (finding 1). Read from the job's log.
- **Not run:** integration tests locally (they need Docker), a browser, E2E tests, a kind deploy.

## Findings

### 1. High: CI's integration tests fail on this commit, in 4 of the 18 new tests

- **Severity:** high: the required check is red, so the PR can't merge, and the end-to-end proof of three paths is missing. **Confidence:** high (the CI log, and a probe).
- **What goes wrong:** the tests, mostly, not the product. Two causes:
  - **The test helper keeps the line break after the code.** `FakeEmailSender.CodeIn` uses `(?m)^\d{6}\s*$`, whose `\s*` is part of the match, so on Linux it returns `"123456\n"` (probe). Sign-in and turning on still pass, because `EmailCodes.Normalise` trims. Turning off and new recovery codes fail, because `TwoFactorCodes.IsAuthenticatorCode` only removes spaces: turning off then treats it as a wrong recovery code ("That code isn't right"), and the recovery codes validator refuses it ("Enter the 6-digit code"). Failing: `TurningOff_NeedsThePasswordAndAnEmailedCode`, `NewRecoveryCodes_WithAnEmailedCode` and `ActivityLog_RecordsEmailCodesOnTheEmailStepAndOff_NeverTheCodeOrTheAddress`.
  - **An exact time against a running clock.** `SignIn_EmailsACode_ThatSignsInOnce` compares the challenge's expiry with `Api.Clock.GetUtcNow()` read after the request. `TestClock` runs on real time, so the two differ by milliseconds (expected `03:11:13.6544649`, found `03:11:13.648419`).
- **The product side, which is small:** the two code checks normalise differently. An API client that sends `"123456\n"` gets in at sign-in but is refused, and counted towards the lockout, when turning two-factor off. The web field strips line breaks, so people typing aren't affected.
- **Evidence:** `MedApp.IntegrationTests/Common/FakeEmailSender.cs:50-53`; `MedApp.IntegrationTests/Auth/EmailCodeTests.cs:92`; `MedApp.Application/Authentication/TwoFactor/Common/TwoFactorCodes.cs:96-100` against `EmailCodes.cs:145`; CI run 44226957287, job `integration-tests`.
- **Fix:**
  - capture only the digits in the helper (a group, `(?m)^(\d{6})\s*$`, or `\b\d{6}\b`);
  - use `BeCloseTo(expected, TimeSpan.FromSeconds(5))` for the expiry;
  - make `IsAuthenticatorCode` normalise as `EmailCodes.Normalise` does (one shared helper), with a unit test for a trailing line break;
  - then let CI rerun `EmailCodeTests` and `ParallelCodeTests`.
- **What happened to it:** fixed in the PR. `FakeEmailSender.CodeIn` captures only the digits; `SignIn_EmailsACode_ThatSignsInOnce` compares the expiry with `BeCloseTo` (5 seconds); and the real bug is fixed: every check now cleans a code one way, `TwoFactorCodes.Normalise` (no whitespace at all, line breaks included), which `IsAuthenticatorCode`, `EmailCodes.Normalise` and the app-code checks at sign-in and when switching all use. Unit tests: `IsAuthenticatorCode_CleansAsEmailCodesDo` and `Disable_EmailedCodeWithALineBreak_IsAcceptedAsAtSignIn` (`"654321\n"`, `"\r\n"`, tabs; they fail without the fix), and `Code_AppCodeWithALineBreak_IsCleanedAsEmailedCodesAre` for sign-in. CI reruns the integration tests.
- **Status:** fixed.

### 2. Medium: the second factor can be replaced with only a session and the password

- **Severity:** medium (it needs a live session and the password; the owner is emailed, and an admin can reset). **Confidence:** high.
- **Analogy:** changing the house's alarm code while standing inside, with only the door key: the alarm never asks for its current code.
- **What goes wrong:** someone finds a colleague's MedApp left signed in on a ward PC, and knows or has seen their password. The account uses codes by email.
  - They open Account Settings, Security, press "Switch to an Authenticator App", scan the QR code with their own phone, and type the password and their app's code.
  - Two-factor stays on, but now on their phone. The owner's other sessions end, their recovery codes stop working (new ones were made), and their next sign-in asks for an app code they don't have.
  - For an admin, the other admins aren't told: the switch skips that notice on purpose.
  - The owner gets one email, "Two-factor authentication is on for your MedApp account", saying it was turned on with an authenticator app, not that their codes by email were replaced.
  - The same session and password can instead move the codes to the attacker's own mailbox, by changing the address and opening the link in that session. That path is recorded (T-52, the to-do list); the switch isn't.
- **The inconsistency:** turning two-factor off needs the password and an emailed code or a recovery code. The switch leaves the attacker better off than turning it off would (the owner is locked out), with less proof.
- **Evidence:**
  - `EnableTwoFactorHandler.cs:37-45`: only an account already on the app is refused; then the password, and a code from the new app.
  - `EnableTwoFactorHandler.cs:68`: no notice to other admins on a switch.
  - `BeginAuthenticatorSetupHandler.cs:25` and `TwoFactorService.cs:68` allow a new key while email codes are on.
  - `ChangeMyEmailHandler.cs:38-39`: the address changes with the password alone.
  - The integration test `RequirementForAdmins_NeedsTheApp_SoAnAdminWithEmailCodesIsHeldToSwitch` switches with only the password and an app code.
  - Docs: T-19 (`docs/security/threat-model.md:110`) presents the one-step switch as a control, and ASVS 7.5.1 (`docs/security/asvs.md:255`) is marked Met. That requirement asks for full re-authentication before changing MFA settings; for these accounts, both the switch and an address change need only the password.
- **Fix:**
  - When switching from email, also require the current factor: an emailed confirm code or a recovery code (`TwoFactorCodes.VerifyOrThrowAsync` with the account's method).
  - Allow `POST api/auth/me/two-factor/email/code` under `TwoFactorSetupRequired` (the held admin signed in with an emailed code, so it reaches their own mailbox), and add an "Email me a code" step to `TwoFactorSetup` when switching.
  - Tell the other admins when an admin switches, and say "switched to an authenticator app" in the owner's notice.
  - For the address, ask for an emailed code to the current address when the method is email (the to-do already proposes it).
  - Then update T-19 and T-52, and mark ASVS 7.5.1 partly met until both are done.
- **What happened to it:** fixed in the PR. Switching from codes by email to the app (`POST …/enable`) now needs `currentCode` as well as the password and the new app's code: an emailed confirm code or a recovery code, counted as any code is (a missing one is refused before the app's code is spent). Moving the address (`PUT me/email`) needs `code` the same way while codes by email are on. `POST …/email/code` now asks for the password before it sends anything, and the `TwoFactorSetupRequired` hold allows it, so a held admin can prove the current factor and switch. A switch emails the owner ("Your MedApp sign-in codes moved to an authenticator app") and, for an admin, every other admin (`AdminTwoFactorSwitchedToApp`). The web app asks for the current code when switching and when changing the address. T-19 and T-52 describe it; ASVS 7.5.1 stays Met, now honestly, with the switch and the address named. Tests: four `EnableAuthenticator_WhileUsingEmailCodes_*` and four `ChangeMyEmail_*` unit tests, and the integration tests `RequirementForAdmins_…` (a switch without the current code is a 400, then with it the admins are told) and `TheAddress_…` (a change without a code is a 400).
- **Status:** fixed.

### 3. Medium: anyone can stop emailed codes for the rest of the hour by asking for reset links

- **Severity:** medium (no password needed; it denies sign-in to everyone using an optional method, until the next hour, and can be repeated; recovery codes and an admin reset still work). **Confidence:** high (a probe of `EmailBudget`).
- **Analogy:** one lift shared by deliveries and by people late for an appointment. Fill it with deliveries, and the people can't get on, however urgent they are.
- **What goes wrong:**
  - Someone who knows seven staff usernames (from a rota, or as a former colleague), or just one on Azure, asks "Forgot your password?" for each, three times every 15 minutes (the per-name limit).
  - Each request sends a real reset link, and each link uses a place in the hour's allowance that codes share: links and codes together may use 80 of the default 100 an hour per API copy, 7 of Azure's 8.
  - Once those are gone, every code is dropped: probe output "80 reset links sent, then a sign-in code goes: False".
  - Every person with codes by email who signs in during that hour is told "We emailed a code to j•••@…", and nothing arrives. They need a recovery code, or an admin to reset their two-factor. The attacker repeats it on the hour.
  - Codes going ahead of links in the queue doesn't help: the links have already used the hour.
- **Evidence:** `MedApp.Infrastructure/Common/Email/EmailBudget.cs:35`; `EmailQueue.cs:47-48`; `EmailCodeService.cs:76-80` (the response says sent either way); `ForgotPasswordHandler.cs:51`; `LoginThrottlingOptions.cs:32`; `deploy/azure/values.yaml:28`. T-53 (`threat-model.md:135`) considers only someone who already has the password. Hazard H-10 (`docs/clinical-safety/hazard-log.md`, "they can't sign in") doesn't list a code that never arrives.
- **Fix:**
  - Give codes a share that links can't use (for example, links capped at half of what notices leave, and codes allowed the rest plus anything links leave unused), or keep separate counts.
  - Add this attack to T-53.
  - Add "an emailed code doesn't arrive (mail delay, the hourly limit, the junk folder, mail down)" to H-10's causes, for the clinical safety officer.
- **What happened to it:** fixed in the PR. `EmailBudget` keeps a fifth of the hour for two-factor codes beside the notices' fifth; links (reset requests, which need no password) can't use it, and codes may also use what links leave (100 an hour: links 60, codes at least 20; Azure's 8: links 6, codes 1 more). `IEmailSender.HasRoomFor` lets `EmailCodeService` look first: a code that can't go now (no room, or no mail server) is neither stored nor promised, so sign-in's `emailCode` is null and the page says "We couldn't email you a code just now", and the other paths give a 409 saying so. T-53 adds the reset-link attack; H-10 lists an emailed code that doesn't arrive, with its controls, for the CSO. Tests: `Budget_ResetLinksCantUseUpTheCodesShare` (100 and 8 an hour; fails with the old budget), `Queue_HasRoomFor_LooksAtTheBudgetWithoutTakingAPlace`, `Send_EmailCantGoNow_SendsAndStoresNothing`.
- **Status:** fixed.

### 4. Medium: after five wrong codes, "Resend" emails a code that can never work

- **Severity:** medium (it misleads in a plausible situation, and for turning off or new recovery codes it locks the owner out, longer each time, with an alarming email). **Confidence:** high (read, then a probe).
- **Analogy:** cutting a new key for a lock that's already jammed: the key is right, the lock still won't turn.
- **What goes wrong:**
  - Someone turning two-factor off mistypes the emailed code five times, or keeps typing the code from an older email after a resend. Each wrong code counts towards the lockout, so the account locks for 5 minutes.
  - Afterwards they press Resend. A new code arrives, they type it correctly, and MedApp says "That code isn't right", every time: the code's row still has its five tries used up.
  - Each of those counts towards the lockout again. After five, the account locks for 10 minutes, and they get "Wrong sign-in codes locked your MedApp account… If it wasn't you, they know your password".
  - Turning codes on has the same dead code, without the lockout.
  - Each resend also pushes the dead row's expiry 10 minutes on, so no fresh code can be made for up to 13 minutes.
  - Signing in isn't affected: its tries belong to the challenge, and a used-up challenge sends people back to the password.
- **Evidence:** `EmailCodeRepository.cs:36-50` (a resend changes the hash, time and expiry, but not `FailedAttempts`), `:52-57` (no try is reserved at 5); `EmailCodeService.cs:54-56` and `:101-113` (the resend check ignores tries) and `:94-95`; `TwoFactorCodes.cs:134-140` (each counts towards the lockout). Probe: "Resend allowed: True … Newest emailed code accepted: False … the dead row lives until 10:13 (first sent 10:00)".
- **Fix:** treat a code with no tries left as not live when sending, so a new row with fresh tries is made (still within 10 emails per 15 minutes); or refuse the resend and say to wait. Add a unit test and an integration test: five wrong, resend, the right code works.
- **What happened to it:** fixed in the PR. A code that has used its five tries is no longer live (`EmailCode.IsLive`, the repository's queries and `TryResendAsync`), so asking again makes a fresh code with fresh tries, bounded as before by 10 emails per account in 15 minutes, the account's code limit and the lockout; and its expiry is no longer pushed on. `CheckAsync` now answers `Right`, `Wrong` or `NotLive`, and `NotLive` (no tries left, none sent, used meanwhile) is never counted as a wrong code: the person is told to ask for a new one. Sign-in is unchanged (a used-up challenge sends people back to the password). Tests mirror the probe: `Send_AfterFiveWrongTries_StartsAFreshCodeThatWorks` (confirm and turning on), `Disable_EmailedCodeThatCantWorkAnyMore_SaysAskForANewOne_WithoutCountingIt`, `Code_NoCodeThatCouldWork_IsRefusedWithoutCountingIt`, and the integration test `TurningOff_AfterFiveWrongCodes_ANewCodeWorks`.
- **Status:** fixed.

### 5. Low: rolling the image back and forward again can turn an app back into codes by email

- **Severity:** low (it needs a rollback, then a change to someone's two-factor, then a roll forward). **Confidence:** high (read).
- **What goes wrong:**
  - Someone turns on codes by email, so their `TwoFactorMethod` is 2.
  - The image is rolled back. The older image's migrations bundle doesn't migrate down, so the column stays, and the old code ignores it.
  - On the old version they can't give an app code, so an admin resets their two-factor (clearing `TwoFactorEnabled`, not the column), and they set up an app (`TwoFactorEnabled` is 1 again, the column still 2).
  - Rolled forward, `EffectiveTwoFactorMethod` reads "email": sign-in emails codes and ignores their app, a silent move to the weaker method, and an admin among them is held.
- **Evidence:** `ApplicationUser.cs:41-44`; `TwoFactorService.cs:151, 174`; the migration's `Down` comment (`20261118021716_AddedEmailTwoFactorCodes.cs:74-76`) covers migrating down, not an image rollback.
- **Fix:** say this in the rollback notes, with a check to run after rolling forward: accounts with `TwoFactorEnabled = 1`, `TwoFactorMethod = 2` and an authenticator key, to look at by hand (a switch in progress looks the same, so not an automatic fix).
- **What happened to it:** documented, not changed: the stored method can't tell an app set up on the older version from a switch in progress. `AGENTS.md` (two-factor) gives the check to run after rolling forward (accounts with `TwoFactorEnabled = 1` and `TwoFactorMethod = 2` that use an app: reset their two-factor), and `docs/todo.md` points to it.
- **Status:** documented.

### 6. Low: two small races when turning codes on

- **Severity:** low (each needs two things at once, mostly self-inflicted, and is recoverable). **Confidence:** high (read).
- **The address can go between "Email me a code" and the code.** Removing it is refused only once the method is email, and turning on doesn't check the address again. Result: codes by email with no address, so sign-in says "We couldn't email you a code" and needs a recovery code. Evidence: `RemoveMyEmailHandler.cs:31`; `EnableEmailCodesHandler.cs:35-43`.
- **Two first sends at once leave two live codes.** Both delete and insert, so two emails go out; only the newer code is checked, and the other email's code is "wrong" (counted, for confirming). `AGENTS.md` and `IEmailCodeRepository` say one live code per purpose, decided by one statement; that's true for resends only. Evidence: `EmailCodeRepository.cs:24-34`.
- **Fix:** check `EmailCodesAvailability.WhyNotAsync` again in `EnableEmailCodesHandler`; make the first send atomic (a filtered unique index on user and purpose for the two non-sign-in purposes), or say in the docs that it isn't.
- **What happened to it:** fixed in the PR. Turning codes on checks the address again (`EnableEmailCodesHandler`: a 409 if it's gone or email is unavailable), and a turn-on code's hash is bound to the address it was sent to, so it works only while that's still the confirmed address. A new migration, `MadeEmailCodesOnePerPurpose`, makes `IX_EmailCodes_UserId_Purpose` unique outside sign-in (after deleting any such codes there, which last 10 minutes); `AddAsync` deletes only codes that can't work any more, and a second first send at the same moment is refused (a 429, no email). Tested up and down on LocalDB (two codes for one purpose before: none after; a second insert refused with 2601; the old index back after the rollback). Tests: `EnableEmail_AddressRemovedSinceTheCodeWasSent_IsAConflict`, `Check_TurningOn_TheCodeWorksOnlyForTheAddressItWentTo`, `Send_TwoFirstSendsAtOnce_OnlyOneStands`, and the integration test `TurningOn_TwoFirstSendsAtOnce_OnlyOneCodeStands`.
- **Status:** fixed.

### 7. Low: the page and the email can promise more than is true

- **Severity:** low. **Confidence:** high (read).
- **"We emailed a code" when nothing was sent.** `EmailCodeService.SendAsync` doesn't check `sender.CanSend`. With no mail server configured, the queue drops the email, logged only at Debug, and sign-in still answers with the masked address. The same happens when the hourly limit drops it (finding 3). Evidence: `EmailCodeService.cs:49-51`; `EmailQueue.cs:41-45`.
- **"It works for 10 minutes", when the sign-in may end sooner.** The sign-in itself ends 10 minutes after the password step, so a code resent at minute 8 works for 2. Evidence: `EmailTemplates.cs:75`; `ResendSignInCodeHandler.cs:29`; `SignInChallenge.EmailLifetime`.
- **Fix:** return null when email can't be sent (the page then shows "We couldn't email you a code"); for sign-in codes, give the template the time the sign-in has left.
- **What happened to it:** fixed in the PR. With no mail server (or no room) nothing is promised (finding 3), and Account Settings already offers codes by email only when the server can send email. A sign-in code ends with its challenge (`EmailCode.ExpiryFor`, resends too), and its email says how long it really works, in whole minutes rounded down ("for 4 minutes", "for 1 minute"). Tests: `Send_ForASignInEndingSoon_TheCodeAndItsEmailEndWithIt`, `TwoFactorCode_SaysHowLongItWorks_NeverMore`, and the resend test checks the challenge's expiry is passed on.
- **Status:** fixed.

### 8. Low: documents that don't quite match

- **Severity:** low. **Confidence:** high for the first two; medium for the ASVS reference.
- **The accepted risk is recorded as undecided.** The PR says the product owner accepted T-52 (for these accounts, a reset link and a code reach the same mailbox) on 18 November 2026. T-52 and `docs/todo.md:54` still say it's "for the product owner to decide". The description of the risk itself is accurate: a reset keeps two-factor on, the next code goes to the same mailbox, and the owner's notice goes there too. Record who accepted it, when, and when to look again; consider marking 6.4.3's gap as an accepted risk.
- **Hazard H-10** (a member of staff can't sign in) isn't updated with the new cause (finding 3). Its controls (recovery codes, an admin reset) still apply.
- **ASVS 5.0 says, at Level 3, that email shouldn't be an authentication factor at all** (6.3.6, as we read it). MedApp aims at Level 2, so it's outside the checklist; the 6.3.3 row could say so, to stay honest about where codes by email stand.
- **What happened to it:** fixed in the PR. T-52, ASVS 6.4.3 and `docs/todo.md` record that the product owner accepted the reset-link risk on 18 November 2026, to be looked at again before first live use; H-10 has the new cause and its controls (hazard log 0.15); the ASVS "Beyond Level 2" list names 6.3.6 (Level 3, as we read it: email shouldn't be an authentication factor) and says codes by email wouldn't meet it.
- **Status:** fixed.

### The browser check, after this review

The PR's browser check found the whole flow working (axe clean) with two things to change, both dealt with in the PR:

- **The recovery codes said "If you lose your authenticator app…" after turning on codes by email.** **What happened to it:** fixed: `RecoveryCodes` takes the method, and for codes by email says "If you can't get MedApp's emails…", on screen and in the downloaded file (`RecoveryCodes.test.tsx`).
- **"Email me a code" in the Turn Off dialog sent before the password was entered,** while setting codes up asked for it first. **What happened to it:** fixed: `POST …/email/code` now takes the password and checks it first, and the new `EmailMeACode` (used by Turn Off, New Recovery Codes, switching to the app and the Email Address page) waits for the password and sends it (`EmailMeACode.test.tsx`).

## Remaining risks

- **Codes by email are weaker by design.** Whoever controls the mailbox controls the account (T-52, accepted). A shared or forwarded mailbox makes that worse.
- **Each API copy counts its own limits** (emailed codes, code checks, the hourly email budget), so two copies allow twice as many.
- **Azure's managed email domain** (8 an hour) is too small for codes by email; a custom domain with a bigger allowance is needed first (documented).
- **A copy of the database reveals live codes** (six digits, hashed without a secret); only their 10-minute life protects them. Recovery codes are still stored in clear (ASVS 6.5.2).
- **Not yet looked at:** no browser check (the PR says "before merging"), no E2E journey, and no demo data account with codes by email.
- **`schema.d.ts` was edited by hand,** not generated from the running API (`npm run gen:api`). The type check passes, but nothing compares it with what the API actually sends; regenerate it before merging.

## Say it like this

"We added a second, weaker two-factor option: a six-digit code by email, stored only as a hash, single-use, ten minutes, with the same attempt limits as the authenticator app. The review found the code handling solid but the edges weak: switching to an app didn't ask for the current code, reset-link requests could use up the email allowance codes depend on, and a resend after five wrong tries sent a dead code. The trade-off we accepted knowingly is that for these accounts the mailbox alone can get in, because the reset link and the code go to the same place."

## Follow-up review

**Commit reviewed:** `c8fe7dd` (the fixes in `e000036` and `8f444c1`, `main` merged in at `9abcf10`, and a test fix), against `main` at `1b00eae` (the merge of #115). **Date:** 18 November 2026.

**Nothing high or critical.** All eight findings are dealt with: seven fixed, one documented as agreed. Five new findings, all low.

**How it was checked:** read the fix commits and the code around them; compared each conflicted file of the last merge with both parents; ran filtered unit tests in Release (the email code service, its handlers, email delivery, sign-in with codes, the code email, two-factor and email handlers): 249 passed. No Docker, browser or full suite.

### The original findings

- **1. Integration tests failing: fixed.** `FakeEmailSender.CodeIn` now captures only the digits. The real bug is fixed too: one cleaning rule, `TwoFactorCodes.Normalise` (`TwoFactorCodes.cs:34-35`), used by `IsAuthenticatorCode` (`:38-42`), `EmailCodes.Normalise`, sign-in (`LoginTwoFactorHandler.cs:113`) and the switch (`EnableTwoFactorHandler.cs:82`).
  - **The later CI failure:** `TurningOff_AfterFiveWrongCodes_ANewCodeWorks` got a 423, because Identity's own lockout check reads the real clock, so moving the test clock didn't end the lockout. `c8fe7dd` ends it in SQL instead (`EmailCodeTests.cs:306`, a parameterised `FormattableString`, so no injection).
  - **Does the test still guard finding 4?** Yes. Without the fix it would still fail, now as a 429 ("too soon", since the clock no longer moves on) rather than a dead code.
- **2. Second factor replaced with a session and the password: fixed.**
  - The switch needs `CurrentCode` (`EnableTwoFactorHandler.cs:79-88`), checked as any code is, recovery codes allowed.
  - Moving the address needs `Code` while the method is email (`ChangeMyEmailHandler.cs:50-58`).
  - Asking for a confirm code needs the password (`SendTwoFactorEmailCodeHandler.cs`), and the hold allows it (`TwoFactorSetupRequired.cs:38`).
  - The owner and other admins are told of a switch (`EnableTwoFactorHandler.cs:109-115`).
  - No other path changes the factor with less: removing the address is still refused while it's used for codes, and moving from the app to email is still a 409. New finding 10 is a small side effect.
- **3. Reset links using up the codes' email allowance: fixed.**
  - Codes have their own fifth (`EmailBudget.cs:30, 57-62`): links stop at 60 of 100 (6 of 8), codes at 80 (7 of 8). Anonymous reset requests can't reach the codes' share, and nothing anonymous sends notices.
  - `HasRoomFor` (`EmailQueue.cs:36`) is checked before a code is made (`EmailCodeService.cs:79`).
  - **Left:** it's a look, not a reservation, so two codes racing for the last place can still have one dropped after it's promised. Rare, logged, and said in `IEmailSender.cs`.
- **4. A dead code after five wrong tries: fixed.** A code out of tries isn't live (`EmailCode.cs:90`; `EmailCodeRepository.cs:22, 59, 72`), so asking again makes a fresh row, and `AddAsync` deletes the dead one first (`:30-35`). `NotLive` is never counted as a guess (`TwoFactorCodes.cs:76-77`; `LoginTwoFactorHandler.cs:103-107`). That's safe: with no live code, no guess can succeed.
- **5. Rollback and roll forward mixing up the method: documented, as agreed.** `AGENTS.md` (two-factor) gives the check to run; `docs/todo.md` points to it.
- **6. Two races when turning codes on: fixed, one narrowed.**
  - **Two first sends:** a filtered unique index (`EmailCodeConfiguration.cs:37-39`) refuses the second insert, which becomes a 429 (`EmailCodeService.cs:97-98`).
  - **The migration `MadeEmailCodesOnePerPurpose`:** `Up` deletes only codes outside sign-in (which last minutes) before making the index unique; `Down` puts the plain index back; the snapshot matches the configuration.
  - **The address:** checked again (`EnableEmailCodesHandler.cs:45-46`), and a turn-on code is bound to the address it went to (`EmailCodes.cs:215-219`). A window of milliseconds remains between that check and the save, which only a removal at that instant could hit. Not worth more.
- **7. Promising more than is true: fixed.** No code is made or promised when it can't go (finding 3). A sign-in code ends with its challenge (`LoginHandler.cs:80`, `ResendSignInCodeHandler.cs:39`, `EmailCode.ExpiryFor`), and the email rounds down (`EmailTemplates.cs:67`).
- **8. Documents: fixed.** T-52, ASVS 6.4.3 and `docs/todo.md` record the acceptance; H-10 has the new cause (hazard log 0.15); ASVS lists 6.3.6 under "Beyond Level 2". ASVS 7.5.1 now names the switch and the address, which matches the code.
- **The merges from `main`: nothing lost.**
  - In every conflicted file (`docs/reviews/README.md`, `schema.d.ts`, the hazard log, the data inventory, the DPIA, the staff privacy notice), both sides' changes are there, and the line counts add up.
  - Version numbers continue in order: hazard log 0.14 is #115's, 0.15 this PR's; DPIA 0.11 and 0.12; inventory 0.9 and 0.10; notice 0.4 and 0.5.
  - Stored numbers don't clash: `ActivityAction` 40 and 41 are new here, and `main` added none; `main`'s `NotificationKind` 18 to 21 aren't touched here.

### 9. Low: the first migration now sorts before one already on `main`

- **Severity:** low. **Confidence:** high that the order is wrong; medium on how EF handles it (it applies migrations missing from the history, whatever their place).
- **What goes wrong:**
  - `AddedEmailTwoFactorCodes` (`20261118021716`) sorts before `main`'s `AddedNoteDeletion` (`20261118025735`), which databases on `main` (kind, through Argo CD) may already have. So the history isn't linear any more, against `AGENTS.md` (Migrations).
  - On deploy it's applied after a later migration, which works.
  - **The trap is rolling back:** `database update AddedEmailTwoFactorCodes` also reverts `AddedNoteDeletion`, dropping note deletion data; `database update AddedNoteDeletion` (to undo this PR) leaves the codes table and column in place.
- **Evidence:** `MedApp.Infrastructure/Migrations/20261118021716_AddedEmailTwoFactorCodes.Designer.cs:15` against `20261118025735_AddedNoteDeletion.Designer.cs:15`.
- **Fix:** before merging, give the PR's two migrations IDs after `AddedNoteDeletion` (rename the files and the `[Migration("…")]` IDs, or remove them and scaffold again on top of `main`), then check `has-pending-model-changes`. They've been applied only to local databases, whose `__EFMigrationsHistory` rows would need the same rename. If that's not worth it, say in the rollback notes which target undoes what.
- **What happened to it:** Not yet.

### 10. Low: a mistyped current code spends the new app's code

- **Severity:** low (it confuses; it doesn't weaken anything). **Confidence:** high.
- **What goes wrong:**
  - Someone switching from codes by email types the app's code right but the emailed code wrong.
  - The app's code is checked first, and checking it records its time step as used (`TwoFactorService.cs:100-105`); then the emailed code fails.
  - They correct the emailed code and press the button again within 30 seconds. Now the app's code is refused as a replay, with "Check that your phone's clock is set automatically", which isn't the problem.
- **Evidence:** `EnableTwoFactorHandler.cs:82-88`.
- **Fix:** check the app's code without recording it, then the current factor, then record the step in the same transaction as `EnableAsync` (a `MatchesAuthenticatorCodeAsync` beside `VerifyAuthenticatorCodeAsync`). Checking the current factor first instead would be worse: a wrong app code would then spend a recovery code.
- **What happened to it:** Not yet.

### 11. Low: Email Address and the switch can hide "enter a code"

- **Severity:** low. **Confidence:** high (read).
- **What goes wrong:**
  - The code field shows only once the two-factor status has loaded and says email (`EmailPage`'s `usesEmailCodes`; `TwoFactorSetup`'s `switchingFromEmail`).
  - If that status is still loading, failed, or out of date (codes turned on in another tab), the form goes without a code, and the API answers 400 on `code` (or `currentCode`).
  - That field is in the list of fields with their own place on the page, so the alert leaves its message out, and there's no field to show it on. The person sees only the alert's general title, not that a code is needed.
- **Evidence:** `MedApp.Web/src/pages/account/EmailPage.tsx:37, 143`; `MedApp.Web/src/components/account/TwoFactorSetup.tsx:86, 179`; `MedApp.Web/src/components/shared/AlertMessage.tsx:54-58`.
- **Fix:** leave the code field out of the list while it isn't shown, so its message appears in the alert, and refetch `twoFactorStatusQuery` on that 400:

  ```diff
  -      <ValidationAlert errors={validation} fields={CHANGE_FIELDS} />
  +      <ValidationAlert errors={validation} fields={usesEmailCodes ? CHANGE_FIELDS : ['email', 'password']} />
  ```

  and the same for `currentCode` in `TwoFactorSetup`.
- **What happened to it:** Not yet.

### 12. Low: during a rolling update, old copies ignore the method

- **Severity:** low (minutes, and only for someone who turns codes on during them). **Confidence:** high (read).
- **What goes wrong:**
  - The migrations Job runs before the upgrade (`pre-upgrade`), and old pods (`main`'s image) serve beside new ones for a few minutes. Old code doesn't know `TwoFactorMethod`.
  - An account that turns codes by email on in that time is asked for an app code by an old pod at sign-in (a recovery code still works).
  - An old pod also lets the address be changed with the password alone, or removed while it's used for codes: findings 2 and 6, on the old code.
- **Evidence:** `deploy/helm/medapp/templates/migrations-job.yaml:14`; `main`'s `ChangeMyEmailHandler`, `RemoveMyEmailHandler` and `LoginHandler` have no method checks.
- **Fix:** none needed in code. Add a line to the rollout notes: don't turn on codes by email until the rollout has finished; afterwards, look for accounts using codes by email with no confirmed address.
- **What happened to it:** Not yet.

### 13. Low: `AGENTS.md` doesn't list the new sign-in failure reason

- **Severity:** low. **Confidence:** high.
- **What goes wrong:** a refused sign-in with no live emailed code is logged with `reason: "CodeNotLive"` (`LoginTwoFactorHandler.cs:105`). `AGENTS.md` (two-factor in the activity log) lists every other reason but this one, so someone reading the log, or adding a reason, doesn't know it exists.
- **Evidence:** `AGENTS.md:452`.
- **Fix:** add `` `CodeNotLive` (no emailed code that could work: none sent, or used meanwhile; not counted) `` to the list.
- **What happened to it:** Not yet.

## Detail

### The probes

A scratch console program referencing the Release build of `MedApp.Application`, `MedApp.Domain` and `MedApp.Infrastructure`, with in-memory fakes that apply `EmailCodeRepository`'s rules as its statements do (the same as `EmailCodeServiceTests.FakeEmailCodes`). Output:

```text
After 5 wrong: attempts=5
Resend allowed: True, emails sent: 2, attempts on row: 5
Newest emailed code accepted: False
After all resends the dead row lives until 10:13 (first sent 10:00)
MaxPerHour 100: 80 reset links sent, then a sign-in code goes: False; a notice: True
MaxPerHour 8: 7 reset links sent, then a sign-in code goes: False; a notice: True
Test helper takes "123456\n": EmailCodes.IsWellFormed=True, TwoFactorCodes.IsAuthenticatorCode=False
Mask of j@x.org: j•••@x.org
```

The helper line used Linux line endings (`\n`), as CI's runner does.

### The CI failures, from the log

```text
Failed ActivityLog_RecordsEmailCodesOnTheEmailStepAndOff_NeverTheCodeOrTheAddress
  POST /api/auth/me/two-factor/disable answered 400: "That code isn't right. Use the code in the newest email from MedApp, or ask for a new one."
Failed NewRecoveryCodes_WithAnEmailedCode
  POST /api/auth/me/two-factor/recovery-codes answered 400: "Enter the 6-digit code from your authenticator app or email."
Failed SignIn_EmailsACode_ThatSignsInOnce
  Expected challenge.ExpiresAtUtc to be <2026-11-18 03:11:13.6544649>, but found <2026-11-18 03:11:13.648419>.
Failed TurningOff_NeedsThePasswordAndAnEmailedCode
  POST /api/auth/me/two-factor/disable answered 400: "That code isn't right. …"
Total tests: 550, Passed: 546, Failed: 4
```

### Questions asked of the PR, and the answers

- **Does anything reveal, before the password is right, whether an account uses email codes or has an address?** No. The password is checked first; the method, the challenge and the masked address come only with a right password. The resend endpoint needs the challenge (256 random bits). Email sending is queued, so timing doesn't reveal it either.
- **Can codes sent at once beat the limits?** Not for checking: sign-in reserves a challenge try in one statement before the code is checked, and turning on and confirming reserve one of the code's tries the same way. Resends are one `UPDATE`. Only the first send isn't atomic (finding 6).
- **Can two-factor be turned off or weakened without the second factor?** Turning off: no (password and emailed code or recovery code). Replacing it: yes, with a session and the password (finding 2). Moving from the app to email isn't possible in one step (409).
- **Is the code ever logged?** No. `EmailMessage`, `LoginOutcome`, the new commands and the new requests that carry a code hide it from `ToString()`; the activity log and security events hold only the method; the email queue logs only the category. The integration test checks the activity log never holds the code or the address. (The older `LoginTwoFactorRequest` would print its challenge and code if anything logged it; nothing does.)
- **Does the email carry anything else?** No: the code, its purpose, "works once, for 10 minutes", and what to do if you didn't ask. No username, no link, no clinical text; every value is HTML-encoded.
- **The migration:** additions only, plus one `UPDATE` marking existing two-factor accounts as app users. `Down` drops both, leaving email-code accounts on, asked for an app code, so they fail closed onto recovery codes (documented). The model and snapshot agree.
- **The ASVS counts** add up: 67 not applicable (33 + 1 + 7 + 7 + 19); of 186 that apply, 111 met (60%), 56 partly, 17 planned and 2 accepted; V6 is 18, 8, 4, 0 and 5, 35 in all. All three changed rows are Level 2.
