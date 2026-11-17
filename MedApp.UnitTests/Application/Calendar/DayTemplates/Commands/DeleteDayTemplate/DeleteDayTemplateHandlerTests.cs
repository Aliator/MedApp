using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.DeleteDayTemplate;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Commands.DeleteDayTemplate;

[TestFixture]
public sealed class DeleteDayTemplateHandlerTests : CalendarSetupHandlerTestBase
{
    private DeleteDayTemplateHandler Handler => Fixture.Create<DeleteDayTemplateHandler>();

    /// <summary><see cref="StaffUserId"/>'s clinic day.</summary>
    [SetUp]
    public void SetUp() =>
        Repository.Setup(r => r.GetDayTemplateAsync(DayTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(BuildDayTemplate());

    [Test]
    public async Task Handle_NeverUsed_DeletesIt()
    {
        await Handler.Handle(new DeleteDayTemplateCommand(DayTemplateId, StaffActor), Token);

        Repository.Verify(r => r.GetDayTemplateAsync(DayTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateUsageAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == DayTemplateId), Today, Token), Times.Once);
        Repository.Verify(r => r.DeleteDayTemplateAsync(DayTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminDeletesSomeoneElses_DeletesIt()
    {
        await Handler.Handle(new DeleteDayTemplateCommand(DayTemplateId, AdminActor), Token);

        Repository.Verify(r => r.DeleteDayTemplateAsync(DayTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_OtherStaffDeletesSomeoneElses_ThrowsForbiddenAndDeletesNothing()
    {
        var act = () => Handler.Handle(new DeleteDayTemplateCommand(DayTemplateId, OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourTemplates);
        Repository.Verify(r => r.DeleteDayTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UsedBeforeButNotNow_ThrowsConflictSuggestingArchiving()
    {
        GivenDayTemplateUsage(DayTemplateId, new DayTemplateUsage(0, true));

        var act = () => Handler.Handle(new DeleteDayTemplateCommand(DayTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{DayTemplateName}'*Archive it instead*");
        Repository.Verify(r => r.DeleteDayTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeleteDayTemplateCommand(Guid.NewGuid(), StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Repository.Verify(r => r.DeleteDayTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
