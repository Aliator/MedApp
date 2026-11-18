using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Teams.Commands.CreateTeam;
using MedApp.Application.Teams.Commands.DeleteTeam;
using MedApp.Application.Teams.Commands.SetTeamArchived;
using MedApp.Application.Teams.Commands.UpdateTeam;
using MedApp.Application.Teams.Queries.GetTeams;
using MedApp.Application.Teams.Queries.GetTeamById;
using MedApp.Contracts.Teams.Requests;
using MedApp.Contracts.Teams.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.TeamsTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class TeamsControllerTests
{
    /// <summary>Teams are organisation-wide, so only admins change them; any staff read them.</summary>
    private static readonly string[] AdminOnly =
    [
        nameof(TeamsController.CreateTeam),
        nameof(TeamsController.UpdateTeam),
        nameof(TeamsController.ArchiveTeam),
        nameof(TeamsController.RestoreTeam),
        nameof(TeamsController.DeleteTeam),
    ];

    private IFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static TeamsController Controller(RecordingMediator mediator) =>
        new TeamsController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, object request, Func<TeamsController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetTeams_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<TeamResponse> teams = [];
        await ExpectOk(teams, new GetTeamsQuery(includeArchived), c => c.GetTeams(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetTeam_ReturnsIt() =>
        await ExpectOk(_fixture.Create<TeamResponse>(), new GetTeamByIdQuery(RecordId), c => c.GetTeam(RecordId, _cts.Token));

    [Test]
    public async Task CreateTeam_ReturnsItWithItsAddress()
    {
        var team = _fixture.Create<TeamResponse>();
        var mediator = new RecordingMediator(_ => team);

        var result = await Controller(mediator).CreateTeam(new CreateTeamRequest { Name = TeamName }, _cts.Token);

        mediator.Request.Should().Be(new CreateTeamCommand(TeamName));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/teams/{team.Id}");
        created.Value.Should().Be(team);
    }

    [Test]
    public async Task UpdateTeam_SendsTheNameAndTheVersionSeen() =>
        await ExpectOk(_fixture.Create<TeamResponse>(),
            new UpdateTeamCommand(RecordId, OtherTeamName, 4),
            c => c.UpdateTeam(RecordId, new UpdateTeamRequest { Name = OtherTeamName, ExpectedVersion = 4 }, _cts.Token));

    [Test]
    public async Task ArchiveAndRestoreTeam_SetItsArchivedFlagFromTheVersionSeen()
    {
        var team = _fixture.Create<TeamResponse>();
        var request = new TeamVersionRequest { ExpectedVersion = 2 };
        await ExpectOk(team, new SetTeamArchivedCommand(RecordId, true, 2), c => c.ArchiveTeam(RecordId, request, _cts.Token));
        await ExpectOk(team, new SetTeamArchivedCommand(RecordId, false, 2), c => c.RestoreTeam(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task DeleteTeam_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeleteTeam(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeleteTeamCommand(RecordId));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task Changes_AdminsOnly()
    {
        foreach (var action in AdminOnly)
        {
            (await ControllerAuthorisation.MayCallAsync<TeamsController>(action, SignedIn("User"))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<TeamsController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<TeamsController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Reading_AnyStaff()
    {
        var anyStaff = ControllerAuthorisation.Actions<TeamsController>().Except(AdminOnly).ToList();
        anyStaff.Should().BeEquivalentTo([nameof(TeamsController.GetTeams), nameof(TeamsController.GetTeam)]);

        foreach (var action in anyStaff)
        {
            (await ControllerAuthorisation.MayCallAsync<TeamsController>(action, SignedIn("User"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<TeamsController>(action, SignedIn())).Should().BeFalse(action);
        }
    }
}
