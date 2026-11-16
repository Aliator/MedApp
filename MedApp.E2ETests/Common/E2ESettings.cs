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

    /// <summary>
    /// Where to save a Playwright trace of each failed test (screenshots, the page's DOM and its network), or null for
    /// none. A trace records what was typed, passwords included, so only CI's throwaway run sets it (main.yml), never
    /// a run against a deployment.
    /// </summary>
    public static string? TraceDirectory =>
        Environment.GetEnvironmentVariable("E2E_TRACE_DIR") is { Length: > 0 } directory ? directory : null;

    /// <summary>The signed-in session, saved once per run by <see cref="E2ESession"/> and loaded into every signed-in test.</summary>
    public static string StorageStatePath => Path.Combine(TestContext.CurrentContext.WorkDirectory, ".auth", "session.json");
}
