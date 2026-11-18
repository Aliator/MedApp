using MedApp.DevMcp.Tools;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Loading the demo data, the one way it's loaded: the MCP tool (<c>reset_demo_data</c>) and the terminal command
/// (<see cref="SeedDemoCli"/>, which <c>tools/seed-demo-data.sh</c> runs) both come through here, so both have the same
/// rules: confirmation, a database whose migrations are all applied, a snapshot first, and the kept accounts (the
/// protected ones always). The local-database guard is checked before either gets this far, and again by the seeder.
/// </summary>
public static class DemoDataReset
{
    /// <summary>The standard demo data set's random seed.</summary>
    public const int StandardSeed = 2026;

    /// <param name="confirm">Must be true: this deletes data.</param>
    /// <param name="keepUsers">Accounts to keep, besides the protected ones.</param>
    /// <param name="snapshot">Whether to save a <c>pre-reset-&lt;timestamp&gt;</c> snapshot first.</param>
    public static async Task<Dictionary<string, object?>> RunAsync(
        IConfiguration configuration,
        DevGuard guard,
        DevMcpOptions options,
        bool confirm,
        int seed,
        IReadOnlyCollection<string>? keepUsers,
        bool snapshot,
        CancellationToken ct)
    {
        Guards.RequireConfirm(confirm, "Resetting the database to demo data");

        var pending = await PendingMigrationsAsync(configuration, ct);
        if (pending.Count > 0)
            throw new McpException(
                $"The database '{guard.Database}' on {guard.Server} has {pending.Count} migration(s) to apply first (the next is {pending[0]}). " +
                "tools/seed-demo-data.sh (or .ps1) applies them, then loads the demo data.");

        string? savedAs = null;
        if (snapshot)
        {
            savedAs = $"pre-reset-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            await DatabaseTools.SaveSnapshot(guard, options, savedAs, ct);
        }

        var summary = await DemoDataSeeder.RunAsync(
            configuration,
            new DemoSeedOptions(seed, DateTime.UtcNow, options.ProtectedUsers.Concat(keepUsers ?? []).ToArray()),
            ct);
        summary["snapshotBeforeReset"] = savedAs;
        return summary;
    }

    /// <summary>The migrations the database hasn't had yet, oldest first (all of them, if it doesn't exist).</summary>
    public static async Task<IReadOnlyList<string>> PendingMigrationsAsync(IConfiguration configuration, CancellationToken ct)
    {
        await using var services = new SeedServices(configuration, DateTime.UtcNow);
        return await services.Run(async sp =>
            (IReadOnlyList<string>)(await sp.GetRequiredService<MedAppDbContext>().Database.GetPendingMigrationsAsync(ct)).ToList());
    }
}
