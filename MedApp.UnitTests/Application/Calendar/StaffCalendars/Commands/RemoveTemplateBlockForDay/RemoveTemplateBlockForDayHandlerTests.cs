using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.RemoveTemplateBlockForDay;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.RemoveTemplateBlockForDay;

[TestFixture]
public sealed class RemoveTemplateBlockForDayHandlerTests : StaffCalendarHandlerTestBase
{
    private RemoveTemplateBlockForDayHandler _handler = null!;
    private DayTemplateBlock _lunch = null!;
    private CalendarBlockException? _exception;
    private CalendarBlock? _replacement;

    [SetUp]
    public void SetUp()
    {
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
        _handler = Fixture.Create<RemoveTemplateBlockForDayHandler>();
    }

    [Test]
    public async Task Handle_TemplateBlockOnTheDay_AddsAnExceptionAndNoReplacement()
    {
        await _handler.Handle(new RemoveTemplateBlockForDayCommand(StaffUserId, NextMonday, _lunch.Id, StaffActor), Token);

        VerifyLockedOnce(StaffUserId);
        Repository.Verify(r => r.GetCalendarDataAsync(StaffUserId, NextMonday, NextMonday, Token), Times.Once);
        Repository.Verify(r => r.AddBlockExceptionAsync(It.IsAny<CalendarBlockException>(), null, Token), Times.Once);
        _exception.Should().NotBeNull();
        _exception!.StaffUserId.Should().Be(StaffUserId);
        _exception.Date.Should().Be(NextMonday);
        _exception.DayTemplateBlockId.Should().Be(_lunch.Id);
        _exception.Id.Should().NotBeEmpty();
        _replacement.Should().BeNull();
    }

    [Test]
    public async Task Handle_AdminOnSomeoneElsesCalendar_AddsTheException()
    {
        await _handler.Handle(new RemoveTemplateBlockForDayCommand(StaffUserId, NextMonday, _lunch.Id, AdminActor), Token);

        VerifyLockedOnce(StaffUserId);
        _exception.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_StaffOnSomeoneElsesCalendar_ThrowsForbidden()
    {
        var act = () => _handler.Handle(new RemoveTemplateBlockForDayCommand(StaffUserId, NextMonday, _lunch.Id, OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNotLocked();
        _exception.Should().BeNull();
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflict()
    {
        var act = () => _handler.Handle(new RemoveTemplateBlockForDayCommand(StaffUserId, Today.AddDays(-3), _lunch.Id, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_TemplateBlockNotOnThatDay_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new RemoveTemplateBlockForDayCommand(StaffUserId, NextMonday, Guid.NewGuid(), StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("That template block isn't on this calendar on Monday 23 November.");
        _exception.Should().BeNull();
    }

    [Test]
    public async Task Handle_AlreadyRemovedThatDay_ThrowsNotFound()
    {
        GivenCalendar(CreateData(NextMonday, templateBlocks: [_lunch], exceptions: [CreateException(NextMonday, _lunch)]));

        var act = () => _handler.Handle(new RemoveTemplateBlockForDayCommand(StaffUserId, NextMonday, _lunch.Id, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        _exception.Should().BeNull();
    }
}
