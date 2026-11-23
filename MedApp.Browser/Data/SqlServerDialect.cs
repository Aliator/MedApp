using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MedApp.Browser.Data;

/// <summary>
/// What Infrastructure says in SQL Server's words, said in SQLite's, so its repositories run unchanged:
/// <list type="bullet">
/// <item>The specimen number sequence (<c>NEXT VALUE FOR</c>) becomes a counter row in <see cref="SqliteModelCustomizer.SequencesTable"/>.</item>
/// <item>Lock hints (<c>WITH (UPDLOCK, HOLDLOCK)</c>) go: one person at a time.</item>
/// <item>A broken unique index or foreign key is raised as SQL Server's error number (2601, 2627, 547), with the
/// index's or keys' names in the message, as the repositories' catch clauses read them.</item>
/// </list>
/// </summary>
public sealed partial class SqlServerDialect : DbCommandInterceptor
{
    public const string CreateSequencesTableSql =
        $"CREATE TABLE IF NOT EXISTS \"{SqliteModelCustomizer.SequencesTable}\" (\"Name\" TEXT NOT NULL PRIMARY KEY, \"Value\" INTEGER NOT NULL)";

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Rewrite(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Rewrite(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Rewrite(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Rewrite(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Rewrite(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Rewrite(command);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Raise(command, eventData);

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Raise(command, eventData);
        return Task.CompletedTask;
    }

    private static void Rewrite(DbCommand command)
    {
        var sql = command.CommandText;
        if (!sql.Contains("NEXT VALUE FOR", StringComparison.Ordinal) && !sql.Contains("WITH (", StringComparison.Ordinal))
            return;

        sql = NextValue().Replace(sql, match =>
            $"INSERT INTO \"{SqliteModelCustomizer.SequencesTable}\" (\"Name\", \"Value\") VALUES ('{match.Groups["name"].Value}', 1) " +
            $"ON CONFLICT (\"Name\") DO UPDATE SET \"Value\" = \"Value\" + 1 RETURNING \"Value\" AS \"Value\"");
        command.CommandText = LockHints().Replace(sql, string.Empty);
    }

    private static void Raise(DbCommand command, CommandErrorEventData eventData)
    {
        if (eventData.Exception is not SqliteException { SqliteErrorCode: 19 } sqlite || eventData.Context is not { } context)
            return;

        switch (sqlite.SqliteExtendedErrorCode)
        {
            case 2067:
                throw new SqlException(2601, Duplicate(context.Model, sqlite.Message), sqlite);
            case 1555:
                throw new SqlException(2627, $"Violation of PRIMARY KEY constraint. {sqlite.Message}", sqlite);
            case 787:
                throw new SqlException(547, ForeignKeys(context.Model, command.CommandText), sqlite);
        }
    }

    /// <summary>SQLite names the table and columns ("UNIQUE constraint failed: Patients.NhsNumber"); SQL Server the index.</summary>
    private static string Duplicate(IModel model, string message)
    {
        var failed = UniqueFailure().Match(message);
        if (!failed.Success)
            return message;

        var columns = failed.Groups["columns"].Value.Split(", ")
            .Select(column => column[(column.IndexOf('.') + 1)..])
            .ToList();
        var table = failed.Groups["columns"].Value.Split('.')[0];
        var index = model.GetEntityTypes()
            .Where(entity => entity.GetTableName() == table)
            .SelectMany(entity => entity.GetIndexes())
            .FirstOrDefault(index => index.IsUnique
                                     && index.Properties.Select(p => p.GetColumnName()).SequenceEqual(columns));

        return $"Cannot insert duplicate key row in object 'dbo.{table}' with unique index '{index?.GetDatabaseName()}'.";
    }

    /// <summary>
    /// SQLite doesn't say which key failed, so the message names every foreign key the statement's tables take part in
    /// (from them, and to them). Near enough for one person's changes: the repositories ask whether a key is named.
    /// </summary>
    private static string ForeignKeys(IModel model, string sql)
    {
        var tables = TableNames().Matches(sql).Select(match => match.Groups["table"].Value).ToHashSet(StringComparer.Ordinal);
        var keys = model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(key => tables.Contains(key.DeclaringEntityType.GetTableName() ?? string.Empty)
                          || tables.Contains(key.PrincipalEntityType.GetTableName() ?? string.Empty))
            .Select(key => key.GetConstraintName())
            .Distinct();

        return $"The statement conflicted with the FOREIGN KEY constraint: {string.Join(", ", keys.Select(k => $"\"{k}\""))}.";
    }

    [GeneratedRegex(@"SELECT NEXT VALUE FOR \[(?<name>[^\]]+)\] AS \[Value\]")]
    private static partial Regex NextValue();

    [GeneratedRegex(@"\s*WITH \((UPDLOCK|HOLDLOCK|ROWLOCK|READPAST|NOLOCK)(,\s*(UPDLOCK|HOLDLOCK|ROWLOCK|READPAST|NOLOCK))*\)")]
    private static partial Regex LockHints();

    [GeneratedRegex(@"UNIQUE constraint failed: (?<columns>[^']+)")]
    private static partial Regex UniqueFailure();

    [GeneratedRegex(@"(?:INSERT INTO|UPDATE|DELETE FROM)\s+[""\[](?<table>[^""\]]+)[""\]]")]
    private static partial Regex TableNames();
}
