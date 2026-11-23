namespace MedApp.DevMcp;

/// <param name="BackupDirectory">Where snapshot files live on this machine.</param>
/// <param name="ServerBackupDirectory">The same folder as the SQL container sees it (unused for LocalDB).</param>
public sealed record DevMcpOptions(IReadOnlyCollection<string> ProtectedUsers, string BackupDirectory, string? ServerBackupDirectory)
{
    public bool IsProtected(string username) =>
        ProtectedUsers.Contains(username, StringComparer.OrdinalIgnoreCase);
}
