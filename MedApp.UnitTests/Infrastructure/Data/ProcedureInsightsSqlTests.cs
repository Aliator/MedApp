using FluentAssertions;
using MedApp.Application.Procedures.Insights.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// The insights queries must translate to SQL Server's SQL, as EF would send them: this builds each one's SQL without
/// a database (EF only connects to run a query). What the SQL returns is covered by the in-memory tests of the same
/// LINQ (<c>ProcedureInsightsQueriesTests</c>).
/// </summary>
[TestFixture]
public sealed class ProcedureInsightsSqlTests
{
    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=insights.invalid;Database=None")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    private IQueryable<PatientProcedure> Procedures => _context.PatientProcedures.ForTemplate(TemplateId);

    private IQueryable<PatientProcedureAuditEntry> Audit =>
        _context.PatientProcedureAuditEntries.ForTemplate(_context.PatientProcedures, TemplateId);

    [Test]
    public void StatusCounts_TranslatesToAGroupBy() =>
        Procedures.StatusCounts(Now).ToQueryString().Should().Contain("GROUP BY");

    [Test]
    public void StatusCountsAt_TranslatesTheFirstChangesSinceToSubqueries() =>
        Procedures.StatusCountsAt(_context.PatientProcedureAuditEntries, Now).ToQueryString()
            .Should().Contain("GROUP BY").And.Contain("TOP(1)").And.Contain(ProcedureInsightsQueries.FromCompleted)
            .And.Contain("JSON_VALUE(").And.Contain("N'$.changes.dueDate.from'");

    [Test]
    public void CompletionsAndCreationsByHour_TranslateToGroupBys()
    {
        Audit.Completions(Now.AddDays(-7), Now).CountByHour().ToQueryString().Should().Contain("DATEPART(hour");
        Audit.Creations(Now.AddDays(-7), Now).CountByHour().ToQueryString().Should().Contain("GROUP BY");
    }

    [Test]
    public void OverdueCounts_TranslateToGroupBys()
    {
        Procedures.Overdue(Now).CountByAssignee().ToQueryString().Should().Contain("GROUP BY");
        Procedures.Overdue(Now).CountByPriority().ToQueryString().Should().Contain("GROUP BY");
    }

    [Test]
    public void StageDurations_AveragesWithDateDiffBig() =>
        Procedures.StageDurations(_context.PatientProcedureStages, _context.PatientProcedureAuditEntries, Now.AddDays(-7), Now)
            .ToQueryString().Should().Contain("DATEDIFF_BIG(second").And.Contain("AVG(").And.Contain("DISTINCT");
}
