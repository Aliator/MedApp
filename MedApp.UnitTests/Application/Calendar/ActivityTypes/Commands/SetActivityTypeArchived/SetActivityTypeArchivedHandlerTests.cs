using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;

[TestFixture]
public sealed class SetActivityTypeArchivedHandlerTests : CalendarSetupHandlerTestBase
{
    private SetActivityTypeArchivedHandler Handler => Fixture.Create<SetActivityTypeArchivedHandler>();

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task Handle_ChangesTheFlag_SavesIt(bool wasArchived, bool archive)
    {
        var activityType = Copy(Lunch);
        activityType.IsArchived = wasArchived;
        GivenActivityTypes(activityType);
        GivenActivityTypesInUse(activityType.Id);

        var response = await Handler.Handle(new SetActivityTypeArchivedCommand(activityType.Id, archive), Token);

        activityType.IsArchived.Should().Be(archive);
        response.IsArchived.Should().Be(archive);
        response.CanBeDeleted.Should().BeFalse();
        Repository.Verify(r => r.UpdateActivityTypeAsync(activityType, Token), Times.Once);
        Repository.Verify(r => r.GetActivityTypesInUseAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_AlreadyAsAsked_SavesNothing(bool archived)
    {
        var activityType = Copy(Lunch);
        activityType.IsArchived = archived;
        GivenActivityTypes(activityType);

        var response = await Handler.Handle(new SetActivityTypeArchivedCommand(activityType.Id, archived), Token);

        response.IsArchived.Should().Be(archived);
        response.CanBeDeleted.Should().BeTrue();
        Repository.Verify(r => r.UpdateActivityTypeAsync(It.IsAny<CalendarActivityType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ActivityTypeDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new SetActivityTypeArchivedCommand(Guid.NewGuid(), true), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
