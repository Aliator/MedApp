using System.Reflection;
using MedApp.Application.Common.Identity;
using MedApp.Browser.Data;
using MedApp.Browser.Infrastructure;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Browser.Hosting;

/// <summary>
/// The backend: the services, the routes and the database. It starts on the database file it finds (the demo
/// database, <c>tools/MedApp.BrowserDb</c>, which the page writes there first); with none, an empty one is made from
/// the model, and given what a deployed one starts with: the standard roles and a first admin (<c>admin</c>), as
/// <c>create-first-admin</c> does.
/// </summary>
public sealed class BrowserBackend
{
    public const string AdminUsername = "admin";

    private readonly BrowserDatabase _database;
    private readonly SqliteConnection _watcher;
    private long _dataVersion;

    private BrowserBackend(IServiceProvider services, ApiPipeline pipeline, BrowserDatabase database, IReadOnlyDictionary<string, long> startTimings)
    {
        Services = services;
        Pipeline = pipeline;
        StartTimings = startTimings;
        _database = database;
        _watcher = database.Open();
        _dataVersion = DataVersion();
    }

    public IServiceProvider Services { get; }
    public ApiPipeline Pipeline { get; }

    /// <summary>How long each part of starting took, in milliseconds.</summary>
    public IReadOnlyDictionary<string, long> StartTimings { get; }

    public static async Task<BrowserBackend> StartAsync(string databasePath, CancellationToken ct)
    {
        var timings = new Dictionary<string, long>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        void Lap(string name)
        {
            timings[name] = clock.ElapsedMilliseconds;
            clock.Restart();
        }

        var database = new BrowserDatabase(databasePath);
        var services = new ServiceCollection().AddBrowserBackend(database).BuildServiceProvider();
        var routes = ApiRoute.All(Assembly.GetExecutingAssembly());
        Lap("servicesAndRoutes");

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MedAppDbContext>();
            _ = context.Model;
            Lap("model");
            await CreateAsync(context, ct);
            Lap("database");

            if (!await context.Users.AnyAsync(ct))
                await SeedFirstAdminAsync(scope.ServiceProvider, context, ct);
            Lap("seed");
        }

        return new BrowserBackend(services, new ApiPipeline(services, routes), database, timings);
    }

    /// <summary>
    /// Makes the database from the model if it's empty, with the browser's own tables, in rollback journal mode (EF
    /// Core makes SQLite databases in WAL mode), so the file holds every committed change and can be saved as it is.
    /// </summary>
    public static async Task CreateAsync(MedAppDbContext context, CancellationToken ct)
    {
        await context.Database.EnsureCreatedAsync(ct);
        await context.Database.ExecuteSqlRawAsync(SqlServerDialect.CreateSequencesTableSql, ct);
        await context.Database.ExecuteSqlRawAsync(SqliteFileStore.CreateTableSql, ct);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = DELETE", ct);
    }

    /// <summary>Whether a change has been committed since it was last asked.</summary>
    public bool DataChanged()
    {
        var version = DataVersion();
        var changed = version != _dataVersion;
        _dataVersion = version;
        return changed;
    }

    /// <summary>The database file's bytes, read between requests (never during one: its changes may be half written).</summary>
    public byte[] Export() => Pipeline.IsBusy
        ? throw new InvalidOperationException("The database can't be read while a request is being served.")
        : File.ReadAllBytes(_database.Path);

    /// <summary>SQLite's count of changes committed by other connections than this one (every request's).</summary>
    private long DataVersion()
    {
        using var command = _watcher.CreateCommand();
        command.CommandText = "PRAGMA data_version";
        return (long)command.ExecuteScalar()!;
    }

    private static async Task SeedFirstAdminAsync(IServiceProvider services, MedAppDbContext context, CancellationToken ct)
    {
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await StandardRoleSetup.EnsureAsync(context, now, resetPermissions: true, ct);

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = AdminUsername, CreatedAt = now };
        Check(await users.CreateAsync(admin));
        Check(await users.AddToRoleAsync(admin, AppRoles.Admin));
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
