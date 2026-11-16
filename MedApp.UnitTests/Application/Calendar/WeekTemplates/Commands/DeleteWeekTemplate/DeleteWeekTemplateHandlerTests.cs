using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Calendar.WeekTemplates.Commands.DeleteWeekTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Commands.DeleteWeekTemplate;

[TestFixture]
public sealed class DeleteWeekTemplateHandlerTests : CalendarSetupHandlerTestBase
{
    private DeleteWeekTemplateHandler Handler => Fixture.Create<DeleteWeekTemplateHandler>();

    /// <summary><see cref="StaffUserId"/>'s standard week.</summary>
    [SetUp]
    public void SetUp() =>
        Repository.Setup(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(BuildWeekTemplate());

    [Test]
    public async Task Handle_NeverSomeonesDefault_DeletesIt()
    {
        await Handler.Handle(new DeleteWeekTemplateCommand(WeekTemplateId, StaffActor), Token);

        Repository.Verify(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplateUsageAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == WeekTemplateId), Today, Token), Times.Once);
        Repository.Verify(r => r.DeleteWeekTemplateAsync(WeekTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminDeletesSomeoneElses_DeletesIt()
    {
        await Handler.Handle(new DeleteWeekTemplateCommand(WeekTemplateId, AdminActor), Token);

        Repository.Verify(r => r.DeleteWeekTemplateAsync(WeekTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_OtherStaffDeletesSomeoneElses_ThrowsForbiddenAndDeletesNothing()
    {
        var act = () => Handler.Handle(new DeleteWeekTemplateCommand(WeekTemplateId, OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourTemplates);
        Repository.Verify(r => r.DeleteWeekTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SomeonesDefaultBefore_ThrowsConflictSuggestingArchiving()
    {
        GivenWeekTemplateUsage(WeekTemplateId, new WeekTemplateUsage(0, true));

        var act = () => Handler.Handle(new DeleteWeekTemplateCommand(WeekTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{WeekTemplateName}'*Archive it instead*");
        Repository.Verify(r => r.DeleteWeekTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeleteWeekTemplateCommand(Guid.NewGuid(), StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Repository.Verify(r => r.DeleteWeekTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
