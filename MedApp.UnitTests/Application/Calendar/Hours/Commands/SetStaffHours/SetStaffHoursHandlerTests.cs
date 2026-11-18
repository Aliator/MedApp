using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Hours.Commands.SetStaffHours;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Sites;
using MedApp.UnitTests.Application.Calendar.StaffCalendars;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours;

namespace MedApp.UnitTests.Application.Calendar.Hours.Commands.SetStaffHours;

[TestFixture]
public sealed class SetStaffHoursHandlerTests : StaffCalendarHandlerTestBase
{
    private static readonly Site Site = SitesTestConstants.NewSite();
    private static readonly TimeOnly Nine = new(9, 0);
    private static readonly TimeOnly One = new(13, 0);

    private Mock<ISiteRepository> _sites = null!;
    private SetStaffHoursHandler _handler = null!;

    /// <summary>Can set hours (ManageClinics), but not change anyone's calendar.</summary>
    private static readonly CalendarActor Clinics = new(AdminUserId, AdminUsername, new HashSet<Permission> { Permission.UseApp, Permission.ManageClinics });

    [SetUp]
    public void SetUp()
    {
        _sites = new Mock<ISiteRepository>();
        GivenSites(Site);
        Fixture.Inject(_sites.Object);
        _handler = Fixture.Create<SetStaffHoursHandler>();
    }

    private void GivenSites(params Site[] sites)
    {
        _sites.Setup(r => r.GetSiteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => sites.FirstOrDefault(s => s.Id == id));
        _sites.Setup(r => r.GetSitesAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync(sites);
    }

    private static SetStaffHoursCommand Command(CalendarActor actor, params StaffHoursDay[] days) => new(StaffUserId, days, actor);

    private static StaffHoursDay Saturday(TimeOnly? starts = null, TimeOnly? ends = null, Guid? siteId = null) =>
        new("Saturday", starts ?? Nine, ends ?? One, siteId ?? Site.Id);

    [Test]
    public async Task Handle_ManageClinics_SetsTheirHoursFromToday_UnderTheirAndTheSitesLocks()
    {
        Hours.Sites.Add(SiteRow(Site.Id, DayOfWeek.Saturday, new TimeOnly(8, 0), One));

        var response = await _handler.Handle(Command(Clinics, Saturday()), Token);

        VerifyLockedOnce(StaffUserId, Site.Id);
        Hours.Staff.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            StaffUserId, DayOfWeek = DayOfWeek.Saturday, Starts = Nine, Ends = One, SiteId = Site.Id, ValidFrom = Today, ValidTo = (DateOnly?)null
        });
        response.HasOwnHours.Should().BeTrue();
        response.CanEdit.Should().BeTrue();
        response.Since.Should().Be(Today);
        response.Days.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DayOfWeek = "Saturday", Starts = Nine, Ends = One, SiteId = (Guid?)Site.Id, SiteName = Site.Name
        });
    }

    [Test]
    public async Task Handle_ManageAnyCalendar_MaySetThemToo()
    {
        await _handler.Handle(Command(AdminActor, new StaffHoursDay("Monday", Nine, One, Site.Id)), Token);

        Hours.Staff.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_TheirOwn_WithoutEitherPermission_Is403()
    {
        var act = () => _handler.Handle(Command(StaffActor, new StaffHoursDay("Monday", Nine, One, Site.Id)), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.CantSetHours);
        Hours.Staff.Should().BeEmpty();
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_NoDays_PutsThemBackOnTheDefault_ClosingYesterdaysRows()
    {
        var old = StaffRow(StaffUserId, DayOfWeek.Monday, Nine, One, Site.Id, validFrom: LongAgo);
        Hours.Staff.Add(old);

        var response = await _handler.Handle(Command(Clinics), Token);

        old.ValidTo.Should().Be(Today.AddDays(-1), "past days keep what applied then");
        response.HasOwnHours.Should().BeFalse();
        response.Days.Select(d => d.DayOfWeek).Should().Equal("Monday", "Tuesday", "Wednesday", "Thursday", "Friday");
        response.Days.Should().AllSatisfy(d => d.SiteId.Should().BeNull());
    }

    [Test]
    public async Task Handle_TheSameHours_ChangesNothing()
    {
        var row = StaffRow(StaffUserId, DayOfWeek.Monday, Nine, One, Site.Id, validFrom: LongAgo);
        Hours.Staff.Add(row);

        await _handler.Handle(Command(Clinics, new StaffHoursDay("Monday", Nine, One, Site.Id)), Token);

        Hours.Staff.Should().ContainSingle().Which.Should().BeSameAs(row);
        row.ValidTo.Should().BeNull();
    }

    [Test]
    public async Task Handle_ADayTheSiteIsClosed_Is409()
    {
        // No hours of its own: open Monday to Friday only.
        var act = () => _handler.Handle(Command(Clinics, Saturday()), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(StaffHoursRules.SiteClosed(Site.Name, DayOfWeek.Saturday));
        Hours.Staff.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_OutsideTheSitesHours_Is409()
    {
        var act = () => _handler.Handle(Command(Clinics, new StaffHoursDay("Tuesday", Nine, new TimeOnly(20, 0), Site.Id)), Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(StaffHoursRules.OutsideSite(Site.Name, DayOfWeek.Tuesday, WorkingHours.Start, WorkingHours.End));
    }

    [Test]
    public async Task Handle_AnUnknownSite_Is404()
    {
        var act = () => _handler.Handle(Command(Clinics, Saturday(siteId: Guid.NewGuid())), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AnArchivedSite_Is409_UnlessTheyAlreadyWorkThere()
    {
        var archived = SitesTestConstants.NewSite(SitesTestConstants.OtherSiteId, SitesTestConstants.OtherSiteName, isArchived: true);
        GivenSites(Site, archived);

        var act = () => _handler.Handle(Command(Clinics, new StaffHoursDay("Monday", Nine, One, archived.Id)), Token);
        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{archived.Name} is archived. Restore it first.");

        Hours.Staff.Add(StaffRow(StaffUserId, DayOfWeek.Monday, Nine, One, archived.Id, validFrom: LongAgo));
        await _handler.Handle(Command(Clinics, new StaffHoursDay("Monday", Nine, new TimeOnly(12, 0), archived.Id)), Token);
        Hours.Staff.Should().HaveCount(2);
    }

    [Test]
    public async Task Handle_SomeoneWhoIsntStaff_Is404()
    {
        var act = () => _handler.Handle(new SetStaffHoursCommand(Guid.NewGuid(), [], Clinics), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
