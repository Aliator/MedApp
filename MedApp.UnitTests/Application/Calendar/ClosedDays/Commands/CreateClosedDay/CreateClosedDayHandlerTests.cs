using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Notifications;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

[TestFixture]
public sealed class CreateClosedDayHandlerTests
{
    private Mock<IClosedDayRepository> _repository = null!;
    private CreateClosedDayHandler _handler = null!;
    private List<ClosedDay> _added = null!;
    private Mock<ICalendarRepository> _calendar = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private Dictionary<DateOnly, List<StaffBookings>> _bookings = null!;
    private List<Notification> _sent = null!;
    private List<ClosedDay> _onDate = null!;
    private Mock<ISiteRepository> _sites = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IClosedDayRepository>();
        _onDate = [];
        _repository.Setup(r => r.GetOnAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly date, CancellationToken _) => (IReadOnlyList<ClosedDay>)_onDate.Where(d => d.Date == date).ToList());
        _sites = new Mock<ISiteRepository>();
        _sites.Setup(r => r.GetSiteAsync(SitesTestConstants.SiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SitesTestConstants.NewSite());
        _sites.Setup(r => r.GetSiteAsync(SitesTestConstants.OtherSiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SitesTestConstants.NewSite(SitesTestConstants.OtherSiteId, SitesTestConstants.OtherSiteName, isArchived: true));
        _added = [];
        _repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ClosedDay>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<ClosedDay> days, CancellationToken _) => _added.AddRange(days))
            .Returns(Task.CompletedTask);
        _calendar = new Mock<ICalendarRepository>();
        _bookings = [];
        _calendar.Setup(c => c.GetBookingsOnAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly date, CancellationToken _) => (IReadOnlyList<StaffBookings>)(_bookings.GetValueOrDefault(date) ?? []));
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = StaffUsername, [OtherStaffUserId] = OtherStaffUsername });
        _sent = [];
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        fixture.Inject(_calendar.Object);
        fixture.Inject(_identity.Object);
        fixture.Inject(sender.Object);
        fixture.Inject(_repository.Object);
        fixture.Inject(_sites.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<CreateClosedDayHandler>();
    }

    [Test]
    public async Task Handle_FutureDate_AddsItWithTheTrimmedNameAndWhoAddedIt()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new CreateClosedDayCommand(ClosedWednesday, $"  {ClosedDayName} ", AdminActor), cts.Token);

        var day = _added.Should().ContainSingle().Subject;
        day.Date.Should().Be(ClosedWednesday);
        day.Name.Should().Be(ClosedDayName);
        day.CreatedAtUtc.Should().Be(Now);
        day.CreatedByUserId.Should().Be(AdminUserId);
        _repository.Verify(r => r.GetOnAsync(ClosedWednesday, cts.Token), Times.Once);
        _repository.Verify(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ClosedDay>>(), cts.Token), Times.Once);

        response.Id.Should().Be(day.Id);
        response.Date.Should().Be(ClosedWednesday);
        response.Name.Should().Be(ClosedDayName);
        response.IsPast.Should().BeFalse();
        response.SiteId.Should().BeNull();
        response.SiteName.Should().BeNull();
        day.SiteId.Should().BeNull();
        _sites.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_ASite_ClosesItThere_WithoutBookingsOrNotifications()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(
            new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor, SitesTestConstants.SiteId), cts.Token);

        var day = _added.Should().ContainSingle().Subject;
        day.SiteId.Should().Be(SitesTestConstants.SiteId);
        response.SiteId.Should().Be(SitesTestConstants.SiteId);
        response.SiteName.Should().Be(SitesTestConstants.SiteName);
        response.Bookings.Should().BeNull();
        _sites.Verify(r => r.GetSiteAsync(SitesTestConstants.SiteId, cts.Token), Times.Once);
        _calendar.Verify(c => c.GetBookingsOnAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnUnknownSite_ThrowsNotFound()
    {
        var act = () => _handler.Handle(
            new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnArchivedSite_ThrowsConflict()
    {
        var act = () => _handler.Handle(
            new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor, SitesTestConstants.OtherSiteId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{SitesTestConstants.OtherSiteName} is archived. Restore it first.");
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ASiteOnADateClosedEverywhere_ThrowsConflictNamingIt()
    {
        _onDate.Add(CalendarTestConstants.CreateClosedDay(name: "Christmas Day"));

        var act = () => _handler.Handle(
            new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor, SitesTestConstants.SiteId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Wednesday 25 November is already a closed day: Christmas Day.");
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ASiteAlreadyClosedThatDay_ThrowsConflictNamingIt()
    {
        _onDate.Add(InMemoryClosedDays.On(ClosedWednesday, "Boiler repair", SitesTestConstants.SiteId));

        var act = () => _handler.Handle(
            new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor, SitesTestConstants.SiteId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"Wednesday 25 November is already a closed day at {SitesTestConstants.SiteName}: Boiler repair.");
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_EverywhereOnADateASiteIsClosed_AddsIt()
    {
        _onDate.Add(InMemoryClosedDays.On(ClosedWednesday, "Boiler repair", SitesTestConstants.SiteId));

        await _handler.Handle(new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor), CancellationToken.None);

        _added.Should().ContainSingle(d => d.SiteId == null);
    }

    [Test]
    public async Task Handle_ASiteOnADateAnotherSiteIsClosed_AddsIt()
    {
        _onDate.Add(InMemoryClosedDays.On(ClosedWednesday, "Boiler repair", Guid.NewGuid()));

        await _handler.Handle(
            new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor, SitesTestConstants.SiteId), CancellationToken.None);

        _added.Should().ContainSingle(d => d.SiteId == SitesTestConstants.SiteId);
    }

    [Test]
    public async Task Handle_Today_AddsIt()
    {
        await _handler.Handle(new CreateClosedDayCommand(Today, ClosedDayName, AdminActor), CancellationToken.None);

        _added.Should().ContainSingle(d => d.Date == Today);
    }

    [Test]
    public async Task Handle_PastDate_ThrowsConflictAndAddsNothing()
    {
        var act = () => _handler.Handle(new CreateClosedDayCommand(Today.AddDays(-1), ClosedDayName, AdminActor), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClosedDayRules.PastCantBeAdded);
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DateAlreadyClosed_ThrowsConflictNamingIt()
    {
        _onDate.Add(CalendarTestConstants.CreateClosedDay(name: "Christmas Day"));

        var act = () => _handler.Handle(new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Wednesday 25 November is already a closed day: Christmas Day.");
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ADayWithBookings_SaysWhatsOnIt_AndTellsEachMemberOfStaffButTheAdmin()
    {
        _bookings[ClosedWednesday] =
        [
            new StaffBookings(StaffUserId, Appointments: 2, Blocks: 0, Meetings: 1),
            new StaffBookings(AdminUserId, Appointments: 0, Blocks: 1, Meetings: 0)
        ];
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor), cts.Token);

        response.Bookings.Should().NotBeNull();
        response.Bookings!.Should().BeEquivalentTo(new { Date = ClosedWednesday, Appointments = 2, Blocks = 1, Meetings = 1 });
        response.Bookings.Staff.Select(s => s.Username).Should().Equal(StaffUsername, "Deleted user");
        _calendar.Verify(c => c.GetBookingsOnAsync(ClosedWednesday, cts.Token), Times.Once);

        var notification = _sent.Should().ContainSingle().Subject;
        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.DayClosed);
        notification.Title.Should().Be("Day closed");
        notification.CalendarDate.Should().Be(ClosedWednesday);
        notification.AppointmentId.Should().BeNull();
        notification.Message.Should().Be(
            $"{AdminUsername} closed Wednesday 25 November ({ClosedDayName}). You have 2 appointments and a meeting then. Move or cancel what's still to come, and tell any patients.");
    }

    [Test]
    public async Task Handle_ANothingBookedDay_TellsNobody()
    {
        var response = await _handler.Handle(new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor), CancellationToken.None);

        response.Bookings!.Should().BeEquivalentTo(new { Appointments = 0, Blocks = 0, Meetings = 0 });
        response.Bookings.Staff.Should().BeEmpty();
        _sent.Should().BeEmpty();
        _identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
