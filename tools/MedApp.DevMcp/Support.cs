using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MedApp.DevMcp;

/// <summary>Refuses to run against anything but a local development database.</summary>
public sealed record DevGuard(string ConnectionString, string Server, string Database, bool IsLocalDb)
{
    private static readonly Regex LocalServer = new(
        @"^(\(localdb\)\\.+|\.|localhost|127\.0\.0\.1|\[::1\]|::1|tcp:(localhost|127\.0\.0\.1))(,\d+)?(\\.+)?$",
        RegexOptions.IgnoreCase);

    public static DevGuard Check(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not set, and there's no SQL container password in the AppHost's user secrets. Run the AppHost once (dotnet run --project MedApp.AppHost) so Aspire creates it.");
        var csb = new SqlConnectionStringBuilder(connectionString);
        if (!LocalServer.IsMatch(csb.DataSource.Trim()))
            throw new InvalidOperationException(
                $"medapp-dev only runs against a local development database (LocalDB or localhost); refusing '{csb.DataSource}'.");
        if (string.IsNullOrWhiteSpace(csb.InitialCatalog))
            throw new InvalidOperationException("The connection string has no database name.");

        var isLocalDb = csb.DataSource.TrimStart().StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase);
        return new DevGuard(connectionString, csb.DataSource, csb.InitialCatalog, isLocalDb);
    }
}

public static class RepoRoot
{
    public static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "MedApp.sln")))
                return dir.FullName;
        throw new InvalidOperationException("Could not find the repo root (MedApp.sln) above " + AppContext.BaseDirectory);
    }
}
