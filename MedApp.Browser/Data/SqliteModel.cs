using MedApp.Application.Pathways.Insights.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MedApp.Browser.Data;

/// <summary>
/// The API's model (<c>MedAppDbContext.OnModelCreating</c>, unchanged), made to fit SQLite after it's built, so the
/// browser's database can be created from it (<c>EnsureCreated</c>). Only the browser's model changes; the API's, and
/// its migrations, don't. One person at a time, so concurrency's mapping goes:
/// <list type="bullet">
/// <item>Row versions aren't generated (SQLite has none) nor checked: one user can't race themselves.</item>
/// <item>The specimen number sequence becomes a counter row (<see cref="SqlServerDialect"/>).</item>
/// <item>SQL Server's column types and its <c>N'…'</c> literals go; text compares ignoring case, as SQL Server's
/// collation does; <c>DateTimeOffset</c>s are stored as numbers, so SQLite can compare and sort them.</item>
/// <item>The insights' functions get SQLite's equivalents.</item>
/// </list>
/// Filtered indexes keep their SQL: SQLite reads SQL Server's bracketed names.
/// </summary>
public sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public const string SequencesTable = "__BrowserSequences";

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        var model = modelBuilder.Model;
        foreach (var sequence in model.GetSequences().ToList())
            model.RemoveSequence(sequence.Name, sequence.Schema);

        foreach (var entity in model.GetEntityTypes())
        {
            foreach (var property in entity.GetDeclaredProperties())
            {
                if (property.ClrType == typeof(byte[]) && property.IsConcurrencyToken && property.ValueGenerated == ValueGenerated.OnAddOrUpdate)
                {
                    property.IsConcurrencyToken = false;
                    property.ValueGenerated = ValueGenerated.Never;
                }

                if (property.GetColumnType() is not null)
                    property.SetColumnType(null);

                if (property.GetDefaultValueSql() is { } defaultSql && defaultSql.StartsWith("N'", StringComparison.Ordinal))
                    property.SetDefaultValueSql(defaultSql[1..]);

                if (property.ClrType == typeof(string))
                    property.SetCollation("NOCASE");

                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }

        modelBuilder
            .HasDbFunction(typeof(InsightsFunctions).GetMethod(nameof(InsightsFunctions.SecondsBetween))!)
            .HasTranslation(args => new SqlBinaryExpression(
                System.Linq.Expressions.ExpressionType.Subtract,
                new SqlFunctionExpression("unixepoch", [args[1]], true, [true], typeof(long), null),
                new SqlFunctionExpression("unixepoch", [args[0]], true, [true], typeof(long), null),
                typeof(long),
                null));

        modelBuilder
            .HasDbFunction(typeof(InsightsFunctions).GetMethod(nameof(InsightsFunctions.JsonValue))!)
            .HasName("json_extract")
            .IsBuiltIn();
    }
}
