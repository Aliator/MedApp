using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MedApp.Application.Calendar.Common;
using MedApp.Browser.Hosting;
using MedApp.Browser.Infrastructure;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace MedApp.BrowserDb;

/// <summary>
/// The demo database, as the browser starts on it: a new SQLite file made from the browser's model (the API's, fitted
/// to SQLite), with the full app's demo data loaded by its own seeder, through the app's handlers, as in the browser
/// (the same services, stand-ins and compiled model). Writes <see cref="DatabaseFile"/>, compressed (<c>.gz</c>), and
/// <see cref="VersionFile"/>, which the page reads to tell whether its saved copy is the published one.
/// </summary>
public static class DemoDatabaseBuild
{
    public const string DatabaseFile = "medapp.db";
    public const string VersionFile = "db-version.json";

    /// <summary>The database's format: raised when a saved copy of an older one can't be used, whatever its date.</summary>
    public const int SeedVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task RunAsync(string folder, TextWriter log, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, DatabaseFile);
        foreach (var file in new[] { path, path + "-journal", path + "-wal", path + "-shm", path + ".gz" })
            File.Delete(file);

        var database = new BrowserDatabase(path);
        var now = DateTime.UtcNow;

        await using (var services = new ServiceCollection().AddBrowserBackend(database).BuildServiceProvider())
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MedAppDbContext>();
            await BrowserBackend.CreateAsync(context, ct);
            await log.WriteLineAsync(CompiledModelCheck.Run(context));
        }

        Dictionary<string, object?> summary;
        await using (var seed = new SeedServices(now, services => services.AddBrowserBackend(
                         database, new Dictionary<string, string?> { ["Email:LinkBaseUrl"] = SeedServices.LinkBaseUrl })))
            summary = await DemoDataSeeder.SeedAsync(seed, new DemoSeedOptions(2026, now, []), wipe: false, filesSkipped: null, ct);

        await using (var connection = database.Open())
        await using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = "VACUUM";
            await vacuum.ExecuteNonQueryAsync(ct);
        }

        SqliteConnection.ClearAllPools();
        var bytes = await File.ReadAllBytesAsync(path, ct);
        await using (var gz = new GZipStream(File.Create(path + ".gz"), CompressionLevel.SmallestSize))
            await gz.WriteAsync(bytes, ct);

        var version = new
        {
            builtOn = UkTime.DateOf(now).ToString("yyyy-MM-dd"),
            seedVersion = SeedVersion,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes = bytes.Length,
            compressedBytes = new FileInfo(path + ".gz").Length,
            tour = summary["tour"],
            accounts = DemoPeople.Roster().Select(p => new
            {
                username = p.Username,
                fullName = p.FullName,
                jobTitle = p.JobTitle,
                roles = p.Roles.Select(r => r.Name).ToArray(),
                teams = p.Teams.Select(t => t.Role == TeamMemberRole.ClinicAdmin ? $"{t.Team} (clinic admin)" : t.Team).ToArray(),
            }),
        };
        await File.WriteAllTextAsync(Path.Combine(folder, VersionFile), JsonSerializer.Serialize(version, Json), ct);

        await log.WriteLineAsync(JsonSerializer.Serialize(new
        {
            folder,
            version.builtOn,
            version.sha256,
            version.bytes,
            version.compressedBytes,
            seedSeconds = summary["seconds"],
            totalSeconds = Math.Round(timer.Elapsed.TotalSeconds, 1),
            counts = summary["counts"],
        }, Json));
    }
}
