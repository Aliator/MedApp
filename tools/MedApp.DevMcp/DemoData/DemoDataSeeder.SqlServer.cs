using Microsoft.Extensions.Configuration;

namespace MedApp.DevMcp.DemoData;

public static partial class DemoDataSeeder
{
    /// <summary>Wipes the local SQL Server database's data and loads the demo data (<see cref="SeedAsync"/>).</summary>
    public static async Task<Dictionary<string, object?>> RunAsync(IConfiguration configuration, DemoSeedOptions options, CancellationToken ct)
    {
        // The tool checks this when it starts; checked again here, as tests and other callers come in this way.
        DevGuard.Check(configuration);

        // Whether files can be stored: without a store, the files the app's commands make are dropped (their records stay).
        var filesSkipped = await DemoFiles.WhyNotAvailableAsync(configuration, ct);
        await using var services = new SeedServices(configuration, options.NowUtc, discardFiles: filesSkipped is not null);
        return await SeedAsync(services, options, wipe: true, filesSkipped, ct);
    }
}
