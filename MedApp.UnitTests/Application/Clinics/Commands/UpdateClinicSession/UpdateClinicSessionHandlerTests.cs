using AutoFixture;
using FluentAssertions;
using MedApp.Application.Clinics.Commands.UpdateClinicSession;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Commands.UpdateClinicSession;

[TestFixture]
public sealed class UpdateClinicSessionHandlerTests : ClinicHandlerTestBase
{
    private UpdateClinicSessionHandler _handler = null!;
    private ClinicSession _session = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = Fixture.Create<UpdateClinicSessionHandler>();
        _session = GivenSession(CreateSession());
    }

    private UpdateClinicSessionCommand Command(
        Guid? roomId = null, DateTime? startUtc = null, DateTime? endUtc = null, int slotMinutes = 20, DateTime? expected = null) =>
        new(_session.Id, roomId ?? _session.RoomId, SpecialtyId, startUtc ?? At(NextMonday, 9), endUtc ?? At(NextMonday, 12), slotMinutes,
            expected ?? _session.LastUpdated, AdminActor);

    private void GivenBooked(params Appointment[] appointments) =>
        Clinics.Setup(r => r.GetSessionAppointmentsForUpdateAsync(_session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointments);

    [Test]
    public async Task Handle_NoBookings_MovesItAndItsSlots()
    {
        await _handler.Handle(Command(startUtc: At(NextMonday, 13), endUtc: At(NextMonday, 15), slotMinutes: 30), Token);

        _session.StartUtc.Should().Be(At(NextMonday, 13));
        _session.EndUtc.Should().Be(At(NextMonday, 15));
        _session.SlotMinutes.Should().Be(30);
        _session.LastUpdated.Should().Be(Now);
        Saved.Should().ContainSingle();
        Lock.LockedOnce.Should().BeEquivalentTo([StaffUserId, _session.RoomId]);
    }

    [Test]
    public async Task Handle_WithBookings_MovingItIs409()
    {
        GivenBooked(InSession(_session, At(NextMonday, 9)));

        var act = () => _handler.Handle(Command(startUtc: At(NextMonday, 10), endUtc: At(NextMonday, 13)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.HasBookings);
        Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_WithBookings_ChangingTheRoomMovesTheAppointmentsWithIt()
    {
        var booked = InSession(_session, At(NextMonday, 9));
        GivenBooked(booked);

        var response = await _handler.Handle(Command(roomId: SitesTestConstants.OtherRoomId), Token);

        _session.RoomId.Should().Be(SitesTestConstants.OtherRoomId);
        booked.RoomId.Should().Be(SitesTestConstants.OtherRoomId);
        booked.LastUpdated.Should().Be(Now);
        Lock.LockedOnce.Should().BeEquivalentTo([StaffUserId, SitesTestConstants.RoomId, SitesTestConstants.OtherRoomId]);
        response.BookedCount.Should().Be(1);
    }

    [Test]
    public async Task Handle_NewRoomTaken_Is409()
    {
        RoomDays[SitesTestConstants.OtherRoomId] = CreateRoomDay(
            room: Room(SitesTestConstants.OtherRoomId, SitesTestConstants.OtherRoomName),
            sessions: [CreateSession(At(NextMonday, 11), At(NextMonday, 12), id: OtherSessionId, staffUserId: OtherStaffUserId)]);

        var act = () => _handler.Handle(Command(roomId: SitesTestConstants.OtherRoomId), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Treatment room is taken by a clinic session, 11:00 to 12:00.");
    }

    [Test]
    public async Task Handle_ChangedSinceItWasRead_Is409WithoutALock()
    {
        var act = () => _handler.Handle(Command(expected: _session.LastUpdated.AddMinutes(-1)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.ChangedMeanwhile);
        Lock.Locked.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Cancelled_Is409()
    {
        _session.Status = ClinicSessionStatus.Cancelled;

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.AlreadyCancelled);
    }

    [Test]
    public async Task Handle_Unknown_Is404()
    {
        var act = () => _handler.Handle(Command() with { SessionId = Guid.NewGuid() }, Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
