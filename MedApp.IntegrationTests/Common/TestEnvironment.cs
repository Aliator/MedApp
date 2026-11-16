using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;

// In the root namespace, so it runs once around every test in the assembly.
// ReSharper disable once CheckNamespace
namespace MedApp.IntegrationTests;

/// <summary>
/// Once per run: starts SQL Server 2022 in Docker (Testcontainers), applies every migration to a new database, and
/// starts the API against it. Before each test, <see cref="ResetDatabaseAsync"/> empties the database (Respawn),
/// keeping the migrations history and the seeded default stage definition.
/// </summary>
[SetUpFixture]
public sealed class TestEnvironment
{
    /// <summary>The SQL Server image the AppHost runs, so the tests use the same engine as development.</summary>
    public const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest";

    public const string DatabaseName = "MedApp_IntegrationTests";

    /// <summary>The stage definition every database is seeded with (PatientProcedureStageDefinitionConfiguration).</summary>
    public static readonly Guid SeededStageDefinitionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static MsSqlContainer? _sql;
    private static MedAppApiFactory? _api;
    private static Respawner? _respawner;

    public static MedAppApiFactory Api => _api ?? throw new InvalidOperationException("The test environment hasn't started.");

    public static string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The SQL Server itself, for a test that needs a database of its own (e.g. the migrations' history).</summary>
    public static string ServerConnectionString(string database) =>
        new SqlConnectionStringBuilder(_sql!.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _sql = new MsSqlBuilder(SqlImage).Build();
        try
        {
            await _sql.StartAsync();
        }
        catch (Exception exception)
        {
            Assert.Fail($"Couldn't start SQL Server in Docker. Is Docker running? {exception.Message}");
        }

        ConnectionString = ServerConnectionString(DatabaseName);
        _api = new MedAppApiFactory(ConnectionString);

        await using (var scope = _api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MedAppDbContext>();
            await context.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = [new Table("__EFMigrationsHistory"), new Table("PatientProcedureStageDefinitions")],
        });
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (_api is not null)
            await _api.DisposeAsync();
        if (_sql is not null)
            await _sql.DisposeAsync();
    }

    /// <summary>
    /// Empties every table except the migrations history, then removes every stage definition but the seeded one, and
    /// puts that back as the migration made it.
    /// </summary>
    public static async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM PatientProcedureStageDefinitions WHERE Id <> @seeded;
            UPDATE PatientProcedureStageDefinitions
            SET Name = N'Definition missing',
                Description = N'This stage definition was deleted.',
                Instructions = N'No instructions available.',
                IsArchived = 1
            WHERE Id = @seeded;
            """;
        command.Parameters.AddWithValue("@seeded", SeededStageDefinitionId);
        await command.ExecuteNonQueryAsync();
    }
}
