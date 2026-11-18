using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Sites.Commands.SetSiteHours;
using MedApp.Application.Sites.Common;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;
using static MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours;

namespace MedApp.UnitTests.Application.Sites.Commands.SetSiteHours;

[TestFixture]
public sealed class SetSiteHoursHandlerTests : SiteHandlerTestBase
{
    private static readonly DateOnly Today = new(2026, 11, 18);
    private static readonly Guid StaffUserId = Guid.Parse("0000000c-0000-0000-0000-000000000001");
    private static readonly TimeOnly Eight = new(8, 0);
    private static readonly TimeOnly Twenty = new(20, 0);

    private SetSiteHoursHandler Handler => Fixture.Create<SetSiteHoursHandler>();

    [SetUp]
    public void SetUp()
    {
        var identity = new Mock<IIdentityReadService>();
        identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = "k.ashford" });
        Fixture.Inject(identity.Object);
        GivenSites(NewSite());
    }

    [Test]
    public async Task Handle_AnEveningClinic_SetsTheHoursFromToday_UnderTheSitesLock()
    {
        var response = await Handler.Handle(
            new SetSiteHoursCommand(SiteId, [new SiteHoursDay("Tuesday", Eight, Twenty), new SiteHoursDay("Monday", Eight, new TimeOnly(18, 0))]),
            Token);

        CalendarLock.LockedOnce.Should().Equal(SiteId);
        CalendarLock.Tokens.Single().Should().Be(Token);
        Hours.Sites.Should().HaveCount(2).And.AllSatisfy(h =>
        {
            h.SiteId.Should().Be(SiteId);
            h.ValidFrom.Should().Be(Today);
        });
        response.HasOwnHours.Should().BeTrue();
        response.Days.Select(d => (d.DayOfWeek, d.Closes)).Should().Equal(("Monday", new TimeOnly(18, 0)), ("Tuesday", Twenty));
    }

    [Test]
    public async Task Handle_NoDays_PutsItBackOnTheDefault()
    {
        var old = SiteRow(SiteId, DayOfWeek.Saturday, Eight, Twenty, validFrom: Today.AddDays(-10));
        Hours.Sites.Add(old);

        var response = await Handler.Handle(new SetSiteHoursCommand(SiteId, []), Token);

        old.ValidTo.Should().Be(Today.AddDays(-1));
        response.HasOwnHours.Should().BeFalse();
        response.Days.Should().HaveCount(5);
    }

    [Test]
    public async Task Handle_StaffWorkingThereOutsideTheNewHours_Is409NamingThem()
    {
        Hours.Staff.Add(StaffRow(StaffUserId, DayOfWeek.Monday, Eight, new TimeOnly(18, 0), SiteId, validFrom: Today.AddDays(-10)));

        var act = () => Handler.Handle(new SetSiteHoursCommand(SiteId, [new SiteHoursDay("Monday", new TimeOnly(9, 0), Twenty)]), Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(SiteRules.StaffOutsideHours(NewSite(), "k.ashford", DayOfWeek.Monday));
        Hours.Sites.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StaffWorkingThereInsideTheNewHours_IsFine()
    {
        Hours.Staff.Add(StaffRow(StaffUserId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), SiteId, validFrom: Today.AddDays(-10)));
        // Hours that ended before today don't count.
        Hours.Staff.Add(StaffRow(StaffUserId, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(17, 0), SiteId, validTo: Today.AddDays(-1)));

        await Handler.Handle(new SetSiteHoursCommand(SiteId, [new SiteHoursDay("Monday", Eight, Twenty)]), Token);

        Hours.Sites.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_AnArchivedSite_Is409()
    {
        Sites.Clear();
        GivenSites(NewSite(isArchived: true));

        var act = () => Handler.Handle(new SetSiteHoursCommand(SiteId, []), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SiteRules.SiteArchived(NewSite()));
    }

    [Test]
    public async Task Handle_AnUnknownSite_Is404()
    {
        var act = () => Handler.Handle(new SetSiteHoursCommand(OtherSiteId, []), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
