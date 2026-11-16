using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The real API, in memory, against the test run's SQL Server. It runs as Production does (Secure session cookies,
/// HSTS, no Swagger), so clients use an https base address. Server-side rendering is off: these tests are for the API.
/// <para>
/// It has a web root of its own holding a stub <c>index.html</c>, as the image's holds the built React app, so a page
/// path gets "the app" and a test can tell that apart from an <c>/api</c> path's 404.
/// </para>
/// <para>
/// Its clock is <see cref="Clock"/>, the real time unless a test moves it, in place of the app's <see cref="TimeProvider"/>.
/// </para>
/// </summary>
public sealed class MedAppApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Every client talks https, as browsers do in production, so the Secure session cookie comes back.</summary>
    public static readonly Uri BaseAddress = new("https://localhost");

    /// <summary>A marker in the stub app's page, to recognise it in a response.</summary>
    public const string StubAppMarker = "medapp-integration-tests-stub-app";

    /// <summary>The API's clock: the real time, until a test moves it. <see cref="IntegrationTest"/> resets it before each test.</summary>
    public TestClock Clock { get; } = new();

    private readonly string _webRoot = Directory.CreateTempSubdirectory("medapp-it-wwwroot-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            $"<!doctype html><html><body><div id=\"root\">{StubAppMarker}</div></body></html>");

        builder.UseEnvironment("Production");
        builder.UseWebRoot(_webRoot);
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Ssr:Enabled", "false");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
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
