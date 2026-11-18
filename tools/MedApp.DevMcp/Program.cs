using MedApp.DevMcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// `seed-demo ...` loads the demo data from a terminal (tools/seed-demo-data.sh runs it), with no MCP client: the same
// seeder, the same local-only guard. Anything else starts the MCP server.
if (args.Length > 0 && args[0] == SeedDemoCli.Command)
    return await SeedDemoCli.RunAsync(args[1..], Console.Out, Console.Error);

// stdout carries the MCP protocol, so everything else (logs, EF SQL) must go to stderr.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    EnvironmentName = Environments.Development,
    ContentRootPath = AppContext.BaseDirectory,
});

var repoRoot = RepoRoot.Find();
builder.Configuration.Sources.Clear();
DevMcpConfiguration.AddTo(builder.Configuration, repoRoot);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var guard = DevGuard.Check(builder.Configuration); // throws (and the server never starts) unless the database is local
builder.Services.AddSingleton(guard);
builder.Services.AddSingleton(DevMcpConfiguration.Options(builder.Configuration, repoRoot));

builder.Services.AddDevMcpCore(builder.Configuration);

builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "medapp-dev", Version = "1.0.0" })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
return 0;

/// <summary>
/// Declared so it stays internal: ASP.NET Core's SDK otherwise makes a top-level program's class public (for
/// WebApplicationFactory), and the integration tests, which reference this tool and the API, would then see two.
/// </summary>
internal partial class Program;
