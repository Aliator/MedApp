using FluentAssertions;
using MedApp.Application.Activity.Queries.SearchActivity;
using MedApp.Domain.Activity;

namespace MedApp.UnitTests.Application.Activity.Queries.SearchActivity;

[TestFixture]
public sealed class ActivitySearchTests
{
    private static readonly Guid PatientA = Guid.NewGuid();
    private static readonly Guid UserA = Guid.NewGuid();

    private static SearchActivityQuery Query() => new(1, 10, false, null, null, null, null, null);

    private static ActivityEntry Entry(
        ActivityAction action = ActivityAction.PatientViewed,
        string username = "staff-one",
        Guid? userId = null,
        Guid? patientId = null,
        string? target = null,
        string? ip = null,
        DateTime? at = null,
        Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Action = action,
        Username = username,
        UserId = userId,
        PatientId = patientId,
        TargetUsername = target,
        IpAddress = ip,
        AtUtc = at ?? new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)
    };

    [Test]
    public void Filter_NoFilters_KeepsEverything()
    {
        var entries = new[] { Entry(), Entry() }.AsQueryable();

        entries.Filter(Query()).Should().HaveCount(2);
    }

    [Test]
    public void Filter_ByPatientAndUser_KeepsOnlyThoseEntries()
    {
        var match = Entry(patientId: PatientA, userId: UserA);
        var entries = new[] { match, Entry(patientId: PatientA), Entry(userId: UserA) }.AsQueryable();

        entries.Filter(Query() with { PatientId = PatientA, UserId = UserA }).Should().Equal(match);
    }

    [Test]
    public void Filter_ByActions_KeepsAnyOfThem_IgnoringUnknownNamesAndCase()
    {
        var viewed = Entry(ActivityAction.PatientViewed);
        var signedIn = Entry(ActivityAction.SignedIn);
        var entries = new[] { viewed, signedIn, Entry(ActivityAction.RolesChanged) }.AsQueryable();

        entries.Filter(Query() with { Actions = ["patientviewed", "SignedIn", "Nonsense", "3"] })
            .Should().BeEquivalentTo([viewed, signedIn]);
    }

    [Test]
    public void Filter_OnlyUnknownActions_KeepsEverything()
    {
        var entries = new[] { Entry(), Entry(ActivityAction.SignedIn) }.AsQueryable();

        entries.Filter(Query() with { Actions = ["Nonsense"] }).Should().HaveCount(2);
    }

    [TestCase("STAFF-ONE")]
    [TestCase("target-acc")]
    [TestCase("192.0.2")]
    public void Filter_BySearch_MatchesUsernameTargetOrAddress(string term)
    {
        var match = Entry(username: "staff-one", target: "target-account", ip: "192.0.2.10");
        var other = Entry(username: "someone-else");

        new[] { match, other }.AsQueryable().Filter(Query() with { Search = $"  {term}  " }).Should().Equal(match);
    }

    [Test]
    public void Filter_BlankSearch_IsIgnored()
    {
        new[] { Entry() }.AsQueryable().Filter(Query() with { Search = "   " }).Should().HaveCount(1);
    }

    [Test]
    public void Filter_ByDates_UsesWholeUkDays()
    {
        // 15 June 2026 in the UK (BST) runs from 23:00 UTC on the 14th to 22:59:59 UTC on the 15th.
        var tooEarly = Entry(at: new DateTime(2026, 6, 14, 22, 59, 0, DateTimeKind.Utc));
        var startOfDay = Entry(at: new DateTime(2026, 6, 14, 23, 0, 0, DateTimeKind.Utc));
        var endOfDay = Entry(at: new DateTime(2026, 6, 15, 22, 59, 0, DateTimeKind.Utc));
        var tooLate = Entry(at: new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc));
        var day = new DateOnly(2026, 6, 15);

        new[] { tooEarly, startOfDay, endOfDay, tooLate }.AsQueryable()
            .Filter(Query() with { From = day, To = day })
            .Should().BeEquivalentTo([startOfDay, endOfDay]);
    }

    [Test]
    public void Sort_NewestFirstByDefault_OldestFirstWhenAscending_TiesById()
    {
        var at = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var old = Entry(at: at.AddDays(-1));
        var tieLow = Entry(at: at, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var tieHigh = Entry(at: at, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var entries = new[] { tieHigh, old, tieLow }.AsQueryable();

        entries.Sort(ascending: false).Should().Equal(tieLow, tieHigh, old);
        entries.Sort(ascending: true).Should().Equal(old, tieLow, tieHigh);
    }

    [Test]
    public void IsAction_AndActionNames()
    {
        ActivitySearch.IsAction("SignInFailed").Should().BeTrue();
        ActivitySearch.IsAction("nope").Should().BeFalse();
        ActivitySearch.IsAction(null).Should().BeFalse();
        ActivitySearch.ActionNames.Should().Contain("PatientViewed").And.Contain("RoleDeleted");
    }
}
