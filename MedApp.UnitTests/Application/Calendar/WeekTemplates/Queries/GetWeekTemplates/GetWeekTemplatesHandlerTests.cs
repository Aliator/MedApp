using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;

[TestFixture]
public sealed class GetWeekTemplatesHandlerTests : CalendarSetupHandlerTestBase
{
    private GetWeekTemplatesHandler Handler => Fixture.Create<GetWeekTemplatesHandler>();

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_Always_AsksForTheOwnersArchivedOnesOrNotForToday(bool includeArchived)
    {
        Repository
            .Setup(r => r.GetWeekTemplatesAsync(StaffUserId, includeArchived, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await Handler.Handle(new GetWeekTemplatesQuery(StaffUserId, StaffActor, includeArchived), Token);

        response.Should().BeEmpty();
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplatesAsync(StaffUserId, includeArchived, Today, Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_SomeUsed_KeepsTheOrderWithEachOnesUsage()
    {
        IReadOnlyList<WeekTemplate> templates =
        [
            BuildWeekTemplate(),
            BuildWeekTemplate(id: OtherWeekTemplateId, name: OtherWeekTemplateName, isArchived: true)
        ];
        Repository.Setup(r => r.GetWeekTemplatesAsync(StaffUserId, true, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);
        GivenWeekTemplateUsage(WeekTemplateId, new WeekTemplateUsage(3, true));

        var response = await Handler.Handle(new GetWeekTemplatesQuery(StaffUserId, StaffActor, true), Token);

        response.Select(r => r.Id).Should().Equal(WeekTemplateId, OtherWeekTemplateId);
        response.Select(r => r.StaffCount).Should().Equal(3, 0);
        response.Select(r => r.CanBeDeleted).Should().Equal(false, true);
        response.Should().OnlyContain(r => r.OwnerUserId == StaffUserId);
        response[1].IsArchived.Should().BeTrue();
        Repository.Verify(r => r.GetWeekTemplateUsageAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { WeekTemplateId, OtherWeekTemplateId })),
            Today, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IEnumerable<TestCaseData> CanEditCases()
    {
        yield return new TestCaseData(StaffActor, true).SetName("Handle_ReadByTheirOwner_CanEditThem");
        yield return new TestCaseData(AdminActor, true).SetName("Handle_ReadByAnAdmin_CanEditThem");
        yield return new TestCaseData(OtherStaffActor, false).SetName("Handle_ReadByOtherStaff_CannotEditThem");
    }

    [TestCaseSource(nameof(CanEditCases))]
    public async Task Handle_ReadBySomeone_SaysWhetherTheyCanEditThem(CalendarActor actor, bool canEdit)
    {
        IReadOnlyList<WeekTemplate> templates =
        [
            BuildWeekTemplate(),
            BuildWeekTemplate(id: OtherWeekTemplateId, name: OtherWeekTemplateName)
        ];
        Repository.Setup(r => r.GetWeekTemplatesAsync(StaffUserId, false, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);

        var response = await Handler.Handle(new GetWeekTemplatesQuery(StaffUserId, actor), Token);

        response.Should().HaveCount(2).And.OnlyContain(r => r.CanEdit == canEdit);
    }

    [Test]
    public async Task Handle_AnotherPersons_AsksForTheirs()
    {
        IReadOnlyList<WeekTemplate> templates = [BuildWeekTemplate(ownerUserId: OtherStaffUserId)];
        Repository.Setup(r => r.GetWeekTemplatesAsync(OtherStaffUserId, false, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);

        var response = await Handler.Handle(new GetWeekTemplatesQuery(OtherStaffUserId, StaffActor), Token);

        response.Should().ContainSingle().Which.OwnerUserId.Should().Be(OtherStaffUserId);
        response[0].CanEdit.Should().BeFalse();
        Repository.Verify(r => r.GetWeekTemplatesAsync(OtherStaffUserId, false, Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_OwnerIsNotStaff_ThrowsNotFound()
    {
        var stranger = Guid.NewGuid();

        var act = () => Handler.Handle(new GetWeekTemplatesQuery(stranger, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{stranger}*");
        Repository.Verify(
            r => r.GetWeekTemplatesAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
