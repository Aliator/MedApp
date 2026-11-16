using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Commands.DeleteActivityType;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Commands.DeleteActivityType;

[TestFixture]
public sealed class DeleteActivityTypeHandlerTests : CalendarSetupHandlerTestBase
{
    private DeleteActivityTypeHandler Handler => Fixture.Create<DeleteActivityTypeHandler>();

    [SetUp]
    public void SetUp() => GivenActivityTypes(Lunch);

    [Test]
    public async Task Handle_NotInUse_DeletesIt()
    {
        await Handler.Handle(new DeleteActivityTypeCommand(Lunch.Id), Token);

        Repository.Verify(r => r.GetActivityTypeAsync(Lunch.Id, Token), Times.Once);
        Repository.Verify(r => r.GetActivityTypesInUseAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == Lunch.Id), Token), Times.Once);
        Repository.Verify(r => r.DeleteActivityTypeAsync(Lunch.Id, Token), Times.Once);
    }

    [Test]
    public async Task Handle_InUse_ThrowsConflictSuggestingArchiving()
    {
        GivenActivityTypesInUse(Lunch.Id);

        var act = () => Handler.Handle(new DeleteActivityTypeCommand(Lunch.Id), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{Lunch.Name}'*Archive it instead*");
        Repository.Verify(r => r.DeleteActivityTypeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ActivityTypeDoesNotExist_ThrowsNotFound()
    {
        var id = Guid.NewGuid();

        var act = () => Handler.Handle(new DeleteActivityTypeCommand(id), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*'{id}'*");
        Repository.Verify(r => r.DeleteActivityTypeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
