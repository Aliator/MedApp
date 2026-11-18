using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MedApp.DevMcp;

/// <summary>
/// The tool's configuration, the same whether it runs as the MCP server or from a terminal (<see cref="SeedDemoCli"/>):
/// the API's own settings, then this tool's, then the AppHost's user secrets (the SQL container's password), then
/// environment variables (e.g. <c>ConnectionStrings__DefaultConnection</c>).
/// </summary>
public static class DevMcpConfiguration
{
    /// <summary>MedApp.AppHost's UserSecretsId, where Aspire keeps the SQL container's sa password.</summary>
    private const string AppHostUserSecretsId = "56d045a8-4da5-4c69-a35c-0f96388e98f6";

    public static void AddTo(ConfigurationManager configuration, string repoRoot)
    {
        configuration
            .AddJsonFile(Path.Combine(repoRoot, "MedApp.API", "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(repoRoot, "MedApp.API", "appsettings.Development.json"), optional: true)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "devmcp.json"), optional: true)
            .AddUserSecrets(AppHostUserSecretsId)
            .AddEnvironmentVariables();

        // Aspire injects the API's connection string at run time, so nothing in the API's appsettings names the database.
        // Unless one is given explicitly, connect to the AppHost's SQL container through its fixed host port.
        if (configuration.GetConnectionString("DefaultConnection") is null
            && configuration["Parameters:sql-password"] is { Length: > 0 } sqlPassword
            && configuration["DevMcp:SqlContainer:ConnectionString"] is { Length: > 0 } containerConnection)
        {
            var csb = new SqlConnectionStringBuilder(containerConnection) { Password = sqlPassword };
            configuration.AddInMemoryCollection([new("ConnectionStrings:DefaultConnection", csb.ConnectionString)]);
        }

        // Attached files: the AppHost's Azurite on its fixed host port, with Azurite's well-known development account (its
        // key is published, not a secret). Only the demo data uses it, and only if it answers; a connection string given
        // explicitly wins.
        if (configuration.GetConnectionString("Attachments") is null
            && configuration["DevMcp:Azurite:ConnectionString"] is { Length: > 0 } azurite)
            configuration.AddInMemoryCollection([new("ConnectionStrings:Attachments", azurite)]);
    }

    public static DevMcpOptions Options(IConfiguration configuration, string repoRoot) => new(
        configuration.GetSection("DevMcp:ProtectedUsers").Get<string[]>() ?? ["admin"],
        Path.Combine(repoRoot, "tools", "MedApp.DevMcp", ".backups"),
        configuration["DevMcp:SqlContainer:BackupDirectory"]);
}
