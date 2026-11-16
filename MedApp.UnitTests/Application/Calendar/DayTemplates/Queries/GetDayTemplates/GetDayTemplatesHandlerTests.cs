using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplates;
using MedApp.Application.Calendar.Repositories;
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
    public async Task Handle_Always_AsksForArchivedOnesOrNotForToday(bool includeArchived)
    {
        Repository.Setup(r => r.GetDayTemplatesAsync(includeArchived, Today, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var response = await Handler.Handle(new GetDayTemplatesQuery(includeArchived), Token);

        response.Should().BeEmpty();
        Repository.Verify(r => r.GetDayTemplatesAsync(includeArchived, Today, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_SomeUsed_KeepsTheOrderWithEachOnesUsage()
    {
        var used = BuildDayTemplate();
        var unused = BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName, isArchived: true);
        IReadOnlyList<DayTemplate> templates = [used, unused];
        Repository.Setup(r => r.GetDayTemplatesAsync(true, Today, It.IsAny<CancellationToken>())).ReturnsAsync(templates);
        GivenDayTemplateUsage(DayTemplateId, new DayTemplateUsage(4, true));

        var response = await Handler.Handle(new GetDayTemplatesQuery(true), Token);

        response.Select(r => r.Id).Should().Equal(DayTemplateId, OtherDayTemplateId);
        response.Select(r => r.WeekTemplateCount).Should().Equal(4, 0);
        response.Select(r => r.CanBeDeleted).Should().Equal(false, true);
        response[1].IsArchived.Should().BeTrue();
        Repository.Verify(r => r.GetDayTemplateUsageAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { DayTemplateId, OtherDayTemplateId })),
            Today, It.IsAny<CancellationToken>()), Times.Once);
    }
}
