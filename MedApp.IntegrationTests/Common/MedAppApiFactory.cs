using MedApp.Application.Common.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The real API, in memory, against the test run's SQL Server and Azurite (none: file storage isn't set up). It runs as Production does (Secure session cookies,
/// HSTS, no Swagger), so clients use an https base address. Server-side rendering is off: these tests are for the API.
/// <para>
/// It has a web root of its own holding a stub <c>index.html</c>, as the image's holds the built React app, so a page
/// path gets "the app" and a test can tell that apart from an <c>/api</c> path's 404.
/// </para>
/// <para>
/// Its clock is <see cref="Clock"/>, the real time unless a test moves it, in place of the app's <see cref="TimeProvider"/>.
/// </para>
/// </summary>
public sealed class MedAppApiFactory(string connectionString, string? attachmentsConnectionString = null) : WebApplicationFactory<Program>
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

    /// <summary>Every letter the API sent by secure email, kept instead of sent (<see cref="FakeLetterTransport"/>).</summary>
    public FakeLetterTransport Letters { get; } = new();

    /// <summary>The API's clock: the real time, until a test moves it (<see cref="TestClock"/>). <see cref="IntegrationTest"/> resets it before each test.</summary>
    public TestClock Clock { get; } = new();

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

        // The tests sign in far more often than people do, all from one address (and the admin under one name), so
        // sign-in throttling is set out of reach here. LoginThrottlingTests puts it back to the real limits.
        builder.UseSetting("LoginThrottling:PerClientPermitLimit", "100000");
        builder.UseSetting("LoginThrottling:PerUsernamePermitLimit", "100000");
        builder.UseSetting("LoginThrottling:ResetRequestsPerIdentifierPermitLimit", "100000");
        builder.UseSetting("LoginThrottling:TwoFactorCodesPerAccountPermitLimit", "100000");
        builder.UseSetting("LoginThrottling:EmailChangesPerAccountPermitLimit", "100000");
        builder.UseSetting("LoginThrottling:EmailCodesPerAccountPermitLimit", "100000");
        builder.UseSetting("Email:LinkBaseUrl", LinkBaseUrl);
        // No scheduled jobs on a timer: tests run them themselves (IScheduledJobRunner.RunNowAsync), at known times.
        builder.UseSetting("Jobs:Enabled", "false");

        // Emails and secure-email letters are kept, not sent, and the clock can be moved on.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.RemoveAll<MedApp.Application.Letters.Common.ILetterTransport>();
            services.AddSingleton<MedApp.Application.Letters.Common.ILetterTransport>(Letters);
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
