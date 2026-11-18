using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// The waiting-time clock's filters and sorts (days waited, breached, paused, review date) must translate to SQL
/// Server's SQL: date arithmetic on <c>DateOnly.DayNumber</c>, which EF turns into <c>DATEDIFF</c>. Built without a
/// database; what they return is covered by the in-memory tests of the same LINQ.
/// </summary>
[TestFixture]
public sealed class WaitingClockSqlTests
{
    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=clock.invalid;Database=None")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public void Pathways_BreachedOnlySortedByDaysWaited_TranslateToDateArithmeticOnTheReferral()
    {
        var query = CreateSearchQuery() with { BreachedOnly = true };

        var sql = _context.PatientPathways.Filter(query, Now).Sort(PathwaySearch.SortByDaysWaited, false, Today).ToQueryString();

        sql.Should().Contain("[Referrals]").And.Contain("COALESCE").And.Contain("DATEDIFF").And.Contain("[WaitStartOn]")
            .And.Contain("[TargetOn]");
    }

    [Test]
    public void Referrals_ClockFiltersAndSorts_Translate()
    {
        var query = new SearchReferralsQuery(1, 10, ReferralSearch.SortByDaysWaited, false, ["Accepted"], null, false, null, null,
            Guid.NewGuid(), BreachedOnly: true, Paused: false, ClockStopped: false);

        var byWait = _context.Referrals.Filter(query, null, Today).Sort(query.Sort, query.Ascending, Today).ToQueryString();
        var byReview = _context.Referrals.Filter(query with { Paused = true }, null, Today)
            .Sort(ReferralSearch.SortByReviewOn, true, Today).ToQueryString();
        var byTarget = _context.Referrals.Sort(ReferralSearch.SortByTargetOn, false, Today).ToQueryString();

        byWait.Should().Contain("DATEDIFF").And.Contain("[StoppedOn] IS NULL").And.Contain("[TargetOn]");
        byReview.Should().Contain("[PauseReviewOn]").And.Contain("[PausedSinceOn] IS NOT NULL");
        byTarget.Should().Contain("ORDER BY [r].[TargetOn] DESC");
    }
}
