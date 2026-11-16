using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

// In the root namespace, so it runs once around every test in the assembly.
// ReSharper disable once CheckNamespace
namespace MedApp.IntegrationTests;

/// <summary>
/// Once per run: starts SQL Server 2022 and Azurite (the Azure Storage emulator, for attached files) in Docker
/// (Testcontainers), applies every migration to a new database, and starts the API against both. Before each test, <see cref="ResetDatabaseAsync"/> empties the database (Respawn),
/// keeping the migrations history and the seeded default stage definition.
/// </summary>
[SetUpFixture]
public sealed class TestEnvironment
{
    /// <summary>The SQL Server image the AppHost runs, so the tests use the same engine as development.</summary>
    public const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest";

    public const string DatabaseName = "MedApp_IntegrationTests";

    /// <summary>The Azurite image the Helm chart runs (deploy/helm/medapp/values.yaml).</summary>
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.37.0";

    /// <summary>The stage definition every database is seeded with (PatientProcedureStageDefinitionConfiguration).</summary>
    public static readonly Guid SeededStageDefinitionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static MsSqlContainer? _sql;
    private static AzuriteContainer? _azurite;
    private static MedAppApiFactory? _api;
    private static Respawner? _respawner;

    public static MedAppApiFactory Api => _api ?? throw new InvalidOperationException("The test environment hasn't started.");

    public static string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Azurite's connection string, for attached files' bytes.</summary>
    public static string AttachmentsConnectionString { get; private set; } = string.Empty;

    /// <summary>The SQL Server itself, for a test that needs a database of its own (e.g. the migrations' history).</summary>
    public static string ServerConnectionString(string database) =>
        new SqlConnectionStringBuilder(_sql!.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _sql = new MsSqlBuilder(SqlImage).Build();
        // The storage SDK asks for newer service versions than Azurite knows; the chart skips the check too.
        _azurite = new AzuriteBuilder(AzuriteImage).WithCommand("--skipApiVersionCheck").Build();
        try
        {
            await Task.WhenAll(_sql.StartAsync(), _azurite.StartAsync());
        }
        catch (Exception exception)
        {
            Assert.Fail($"Couldn't start SQL Server and Azurite in Docker. Is Docker running? {exception.Message}");
        }

        ConnectionString = ServerConnectionString(DatabaseName);
        AttachmentsConnectionString = _azurite.GetConnectionString();
        _api = new MedAppApiFactory(ConnectionString, AttachmentsConnectionString);

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
            // The data protection keys stay, as they would in a real database: the API caches its key ring, and a key
            // deleted under it would make tokens it seals unreadable after the cache refreshes.
            TablesToIgnore = [new Table("__EFMigrationsHistory"), new Table("PatientProcedureStageDefinitions"), new Table("DataProtectionKeys")],
        });
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (_api is not null)
            await _api.DisposeAsync();
        if (_sql is not null)
            await _sql.DisposeAsync();
        if (_azurite is not null)
            await _azurite.DisposeAsync();
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
