# MedApp.E2ETests

End-to-end tests for the React app (`MedApp.Web`), written with [Playwright for .NET](https://playwright.dev/dotnet/) on NUnit, the same stack as `MedApp.UnitTests`.

**There are no tests yet.** The setup is ready so they can be added later (plan Q13). `dotnet test MedApp.UnitTests` and the PR workflow don't run this project.

## What's here

| File | Purpose |
|---|---|
| `Common/E2ETest.cs` | Base class. Each test gets a fresh page at the base URL: desktop 1440×900, `en-GB`, `Europe/London`, signed in. Override `IsSignedIn` to start signed out. |
| `Common/E2ESession.cs` | Signs in once per run through `POST /api/auth/login` and saves the session for every signed-in test. |
| `Common/E2ESettings.cs` | Reads the environment variables below. |

## Settings

| Variable | Default | Purpose |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost:5173` | The app under test (the React dev server the AppHost runs) |
| `E2E_USERNAME` / `E2E_PASSWORD` | none | A dev-only account. Without them, the run is skipped. |

Never commit credentials. Use a synthetic dev account, for example one made with `medapp-dev`'s `create_user`.

## Running locally

```bash
dotnet build MedApp.E2ETests
pwsh MedApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium   # once
dotnet run --project MedApp.AppHost                                       # in another terminal
E2E_USERNAME=... E2E_PASSWORD=... dotnet test MedApp.E2ETests
```

## Writing a test

```csharp
public sealed class LoginTests : E2ETest
{
    protected override bool IsSignedIn => false;

    [Test]
    public async Task Login_WrongPassword_ShowsInvalidCredentials()
    {
        await Page.GotoAsync("/login");
        // ...
    }
}
```

Use synthetic data only, and leave the database as you found it.

## CI

`.github/workflows/e2e.yml` runs only by hand (**Actions → E2E tests → Run workflow**), against a deployed URL you give it, with the credentials from the `E2E_USERNAME` / `E2E_PASSWORD` repository secrets.
