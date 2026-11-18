using FluentAssertions;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// The insights queries must translate to SQL Server's SQL, as EF would send them: this builds each one's SQL without
/// a database (EF only connects to run a query). What the SQL returns is covered by the in-memory tests of the same
/// LINQ (<c>PathwayInsightsQueriesTests</c>).
/// </summary>
[TestFixture]
public sealed class PathwayInsightsSqlTests
{
    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=insights.invalid;Database=None")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    private IQueryable<PatientPathway> Pathways => _context.PatientPathways.ForTemplate(TemplateId);

    private IQueryable<PatientPathwayAuditEntry> Audit =>
        _context.PatientPathwayAuditEntries.ForTemplate(_context.PatientPathways, TemplateId);

    [Test]
    public void StatusCounts_TranslatesToAGroupBy() =>
        Pathways.StatusCounts(Now).ToQueryString().Should().Contain("GROUP BY");

    [Test]
    public void StatusCountsAt_TranslatesTheFirstChangesSinceToSubqueries() =>
        Pathways.StatusCountsAt(_context.PatientPathwayAuditEntries, Now).ToQueryString()
            .Should().Contain("GROUP BY").And.Contain("TOP(1)").And.Contain(PathwayInsightsQueries.FromCompleted)
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
        Pathways.Overdue(Now).CountByAssignee().ToQueryString().Should().Contain("GROUP BY");
        Pathways.Overdue(Now).CountByPriority().ToQueryString().Should().Contain("GROUP BY");
    }

    [Test]
    public void StageDurations_AveragesWithDateDiffBig() =>
        Pathways.StageDurations(_context.PatientPathwayStages, _context.PatientPathwayAuditEntries, Now.AddDays(-7), Now)
            .ToQueryString().Should().Contain("DATEDIFF_BIG(second").And.Contain("AVG(").And.Contain("DISTINCT");
}
