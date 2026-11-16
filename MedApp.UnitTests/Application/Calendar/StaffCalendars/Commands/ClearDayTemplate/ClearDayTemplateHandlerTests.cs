using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ClearDayTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.ClearDayTemplate;

[TestFixture]
public sealed class ClearDayTemplateHandlerTests : StaffCalendarHandlerTestBase
{
    private ClearDayTemplateHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<ClearDayTemplateHandler>();

    private CalendarDayOverride GivenOverride(DateOnly date)
    {
        var dayOverride = CreateOverride(date, OtherDayTemplateId);
        Repository
            .Setup(r => r.GetDayOverrideForUpdateAsync(StaffUserId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dayOverride);
        return dayOverride;
    }

    [Test]
    public async Task Handle_DayWithItsOwnTemplate_DeletesTheOverride()
    {
        var dayOverride = GivenOverride(NextMonday);

        await _handler.Handle(new ClearDayTemplateCommand(StaffUserId, NextMonday, StaffActor), Token);

        Repository.Verify(r => r.GetDayOverrideForUpdateAsync(StaffUserId, NextMonday, Token), Times.Once);
        Repository.Verify(r => r.DeleteDayOverrideAsync(dayOverride, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminOnSomeoneElsesCalendar_DeletesTheOverride()
    {
        var dayOverride = GivenOverride(Today);

        await _handler.Handle(new ClearDayTemplateCommand(StaffUserId, Today, AdminActor), Token);

        Repository.Verify(r => r.DeleteDayOverrideAsync(dayOverride, Token), Times.Once);
    }

    [Test]
    public async Task Handle_StaffOnSomeoneElsesCalendar_ThrowsForbidden()
    {
        GivenOverride(NextMonday);

        var act = () => _handler.Handle(new ClearDayTemplateCommand(StaffUserId, NextMonday, OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourCalendar);
        Repository.Verify(r => r.DeleteDayOverrideAsync(It.IsAny<CalendarDayOverride>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflict()
    {
        var friday = Today.AddDays(-3);
        GivenOverride(friday);

        var act = () => _handler.Handle(new ClearDayTemplateCommand(StaffUserId, friday, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        Repository.Verify(r => r.DeleteDayOverrideAsync(It.IsAny<CalendarDayOverride>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_DayWithoutItsOwnTemplate_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new ClearDayTemplateCommand(StaffUserId, NextMonday, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("This day has no day template of its own.");
    }
}
