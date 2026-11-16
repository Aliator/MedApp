using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.DeleteCalendarBlock;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.DeleteCalendarBlock;

[TestFixture]
public sealed class DeleteCalendarBlockHandlerTests : StaffCalendarHandlerTestBase
{
    private DeleteCalendarBlockHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<DeleteCalendarBlockHandler>();

    private CalendarBlock GivenBlock(DateOnly date)
    {
        var block = CreateBlock(At(date, 9), At(date, 10));
        Repository.Setup(r => r.GetBlockForUpdateAsync(block.Id, It.IsAny<CancellationToken>())).ReturnsAsync(block);
        return block;
    }

    [Test]
    public async Task Handle_OwnBlock_DeletesIt()
    {
        var block = GivenBlock(NextMonday);

        await _handler.Handle(new DeleteCalendarBlockCommand(block.Id, StaffActor), Token);

        Repository.Verify(r => r.GetBlockForUpdateAsync(block.Id, Token), Times.Once);
        Repository.Verify(r => r.DeleteBlockAsync(block, Token), Times.Once);
    }

    [Test]
    public async Task Handle_TodaysBlock_DeletesIt()
    {
        var block = GivenBlock(Today);

        await _handler.Handle(new DeleteCalendarBlockCommand(block.Id, StaffActor), Token);

        Repository.Verify(r => r.DeleteBlockAsync(block, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminOnSomeoneElsesBlock_DeletesIt()
    {
        var block = GivenBlock(NextMonday);

        await _handler.Handle(new DeleteCalendarBlockCommand(block.Id, AdminActor), Token);

        Repository.Verify(r => r.DeleteBlockAsync(block, Token), Times.Once);
    }

    [Test]
    public async Task Handle_StaffOnSomeoneElsesBlock_ThrowsForbidden()
    {
        var block = GivenBlock(NextMonday);

        var act = () => _handler.Handle(new DeleteCalendarBlockCommand(block.Id, OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourCalendar);
        Repository.Verify(r => r.DeleteBlockAsync(It.IsAny<CalendarBlock>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_PastBlock_ThrowsConflict()
    {
        var block = GivenBlock(Today.AddDays(-3));

        var act = () => _handler.Handle(new DeleteCalendarBlockCommand(block.Id, AdminActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        Repository.Verify(r => r.DeleteBlockAsync(It.IsAny<CalendarBlock>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownBlock_ThrowsNotFound()
    {
        var id = Guid.NewGuid();

        var act = () => _handler.Handle(new DeleteCalendarBlockCommand(id, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Block '{id}' was not found.");
    }
}
