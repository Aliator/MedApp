using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Queries.GetTeams;
using MedApp.Application.Teams.Queries.GetTeamById;
using MedApp.Domain.Teams;
using Moq;
using static MedApp.UnitTests.Common.Constants.TeamsTestConstants;

namespace MedApp.UnitTests.Application.Teams.Queries;

[TestFixture]
public sealed class TeamQueriesTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task GetTeams_ReturnsEachWithWhatHasIt(bool includeArchived)
    {
        var used = CreateTeam();
        var unused = CreateTeam(OtherTeamName);
        var repository = RepositoryWith(used, unused);
        repository.Setup(r => r.GetAllAsync(includeArchived, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        repository
            .Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamUsage> { [used.Id] = new(1, 2, 3) });
        using var cts = new CancellationTokenSource();

        var response = await new GetTeamsHandler(repository.Object).Handle(new GetTeamsQuery(includeArchived), cts.Token);

        response.Select(s => (s.Name, s.TemplateCount, s.PathwayCount, s.MemberCount, s.CanBeDeleted))
            .Should().Equal((TeamName, 1, 2, 3, false), (OtherTeamName, 0, 0, 0, true));
        repository.Verify(r => r.GetAllAsync(includeArchived, cts.Token));
    }

    [Test]
    public async Task GetTeamById_ReturnsIt()
    {
        var team = CreateTeam();

        var response = await new GetTeamByIdHandler(RepositoryWith(team).Object)
            .Handle(new GetTeamByIdQuery(team.Id), CancellationToken.None);

        response.Id.Should().Be(team.Id);
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task GetTeamById_Unknown_ThrowsNotFound()
    {
        var act = () => new GetTeamByIdHandler(RepositoryWith().Object)
            .Handle(new GetTeamByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void TeamUsage_IsInUseWhenAnythingHasIt()
    {
        TeamUsage.None.IsInUse.Should().BeFalse();
        new TeamUsage(0, 0, 1).IsInUse.Should().BeTrue();
        new Team().IsArchived.Should().BeFalse();
    }
}
