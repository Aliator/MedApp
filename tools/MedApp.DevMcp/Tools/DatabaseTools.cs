using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

/// <summary>
/// Snapshots of the whole local database, for resetting state between runs while building the React UI.
/// The .bak files always end up in the git-ignored .backups folder: LocalDB writes there directly, and the Aspire SQL
/// container writes to its own backup path, which the AppHost bind-mounts onto that folder.
/// </summary>
[McpServerToolType]
public sealed partial class DatabaseTools
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SnapshotName();

    [McpServerTool(Name = "list_snapshots", ReadOnly = true, Idempotent = true)]
    [Description("Lists saved database snapshots, newest first.")]
    public static object ListSnapshots(DevMcpOptions options) =>
        !Directory.Exists(options.BackupDirectory)
            ? Array.Empty<object>()
            : new DirectoryInfo(options.BackupDirectory).GetFiles("*.bak")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new { name = Path.GetFileNameWithoutExtension(f.Name), savedAtUtc = f.LastWriteTimeUtc, sizeKb = f.Length / 1024 })
                .ToArray();

    [McpServerTool(Name = "save_snapshot")]
    [Description("Saves a full copy of the local database under a name (letters, digits, - and _), overwriting a snapshot of the same name. Use before a destructive experiment so restore_snapshot can undo it.")]
    public static async Task<object> SaveSnapshot(
        DevGuard guard,
        DevMcpOptions options,
        [Description("Snapshot name, e.g. before-react-login.")] string name,
        CancellationToken ct = default)
    {
        var (path, serverPath) = SnapshotPaths(guard, options, name);
        Directory.CreateDirectory(options.BackupDirectory);
        await ExecuteOnMaster(guard,
            $"BACKUP DATABASE {Quote(guard.Database)} TO DISK = @path WITH INIT, COPY_ONLY", serverPath, ct);
        if (!File.Exists(path))
            throw new McpException(
                $"SQL Server wrote the backup to '{serverPath}', but it isn't in '{options.BackupDirectory}'. Is the AppHost's bind mount for the backups folder in place?");
        return new { name, path, sizeKb = new FileInfo(path).Length / 1024 };
    }

    [McpServerTool(Name = "restore_snapshot", Destructive = true)]
    [Description("Replaces the whole local database with a saved snapshot. It first saves the current state as 'pre-restore-<timestamp>', and disconnects the running API's connections. Needs confirm: true.")]
    public static async Task<object> RestoreSnapshot(
        DevGuard guard,
        DevMcpOptions options,
        [Description("Snapshot to restore (from list_snapshots).")] string name,
        [Description("Must be true to restore.")] bool confirm = false,
        CancellationToken ct = default)
    {
        var (path, serverPath) = SnapshotPaths(guard, options, name);
        if (!File.Exists(path)) throw new McpException($"No snapshot named '{name}'. Use list_snapshots.");
        Guards.RequireConfirm(confirm, $"Restoring snapshot '{name}' over the current database");

        var safety = $"pre-restore-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        await SaveSnapshot(guard, options, safety, ct);

        var db = Quote(guard.Database);
        await ExecuteOnMaster(guard,
            $"""
             ALTER DATABASE {db} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
             BEGIN TRY
                 RESTORE DATABASE {db} FROM DISK = @path WITH REPLACE;
             END TRY
             BEGIN CATCH
                 ALTER DATABASE {db} SET MULTI_USER;
                 THROW;
             END CATCH;
             ALTER DATABASE {db} SET MULTI_USER;
             """, serverPath, ct);
        SqlConnection.ClearAllPools();
        return new { restored = name, safetySnapshot = safety };
    }

    /// <summary>The snapshot file on this machine, and the same file as SQL Server sees it (they differ for the container).</summary>
    private static (string Path, string ServerPath) SnapshotPaths(DevGuard guard, DevMcpOptions options, string name)
    {
        if (!SnapshotName().IsMatch(name))
            throw new McpException("Snapshot names may use letters, digits, '-' and '_' only (max 64).");
        var file = name + ".bak";
        var path = Path.Combine(options.BackupDirectory, file);
        if (guard.IsLocalDb) return (path, path);
        if (string.IsNullOrWhiteSpace(options.ServerBackupDirectory))
            throw new McpException("DevMcp:SqlContainer:BackupDirectory isn't set in devmcp.json, so snapshots can't be saved on this server.");
        return (path, options.ServerBackupDirectory.TrimEnd('/') + "/" + file);
    }

    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    private static async Task ExecuteOnMaster(DevGuard guard, string sql, string path, CancellationToken ct)
    {
        var csb = new SqlConnectionStringBuilder(guard.ConnectionString) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(csb.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };
        command.Parameters.AddWithValue("@path", path);
        await command.ExecuteNonQueryAsync(ct);
    }
}
