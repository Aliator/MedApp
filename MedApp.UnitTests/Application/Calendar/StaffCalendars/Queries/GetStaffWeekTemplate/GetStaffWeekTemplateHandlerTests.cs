using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetStaffWeekTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetStaffWeekTemplate;

[TestFixture]
public sealed class GetStaffWeekTemplateHandlerTests : StaffCalendarHandlerTestBase
{
    private GetStaffWeekTemplateHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<GetStaffWeekTemplateHandler>();

    [Test]
    public async Task Handle_WithADefaultWeek_ReturnsItAndSince()
    {
        var week = CreateStaffWeek(validFrom: LongAgo);
        week.WeekTemplate = CreateWeekTemplate();
        Repository.Setup(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(week);

        var response = await _handler.Handle(new GetStaffWeekTemplateQuery(StaffUserId), Token);

        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Repository.Verify(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, Token), Times.Once);
        response.StaffUserId.Should().Be(StaffUserId);
        response.WeekTemplateId.Should().Be(WeekTemplateId);
        response.WeekTemplateName.Should().Be(WeekTemplateName);
        response.Since.Should().Be(LongAgo);
    }

    [Test]
    public async Task Handle_WithoutADefaultWeek_ReturnsNone()
    {
        Repository.Setup(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, It.IsAny<CancellationToken>())).ReturnsAsync((StaffWeekTemplate?)null);

        var response = await _handler.Handle(new GetStaffWeekTemplateQuery(StaffUserId), Token);

        response.StaffUserId.Should().Be(StaffUserId);
        response.WeekTemplateId.Should().BeNull();
        response.WeekTemplateName.Should().BeNull();
        response.Since.Should().BeNull();
    }

    [Test]
    public async Task Handle_NotAMemberOfStaff_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new GetStaffWeekTemplateQuery(Guid.NewGuid()), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Repository.Verify(r => r.GetStaffWeekTemplateOnAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
