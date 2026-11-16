using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;

[TestFixture]
public sealed class ChangeTemplateBlockForDayHandlerTests : StaffCalendarHandlerTestBase
{
    private ChangeTemplateBlockForDayHandler _handler = null!;
    private DayTemplateBlock _lunch = null!;
    private CalendarBlockException? _exception;
    private CalendarBlock? _replacement;

    [SetUp]
    public void SetUp()
    {
        GivenActivityType(Lunch);
        GivenActivityType(Training);
        _lunch = CreateTemplateBlock(12, 13);
        GivenCalendar(CreateData(NextMonday, templateBlocks: [_lunch]));
        _exception = null;
        _replacement = null;
        Repository
            .Setup(r => r.AddBlockExceptionAsync(It.IsAny<CalendarBlockException>(), It.IsAny<CalendarBlock?>(), It.IsAny<CancellationToken>()))
            .Callback((CalendarBlockException exception, CalendarBlock? replacement, CancellationToken _) =>
            {
                _exception = exception;
                _replacement = replacement;
            })
            .Returns(Task.CompletedTask);
        _handler = Fixture.Create<ChangeTemplateBlockForDayHandler>();
    }

    private ChangeTemplateBlockForDayCommand Command(
        CalendarActor? actor = null,
        DateOnly? date = null,
        Guid? templateBlockId = null,
        Guid? activityTypeId = null,
        DateTime? start = null,
        DateTime? end = null,
        string? note = " " + BlockNote + " ") =>
        new(StaffUserId,
            date ?? NextMonday,
            templateBlockId ?? _lunch.Id,
            activityTypeId ?? Lunch.Id,
            start ?? At(NextMonday, 12, 30),
            end ?? At(NextMonday, 13, 30),
            note,
            actor ?? StaffActor);

    [Test]
    public async Task Handle_TemplateBlockOnTheDay_AddsAnExceptionAndItsReplacementTogether()
    {
        var response = await _handler.Handle(Command(), Token);

        VerifyLockedOnce(StaffUserId);
        Repository.Verify(r => r.GetCalendarDataAsync(StaffUserId, NextMonday, NextMonday, Token), Times.Once);
        Repository.Verify(r => r.AddBlockExceptionAsync(It.IsAny<CalendarBlockException>(), It.IsAny<CalendarBlock?>(), Token), Times.Once);
        SetupRepository.Verify(r => r.GetActivityTypeAsync(Lunch.Id, Token), Times.Once);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);

        _exception!.StaffUserId.Should().Be(StaffUserId);
        _exception.Date.Should().Be(NextMonday);
        _exception.DayTemplateBlockId.Should().Be(_lunch.Id);

        _replacement.Should().NotBeNull();
        _replacement!.StaffUserId.Should().Be(StaffUserId);
        _replacement.ActivityTypeId.Should().Be(Lunch.Id);
        _replacement.StartUtc.Should().Be(At(NextMonday, 12, 30));
        _replacement.EndUtc.Should().Be(At(NextMonday, 13, 30));
        _replacement.Note.Should().Be(BlockNote);
        _replacement.CreatedAtUtc.Should().Be(Now);
        _replacement.LastUpdated.Should().Be(Now);
        _replacement.CreatedByUserId.Should().Be(StaffUserId);

        response.BlockId.Should().Be(_replacement.Id);
        response.IsFromTemplate.Should().BeFalse();
        response.ActivityName.Should().Be(Lunch.Name);
        response.Date.Should().Be(NextMonday);
        response.CanEdit.Should().BeTrue();
    }

    [Test]
    public async Task Handle_NoNote_StoresAnEmptyNote()
    {
        await _handler.Handle(Command(note: null), Token);

        _replacement!.Note.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnotherActivity_UsesIt()
    {
        var response = await _handler.Handle(Command(activityTypeId: Training.Id), Token);

        _replacement!.ActivityTypeId.Should().Be(Training.Id);
        response.ActivityName.Should().Be(Training.Name);
    }

    [Test]
    public async Task Handle_AdminOnSomeoneElsesCalendar_ChangesItAsTheAdmin()
    {
        await _handler.Handle(Command(actor: AdminActor), Token);

        VerifyLockedOnce(StaffUserId);
        _replacement!.CreatedByUserId.Should().Be(AdminUserId);
    }

    [Test]
    public async Task Handle_StaffOnSomeoneElsesCalendar_ThrowsForbidden()
    {
        var act = () => _handler.Handle(Command(actor: OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNotLocked();
        _exception.Should().BeNull();
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflict()
    {
        var friday = Today.AddDays(-3);

        var act = () => _handler.Handle(Command(date: friday, start: At(friday, 12), end: At(friday, 13)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_NotAMemberOfStaff_ThrowsNotFound()
    {
        Identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<(Guid, string)>());

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_TemplateBlockNotOnThatDay_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Command(templateBlockId: Guid.NewGuid()), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        _exception.Should().BeNull();
    }

    [Test]
    public async Task Handle_TemplateBlocksOwnArchivedActivity_IsAllowed()
    {
        var archived = GivenActivityType(CreateArchivedActivityType());
        var block = CreateTemplateBlock(12, 13, archived);
        GivenCalendar(CreateData(NextMonday, templateBlocks: [block]));

        await _handler.Handle(Command(templateBlockId: block.Id, activityTypeId: archived.Id), Token);

        _replacement!.ActivityTypeId.Should().Be(archived.Id);
    }

    [Test]
    public async Task Handle_AnotherArchivedActivity_ThrowsConflict()
    {
        var archived = GivenActivityType(CreateArchivedActivityType());

        var act = () => _handler.Handle(Command(activityTypeId: archived.Id), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
        _exception.Should().BeNull();
    }

    [Test]
    public async Task Handle_OverlapsOnlyTheTemplateBlockItReplaces_IsNotAClash()
    {
        await _handler.Handle(Command(start: At(NextMonday, 12), end: At(NextMonday, 13)), Token);

        _replacement.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_ClashesWithSomethingElse_ThrowsConflictAndAddsNothing()
    {
        GivenCalendar(CreateData(NextMonday,
            templateBlocks: [_lunch],
            blocks: [CreateBlock(At(NextMonday, 13), At(NextMonday, 14))]));

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That time overlaps Training, 13:00 to 14:00.");
        _exception.Should().BeNull();
    }
}
