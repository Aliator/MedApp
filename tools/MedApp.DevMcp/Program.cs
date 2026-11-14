using MedApp.Application.Common.Extensions;
using MedApp.DevMcp;
using MedApp.Infrastructure.Common.Extensions;
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

// Same configuration the API runs with locally, then this tool's own settings, then environment
// variables (e.g. ConnectionStrings__DefaultConnection injected by Aspire later).
var repoRoot = RepoRoot.Find();
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile(Path.Combine(repoRoot, "MedApp.API", "appsettings.json"), optional: false)
    .AddJsonFile(Path.Combine(repoRoot, "MedApp.API", "appsettings.Development.json"), optional: true)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "devmcp.json"), optional: true)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var guard = DevGuard.Check(builder.Configuration); // throws (and the server never starts) unless the database is local
builder.Services.AddSingleton(guard);
builder.Services.AddSingleton(new DevMcpOptions(
    builder.Configuration.GetSection("DevMcp:ProtectedUsers").Get<string[]>() ?? ["admin"],
    Path.Combine(repoRoot, "tools", "MedApp.DevMcp", ".backups")));

builder.Services.AddDataProtection(); // the API's web host provides this; Identity's token providers need it
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "medapp-dev", Version = "1.0.0" })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
