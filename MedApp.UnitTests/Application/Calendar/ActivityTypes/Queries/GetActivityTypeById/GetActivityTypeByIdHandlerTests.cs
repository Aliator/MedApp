using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypeById;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Queries.GetActivityTypeById;

[TestFixture]
public sealed class GetActivityTypeByIdHandlerTests : CalendarSetupHandlerTestBase
{
    private GetActivityTypeByIdHandler Handler => Fixture.Create<GetActivityTypeByIdHandler>();

    [Test]
    public async Task Handle_Exists_ReturnsIt()
    {
        GivenActivityTypes(Lunch);

        var response = await Handler.Handle(new GetActivityTypeByIdQuery(Lunch.Id), Token);

        response.Id.Should().Be(Lunch.Id);
        response.Name.Should().Be(Lunch.Name);
        response.Colour.Should().Be(Lunch.Colour);
        response.CanBeDeleted.Should().BeTrue();
        Repository.Verify(r => r.GetActivityTypeAsync(Lunch.Id, Token), Times.Once);
        Repository.Verify(r => r.GetActivityTypesInUseAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }

    [Test]
    public async Task Handle_InUse_ReturnsItNotDeletable()
    {
        GivenActivityTypes(Lunch);
        GivenActivityTypesInUse(Lunch.Id);

        var response = await Handler.Handle(new GetActivityTypeByIdQuery(Lunch.Id), Token);

        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_DoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new GetActivityTypeByIdQuery(Guid.NewGuid()), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Activity type*was not found.");
    }
}
