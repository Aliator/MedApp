using MedApp.Application.Attachments.Common;
using Microsoft.Data.Sqlite;

namespace MedApp.Browser.Infrastructure;

/// <summary>Attached files' bytes, in a table of the browser's database (<c>BrowserFiles</c>) instead of a blob store.</summary>
public sealed class SqliteFileStore(BrowserDatabase database) : IFileStore
{
    public const string CreateTableSql =
        "CREATE TABLE IF NOT EXISTS \"BrowserFiles\" (\"Key\" TEXT NOT NULL PRIMARY KEY, \"ContentType\" TEXT NOT NULL, \"Content\" BLOB NOT NULL)";

    public bool IsConfigured => true;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        await using var connection = database.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO \"BrowserFiles\" (\"Key\", \"ContentType\", \"Content\") VALUES ($key, $type, $content)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$type", contentType);
        command.Parameters.AddWithValue("$content", buffer.ToArray());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        await using var connection = database.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Content\" FROM \"BrowserFiles\" WHERE \"Key\" = $key";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(ct) is byte[] bytes ? new MemoryStream(bytes, writable: false) : null;
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await using var connection = database.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM \"BrowserFiles\" WHERE \"Key\" = $key";
        command.Parameters.AddWithValue("$key", key);
        await command.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Where the browser's SQLite database is: a file in the runtime's in-memory file system.</summary>
public sealed class BrowserDatabase(string path)
{
    public string Path { get; } = path;

    public string ConnectionString { get; } = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }
}
