using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Teams;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// The real <see cref="TeamBoundary"/> over grants kept in a list (<see cref="Grants"/>) and named teams, recording its
/// refusals in <see cref="Activity"/>, for testing an area's team walls through its handlers. Actors without a scope are
/// the system's, which sees and changes everything, so tests that aren't about the walls pass it unchanged.
/// </summary>
public sealed class TeamWallsTestBoundary
{
    public TeamWallsTestBoundary(RecordingActivityLog? activity = null)
    {
        Activity = activity ?? new RecordingActivityLog();
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken _) =>
                (IReadOnlyList<PathwayAccess>)Grants.Where(g => ids.Contains(g.PathwayId))
                    .Where(TeamAccess.LiveGrantFor(scope).Compile()).ToList());
        var teams = new Mock<ITeamRepository>();
        teams.Setup(t => t.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                TeamNames.TryGetValue(id, out var name) ? new Team { Id = id, Name = name } : null);
        Boundary = new TeamBoundary(access.Object, teams.Object, Activity);
    }

    /// <summary>The pathway access grants the boundary reads (add an owner's for each pathway a test makes).</summary>
    public List<PathwayAccess> Grants { get; } = [];

    /// <summary>The teams' names, for the 409s' words.</summary>
    public Dictionary<Guid, string> TeamNames { get; } = [];

    public RecordingActivityLog Activity { get; }

    public ITeamBoundary Boundary { get; }

    /// <summary>A viewer in these teams (not an admin).</summary>
    public static TeamScope StaffIn(Guid userId, params Guid[] teamIds) => new(userId, teamIds, seesAllTeams: false);

    /// <summary>An admin: every team's records, read only.</summary>
    public static TeamScope AdminScope(Guid userId) => new(userId, [], seesAllTeams: true);

    /// <summary>Checks one refusal across the wall was recorded, for this target.</summary>
    public void ShouldHaveRefused(string targetKind, Guid targetId)
    {
        var (action, details) = Activity.Once.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.TeamBoundaryRefused);
        var root = JsonDocument.Parse(details).RootElement;
        root.GetProperty("targetKind").GetString().Should().Be(targetKind);
        root.GetProperty("targetId").GetGuid().Should().Be(targetId);
    }
}
