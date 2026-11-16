using MedApp.Application.Common.Email;
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
/// </summary>
public sealed class MedAppApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Every client talks https, as browsers do in production, so the Secure session cookie comes back.</summary>
    public static readonly Uri BaseAddress = new("https://localhost");

    /// <summary>A marker in the stub app's page, to recognise it in a response.</summary>
    public const string StubAppMarker = "medapp-integration-tests-stub-app";

    /// <summary>Where emailed links point: the tests read the account and token from them, as the page does.</summary>
    public const string LinkBaseUrl = "https://medapp.example.test";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("medapp-it-wwwroot-").FullName;

    /// <summary>Every email the API sent, kept instead of sent (<see cref="FakeEmailSender"/>).</summary>
    public FakeEmailSender Emails { get; } = new();

    /// <summary>The API's clock, which a test can move forward (<see cref="TestClock"/>).</summary>
    public TestClock Clock { get; } = new();

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
        // The tests sign in far more often than people do, all from one address (and the admin under one name), so
        // sign-in throttling is set out of reach here. LoginThrottlingTests puts it back to the real limits.
        builder.UseSetting("LoginThrottling:PerClientPermitLimit", "100000");
        builder.UseSetting("LoginThrottling:PerUsernamePermitLimit", "100000");
        builder.UseSetting("LoginThrottling:ResetRequestsPerIdentifierPermitLimit", "100000");
        builder.UseSetting("LoginThrottling:TwoFactorCodesPerAccountPermitLimit", "100000");
        builder.UseSetting("LoginThrottling:EmailChangesPerAccountPermitLimit", "100000");
        builder.UseSetting("Email:LinkBaseUrl", LinkBaseUrl);

        // Emails are kept, not sent, and the clock can be moved on.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
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
