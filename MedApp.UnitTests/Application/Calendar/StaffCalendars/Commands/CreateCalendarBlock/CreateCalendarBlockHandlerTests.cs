using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;

[TestFixture]
public sealed class CreateCalendarBlockHandlerTests : StaffCalendarHandlerTestBase
{
    private CreateCalendarBlockHandler _handler = null!;
    private CalendarBlock? _added;

    [SetUp]
    public void SetUp()
    {
        GivenActivityType(Training);
        GivenCalendar(CreateData(NextMonday, staffWeeks: []));
        Repository
            .Setup(r => r.AddBlockAsync(It.IsAny<CalendarBlock>(), It.IsAny<CancellationToken>()))
            .Callback((CalendarBlock block, CancellationToken _) => _added = block)
            .Returns(Task.CompletedTask);
        _added = null;
        _handler = Fixture.Create<CreateCalendarBlockHandler>();
    }

    private static CreateCalendarBlockCommand Command(
        CalendarActor? actor = null,
        DateTime? start = null,
        DateTime? end = null,
        Guid? staffUserId = null,
        Guid? activityTypeId = null,
        string? note = "  " + BlockNote + "  ") =>
        new(staffUserId ?? StaffUserId,
            activityTypeId ?? Training.Id,
            start ?? At(NextMonday, 10),
            end ?? At(NextMonday, 11),
            note,
            actor ?? StaffActor);

    [Test]
    public async Task Handle_OwnCalendar_AddsTheBlockHoldingTheLock()
    {
        var response = await _handler.Handle(Command(), Token);

        VerifyLockedOnce(StaffUserId);
        Repository.Verify(r => r.GetCalendarDataAsync(StaffUserId, NextMonday, NextMonday, Token), Times.Once);
        Repository.Verify(r => r.AddBlockAsync(It.IsAny<CalendarBlock>(), Token), Times.Once);
        SetupRepository.Verify(r => r.GetActivityTypeAsync(Training.Id, Token), Times.Once);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);

        _added.Should().NotBeNull();
        _added!.StaffUserId.Should().Be(StaffUserId);
        _added.ActivityTypeId.Should().Be(Training.Id);
        _added.StartUtc.Should().Be(At(NextMonday, 10));
        _added.EndUtc.Should().Be(At(NextMonday, 11));
        _added.Note.Should().Be(BlockNote);
        _added.CreatedAtUtc.Should().Be(Now);
        _added.LastUpdated.Should().Be(Now);
        _added.CreatedByUserId.Should().Be(StaffUserId);
        _added.Id.Should().NotBeEmpty();

        response.BlockId.Should().Be(_added.Id);
        response.IsFromTemplate.Should().BeFalse();
        response.TemplateBlockId.Should().BeNull();
        response.StaffUserId.Should().Be(StaffUserId);
        response.Date.Should().Be(NextMonday);
        response.ActivityName.Should().Be(Training.Name);
        response.Colour.Should().Be(Training.Colour);
        response.Note.Should().Be(BlockNote);
        response.CanEdit.Should().BeTrue();
        response.OverlapsAppointment.Should().BeFalse();
    }

    [Test]
    public async Task Handle_NoNote_StoresAnEmptyNote()
    {
        await _handler.Handle(Command(note: null), Token);

        _added!.Note.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_TimesWithoutAKind_AreTakenAsUtc()
    {
        var start = DateTime.SpecifyKind(At(NextMonday, 10), DateTimeKind.Unspecified);
        var end = DateTime.SpecifyKind(At(NextMonday, 11), DateTimeKind.Unspecified);

        await _handler.Handle(Command(start: start, end: end), Token);

        _added!.StartUtc.Kind.Should().Be(DateTimeKind.Utc);
        _added.StartUtc.Should().Be(At(NextMonday, 10));
    }

    [Test]
    public async Task Handle_Today_IsAllowed()
    {
        GivenCalendar(CreateData(Today, staffWeeks: []));

        var response = await _handler.Handle(Command(start: At(Today, 14), end: At(Today, 15)), Token);

        response.Date.Should().Be(Today);
        response.CanEdit.Should().BeTrue();
    }

    [Test]
    public async Task Handle_AdminOnSomeoneElsesCalendar_AddsTheBlockAsTheAdmin()
    {
        await _handler.Handle(Command(actor: AdminActor), Token);

        VerifyLockedOnce(StaffUserId);
        _added!.StaffUserId.Should().Be(StaffUserId);
        _added.CreatedByUserId.Should().Be(AdminUserId);
    }

    [Test]
    public async Task Handle_StaffOnSomeoneElsesCalendar_ThrowsForbidden()
    {
        var act = () => _handler.Handle(Command(actor: OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourCalendar);
        VerifyNotLocked();
        Repository.Verify(r => r.AddBlockAsync(It.IsAny<CalendarBlock>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NotAMemberOfStaff_ThrowsNotFound()
    {
        var stranger = Guid.NewGuid();

        var act = () => _handler.Handle(Command(actor: AdminActor, staffUserId: stranger), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{stranger}*");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflict()
    {
        var friday = Today.AddDays(-3);

        var act = () => _handler.Handle(Command(start: At(friday, 10), end: At(friday, 11)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_UnknownActivityType_ThrowsNotFound()
    {
        var unknown = Guid.NewGuid();

        var act = () => _handler.Handle(Command(activityTypeId: unknown), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Activity type '{unknown}' was not found.");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_ArchivedActivityType_ThrowsConflict()
    {
        var archived = GivenActivityType(CreateArchivedActivityType());

        var act = () => _handler.Handle(Command(activityTypeId: archived.Id), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_Clash_ThrowsConflictNamingItAndAddsNothing()
    {
        GivenCalendar(CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(10, 11)]));

        var act = () => _handler.Handle(Command(start: At(NextMonday, 10, 30), end: At(NextMonday, 11, 30)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That time overlaps Lunch, 10:00 to 11:00.");
        VerifyLockedOnce(StaffUserId);
        Repository.Verify(r => r.AddBlockAsync(It.IsAny<CalendarBlock>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TouchingAnotherBlock_AddsIt()
    {
        GivenCalendar(CreateData(NextMonday, blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 10))], staffWeeks: []));

        await _handler.Handle(Command(), Token);

        _added.Should().NotBeNull();
    }
}
