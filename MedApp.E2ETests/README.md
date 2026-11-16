# MedApp.E2ETests

End-to-end tests for the React app (`MedApp.Web`), written with [Playwright for .NET](https://playwright.dev/dotnet/) on NUnit, the same stack as `MedApp.UnitTests`. They drive the app in Chromium as a person would, against a running MedApp: the AppHost locally, and the production image in CI.

## What they cover

| Test | Journey |
|---|---|
| `Auth/SignInTests` | Signing in and out through the login page, and a wrong password ("Invalid credentials"). |
| `Patients/PatientTests` | Adding a patient through the form and finding them with the filter bar; editing; deleting, confirmed and cancelled. |
| `Procedures/ProcedureTests` | A new procedure for a patient from a template, then its stages worked: notes, Finish Stage, the status from Unassigned to In progress to In review. |
| `Tasks/MyTasksTests` | A procedure assigned to you in My Tasks, under Not started, then Started, and opening it. |
| `Users/UserTests` | Adding a user: the one-time password dialog, and that the password shown signs in. Deleting a user, confirmed with your password. |
| `Notifications/NotificationBellTests` | Another admin assigns you a procedure: the bell's count goes up live (SignalR, no reload), and the notification opens the procedure and is marked read. |

## What's here

| File | Purpose |
|---|---|
| `Common/E2ETest.cs` | Base class. Each test gets a fresh page at the base URL: desktop 1440×900, `en-GB`, `Europe/London`, signed in. Override `IsSignedIn` to start signed out. It also gives the test the API (`Api`), its clean-up list (`Data`), and helpers to make patients, templates and procedures. |
| `Common/E2ESession.cs` | Signs in once per run through `POST /api/auth/login`, saves the session for every signed-in test, and signs it out at the end. |
| `Common/E2EApi.cs` | The app's API, for a test's setup, its checks and its clean-up. |
| `Common/E2EData.cs` | Unique synthetic names, and the clean-ups a test registers, run newest first when it ends, pass or fail. |
| `Common/E2ESettings.cs` | Reads the environment variables below. |

## Settings

| Variable | Default | Purpose |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost:5173` | The app under test (the React dev server the AppHost runs) |
| `E2E_USERNAME` / `E2E_PASSWORD` | none | An **admin** account (Users and procedures' admin actions need one). Without them, the run is skipped. |

Never commit credentials. Locally, use the dev-only `admin` / `admin` login that `medapp-dev` makes (it exists only in local dev databases).

## Running locally

```bash
dotnet build MedApp.E2ETests
pwsh MedApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium   # once (Windows PowerShell works too)
dotnet run --project MedApp.AppHost                                       # in another terminal
E2E_USERNAME=admin E2E_PASSWORD=admin dotnet test MedApp.E2ETests
```

The whole suite takes about a minute. The tests write to the database the app uses (locally, your dev database) and delete what they made. What the app has no way to delete stays: the audit log entries of the procedures they made, and the notification the bell test sends (marked read).

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

## CI

- **Every pull request:** the `e2e` job in `.github/workflows/main.yml` builds the production image, runs SQL Server as a service container, migrates with the image's `/app/efbundle`, makes the first admin (`create-first-admin`, with a password generated for the run), starts the API on http://localhost:8080 and runs these tests against it. The session cookie is `Secure` there, as in production; Chromium keeps Secure cookies from `http://localhost`, so nothing is relaxed for the test.
- **By hand:** `.github/workflows/e2e.yml` (**Actions → E2E tests → Run workflow**) runs them against a deployed URL you give it, with the credentials from the `E2E_USERNAME` / `E2E_PASSWORD` repository secrets (an admin). They change that deployment's data, and clean up after themselves as they do locally.
