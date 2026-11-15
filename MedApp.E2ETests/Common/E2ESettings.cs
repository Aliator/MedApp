namespace MedApp.E2ETests.Common;

/// <summary>
/// Where the tests point and who they sign in as, from environment variables. Credentials are never committed:
/// set them in your shell (locally) or as repository secrets (the e2e workflow).
/// </summary>
public static class E2ESettings
{
    /// <summary>The app under test. Defaults to the React dev server the AppHost runs.</summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL") is { Length: > 0 } url ? url.TrimEnd('/') : "http://localhost:5173";

    public static string? Username => Environment.GetEnvironmentVariable("E2E_USERNAME");

    public static string? Password => Environment.GetEnvironmentVariable("E2E_PASSWORD");

    /// <summary>The signed-in session, saved once per run by <see cref="E2ESession"/> and loaded into every signed-in test.</summary>
    public static string StorageStatePath => Path.Combine(TestContext.CurrentContext.WorkDirectory, ".auth", "session.json");
}
