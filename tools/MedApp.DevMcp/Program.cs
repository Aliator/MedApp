using MedApp.Application.Common.Extensions;
using MedApp.DevMcp;
using MedApp.Infrastructure.Common.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// stdout carries the MCP protocol, so everything else (logs, EF SQL) must go to stderr.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    EnvironmentName = Environments.Development,
    ContentRootPath = AppContext.BaseDirectory,
});

// Same configuration the API runs with locally, then this tool's own settings, then the AppHost's user
// secrets (the SQL container's password), then environment variables (e.g. ConnectionStrings__DefaultConnection).
var repoRoot = RepoRoot.Find();
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile(Path.Combine(repoRoot, "MedApp.API", "appsettings.json"), optional: false)
    .AddJsonFile(Path.Combine(repoRoot, "MedApp.API", "appsettings.Development.json"), optional: true)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "devmcp.json"), optional: true)
    .AddUserSecrets(AppHostUserSecretsId)
    .AddEnvironmentVariables();

// Aspire injects the API's connection string at run time, so nothing in the API's appsettings names the database.
// Unless one is given explicitly, connect to the AppHost's SQL container through its fixed host port.
if (builder.Configuration.GetConnectionString("DefaultConnection") is null
    && builder.Configuration["Parameters:sql-password"] is { Length: > 0 } sqlPassword
    && builder.Configuration["DevMcp:SqlContainer:ConnectionString"] is { Length: > 0 } containerConnection)
{
    var csb = new SqlConnectionStringBuilder(containerConnection) { Password = sqlPassword };
    builder.Configuration.AddInMemoryCollection([new("ConnectionStrings:DefaultConnection", csb.ConnectionString)]);
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var guard = DevGuard.Check(builder.Configuration); // throws (and the server never starts) unless the database is local
builder.Services.AddSingleton(guard);
builder.Services.AddSingleton(new DevMcpOptions(
    builder.Configuration.GetSection("DevMcp:ProtectedUsers").Get<string[]>() ?? ["admin"],
    Path.Combine(repoRoot, "tools", "MedApp.DevMcp", ".backups"),
    builder.Configuration["DevMcp:SqlContainer:BackupDirectory"]));

builder.Services.AddDataProtection(); // the API's web host provides this; Identity's token providers need it
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "medapp-dev", Version = "1.0.0" })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

internal partial class Program
{
    /// <summary>MedApp.AppHost's UserSecretsId, where Aspire keeps the SQL container's sa password.</summary>
    private const string AppHostUserSecretsId = "56d045a8-4da5-4c69-a35c-0f96388e98f6";
}
