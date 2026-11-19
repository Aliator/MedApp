using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Teams;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// The real <see cref="TeamBoundary"/> over a list of pathway grants (<see cref="Grants"/>) and a team repository, with
/// refusals recorded in a <see cref="RecordingActivityLog"/>: for handler tests of the team walls in MC5's areas.
/// </summary>
public sealed class GrantsTeamBoundary
{
    public GrantsTeamBoundary(RecordingActivityLog activity, Mock<ITeamRepository>? teams = null)
    {
        Activity = activity;
        Teams = teams ?? new Mock<ITeamRepository>();
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken _) =>
                (IReadOnlyList<PathwayAccess>)Grants
                    .Where(g => ids.Contains(g.PathwayId))
                    .Where(TeamAccess.LiveGrantFor(scope).Compile())
                    .ToList());
        Boundary = new TeamBoundary(access.Object, Teams.Object, activity);
    }

    /// <summary>The pathway grants the boundary reads.</summary>
    public List<PathwayAccess> Grants { get; } = [];

    public Mock<ITeamRepository> Teams { get; }

    public RecordingActivityLog Activity { get; }

    public ITeamBoundary Boundary { get; }

    /// <summary>A member of these teams (not an admin).</summary>
    public static TeamScope MemberOf(Guid userId, params Guid[] teamIds) => new(userId, teamIds, seesAllTeams: false);

    /// <summary>An admin: every team's records, read only.</summary>
    public static TeamScope AdminScope(Guid userId) => new(userId, [], seesAllTeams: true);
}
