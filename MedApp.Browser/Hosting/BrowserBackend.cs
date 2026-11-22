using System.Reflection;
using MedApp.Application.Common.Identity;
using MedApp.Browser.Data;
using MedApp.Browser.Infrastructure;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Browser.Hosting;

/// <summary>
/// The backend: the services, the routes and the database. An empty database is made from the model, and given what
/// a deployed one starts with: the standard roles and a first admin (<c>admin</c>), as <c>create-first-admin</c> does.
/// </summary>
public sealed class BrowserBackend
{
    public const string AdminUsername = "admin";

    private BrowserBackend(IServiceProvider services, ApiPipeline pipeline, IReadOnlyDictionary<string, long> startTimings)
    {
        Services = services;
        Pipeline = pipeline;
        StartTimings = startTimings;
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
            await context.Database.EnsureCreatedAsync(ct);
            await context.Database.ExecuteSqlRawAsync(SqlServerDialect.CreateSequencesTableSql, ct);
            await context.Database.ExecuteSqlRawAsync(SqliteFileStore.CreateTableSql, ct);
            Lap("database");

            if (!await context.Users.AnyAsync(ct))
                await SeedFirstAdminAsync(scope.ServiceProvider, context, ct);
            Lap("seed");
        }

        return new BrowserBackend(services, new ApiPipeline(services, routes), timings);
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
