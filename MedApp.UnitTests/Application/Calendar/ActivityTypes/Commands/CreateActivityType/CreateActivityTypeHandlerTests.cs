using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Commands.CreateActivityType;

[TestFixture]
public sealed class CreateActivityTypeHandlerTests : CalendarSetupHandlerTestBase
{
    private CreateActivityTypeHandler Handler => Fixture.Create<CreateActivityTypeHandler>();

    [Test]
    public async Task Handle_NewName_AddsItTidiedAndReturnsItDeletable()
    {
        CalendarActivityType? added = null;
        Repository
            .Setup(r => r.AddActivityTypeAsync(It.IsAny<CalendarActivityType>(), It.IsAny<CancellationToken>()))
            .Callback((CalendarActivityType t, CancellationToken _) => added = t)
            .Returns(Task.CompletedTask);

        var response = await Handler.Handle(new CreateActivityTypeCommand($"  {ActivityTypeName} ", UntidyColour), Token);

        added.Should().NotBeNull();
        added!.Id.Should().NotBeEmpty();
        added.Name.Should().Be(ActivityTypeName);
        added.Colour.Should().Be(TidyColour);
        added.IsArchived.Should().BeFalse();
        response.Id.Should().Be(added.Id);
        response.Name.Should().Be(ActivityTypeName);
        response.Colour.Should().Be(TidyColour);
        response.IsArchived.Should().BeFalse();
        response.CanBeDeleted.Should().BeTrue();
        Repository.Verify(r => r.ActivityTypeNameExistsAsync(ActivityTypeName, null, Token), Times.Once);
        Repository.Verify(r => r.AddActivityTypeAsync(added, Token), Times.Once);
    }

    [Test]
    public async Task Handle_NameTaken_ThrowsConflictAndAddsNothing()
    {
        Repository.Setup(r => r.ActivityTypeNameExistsAsync(ActivityTypeName, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(new CreateActivityTypeCommand(ActivityTypeName, TidyColour), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{ActivityTypeName}' already exists*");
        Repository.Verify(r => r.AddActivityTypeAsync(It.IsAny<CalendarActivityType>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
