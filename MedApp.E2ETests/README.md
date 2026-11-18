# MedApp.E2ETests

End-to-end tests for the React app (`MedApp.Web`), written with [Playwright for .NET](https://playwright.dev/dotnet/) on NUnit, the same stack as `MedApp.UnitTests`. They drive the app in Chromium as a person would, against a running MedApp: the AppHost locally, and the production image in CI.

## What they cover

| Test | Journey |
|---|---|
| `Auth/SignInTests` | Signing in and out through the login page, and a wrong password ("Invalid credentials"). |
| `Patients/PatientTests` | Adding a patient through the form and finding them with the filter bar; editing; deleting, confirmed and cancelled. |
| `Pathways/PathwayTests` | A new pathway for a patient from a template, then its stages worked: notes, Finish Stage, the status from Unassigned to In progress to In review. |
| `Tasks/MyTasksTests` | A pathway assigned to you in My Worklist, under Not started, then Started, and opening it. |
| `Auth/TwoFactorTests` | Two-factor authentication: a member of staff sets up an authenticator app in Account Settings, Security (the key is read from the page, and codes made from it as an app makes them), sees their recovery codes once, then signs in again with their password and a code. Needs no email, so it runs against the production image as CI starts it. |
| `Users/UserTests` | A new user from start to finish: the admin adds them (the one-time password dialog) and gives them a role; they sign in with that password, land on `/change-password`, choose their own and reach the app. Deleting a user, confirmed with your password. |
| `Calendar/BookingTests` | Booking an appointment from the patient page (with you, 30 minutes, the first free day and time), finding it on your Calendar that day, opening it and cancelling it with a reason. |
| `Calendar/MeetingTests` | Arranging a meeting: the admin opens the Calendar's team view narrowed to themselves and a new member of staff, takes the first suggested time in New Meeting, finds the meeting on the member of staff's calendar that day, and cancels it there. |
| `Calendar/BlockTimeTests` | Selecting time with the mouse: dragging over free time on your Calendar (an hour, three weeks ahead), the menu that opens staying open, Block time blocking exactly that hour, and deleting the block again. jsdom can't show the menu closing itself, so this is the test for it. |
| `Calendar/StageVisitTests` | The visit rule: a stage that requires a visit shows "Requires appointment"; a senior clinician (made by the test, in a browser of their own: the built-in Admin role can't finish a stage without its visit) clicks Finish Stage, which asks "Finish without a visit?" with a reason; the stage finishes, its requested visit comes off Requires Appointment, and the patient's timeline says it was finished without a visit, and why. |
| `Referrals/TriageTests` | Triage: the E2E admin registers a referral; a new Clinician of its specialty, in a browser of their own, accepts it, choosing the template, priority and target date (none prefilled), checks the patient, specialty and template, and lands on the new pathway, which links back to the referral; the admin is told. |
| `Letters/LetterTests` | A letter from start to sent: a new Clinician, in a browser of their own, writes one on a pathway from a template (Write Letter, nothing pre-selected), adds to its body and saves, approves it themselves (the check shows the patient and the recipients; the PDF is made), and records it as posted to each recipient. |
| `Notifications/NotificationBellTests` | Another admin assigns you a pathway: the bell's count goes up live (SignalR, no reload), and the notification opens the pathway and is marked read. |
| `Security/ContentSecurityPolicyTests` | Each main page (home, patients, pathways, My Worklist, account, users, insights) is rendered on the server with its own response's CSP nonce, which the API passes to the page server, in its container or beside it. Skipped against the development server, which sends no policy. |

## What's here

| File | Purpose |
|---|---|
| `Common/E2ETest.cs` | Base class. Each test gets a fresh page at the base URL: desktop 1440×900, `en-GB`, `Europe/London`, signed in. Override `IsSignedIn` to start signed out. It fails any test whose pages break the Content Security Policy (a `securitypolicyviolation` listener on every page, reported through the console), so a refused script or connection fails the suite against the production image even when the journey still works. It also gives the test the API (`Api`), its clean-up list (`Data`), and helpers to make patients, templates and pathways. |
| `Common/E2ESession.cs` | Signs in once per run through `POST /api/auth/login`, saves the session for every signed-in test, and signs it out at the end. |
| `Common/E2EApi.cs` | The app's API, for a test's setup, its checks and its clean-up. |
| `Common/E2EData.cs` | Unique synthetic names, and the clean-ups a test registers, run newest first when it ends, pass or fail. |
| `Common/E2ESettings.cs` | Reads the environment variables below. |

## Settings

| Variable | Default | Purpose |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost:5173` | The app under test (the React dev server the AppHost runs) |
| `E2E_USERNAME` / `E2E_PASSWORD` | none | An **admin** account (Users and pathways' admin actions need one). Without them the suite is **skipped** locally but **fails** in CI (where `CI=true`), so a green CI run always ran the journeys. |
| `E2E_TRACE_DIR` | none | Saves a Playwright trace (screenshots, DOM, network) of each failed test there. A trace records what was typed, passwords included, so only CI's throwaway run (`main.yml`) sets it; never set it against a deployment. Open one with `pwsh bin/Release/net10.0/playwright.ps1 show-trace <file>.zip`. |

Never commit credentials. Locally, use the dev-only `admin` / `admin` login that `medapp-dev` makes (it exists only in local dev databases).

## Running locally

```bash
dotnet build MedApp.E2ETests
pwsh MedApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium   # once (Windows PowerShell works too)
dotnet run --project MedApp.AppHost                                       # in another terminal
E2E_USERNAME=admin E2E_PASSWORD=admin dotnet test MedApp.E2ETests
```

The whole suite takes about a minute. The tests write to the database the app uses (locally, your dev database) and delete what they made. What the app has no way to delete stays:
- the audit log entries of the pathways they made (by design: the audit log outlives deletes)
- the notification the bell test sends (marked read)
- the session rows of the accounts they made and deleted (none can be used: their users are gone)
- nothing from the meeting journey: it cancels its meeting, and its clean-up cancels it too if the test fails first
- the triage journey's referral and its `E2E …` patient: referrals are never deleted, and a patient with one can't be (its pathway is deleted; its "Referral triaged" notification is marked read)
- the letter journey's letter, its pathway (cancelled: a pathway with letters can't be deleted, as letters are clinical correspondence) and its `E2E …` patient, and its letter template (archived: one a letter was written from can't be deleted)
- the appointments they made, cancelled (the app never deletes appointments), with their history, and so the `E2E …` patients they were for: a patient who has had an appointment can't be deleted

A run that's cancelled or crashes before its clean-up (against a deployment, through `e2e.yml`, say) can also leave what it made: `E2E …` patients, stages and templates, and `e2e-…` accounts, one of them an admin. Their passwords are random for the run, but delete them by hand.

## Writing a test

```csharp
public sealed class PatientTests : E2ETest
{
    [Test]
    public async Task EditPatient_ChangedFields_AreSaved()
    {
        var patient = await CreatePatientAsync();          // through the API, deleted after the test
        await OpenAsync($"/patients/{patient.Id}/edit");   // waits until the page can be used

        await Main.GetByLabel("Last Name").FillAsync(E2EData.UniqueName("Renamed"));
        // ...
    }
}
```

- **Open pages with `OpenAsync`.** Pages are rendered on the server, then taken over by React in the browser; typing or clicking before that is lost. After `Page.ReloadAsync`, call `WaitUntilInteractiveAsync`.
- **Find things by role and label** (`GetByRole`, `GetByLabel`), with the names people and screen readers get.
- **Make the data you need through `Api`**, with names from `E2EData.UniqueName`, and register its clean-up with `Data.OnCleanUp` straight after making each thing. Things a test makes through the UI need one too (find them by their unique name).
- **Rely on nothing you didn't make**, except the signed-in account and the seeded default stage definition. Other data may be there, so filter lists down to your own rows rather than counting everything.
- **Use synthetic data only.** This is a medical app: never realistic patient details.
- An account an admin makes must change its generated password before anything else (`UpdateOwnPasswordAsync`), as `NotificationBellTests` does for its second admin.
- **Waiting for a save:** a dialog that's saving is an `alertdialog`, not a `dialog`. Our dialogs set Astryx's `purpose="required"` while they save (so Escape and a click outside can't close them), and Astryx gives such a dialog the role `alertdialog`. So `Expect(dialog).ToBeHiddenAsync()` on a `GetByRole(AriaRole.Dialog, ...)` locator passes the moment Save is pressed, before the request has finished. Follow it with something that waits for the result on the page (`ToBeVisibleAsync`, `ToContainTextAsync`), or, before reading the API, wrap the click in `Page.RunAndWaitForResponseAsync` for the request and check its status, as `MeetingTests` does.

## CI

- **On `main` after every merge, nightly, and on a pull request labelled `ci:full`** (`AGENTS.md`, CI): the `e2e` job in `.github/workflows/main.yml` builds the production image, starts SQL Server (on the runner's loopback, with a random password), migrates with the image's `/app/efbundle`, makes the first admin (`create-first-admin`, with a password generated for the run), starts the API on http://localhost:8080 under the chart's restrictions (read-only root, `/tmp` only, no capabilities) and runs these tests against it. A failed test's trace is uploaded as the `e2e-traces` artifact. The job is a required check on `main`. The session cookie is `Secure` there, as in production; Chromium keeps Secure cookies from `http://localhost`, so nothing is relaxed for the test.
- **By hand:** `.github/workflows/e2e.yml` (**Actions → E2E tests → Run workflow**) runs them against a deployed URL you give it, with the credentials from the `E2E_USERNAME` / `E2E_PASSWORD` repository secrets (an admin). **Those secrets don't exist yet: create them (Settings → Secrets and variables → Actions) for the deployment you mean to test before you run it.** Without them the run fails. The tests change that deployment's data and clean up as they do locally (see above for what stays). This workflow saves no traces, as they would record the real credentials.
