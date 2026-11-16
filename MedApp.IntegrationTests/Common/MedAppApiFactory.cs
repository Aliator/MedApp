using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The real API, in memory, against the test run's SQL Server. It runs as Production does (Secure session cookies,
/// HSTS, no Swagger), so clients use an https base address. Server-side rendering is off: these tests are for the API.
/// </summary>
public sealed class MedAppApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Every client talks https, as browsers do in production, so the Secure session cookie comes back.</summary>
    public static readonly Uri BaseAddress = new("https://localhost");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
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
}
