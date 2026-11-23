using System.Reflection;
using MedApp.Application.Pathways.Insights.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace MedApp.Browser.Data;

/// <summary>
/// SQLite's form of <see cref="InsightsFunctions.SecondsBetween"/>: <c>unixepoch(to) - unixepoch(from)</c>. A
/// translator, not the model's own (<c>HasTranslation</c>), as a compiled model can't hold a translation.
/// </summary>
public sealed class SqliteInsightsTranslations(ISqlExpressionFactory sql) : IMethodCallTranslatorPlugin, IMethodCallTranslator
{
    public static readonly MethodInfo SecondsBetween = typeof(InsightsFunctions).GetMethod(nameof(InsightsFunctions.SecondsBetween))!;

    public IEnumerable<IMethodCallTranslator> Translators => [this];

    public SqlExpression? Translate(
        SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger) =>
        method == SecondsBetween ? sql.Subtract(UnixEpoch(arguments[1]), UnixEpoch(arguments[0])) : null;

    private SqlExpression UnixEpoch(SqlExpression moment) =>
        sql.Function("unixepoch", [moment], nullable: true, argumentsPropagateNullability: [true], typeof(long));

    /// <summary>Adds the translator to a context's options.</summary>
    public static void AddTo(DbContextOptionsBuilder options) =>
        ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new Extension());

    private sealed class Extension : IDbContextOptionsExtension
    {
        public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

        public void ApplyServices(IServiceCollection services) =>
            new EntityFrameworkRelationalServicesBuilder(services).TryAdd<IMethodCallTranslatorPlugin, SqliteInsightsTranslations>();

        public void Validate(IDbContextOptions options)
        {
        }

        private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
        {
            public override bool IsDatabaseProvider => false;

            public override string LogFragment => string.Empty;

            public override int GetServiceProviderHashCode() => 0;

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
            }
        }
    }
}
