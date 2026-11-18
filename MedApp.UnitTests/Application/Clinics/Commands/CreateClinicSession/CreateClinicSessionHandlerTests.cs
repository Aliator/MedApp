using AutoFixture;
using FluentAssertions;
using MedApp.Application.Clinics.Commands.CreateClinicSession;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Clinics;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Commands.CreateClinicSession;

[TestFixture]
public sealed class CreateClinicSessionHandlerTests : ClinicHandlerTestBase
{
    private CreateClinicSessionHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<CreateClinicSessionHandler>();

    private static CreateClinicSessionCommand Command(Guid? roomId = null) =>
        new(StaffUserId, roomId ?? SitesTestConstants.RoomId, TeamId, At(NextMonday, 9), At(NextMonday, 12), 20, AdminActor);

    [Test]
    public async Task Handle_FreeTime_AddsThePlannedSessionHoldingTheClinicianAndRoomLocks()
    {
        Clinics.Setup(r => r.GetSessionAsync(It.IsAny<Guid>(), Token)).ReturnsAsync((Guid id, CancellationToken _) => CreateSession(id: id));

        var response = await _handler.Handle(Command(), Token);

        var session = AddedSessions.Should().ContainSingle().Subject;
        session.Should().BeEquivalentTo(new
        {
            StaffUserId,
            RoomId = SitesTestConstants.RoomId,
            TeamId,
            Date = NextMonday,
            StartUtc = At(NextMonday, 9),
            EndUtc = At(NextMonday, 12),
            SlotMinutes = 20,
            Status = ClinicSessionStatus.Planned,
            CreatedByUserId = AdminUserId,
            CreatedAtUtc = Now,
            LastUpdated = Now
        });
        Lock.LockedOnce.Should().BeEquivalentTo([StaffUserId, SitesTestConstants.RoomId]);
        Lock.Tokens.Single().Should().Be(Token);
        response.Id.Should().Be(session.Id);
        response.StaffUsername.Should().Be(StaffUsername);
        response.SlotCount.Should().Be(9);
        response.FreeSlotCount.Should().Be(9);
        response.CanChange.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ClashOnTheClinicianCalendar_Is409AndAddsNothing()
    {
        Calendar = CreateData(NextMonday, blocks: [CreateBlock(At(NextMonday, 11), At(NextMonday, 13))]);

        var act = () => _handler.Handle(Command(), Token);

        (await act.Should().ThrowAsync<ConflictException>()).WithMessage("That time overlaps Training, 11:00 to 13:00.");
        AddedSessions.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RoomTaken_Is409()
    {
        RoomDays[SitesTestConstants.RoomId] = CreateRoomDay(sessions: [CreateSession(At(NextMonday, 8), At(NextMonday, 10), id: OtherSessionId, staffUserId: OtherStaffUserId)]);

        var act = () => _handler.Handle(Command(), Token);

        (await act.Should().ThrowAsync<ConflictException>()).WithMessage("Room 2 is taken by a clinic session, 08:00 to 10:00.");
        AddedSessions.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ArchivedRoom_Is409()
    {
        RoomDays[SitesTestConstants.RoomId] = CreateRoomDay(room: Room(isArchived: true));

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Room 2 is archived*");
    }

    [Test]
    public async Task Handle_UnknownRoom_Is404()
    {
        Clinics.Setup(r => r.GetRoomDayAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), Token)).ReturnsAsync((MedApp.Application.Clinics.Common.RoomDay?)null);

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_UnknownTeam_Is404AndTakesNoLock()
    {
        var act = () => _handler.Handle(Command() with { TeamId = Guid.NewGuid() }, Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Lock.Locked.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NotStaff_Is404()
    {
        var act = () => _handler.Handle(Command() with { StaffUserId = Guid.NewGuid() }, Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
