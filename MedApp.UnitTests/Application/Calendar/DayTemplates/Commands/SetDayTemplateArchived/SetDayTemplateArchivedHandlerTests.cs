using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.SetDayTemplateArchived;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Commands.SetDayTemplateArchived;

[TestFixture]
public sealed class SetDayTemplateArchivedHandlerTests : CalendarSetupHandlerTestBase
{
    private SetDayTemplateArchivedHandler Handler => Fixture.Create<SetDayTemplateArchivedHandler>();

    private DayTemplate GivenTemplate(bool isArchived)
    {
        var template = BuildDayTemplate([CreateTemplateBlock(12, 13)], isArchived: isArchived);
        Repository.Setup(r => r.GetDayTemplateForUpdateAsync(DayTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        return template;
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task Handle_ChangesTheFlag_SavesItWithoutTouchingTheBlocks(bool wasArchived, bool archive)
    {
        var template = GivenTemplate(wasArchived);
        GivenDayTemplateUsage(DayTemplateId, new DayTemplateUsage(2, true));

        var response = await Handler.Handle(new SetDayTemplateArchivedCommand(DayTemplateId, archive), Token);

        template.IsArchived.Should().Be(archive);
        response.IsArchived.Should().Be(archive);
        response.Blocks.Should().ContainSingle();
        response.WeekTemplateCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
        Repository.Verify(r => r.GetDayTemplateForUpdateAsync(DayTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.SaveDayTemplateAsync(template, null, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), Today, Token), Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_AlreadyAsAsked_SavesNothing(bool archived)
    {
        GivenTemplate(archived);

        var response = await Handler.Handle(new SetDayTemplateArchivedCommand(DayTemplateId, archived), Token);

        response.IsArchived.Should().Be(archived);
        response.CanBeDeleted.Should().BeTrue();
        Repository.Verify(r => r.SaveDayTemplateAsync(
            It.IsAny<DayTemplate>(), It.IsAny<TemplateHistoryChange<DayTemplateBlock>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new SetDayTemplateArchivedCommand(Guid.NewGuid(), true), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
