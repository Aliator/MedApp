using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Calendar.WeekTemplates.Commands.SetWeekTemplateArchived;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Commands.SetWeekTemplateArchived;

[TestFixture]
public sealed class SetWeekTemplateArchivedHandlerTests : CalendarSetupHandlerTestBase
{
    private SetWeekTemplateArchivedHandler Handler => Fixture.Create<SetWeekTemplateArchivedHandler>();

    private WeekTemplate GivenTemplate(bool isArchived)
    {
        var template = BuildWeekTemplate([CreateLoadedWeekDay(DayOfWeek.Monday, BuildDayTemplate())], isArchived: isArchived);
        Repository.Setup(r => r.GetWeekTemplateForUpdateAsync(WeekTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        return template;
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task Handle_ChangesTheFlag_SavesItWithoutTouchingTheDays(bool wasArchived, bool archive)
    {
        var template = GivenTemplate(wasArchived);
        GivenWeekTemplateUsage(WeekTemplateId, new WeekTemplateUsage(2, true));

        var response = await Handler.Handle(new SetWeekTemplateArchivedCommand(WeekTemplateId, archive), Token);

        template.IsArchived.Should().Be(archive);
        response.IsArchived.Should().Be(archive);
        response.Days[0].DayTemplateName.Should().Be(DayTemplateName);
        response.StaffCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
        Repository.Verify(r => r.GetWeekTemplateForUpdateAsync(WeekTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.SaveWeekTemplateAsync(template, null, Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), Today, Token), Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_AlreadyAsAsked_SavesNothing(bool archived)
    {
        GivenTemplate(archived);

        var response = await Handler.Handle(new SetWeekTemplateArchivedCommand(WeekTemplateId, archived), Token);

        response.IsArchived.Should().Be(archived);
        response.CanBeDeleted.Should().BeTrue();
        Repository.Verify(r => r.SaveWeekTemplateAsync(
            It.IsAny<WeekTemplate>(), It.IsAny<TemplateHistoryChange<WeekTemplateDay>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new SetWeekTemplateArchivedCommand(Guid.NewGuid(), true), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
