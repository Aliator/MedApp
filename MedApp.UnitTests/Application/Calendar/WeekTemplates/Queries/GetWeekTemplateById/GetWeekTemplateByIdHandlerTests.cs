using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplateById;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Queries.GetWeekTemplateById;

[TestFixture]
public sealed class GetWeekTemplateByIdHandlerTests : CalendarSetupHandlerTestBase
{
    private GetWeekTemplateByIdHandler Handler => Fixture.Create<GetWeekTemplateByIdHandler>();

    [Test]
    public async Task Handle_Exists_ReturnsTodaysDaysAndItsUsage()
    {
        var clinicDay = BuildDayTemplate();
        var theatreDay = BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName);
        var template = BuildWeekTemplate(
        [
            CreateLoadedWeekDay(DayOfWeek.Monday, theatreDay, validTo: Today.AddDays(-1)),
            CreateLoadedWeekDay(DayOfWeek.Monday, clinicDay, validFrom: Today),
            CreateLoadedWeekDay(DayOfWeek.Thursday, theatreDay)
        ]);
        Repository.Setup(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenWeekTemplateUsage(WeekTemplateId, new WeekTemplateUsage(1, true));

        var response = await Handler.Handle(new GetWeekTemplateByIdQuery(WeekTemplateId, StaffActor), Token);

        response.OwnerUserId.Should().Be(StaffUserId);
        response.Name.Should().Be(WeekTemplateName);
        response.Days.Select(d => d.DayTemplateName).Should().Equal(DayTemplateName, null, null, OtherDayTemplateName, null);
        response.StaffCount.Should().Be(1);
        response.CanBeDeleted.Should().BeFalse();
        Repository.Verify(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), Today, Token), Times.Once);
    }

    private static IEnumerable<TestCaseData> CanEditCases()
    {
        yield return new TestCaseData(StaffActor, true).SetName("Handle_ReadByItsOwner_CanEdit");
        yield return new TestCaseData(AdminActor, true).SetName("Handle_ReadByAnAdmin_CanEdit");
        yield return new TestCaseData(OtherStaffActor, false).SetName("Handle_ReadByOtherStaff_CannotEdit");
    }

    [TestCaseSource(nameof(CanEditCases))]
    public async Task Handle_ReadBySomeone_SaysWhetherTheyCanEditIt(CalendarActor actor, bool canEdit)
    {
        Repository.Setup(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(BuildWeekTemplate());

        var response = await Handler.Handle(new GetWeekTemplateByIdQuery(WeekTemplateId, actor), Token);

        response.OwnerUserId.Should().Be(StaffUserId);
        response.CanEdit.Should().Be(canEdit);
    }

    [Test]
    public async Task Handle_DoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new GetWeekTemplateByIdQuery(Guid.NewGuid(), StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
