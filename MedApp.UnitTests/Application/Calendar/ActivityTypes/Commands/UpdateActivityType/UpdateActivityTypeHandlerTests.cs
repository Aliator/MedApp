using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;

[TestFixture]
public sealed class UpdateActivityTypeHandlerTests : CalendarSetupHandlerTestBase
{
    private CalendarActivityType _activityType = null!;

    private UpdateActivityTypeHandler Handler => Fixture.Create<UpdateActivityTypeHandler>();

    [SetUp]
    public void SetUp()
    {
        _activityType = Copy(Lunch);
        GivenActivityTypes(_activityType);
    }

    [Test]
    public async Task Handle_NewNameAndColour_SavesBothTidied()
    {
        var response = await Handler.Handle(new UpdateActivityTypeCommand(_activityType.Id, $" {ActivityTypeName} ", UntidyColour), Token);

        _activityType.Name.Should().Be(ActivityTypeName);
        _activityType.Colour.Should().Be(TidyColour);
        response.Name.Should().Be(ActivityTypeName);
        response.Colour.Should().Be(TidyColour);
        Repository.Verify(r => r.ActivityTypeNameExistsAsync(ActivityTypeName, _activityType.Id, Token), Times.Once);
        Repository.Verify(r => r.UpdateActivityTypeAsync(_activityType, Token), Times.Once);
    }

    [Test]
    public async Task Handle_NothingGiven_KeepsEverythingAndStillSaves()
    {
        var response = await Handler.Handle(new UpdateActivityTypeCommand(_activityType.Id, null, null), Token);

        _activityType.Name.Should().Be(Lunch.Name);
        _activityType.Colour.Should().Be(Lunch.Colour);
        response.Name.Should().Be(Lunch.Name);
        Repository.Verify(r => r.ActivityTypeNameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        Repository.Verify(r => r.UpdateActivityTypeAsync(_activityType, Token), Times.Once);
    }

    [Test]
    public async Task Handle_SameName_DoesNotCheckItIsTaken()
    {
        await Handler.Handle(new UpdateActivityTypeCommand(_activityType.Id, $" {Lunch.Name}", null), Token);

        Repository.Verify(r => r.ActivityTypeNameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _activityType.Name.Should().Be(Lunch.Name);
    }

    [Test]
    public async Task Handle_NameTaken_ThrowsConflictAndSavesNothing()
    {
        Repository.Setup(r => r.ActivityTypeNameExistsAsync(ActivityTypeName, _activityType.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(new UpdateActivityTypeCommand(_activityType.Id, ActivityTypeName, null), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{ActivityTypeName}' already exists*");
        _activityType.Name.Should().Be(Lunch.Name);
        Repository.Verify(r => r.UpdateActivityTypeAsync(It.IsAny<CalendarActivityType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task Handle_Saved_SaysWhetherItCanBeDeleted(bool inUse, bool canBeDeleted)
    {
        if (inUse)
            GivenActivityTypesInUse(_activityType.Id);

        var response = await Handler.Handle(new UpdateActivityTypeCommand(_activityType.Id, null, TidyColour), Token);

        response.CanBeDeleted.Should().Be(canBeDeleted);
        Repository.Verify(r => r.GetActivityTypesInUseAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == _activityType.Id), Token), Times.Once);
    }

    [Test]
    public async Task Handle_ActivityTypeDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdateActivityTypeCommand(Guid.NewGuid(), ActivityTypeName, null), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Repository.Verify(r => r.UpdateActivityTypeAsync(It.IsAny<CalendarActivityType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Always_LoadsWithTheToken()
    {
        await Handler.Handle(new UpdateActivityTypeCommand(_activityType.Id, null, null), Token);

        Repository.Verify(r => r.GetActivityTypeAsync(_activityType.Id, Token), Times.Once);
    }
}
