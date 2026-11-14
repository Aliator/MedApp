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

// The Blazor client (WASM) can't read Aspire's environment variables, so it calls the API's fixed https
// port (7038, from the API's launch profile). Its own port (7292) is the one the API's CORS policy allows.
builder.AddProject<Projects.MedApp_Client>("blazor", launchProfileName: "https")
    .WaitFor(api);

// The React app (MedApp.Web), built alongside Blazor until cutover (plan Q12). Vite proxies /api to the API's URL,
// which WithReference passes in, so the session cookie stays first-party. Port 5173 is fixed so the UI parity
// tool's WEB_URL doesn't change between runs.
builder.AddViteApp("web", "../MedApp.Web")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5173;
        endpoint.IsProxied = false;
    })
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
