using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;

namespace MedApp.DevMcp;

public sealed record DevMcpOptions(IReadOnlyCollection<string> ProtectedUsers, string BackupDirectory)
{
    public bool IsProtected(string username) =>
        ProtectedUsers.Contains(username, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Refuses to run against anything but a local development database.</summary>
public sealed record DevGuard(string ConnectionString, string Server, string Database, bool IsLocalDb)
{
    private static readonly Regex LocalServer = new(
        @"^(\(localdb\)\\.+|\.|localhost|127\.0\.0\.1|\[::1\]|::1|tcp:(localhost|127\.0\.0\.1))(,\d+)?(\\.+)?$",
        RegexOptions.IgnoreCase);

    public static DevGuard Check(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not set.");
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

public static class Guards
{
    public static void RequireConfirm(bool confirm, string action)
    {
        if (!confirm)
            throw new McpException($"{action} is destructive. Call again with confirm: true to go ahead.");
    }

    public static void RequireNotProtected(DevMcpOptions options, string username, string action)
    {
        if (options.IsProtected(username))
            throw new McpException(
                $"'{username}' is a protected account (DevMcp:ProtectedUsers in devmcp.json); medapp-dev won't {action} it.");
    }

    public static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new McpException($"{action} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }

    /// <summary>Runs a MediatR call and turns FluentValidation failures into a readable tool error.</summary>
    public static async Task<T> Validated<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException ex)
        {
            throw new McpException("Validation failed: " +
                string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
        }
    }
}

public static class Passwords
{
    /// <summary>Letters and digits only, with at least one upper, lower and digit (meets the Identity rules in appsettings).</summary>
    public static string Generate(int length = 20)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789";
        const string all = upper + lower + digits;
        var chars = new char[length];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
