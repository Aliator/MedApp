using MedApp.E2ETests.Common;
using Microsoft.Playwright;

// In the root namespace, so it runs once before every test in the assembly (a SetUpFixture covers only its own
// namespace and those under it).
// ReSharper disable once CheckNamespace
namespace MedApp.E2ETests;

/// <summary>
/// Signs in once per test run, through the API the app uses, and saves the session cookie for every signed-in
/// test to reuse. It only runs when the run contains tests in this assembly.
/// </summary>
[SetUpFixture]
public sealed class E2ESession
{
    [OneTimeSetUp]
    public async Task SignInAsync()
    {
        if (string.IsNullOrEmpty(E2ESettings.Username) || string.IsNullOrEmpty(E2ESettings.Password))
        {
            Assert.Ignore("Set E2E_USERNAME and E2E_PASSWORD to run the end-to-end tests (see MedApp.E2ETests/README.md).");
        }

        using var playwright = await Playwright.CreateAsync();
        await using var request = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = E2ESettings.BaseUrl,
            IgnoreHTTPSErrors = true, // ASP.NET Core dev certificates
        });

        var response = await request.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { username = E2ESettings.Username, password = E2ESettings.Password },
        });
        Assert.That(response.Ok, Is.True, $"Signing in to {E2ESettings.BaseUrl} failed with {response.Status}. Check E2E_USERNAME and E2E_PASSWORD.");

        Directory.CreateDirectory(Path.GetDirectoryName(E2ESettings.StorageStatePath)!);
        await request.StorageStateAsync(new APIRequestContextStorageStateOptions { Path = E2ESettings.StorageStatePath });
    }

    /// <summary>Signs the shared session out when the run ends, so runs don't leave sessions behind.</summary>
    [OneTimeTearDown]
    public async Task SignOutAsync()
    {
        if (!File.Exists(E2ESettings.StorageStatePath))
            return;

        using var playwright = await Playwright.CreateAsync();
        await using var request = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = E2ESettings.BaseUrl,
            IgnoreHTTPSErrors = true,
            StorageStatePath = E2ESettings.StorageStatePath,
        });
        await request.PostAsync("/api/auth/logout");
        File.Delete(E2ESettings.StorageStatePath);
    }
}
