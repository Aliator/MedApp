using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplateById;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Queries.GetDayTemplateById;

[TestFixture]
public sealed class GetDayTemplateByIdHandlerTests : CalendarSetupHandlerTestBase
{
    private GetDayTemplateByIdHandler Handler => Fixture.Create<GetDayTemplateByIdHandler>();

    [Test]
    public async Task Handle_Exists_ReturnsTodaysBlocksAndItsUsage()
    {
        var current = CreateTemplateBlock(12, 13);
        var closed = CreateTemplateBlock(9, 10, Training, validTo: Today.AddDays(-1));
        Repository
            .Setup(r => r.GetDayTemplateAsync(DayTemplateId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildDayTemplate([closed, current]));
        GivenDayTemplateUsage(DayTemplateId, new DayTemplateUsage(1, true));

        var response = await Handler.Handle(new GetDayTemplateByIdQuery(DayTemplateId, StaffActor), Token);

        response.OwnerUserId.Should().Be(StaffUserId);
        response.Name.Should().Be(DayTemplateName);
        response.Blocks.Select(b => b.Id).Should().Equal(current.Id);
        response.WeekTemplateCount.Should().Be(1);
        response.CanBeDeleted.Should().BeFalse();
        Repository.Verify(r => r.GetDayTemplateAsync(DayTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), Today, Token), Times.Once);
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
        Repository
            .Setup(r => r.GetDayTemplateAsync(DayTemplateId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildDayTemplate());

        var response = await Handler.Handle(new GetDayTemplateByIdQuery(DayTemplateId, actor), Token);

        response.OwnerUserId.Should().Be(StaffUserId);
        response.CanEdit.Should().Be(canEdit);
    }

    [Test]
    public async Task Handle_DoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new GetDayTemplateByIdQuery(Guid.NewGuid(), StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
