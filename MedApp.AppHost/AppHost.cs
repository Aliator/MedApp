var builder = DistributedApplication.CreateBuilder(args);

// SQL Server in Docker. Aspire generates the sa password on first run and keeps it in this project's
// user secrets as Parameters:sql-password. The fixed host port and the persistent lifetime let tools
// outside Aspire (dotnet ef, the medapp-dev MCP) reach the database, even while the AppHost is stopped.
var sql = builder.AddSqlServer("sql", port: 14330)
    .WithDataVolume("medapp-sql-data")
    // medapp-dev's save_snapshot / restore_snapshot write .bak files here, so they land in the repo's git-ignored folder.
    .WithBindMount("../tools/MedApp.DevMcp/.backups", "/var/opt/mssql/backups")
    // SQL Server otherwise takes up to 80% of the memory it can see, which in Docker Desktop is the WSL machine's (half
    // the PC's by default). 2 GB, its documented minimum, is plenty for development. Changing an environment variable
    // recreates the persistent container once; the data stays in the volume.
    .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "2048")
    .WithLifetime(ContainerLifetime.Persistent);

// Aspire creates the database if it's missing; migrations are still applied by hand (see AGENTS.md).
var db = sql.AddDatabase("medapp", databaseName: "MedApp");

// Mailpit catches every email the API sends (nothing leaves this machine) and shows them in a web inbox: the "Inbox"
// link on the dashboard's mailpit resource. It keeps mail in memory, so a restart empties it. Pinned to a release.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.33.0")
    .WithHttpEndpoint(targetPort: 8025, name: "inbox")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithUrlForEndpoint("inbox", url => url.DisplayText = "Inbox");
var smtp = mailpit.GetEndpoint("smtp");

var api = builder.AddProject<Projects.MedApp_API>("api", launchProfileName: "https")
    .WithReference(db, connectionName: "DefaultConnection")
    .WaitFor(db)
    .WithHttpHealthCheck("/health")
    // Email goes to Mailpit, unencrypted (it's local), with links to the Vite dev server where the app is opened.
    .WithEnvironment("Email__Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Email__Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("Email__Smtp__Security", "None")
    .WithEnvironment("Email__FromAddress", "medapp@localhost")
    .WithEnvironment("Email__LinkBaseUrl", "http://localhost:5173")
    // Letters sent by "secure email" go to Mailpit too, so the whole sending path can be tried locally. Mailpit isn't a
    // secure relay: declaring it one (RelayMeetsDcb1596) is only ever right here, where nothing leaves the machine. A
    // deployment sets its organisation's NHSmail or accredited relay, and declares it, itself.
    .WithEnvironment("Letters__SecureEmail__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Letters__SecureEmail__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("Letters__SecureEmail__Security", "None")
    .WithEnvironment("Letters__SecureEmail__FromAddress", "letters@localhost.nhs.net")
    .WithEnvironment("Letters__SecureEmail__FromName", "MedApp development centre")
    .WithEnvironment("Letters__SecureEmail__RelayMeetsDcb1596", "true")
    .WaitFor(mailpit);

// Attached files' bytes: Azure Blob Storage, emulated by Azurite in Docker, kept in a volume and running between
// AppHost runs, like SQL Server. The API gets its connection string as ConnectionStrings__Attachments and creates the
// container itself. Only when running: deployments set up storage with the Helm chart (Azurite on kind, a storage
// account on Azure), not from here. The blob endpoint has a fixed host port (bound to 127.0.0.1, like SQL Server's
// 14330), so medapp-dev's reset_demo_data can attach files, with Azurite's well-known development account.
if (builder.ExecutionContext.IsRunMode)
{
    var attachments = builder.AddAzureStorage("storage")
        .RunAsEmulator(azurite => azurite
            .WithBlobPort(14331)
            .WithDataVolume("medapp-azurite-data")
            .WithLifetime(ContainerLifetime.Persistent))
        .AddBlobs("attachments");

    api.WithReference(attachments, connectionName: "Attachments")
        .WaitFor(attachments);
}

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

// In production the API serves the built React app from its wwwroot, on the same origin as /api (plan Q4), and Node
// renders its pages on the server (/app/ssr in the image: started by the API, or in Kubernetes a container of its
// own). MedApp.API/Dockerfile builds them into one image, from the repo root, so the Vite dev server is for
// development only.
api.PublishAsDockerFile(container => container.WithDockerfile("..", "MedApp.API/Dockerfile"));
web.ExcludeFromManifest();

builder.Build().Run();
