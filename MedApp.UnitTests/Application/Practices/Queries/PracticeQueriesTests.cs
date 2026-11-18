using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Queries.GetPracticeById;
using MedApp.Application.Practices.Queries.GetPractices;
using Moq;
using static MedApp.UnitTests.Common.Constants.PracticesTestConstants;

namespace MedApp.UnitTests.Application.Practices.Queries;

[TestFixture]
public sealed class PracticeQueriesTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task GetPractices_ListsThemWithTheirUsage(bool includeArchived)
    {
        var used = CreatePractice();
        var unused = CreatePractice(OtherPracticeName, OtherOdsCode);
        var repository = RepositoryWith(used, unused);
        repository.Setup(r => r.GetAllAsync(includeArchived, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        repository.Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PracticeUsage> { [used.Id] = new(1, 0) });
        using var cts = new CancellationTokenSource();

        var response = await new GetPracticesHandler(repository.Object).Handle(new GetPracticesQuery(includeArchived), cts.Token);

        response.Select(p => (p.Name, p.PatientCount, p.CanBeDeleted))
            .Should().Equal((PracticeName, 1, false), (OtherPracticeName, 0, true));
        repository.Verify(r => r.GetAllAsync(includeArchived, cts.Token));
    }

    [Test]
    public async Task GetPracticeById_Known_ReturnsIt_UnknownIsNotFound()
    {
        var practice = CreatePractice();
        var handler = new GetPracticeByIdHandler(RepositoryWith(practice).Object);

        (await handler.Handle(new GetPracticeByIdQuery(practice.Id), CancellationToken.None)).Id.Should().Be(practice.Id);

        var act = () => handler.Handle(new GetPracticeByIdQuery(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
