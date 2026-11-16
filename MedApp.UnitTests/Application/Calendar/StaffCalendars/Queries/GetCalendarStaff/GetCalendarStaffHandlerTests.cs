using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

[TestFixture]
public sealed class GetCalendarStaffHandlerTests : StaffCalendarHandlerTestBase
{
    private GetCalendarStaffHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<GetCalendarStaffHandler>();

    private static StaffWeekTemplate Week(Guid staffUserId, Guid weekTemplateId, DateOnly validFrom)
    {
        var week = CreateStaffWeek(weekTemplateId, staffUserId, validFrom);
        week.WeekTemplate = CreateWeekTemplate(weekTemplateId);
        week.WeekTemplate.Name = weekTemplateId == WeekTemplateId ? WeekTemplateName : "Late week";
        return week;
    }

    [Test]
    public async Task Handle_ListsEveryMemberOfStaffInOrderWithTheirDefaultWeek()
    {
        Repository
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Week(StaffUserId, WeekTemplateId, LongAgo)]);

        var response = await _handler.Handle(new GetCalendarStaffQuery(), Token);

        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Repository.Verify(r => r.GetStaffWeekTemplatesOnAsync(Today, Token), Times.Once);
        response.Select(s => s.UserId).Should().Equal(StaffUserId, OtherStaffUserId, AdminUserId);
        response.Select(s => s.Username).Should().Equal(StaffUsername, OtherStaffUsername, AdminUsername);
        response[0].WeekTemplateId.Should().Be(WeekTemplateId);
        response[0].WeekTemplateName.Should().Be(WeekTemplateName);
        response[1].WeekTemplateId.Should().BeNull();
        response[1].WeekTemplateName.Should().BeNull();
    }

    [Test]
    public async Task Handle_TwoRowsForOnePerson_UsesTheLatest()
    {
        Repository
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Week(OtherStaffUserId, OtherWeekTemplateId, Today),
                Week(OtherStaffUserId, WeekTemplateId, LongAgo)
            ]);

        var response = await _handler.Handle(new GetCalendarStaffQuery(), Token);

        response.Single(s => s.UserId == OtherStaffUserId).WeekTemplateId.Should().Be(OtherWeekTemplateId);
    }

    [Test]
    public async Task Handle_NoStaff_ReturnsEmpty()
    {
        Identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<(Guid, string)>());
        Repository
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StaffWeekTemplate>());

        var response = await _handler.Handle(new GetCalendarStaffQuery(), Token);

        response.Should().BeEmpty();
    }
}
