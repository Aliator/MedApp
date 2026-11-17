using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplates;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Queries.GetDayTemplates;

[TestFixture]
public sealed class GetDayTemplatesHandlerTests : CalendarSetupHandlerTestBase
{
    private GetDayTemplatesHandler Handler => Fixture.Create<GetDayTemplatesHandler>();

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_Always_AsksForTheOwnersArchivedOnesOrNotForToday(bool includeArchived)
    {
        Repository
            .Setup(r => r.GetDayTemplatesAsync(StaffUserId, includeArchived, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await Handler.Handle(new GetDayTemplatesQuery(StaffUserId, StaffActor, includeArchived), Token);

        response.Should().BeEmpty();
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplatesAsync(StaffUserId, includeArchived, Today, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_SomeUsed_KeepsTheOrderWithEachOnesUsage()
    {
        var used = BuildDayTemplate();
        var unused = BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName, isArchived: true);
        IReadOnlyList<DayTemplate> templates = [used, unused];
        Repository.Setup(r => r.GetDayTemplatesAsync(StaffUserId, true, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);
        GivenDayTemplateUsage(DayTemplateId, new DayTemplateUsage(4, true));

        var response = await Handler.Handle(new GetDayTemplatesQuery(StaffUserId, StaffActor, true), Token);

        response.Select(r => r.Id).Should().Equal(DayTemplateId, OtherDayTemplateId);
        response.Select(r => r.WeekTemplateCount).Should().Equal(4, 0);
        response.Select(r => r.CanBeDeleted).Should().Equal(false, true);
        response.Should().OnlyContain(r => r.OwnerUserId == StaffUserId);
        response[1].IsArchived.Should().BeTrue();
        Repository.Verify(r => r.GetDayTemplateUsageAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { DayTemplateId, OtherDayTemplateId })),
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
        IReadOnlyList<DayTemplate> templates = [BuildDayTemplate(), BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName)];
        Repository.Setup(r => r.GetDayTemplatesAsync(StaffUserId, false, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);

        var response = await Handler.Handle(new GetDayTemplatesQuery(StaffUserId, actor), Token);

        response.Should().HaveCount(2).And.OnlyContain(r => r.CanEdit == canEdit);
    }

    [Test]
    public async Task Handle_AnotherPersons_AsksForTheirs()
    {
        IReadOnlyList<DayTemplate> templates = [BuildDayTemplate(ownerUserId: OtherStaffUserId)];
        Repository.Setup(r => r.GetDayTemplatesAsync(OtherStaffUserId, false, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);

        var response = await Handler.Handle(new GetDayTemplatesQuery(OtherStaffUserId, StaffActor), Token);

        response.Should().ContainSingle().Which.OwnerUserId.Should().Be(OtherStaffUserId);
        response[0].CanEdit.Should().BeFalse();
        Repository.Verify(r => r.GetDayTemplatesAsync(OtherStaffUserId, false, Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_OwnerIsNotStaff_ThrowsNotFound()
    {
        var stranger = Guid.NewGuid();

        var act = () => Handler.Handle(new GetDayTemplatesQuery(stranger, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{stranger}*");
        Repository.Verify(
            r => r.GetDayTemplatesAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
