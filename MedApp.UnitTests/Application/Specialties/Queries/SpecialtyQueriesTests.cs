using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Queries.GetSpecialties;
using MedApp.Application.Specialties.Queries.GetSpecialtyById;
using MedApp.Domain.Specialties;
using Moq;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Application.Specialties.Queries;

[TestFixture]
public sealed class SpecialtyQueriesTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task GetSpecialties_ReturnsEachWithWhatHasIt(bool includeArchived)
    {
        var used = CreateSpecialty();
        var unused = CreateSpecialty(OtherSpecialtyName);
        var repository = RepositoryWith(used, unused);
        repository.Setup(r => r.GetAllAsync(includeArchived, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        repository
            .Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SpecialtyUsage> { [used.Id] = new(1, 2, 3) });
        using var cts = new CancellationTokenSource();

        var response = await new GetSpecialtiesHandler(repository.Object).Handle(new GetSpecialtiesQuery(includeArchived), cts.Token);

        response.Select(s => (s.Name, s.TemplateCount, s.PathwayCount, s.MemberCount, s.CanBeDeleted))
            .Should().Equal((SpecialtyName, 1, 2, 3, false), (OtherSpecialtyName, 0, 0, 0, true));
        repository.Verify(r => r.GetAllAsync(includeArchived, cts.Token));
    }

    [Test]
    public async Task GetSpecialtyById_ReturnsIt()
    {
        var specialty = CreateSpecialty();

        var response = await new GetSpecialtyByIdHandler(RepositoryWith(specialty).Object)
            .Handle(new GetSpecialtyByIdQuery(specialty.Id), CancellationToken.None);

        response.Id.Should().Be(specialty.Id);
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task GetSpecialtyById_Unknown_ThrowsNotFound()
    {
        var act = () => new GetSpecialtyByIdHandler(RepositoryWith().Object)
            .Handle(new GetSpecialtyByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void SpecialtyUsage_IsInUseWhenAnythingHasIt()
    {
        SpecialtyUsage.None.IsInUse.Should().BeFalse();
        new SpecialtyUsage(0, 0, 1).IsInUse.Should().BeTrue();
        new Specialty().IsArchived.Should().BeFalse();
    }
}
