using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.Domain.Clinics;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentPlacesTests
{
    private Mock<IClinicRepository> _clinics = null!;
    private ClinicSession _session = null!;
    private RoomDay? _roomDay;

    [SetUp]
    public void SetUp()
    {
        _clinics = new Mock<IClinicRepository>();
        _session = CreateSession();
        _roomDay = null;
        _clinics.Setup(r => r.GetSessionAsync(_session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => _session);
        _clinics.Setup(r => r.GetRoomDayAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid roomId, DateOnly date, CancellationToken _) => _roomDay ?? CreateRoomDay(date, Room(roomId)));
    }

    private Task<AppointmentPlace> CheckAsync(Guid? sessionId, Guid? roomId, DateTime? start = null, Guid? staff = null) =>
        AppointmentPlaces.CheckAsync(
            _clinics.Object, staff ?? StaffUserId, start ?? At(NextMonday, 9, 20), (start ?? At(NextMonday, 9, 20)).AddMinutes(20), sessionId, roomId, null, CancellationToken.None);

    [Test]
    public void Locks_TheRoomOnlyOutsideASession()
    {
        AppointmentPlaces.Locks(StaffUserId, null, null).Should().Equal(StaffUserId);
        AppointmentPlaces.Locks(StaffUserId, SessionId, SitesTestConstants.RoomId).Should().Equal(StaffUserId);
        AppointmentPlaces.Locks(StaffUserId, null, SitesTestConstants.RoomId).Should().Equal(StaffUserId, SitesTestConstants.RoomId);
    }

    [Test]
    public async Task CheckAsync_Neither_IsNowhere() => (await CheckAsync(null, null)).Should().Be(AppointmentPlace.None);

    [Test]
    public async Task CheckAsync_ASessionsSlot_TakesItsRoom() =>
        (await CheckAsync(_session.Id, null)).Should().Be(new AppointmentPlace(_session.Id, _session.RoomId));

    [Test]
    public async Task CheckAsync_NotASlot_Is409() =>
        await FluentActions.Invoking(() => CheckAsync(_session.Id, null, At(NextMonday, 9, 10)))
            .Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.NotASlot(_session));

    [Test]
    public async Task CheckAsync_SomeoneElse_Is409() =>
        await FluentActions.Invoking(() => CheckAsync(_session.Id, null, staff: OtherStaffUserId))
            .Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.NotSessionsClinician);

    [Test]
    public async Task CheckAsync_AnotherRoom_Is409() =>
        await FluentActions.Invoking(() => CheckAsync(_session.Id, Guid.NewGuid()))
            .Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.NotSessionsRoom);

    [Test]
    public async Task CheckAsync_CancelledSession_Is409()
    {
        _session = CreateSession(status: ClinicSessionStatus.Cancelled);

        await FluentActions.Invoking(() => CheckAsync(_session.Id, null))
            .Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.SessionCancelled);
    }

    [Test]
    public async Task CheckAsync_SessionsSiteClosedThatDay_Is409()
    {
        _roomDay = CreateRoomDay(closedDays: [new ClosedDay { Id = Guid.NewGuid(), Date = NextMonday, Name = ClosedDayName, SiteId = SitesTestConstants.SiteId }]);

        await FluentActions.Invoking(() => CheckAsync(_session.Id, null))
            .Should().ThrowAsync<ConflictException>().WithMessage("*is closed on Monday 23 November*");
    }

    [Test]
    public async Task CheckAsync_UnknownSession_Is404() =>
        await FluentActions.Invoking(() => CheckAsync(Guid.NewGuid(), null)).Should().ThrowAsync<NotFoundException>();

    [Test]
    public async Task CheckAsync_AFreeRoom_IsThatRoom() =>
        (await CheckAsync(null, SitesTestConstants.RoomId)).Should().Be(new AppointmentPlace(null, SitesTestConstants.RoomId));

    [Test]
    public async Task CheckAsync_ARoomWithASessionThen_Is409()
    {
        _roomDay = CreateRoomDay(sessions: [_session]);

        await FluentActions.Invoking(() => CheckAsync(null, SitesTestConstants.RoomId))
            .Should().ThrowAsync<ConflictException>().WithMessage("Room 2 is taken by a clinic session, 09:00 to 12:00.");
    }

    [Test]
    public async Task CheckAsync_AnArchivedRoom_Is409UnlessItsAlreadyThere()
    {
        _roomDay = CreateRoomDay(room: Room(isArchived: true));
        var appointment = CreateAppointment(startUtc: At(NextMonday, 14));
        appointment.RoomId = SitesTestConstants.RoomId;

        await FluentActions.Invoking(() => CheckAsync(null, SitesTestConstants.RoomId)).Should().ThrowAsync<ConflictException>();
        (await AppointmentPlaces.CheckAsync(_clinics.Object, StaffUserId, At(NextMonday, 14), At(NextMonday, 14, 30), null, SitesTestConstants.RoomId, appointment, CancellationToken.None))
            .RoomId.Should().Be(SitesTestConstants.RoomId);
    }

    [Test]
    public async Task CheckAsync_UnknownRoom_Is404()
    {
        _clinics.Setup(r => r.GetRoomDayAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync((RoomDay?)null);

        await FluentActions.Invoking(() => CheckAsync(null, Guid.NewGuid())).Should().ThrowAsync<NotFoundException>();
    }
}
