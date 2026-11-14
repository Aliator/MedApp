var builder = DistributedApplication.CreateBuilder(args);

// SQL Server in Docker. Aspire generates the sa password on first run and keeps it in this project's
// user secrets as Parameters:sql-password. The fixed host port and the persistent lifetime let tools
// outside Aspire (dotnet ef, the medapp-dev MCP) reach the database, even while the AppHost is stopped.
var sql = builder.AddSqlServer("sql", port: 14330)
    .WithDataVolume("medapp-sql-data")
    // medapp-dev's save_snapshot / restore_snapshot write .bak files here, so they land in the repo's git-ignored folder.
    .WithBindMount("../tools/MedApp.DevMcp/.backups", "/var/opt/mssql/backups")
    .WithLifetime(ContainerLifetime.Persistent);

// Aspire creates the database if it's missing; migrations are still applied by hand (see AGENTS.md).
var db = sql.AddDatabase("medapp", databaseName: "MedApp");

var api = builder.AddProject<Projects.MedApp_API>("api", launchProfileName: "https")
    .WithReference(db, connectionName: "DefaultConnection")
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

// The React app (MedApp.Web), the only UI since cutover (plan Q12; the Blazor client is deprecated and not run).
// In development, Vite proxies /api to the API's URL, which WithReference passes in, so the session cookie stays
// first-party. Port 5173 is fixed so the UI parity tool's WEB_URL and the E2E default don't change between runs.
var web = builder.AddViteApp("web", "../MedApp.Web")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5173;
        endpoint.IsProxied = false;
    })
    .WithReference(api)
    .WaitFor(api);

// In production the API serves the built React app from its wwwroot, on the same origin as /api (plan Q4).
api.PublishWithContainerFiles(web, "./wwwroot");

builder.Build().Run();
