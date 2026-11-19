using FluentAssertions;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Attachments;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// The team walls translate to SQL Server's SQL (decision 205's <c>TeamAccessSqlTests</c>): one <c>EXISTS</c> on
/// <c>PathwayAccess</c> per pathway, and nothing for a scope that sees every team. Built without a database (EF only
/// connects to run a query); what they return is covered by <c>TeamAccessTests</c>.
/// </summary>
[TestFixture]
public sealed class TeamAccessSqlTests
{
    private static readonly TeamScope Staff = new(Guid.NewGuid(), [TeamId], seesAllTeams: false);
    private static readonly TeamScope Admin = new(Guid.NewGuid(), [], seesAllTeams: true);

    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=walls.invalid;Database=None")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public void Pathways_AScopedViewer_IsOneExistsOnTheGrants()
    {
        var sql = _context.PatientPathways.Where(TeamAccess.Pathways(Staff, _context.PathwayAccess)).ToQueryString();

        sql.Should().Contain("EXISTS (").And.Contain("FROM [PathwayAccess]").And.Contain("[EndedAtUtc] IS NULL");
        Occurrences(sql, "EXISTS (").Should().Be(1);
    }

    [Test]
    public void Pathways_AnAdmin_AddsNoCondition()
    {
        var sql = _context.PatientPathways.Where(TeamAccess.Pathways(Admin, _context.PathwayAccess)).ToQueryString();

        sql.Should().NotContain("PathwayAccess");
    }

    [Test]
    public void Search_AScopedViewer_FiltersByTheGrantsBeforeItsOwnFilters()
    {
        var sql = _context.PatientPathways
            .Where(TeamAccess.Pathways(Staff, _context.PathwayAccess))
            .Filter(CreateSearchQuery(), Now)
            .ToQueryString();

        sql.Should().Contain("FROM [PathwayAccess]");
    }

    [Test]
    public void ByPathway_TranslatesTheTeamAndThePathwaysGrants()
    {
        var sql = _context.Attachments
            .Where(TeamAccess.ByPathway<Attachment>(
                new TeamScope(Guid.NewGuid(), [TeamId, Guid.NewGuid()], seesAllTeams: false),
                _context.PathwayAccess, a => a.PathwayId, a => a.TeamId))
            .ToQueryString();

        sql.Should().Contain("[TeamId] IN (").And.Contain("FROM [PathwayAccess]");
    }

    [Test]
    public void AttachmentsList_AScopedViewer_Translates()
    {
        var query = new GetAttachmentsQuery(Guid.NewGuid(), null, null, null, false, Guid.NewGuid(), false, true, Staff);

        AttachmentSearch.Filter(_context.Attachments, query, _context.PathwayAccess).ToQueryString()
            .Should().Contain("FROM [PathwayAccess]");
    }

    [Test]
    public void Timeline_AScopedViewer_EachSideTranslates()
    {
        var query = new GetPatientTimelineQuery(Guid.NewGuid(), null, 20, null, null, false, Staff);

        PatientTimelineSearch.Page(_context.PatientPathwayAuditEntries, _context.PatientPathways, query, _context.PathwayAccess)
            .ToQueryString().Should().Contain("FROM [PathwayAccess]");
        AppointmentTimelineSearch.Page(_context.AppointmentEvents, _context.PatientPathways, query, _context.PathwayAccess)
            .ToQueryString().Should().Contain("FROM [PathwayAccess]");
        ReferralTimelineSearch.Page(_context.ReferralEvents, _context.Referrals, query, _context.PathwayAccess)
            .ToQueryString().Should().Contain("FROM [PathwayAccess]");
    }

    [Test]
    public void Timeline_AScopedViewerWithoutTheGrants_Throws()
    {
        var query = new GetPatientTimelineQuery(Guid.NewGuid(), null, 20, null, null, false, Staff);

        var act = () => PatientTimelineSearch.Page(_context.PatientPathwayAuditEntries, _context.PatientPathways, query);

        act.Should().Throw<ArgumentNullException>("a forgotten wall must fail, never show every team's entries");
    }

    private static int Occurrences(string text, string part) =>
        (text.Length - text.Replace(part, string.Empty).Length) / part.Length;
}
