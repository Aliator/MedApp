using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace MedApp.E2ETests.Common;

/// <summary>
/// Base class for end-to-end tests: a fresh browser page per test, at the app's base URL, signed in as the
/// E2E account. The settings match the UI parity screenshots: desktop 1440×900, en-GB, Europe/London.
/// Override <see cref="IsSignedIn"/> for tests that start signed out, such as the login page.
/// </summary>
public abstract class E2ETest : PageTest
{
    protected virtual bool IsSignedIn => true;

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = E2ESettings.BaseUrl,
        ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        Locale = "en-GB",
        TimezoneId = "Europe/London",
        IgnoreHTTPSErrors = true, // ASP.NET Core dev certificates
        StorageStatePath = IsSignedIn ? E2ESettings.StorageStatePath : null,
    };
}
