using FluentAssertions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Application.Results.Queries.SearchResults;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// The team walls on results, MDT items and Insights (MC5) must translate to SQL Server's SQL, as EF would send them: one
/// <c>EXISTS</c> on the grants, built without a database. What they return is covered by the in-memory tests of the same
/// LINQ.
/// </summary>
[TestFixture]
public sealed class ResultsAndMdtTeamWallsSqlTests
{
    private static readonly TeamScope Staff =
        new(Guid.Parse("0000005f-0000-0000-0000-000000000001"), [Guid.Parse("0000005f-0000-0000-0000-0000000000d1")], seesAllTeams: false);

    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=walls.invalid;Database=None")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public void Results_StaffsSearchAndCount_Translate()
    {
        var query = new SearchResultsQuery(1, 10, "Awaiting", Staff.TeamIds.Single(), null, null, Staff.UserId, "freya", Scope: Staff);

        var search = _context.Results.Filter(query, [Guid.NewGuid()], _context.PathwayAccess).Sort(query.ParsedView).ToQueryString();
        var count = _context.Results.Visible(Staff, _context.PathwayAccess).Where(r => r.ResponsibleUserId == Staff.UserId).ToQueryString();

        search.Should().Contain("EXISTS").And.Contain("[PathwayAccess]").And.Contain("ORDER BY");
        count.Should().Contain("EXISTS").And.Contain("[PathwayAccess]");
    }

    [Test]
    public void MdtItems_StaffsView_Translates() =>
        _context.MdtItems.Visible(Staff, _context.PathwayAccess).Where(i => i.MeetingId != null)
            .GroupBy(i => i.MeetingId).Select(g => new { g.Key, Count = g.Count() })
            .ToQueryString().Should().Contain("EXISTS").And.Contain("GROUP BY");

    [Test]
    public void Insights_TheViewersTeams_Translate() =>
        _context.PatientPathwayAuditEntries
            .InScope(_context.PatientPathways, new InsightsScope(TeamIds: [.. Staff.TeamIds]))
            .ToQueryString().Should().Contain("EXISTS").And.Contain("[TeamId]");
}
