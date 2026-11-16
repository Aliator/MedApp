using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The real API, in memory, against the test run's SQL Server and Azurite (none: file storage isn't set up). It runs as Production does (Secure session cookies,
/// HSTS, no Swagger), so clients use an https base address. Server-side rendering is off: these tests are for the API.
/// <para>
/// It has a web root of its own holding a stub <c>index.html</c>, as the image's holds the built React app, so a page
/// path gets "the app" and a test can tell that apart from an <c>/api</c> path's 404.
/// </para>
/// </summary>
public sealed class MedAppApiFactory(string connectionString, string? attachmentsConnectionString = null) : WebApplicationFactory<Program>
{
    /// <summary>Every client talks https, as browsers do in production, so the Secure session cookie comes back.</summary>
    public static readonly Uri BaseAddress = new("https://localhost");

    /// <summary>A marker in the stub app's page, to recognise it in a response.</summary>
    public const string StubAppMarker = "medapp-integration-tests-stub-app";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("medapp-it-wwwroot-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            $"<!doctype html><html><body><div id=\"root\">{StubAppMarker}</div></body></html>");

        builder.UseEnvironment("Production");
        builder.UseWebRoot(_webRoot);
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        if (attachmentsConnectionString is not null)
            builder.UseSetting("ConnectionStrings:Attachments", attachmentsConnectionString);
        builder.UseSetting("Ssr:Enabled", "false");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
    }

    /// <summary>A client with its own cookie jar (so its own session), that doesn't follow redirects.</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = BaseAddress,
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try
        {
            Directory.Delete(_webRoot, recursive: true);
        }
        catch (IOException)
        {
            // Left in the temp folder; nothing depends on it.
        }
    }
}
