using AutoFixture;
using FluentAssertions;
using MedApp.Application.Clinics.Commands.CreateClinicPattern;
using MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;
using MedApp.Application.Clinics.Commands.UpdateClinicPattern;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Clinics;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Commands;

/// <summary>Adding and changing clinic patterns, and Create sessions.</summary>
[TestFixture]
public sealed class ClinicPatternHandlerTests : ClinicHandlerTestBase
{
    private ClinicPattern _pattern = null!;

    [SetUp]
    public void SetUp()
    {
        _pattern = CreatePattern();
        Clinics.Setup(r => r.GetPatternAsync(PatternId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _pattern);
        Clinics.Setup(r => r.GetPatternForUpdateAsync(PatternId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _pattern);
        Clinics.Setup(r => r.GetPatternDatesAsync(PatternId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<DateOnly>());
        Clinics.Setup(r => r.GetSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => AddedSessions.FirstOrDefault(s => s.Id == id) is { } added
                ? CreateSession(added.StartUtc, added.EndUtc, id: added.Id)
                : null);
    }

    [Test]
    public async Task CreatePattern_AddsItAsGiven()
    {
        ClinicPattern? added = null;
        Clinics.Setup(r => r.AddPatternAsync(It.IsAny<ClinicPattern>(), Token)).Callback((ClinicPattern p, CancellationToken _) => added = p);

        var response = await Fixture.Create<CreateClinicPatternHandler>().Handle(
            new CreateClinicPatternCommand(StaffUserId, SitesTestConstants.RoomId, SpecialtyId, "Tuesday", new TimeOnly(13, 0), new TimeOnly(17, 0), 30, 2, NextMonday, null, AdminActor),
            Token);

        added.Should().NotBeNull();
        added!.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        added.EveryWeeks.Should().Be(2);
        added.SlotMinutes.Should().Be(30);
        added.CreatedByUserId.Should().Be(AdminUserId);
        response.StaffUsername.Should().Be(StaffUsername);
    }

    [Test]
    public async Task CreatePattern_ArchivedRoom_Is409()
    {
        RoomDays[SitesTestConstants.RoomId] = CreateRoomDay(room: Room(isArchived: true));

        var act = () => Fixture.Create<CreateClinicPatternHandler>().Handle(
            new CreateClinicPatternCommand(StaffUserId, SitesTestConstants.RoomId, SpecialtyId, "Monday", new TimeOnly(9, 0), new TimeOnly(12, 0), 20, 1, Today, null, AdminActor),
            Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task UpdatePattern_ChangesItFromTheVersionRead()
    {
        var response = await Fixture.Create<UpdateClinicPatternHandler>().Handle(UpdateCommand(_pattern.LastUpdated), Token);

        _pattern.UntilDate.Should().Be(NextMonday.AddDays(28));
        _pattern.Starts.Should().Be(new TimeOnly(10, 0));
        _pattern.LastUpdated.Should().Be(Now);
        Saved.Should().ContainSingle();
        response.Id.Should().Be(PatternId);
    }

    [Test]
    public async Task UpdatePattern_Stale_Is409()
    {
        var act = () => Fixture.Create<UpdateClinicPatternHandler>().Handle(UpdateCommand(_pattern.LastUpdated.AddSeconds(1)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.PatternChangedMeanwhile);
        Saved.Should().BeEmpty();
    }

    private static UpdateClinicPatternCommand UpdateCommand(DateTime expected) => new(
        PatternId, StaffUserId, SitesTestConstants.RoomId, SpecialtyId, "Monday", new TimeOnly(10, 0), new TimeOnly(12, 0), 20, 1, Today,
        NextMonday.AddDays(28), expected, AdminActor);

    [Test]
    public async Task CreateSessions_MakesOneEachDate_SkippingThoseItHasMadeAndThoseStarted()
    {
        // Today's 09:00 started before now (09:00 UTC is 09:00 UK time in November), so it's skipped; next Monday's
        // is already made; the two after are made.
        Clinics.Setup(r => r.GetPatternDatesAsync(PatternId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), Token))
            .ReturnsAsync(new HashSet<DateOnly> { NextMonday });

        var response = await Fixture.Create<CreateSessionsFromPatternHandler>().Handle(new CreateSessionsFromPatternCommand(PatternId, 4, AdminActor), Token);

        response.Created.Select(s => s.Date).Should().Equal(NextMonday.AddDays(7), NextMonday.AddDays(14));
        response.Skipped.Should().BeEquivalentTo([
            new { Date = Today, Reason = ClinicPatternRules.AlreadyStarted },
            new { Date = NextMonday, Reason = ClinicPatternRules.AlreadyMade }
        ]);
        AddedSessions.Should().HaveCount(2).And.OnlyContain(s => s.PatternId == PatternId && s.CreatedByUserId == AdminUserId);
        Lock.Locked.Should().HaveCount(2).And.OnlyContain(l => l.Contains(StaffUserId) && l.Contains(SitesTestConstants.RoomId));
    }

    [Test]
    public async Task CreateSessions_AClashOrAClosedDay_IsSkippedWithItsReason()
    {
        Calendar = CreateData(NextMonday, NextMonday.AddDays(7), blocks: [CreateBlock(At(NextMonday, 11), At(NextMonday, 13))]);

        var response = await Fixture.Create<CreateSessionsFromPatternHandler>().Handle(new CreateSessionsFromPatternCommand(PatternId, 2, AdminActor), Token);

        response.Skipped.Should().ContainEquivalentOf(new { Date = NextMonday, Reason = "That time overlaps Training, 11:00 to 13:00." });
    }

    [Test]
    public async Task CreateSessions_ArchivedRoom_SkipsEveryDate()
    {
        _pattern = CreatePattern(room: Room(isArchived: true));

        var response = await Fixture.Create<CreateSessionsFromPatternHandler>().Handle(new CreateSessionsFromPatternCommand(PatternId, 3, AdminActor), Token);

        response.Created.Should().BeEmpty();
        response.Skipped.Should().HaveCount(3).And.OnlyContain(s => s.Reason == ClinicRules.RoomArchived(_pattern.Room));
        Lock.Locked.Should().BeEmpty();
    }

    [Test]
    public async Task CreateSessions_AnotherRequestMadeItFirst_IsSkipped()
    {
        Clinics.Setup(r => r.AddSessionAsync(It.IsAny<ClinicSession>(), Token)).ThrowsAsync(new ConflictException("Its pattern has already made a session on that date."));

        var response = await Fixture.Create<CreateSessionsFromPatternHandler>().Handle(new CreateSessionsFromPatternCommand(PatternId, 2, AdminActor), Token);

        response.Created.Should().BeEmpty();
        response.Skipped.Should().ContainEquivalentOf(new { Date = NextMonday, Reason = "Its pattern has already made a session on that date." });
    }

    [Test]
    public async Task CreateSessions_UnknownPattern_Is404()
    {
        var act = () => Fixture.Create<CreateSessionsFromPatternHandler>().Handle(new CreateSessionsFromPatternCommand(Guid.NewGuid(), 2, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
