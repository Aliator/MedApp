using MedApp.Application.Procedures.Insights.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace MedApp.Infrastructure.Data;

/// <summary>
/// Maps <see cref="InsightsFunctions"/> to SQL Server, so the insights queries can use them in SQL. They're functions,
/// not tables or columns, so the migrations don't see them.
/// </summary>
public static class InsightsDbFunctions
{
    public static void Register(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasDbFunction(typeof(InsightsFunctions).GetMethod(nameof(InsightsFunctions.SecondsBetween))!)
            .HasTranslation(args => new SqlFunctionExpression(
                "DATEDIFF_BIG",
                [new SqlFragmentExpression("second"), args[0], args[1]],
                nullable: true,
                argumentsPropagateNullability: [false, true, true],
                typeof(long),
                null));

        modelBuilder
            .HasDbFunction(typeof(InsightsFunctions).GetMethod(nameof(InsightsFunctions.JsonValue))!)
            .HasName("JSON_VALUE")
            .IsBuiltIn();
    }
}
