using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Infrastructure;

// Infrastructure's repositories catch SQL Server's errors by number (2601 and 2627 a duplicate key, 547 a foreign key).
// SqlServerDialect raises this stand-in, with the same number and a message naming the index or key, from SQLite's.
namespace Microsoft.Data.SqlClient
{
    public sealed class SqlException(int number, string message, Exception inner) : DbException(message, inner)
    {
        public int Number { get; } = number;
    }

    /// <summary>Compiles the SQL Server lock calls; nothing in the browser sends one (no locks: one user).</summary>
    public sealed class SqlParameter : DbParameter
    {
        public SqlParameter(string parameterName, SqlDbType dbType, int size = 0)
        {
            ParameterName = parameterName;
            Size = size;
        }

        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = string.Empty;
        [AllowNull] public override string SourceColumn { get; set; } = string.Empty;
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }

        public override void ResetDbType()
        {
        }
    }
}

namespace Microsoft.EntityFrameworkCore
{
    public static class SqlServerDatabaseFacadeStandIn
    {
        /// <summary>The browser's database is SQLite, never SQL Server.</summary>
        public static bool IsSqlServer(this DatabaseFacade database) => false;
    }
}
