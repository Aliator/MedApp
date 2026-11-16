using FluentAssertions;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>The migrations against a real SQL Server: the whole history applies, and the model has nothing more.</summary>
[TestFixture]
public sealed class MigrationTests
{
    [Test]
    public async Task Migrate_NewDatabase_AppliesTheWholeHistoryInOrder()
    {
        // The run's database was made by MigrateAsync on an empty server (TestEnvironment).
        await using var scope = TestEnvironment.Api.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MedAppDbContext>().Database;

        var applied = (await database.GetAppliedMigrationsAsync()).ToList();

        applied.Should().Equal(database.GetMigrations());
        applied[0].Should().EndWith("_InitialCreate");
        (await database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task Model_ComparedWithTheLatestMigration_HasNoPendingChanges()
    {
        await using var scope = TestEnvironment.Api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MedAppDbContext>();

        context.Database.HasPendingModelChanges().Should().BeFalse(
            "the model has changes no migration covers: add one with dotnet ef migrations add");
    }

    [Test]
    public async Task Migrate_DownToNothingAndUpAgain_RunsEveryMigrationBothWays()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString("MedApp_MigrationsRoundTrip"))
            .Options;
        await using var context = new MedAppDbContext(options);
        var migrator = context.GetService<IMigrator>();

        try
        {
            await context.Database.MigrateAsync();
            await migrator.MigrateAsync(Migration.InitialDatabase);
            (await context.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();

            await context.Database.MigrateAsync();
            (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(context.Database.GetMigrations());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
