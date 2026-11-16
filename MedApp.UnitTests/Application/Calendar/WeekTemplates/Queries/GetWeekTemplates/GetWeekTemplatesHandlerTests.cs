using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;
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
    public async Task Handle_Always_AsksForArchivedOnesOrNotForToday(bool includeArchived)
    {
        Repository.Setup(r => r.GetWeekTemplatesAsync(includeArchived, Today, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var response = await Handler.Handle(new GetWeekTemplatesQuery(includeArchived), Token);

        response.Should().BeEmpty();
        Repository.Verify(r => r.GetWeekTemplatesAsync(includeArchived, Today, Token), Times.Once);
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
        Repository.Setup(r => r.GetWeekTemplatesAsync(true, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);
        GivenWeekTemplateUsage(WeekTemplateId, new WeekTemplateUsage(3, true));

        var response = await Handler.Handle(new GetWeekTemplatesQuery(true), Token);

        response.Select(r => r.Id).Should().Equal(WeekTemplateId, OtherWeekTemplateId);
        response.Select(r => r.StaffCount).Should().Equal(3, 0);
        response.Select(r => r.CanBeDeleted).Should().Equal(false, true);
        response[1].IsArchived.Should().BeTrue();
        Repository.Verify(r => r.GetWeekTemplateUsageAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { WeekTemplateId, OtherWeekTemplateId })),
            Today, It.IsAny<CancellationToken>()), Times.Once);
    }
}
