using MedApp.Application.Common.Identity;

namespace MedApp.Application.Common.Teams;

/// <summary>
/// Whose team walls apply to a request (decision 199 of <c>docs/multi-clinic-plan.md</c>): the viewer's teams
/// (<see cref="TeamIds"/>), whether they see every team's records (<see cref="SeesAllTeams"/>: admins, through
/// <see cref="Permission.ViewAllTeams"/>, read only), and whether it's the system itself (<see cref="IsSystem"/>: jobs,
/// the seed, <c>medapp-dev</c>; unscoped, never from a request).
/// <para>
/// Built per request from the session (<c>ClaimsPrincipal.ToTeamScope()</c> in the API: a <c>medapp:team</c> claim per
/// membership, read with the roles on every request), carried on <c>PathwayActor.Scope</c> and
/// <c>CalendarActor.Scope</c>, and passed to every query on team data. The rules that use it are in
/// <see cref="TeamAccess"/>; refusals across a wall go through <see cref="ITeamBoundary"/>.
/// </para>
/// </summary>
public sealed record TeamScope
{
    public TeamScope(Guid userId, IEnumerable<Guid> teamIds, bool seesAllTeams, bool isSystem = false)
    {
        UserId = userId;
        TeamIds = teamIds.ToHashSet();
        SeesAllTeams = seesAllTeams || isSystem;
        IsSystem = isSystem;
    }

    /// <summary>The viewer (an MDT grant is to a person).</summary>
    public Guid UserId { get; }

    /// <summary>The teams the viewer belongs to.</summary>
    public IReadOnlySet<Guid> TeamIds { get; }

    /// <summary>Sees every team's records: admins (read only), and the system.</summary>
    public bool SeesAllTeams { get; }

    /// <summary>The system itself (jobs, the seed): unscoped, and may change anything. Never a request's.</summary>
    public bool IsSystem { get; }

    /// <summary>The system's scope: every team's records, and every change. Never from a request.</summary>
    public static TeamScope System { get; } = new(Guid.Empty, [], seesAllTeams: true, isSystem: true);

    /// <summary>A viewer's scope from their memberships and what their roles allow.</summary>
    public static TeamScope For(Guid userId, IEnumerable<Guid> teamIds, IReadOnlySet<Permission>? permissions) =>
        new(userId, teamIds, permissions?.Contains(Permission.ViewAllTeams) == true);

    /// <summary>
    /// The team a record they make takes when nothing else decides it (a patient's own file, an appointment without a
    /// pathway): one of theirs, the same each time, or null when they're in none.
    /// </summary>
    public Guid? DefaultTeamId => TeamIds.Count == 0 ? null : TeamIds.Min();

    /// <summary>Whether the viewer belongs to this team.</summary>
    public bool IsMember(Guid teamId) => TeamIds.Contains(teamId);

    /// <summary>Scopes compare their teams by content.</summary>
    public bool Equals(TeamScope? other) =>
        other is not null
        && UserId == other.UserId
        && SeesAllTeams == other.SeesAllTeams
        && IsSystem == other.IsSystem
        && TeamIds.SetEquals(other.TeamIds);

    public override int GetHashCode() => HashCode.Combine(UserId, SeesAllTeams, IsSystem, TeamIds.Count);
}
