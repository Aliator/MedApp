using MedApp.Application.Attachments.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedApp.DevMcp.DemoData;

public sealed partial class SeedServices
{
    /// <summary>The app's services against the local SQL Server database, as the tool has them.</summary>
    /// <param name="discardFiles">
    /// No attachment store answers: files the app's commands make (letters' PDFs) are dropped, so their records still
    /// save (as a snapshot loaded elsewhere has the records without the bytes).
    /// </param>
    public SeedServices(IConfiguration configuration, DateTime nowUtc, bool discardFiles = false)
        : this(nowUtc, services => AddSqlServerApp(services, configuration), discardFiles ? new DiscardedFiles() : null)
    {
    }

    private static void AddSqlServerApp(IServiceCollection services, IConfiguration configuration)
    {
        var overlay = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            // Without a base for links, the app makes no confirmation link and records no address request. Nothing is
            // sent: email goes to DiscardedEmail.
            .AddInMemoryCollection(configuration["Email:LinkBaseUrl"] is { Length: > 0 }
                ? []
                : [new KeyValuePair<string, string?>("Email:LinkBaseUrl", LinkBaseUrl)])
            .Build();

        services
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .AddSingleton<IConfiguration>(overlay)
            .AddDevMcpCore(overlay);
    }
}

/// <summary>The seed's file store when no attachment store answers: it takes every file and keeps none.</summary>
public sealed class DiscardedFiles : IFileStore
{
    public bool IsConfigured => true;

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.CompletedTask;

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
}
