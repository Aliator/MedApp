using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypes;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Queries.GetActivityTypes;

[TestFixture]
public sealed class GetActivityTypesHandlerTests : CalendarSetupHandlerTestBase
{
    private GetActivityTypesHandler Handler => Fixture.Create<GetActivityTypesHandler>();

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_Always_AsksForArchivedOnesOrNotWithTheToken(bool includeArchived)
    {
        Repository.Setup(r => r.GetActivityTypesAsync(includeArchived, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var response = await Handler.Handle(new GetActivityTypesQuery(includeArchived), Token);

        response.Should().BeEmpty();
        Repository.Verify(r => r.GetActivityTypesAsync(includeArchived, Token), Times.Once);
        Repository.Verify(r => r.GetActivityTypesInUseAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }

    [Test]
    public async Task Handle_SomeInUse_KeepsTheOrderAndSaysWhichCanBeDeleted()
    {
        var archived = CreateArchivedActivityType();
        IReadOnlyList<CalendarActivityType> types = [archived, Lunch, Training];
        Repository.Setup(r => r.GetActivityTypesAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync(types);
        GivenActivityTypesInUse(Lunch.Id);

        var response = await Handler.Handle(new GetActivityTypesQuery(true), Token);

        response.Select(r => r.Id).Should().Equal(archived.Id, Lunch.Id, Training.Id);
        response.Select(r => r.CanBeDeleted).Should().Equal(true, false, true);
        response[0].IsArchived.Should().BeTrue();
        Repository.Verify(r => r.GetActivityTypesInUseAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { archived.Id, Lunch.Id, Training.Id })),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
