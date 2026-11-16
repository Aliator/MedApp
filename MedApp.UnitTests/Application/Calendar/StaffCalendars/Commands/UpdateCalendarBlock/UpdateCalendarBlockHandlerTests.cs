using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;

[TestFixture]
public sealed class UpdateCalendarBlockHandlerTests : StaffCalendarHandlerTestBase
{
    private UpdateCalendarBlockHandler _handler = null!;
    private CalendarBlock _existing = null!;
    private CalendarBlock _tracked = null!;

    [SetUp]
    public void SetUp()
    {
        GivenActivityType(Training);
        GivenActivityType(Lunch);
        GivenBlock(CreateBlock(At(NextMonday, 9), At(NextMonday, 10)));
        _handler = Fixture.Create<UpdateCalendarBlockHandler>();
    }

    /// <summary>The block as read (with its activity type) and as loaded for update (a copy, as EF would give).</summary>
    private void GivenBlock(CalendarBlock block)
    {
        _existing = block;
        _tracked = new CalendarBlock
        {
            Id = block.Id,
            StaffUserId = block.StaffUserId,
            ActivityTypeId = block.ActivityTypeId,
            StartUtc = block.StartUtc,
            EndUtc = block.EndUtc,
            Note = block.Note,
            CreatedAtUtc = block.CreatedAtUtc,
            CreatedByUserId = block.CreatedByUserId,
            LastUpdated = block.LastUpdated
        };
        Repository.Setup(r => r.GetBlockAsync(block.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_existing);
        Repository.Setup(r => r.GetBlockForUpdateAsync(block.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_tracked);
        var date = UkTime.DateOf(block.StartUtc);
        GivenCalendar(CreateData(date, blocks: [_existing], staffWeeks: []));
    }

    private UpdateCalendarBlockCommand Command(
        CalendarActor? actor = null,
        DateTime? start = null,
        DateTime? end = null,
        Guid? activityTypeId = null,
        string? note = " " + BlockNote + " ") =>
        new(_existing.Id,
            activityTypeId ?? Lunch.Id,
            start ?? At(NextMonday, 9, 30),
            end ?? At(NextMonday, 10, 30),
            note,
            actor ?? StaffActor);

    [Test]
    public async Task Handle_OwnBlock_ChangesItHoldingTheLock()
    {
        var response = await _handler.Handle(Command(), Token);

        VerifyLockedOnce(StaffUserId);
        Repository.Verify(r => r.GetBlockAsync(_existing.Id, Token), Times.Once);
        Repository.Verify(r => r.GetBlockForUpdateAsync(_existing.Id, Token), Times.Once);
        Repository.Verify(r => r.GetCalendarDataAsync(StaffUserId, NextMonday, NextMonday, Token), Times.Once);
        Repository.Verify(r => r.SaveAsync(Token), Times.Once);
        SetupRepository.Verify(r => r.GetActivityTypeAsync(Lunch.Id, Token), Times.Once);

        _tracked.ActivityTypeId.Should().Be(Lunch.Id);
        _tracked.StartUtc.Should().Be(At(NextMonday, 9, 30));
        _tracked.EndUtc.Should().Be(At(NextMonday, 10, 30));
        _tracked.Note.Should().Be(BlockNote);
        _tracked.LastUpdated.Should().Be(Now);

        response.BlockId.Should().Be(_existing.Id);
        response.ActivityTypeId.Should().Be(Lunch.Id);
        response.ActivityName.Should().Be(Lunch.Name);
        response.Colour.Should().Be(Lunch.Colour);
        response.StartUtc.Should().Be(At(NextMonday, 9, 30));
        response.Note.Should().Be(BlockNote);
        response.CanEdit.Should().BeTrue();
    }

    [Test]
    public async Task Handle_NoNote_StoresAnEmptyNote()
    {
        await _handler.Handle(Command(note: null), Token);

        _tracked.Note.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_MovedToAnotherDay_ChecksThatDaysCalendar()
    {
        GivenCalendar(CreateData(NextFriday, staffWeeks: []));

        await _handler.Handle(Command(start: At(NextFriday, 14), end: At(NextFriday, 15)), Token);

        Repository.Verify(r => r.GetCalendarDataAsync(StaffUserId, NextFriday, NextFriday, Token), Times.Once);
        _tracked.StartUtc.Should().Be(At(NextFriday, 14));
    }

    [Test]
    public async Task Handle_AdminOnSomeoneElsesBlock_ChangesIt()
    {
        await _handler.Handle(Command(actor: AdminActor), Token);

        VerifyLockedOnce(StaffUserId);
        Repository.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_StaffOnSomeoneElsesBlock_ThrowsForbidden()
    {
        var act = () => _handler.Handle(Command(actor: OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourCalendar);
        VerifyNotLocked();
        Repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownBlock_ThrowsNotFound()
    {
        var command = Command() with { BlockId = Guid.NewGuid() };

        var act = () => _handler.Handle(command, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Block '{command.BlockId}' was not found.");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_BlockDeletedBeforeTheLock_ThrowsNotFound()
    {
        Repository.Setup(r => r.GetBlockForUpdateAsync(_existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync((CalendarBlock?)null);

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_BlockOnAPastDay_ThrowsConflict()
    {
        var friday = Today.AddDays(-3);
        GivenBlock(CreateBlock(At(friday, 9), At(friday, 10)));

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_MovedToAPastDay_ThrowsConflict()
    {
        var friday = Today.AddDays(-3);

        var act = () => _handler.Handle(Command(start: At(friday, 9), end: At(friday, 10)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_KeepsItsArchivedActivityType_IsAllowed()
    {
        var archived = GivenActivityType(CreateArchivedActivityType());
        var block = CreateBlock(At(NextMonday, 9), At(NextMonday, 10), archived);
        GivenBlock(block);

        var response = await _handler.Handle(Command(activityTypeId: archived.Id), Token);

        response.ActivityName.Should().Be(archived.Name);
        Repository.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_ChangedToAnArchivedActivityType_ThrowsConflict()
    {
        var archived = GivenActivityType(CreateArchivedActivityType());

        var act = () => _handler.Handle(Command(activityTypeId: archived.Id), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_OverlapsItsOwnOldTime_IsNotAClash()
    {
        // The data holds the block itself at 09:00 to 10:00; moving it to 09:30 overlaps only itself.
        await _handler.Handle(Command(), Token);

        Repository.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_ClashesWithSomethingElse_ThrowsConflictAndSavesNothing()
    {
        GivenCalendar(CreateData(NextMonday,
            blocks: [_existing],
            templateBlocks: [CreateTemplateBlock(10, 11)]));

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That time overlaps Lunch, 10:00 to 11:00.");
        Repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tracked.StartUtc.Should().Be(At(NextMonday, 9));
    }
}
