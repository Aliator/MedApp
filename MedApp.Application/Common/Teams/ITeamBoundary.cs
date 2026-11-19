using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Common.Teams;

/// <summary>
/// The team walls at a single record (the shared caller-scope service, like the light demo's <c>teamWalls.ts</c>):
/// whether the viewer (<see cref="TeamScope"/>) sees a pathway or a team's record, and the refusals across a wall. Lists
/// filter in SQL with <see cref="TeamAccess"/> instead.
/// <list type="bullet">
/// <item><b>Across a wall is a 404</b>, never a 403, with the same words as a record that doesn't exist, so its
/// existence isn't confirmed; the refusal records <c>TeamBoundaryRefused</c> (<c>{targetKind, targetId}</c>, once per
/// target in the view window) for the privacy officer.</item>
/// <item><b>Seen but not the viewer's team's</b> (an admin's read, the lab's after hand-back) is a 409 on a change,
/// naming whose it is.</item>
/// </list>
/// </summary>
public interface ITeamBoundary
{
    /// <summary>
    /// Why the viewer sees this pathway in full (the strongest: owner, stage owner, consult, MDT, admin, system), or
    /// null when it's behind their wall.
    /// </summary>
    Task<TeamAccessKind?> GetPathwayAccessAsync(Guid pathwayId, TeamScope scope, CancellationToken ct);

    /// <summary>Which of these pathways the viewer sees in full.</summary>
    Task<IReadOnlySet<Guid>> VisiblePathwaysAsync(IEnumerable<Guid> pathwayIds, TeamScope scope, CancellationToken ct);

    /// <summary>
    /// Why the viewer sees this pathway; behind their wall, a 404 (<paramref name="notFoundMessage"/>, the words of a
    /// missing one), recorded.
    /// </summary>
    Task<TeamAccessKind> EnsurePathwayVisibleAsync(
        PatientPathway pathway, TeamScope scope, string notFoundMessage, CancellationToken ct);

    /// <summary>
    /// For a change: the pathway seen (else a 404, recorded) and its team the viewer's (else a 409:
    /// <see cref="TeamWalls.NotYourTeams"/>).
    /// </summary>
    Task EnsureCanChangePathwayAsync(PatientPathway pathway, TeamScope scope, string notFoundMessage, CancellationToken ct);

    /// <summary>
    /// For a record with no pathway (MC5's referrals, appointments without a stage): seen when its team is one of the
    /// viewer's (or they see every team); otherwise a 404 (<paramref name="notFoundMessage"/>), recorded.
    /// </summary>
    Task EnsureTeamVisibleAsync(
        string targetKind, Guid targetId, Guid teamId, TeamScope scope, string notFoundMessage, CancellationToken ct);

    /// <summary>
    /// For a record that follows its pathway, or its own team when it has none (<see cref="TeamAccess.ByPathway{T}"/> at
    /// one record: MC5's letters, results, consent, MDT items, an accepted referral): why the viewer sees it (its team
    /// theirs: <see cref="TeamAccessKind.Owner"/>; else its pathway's access; else an admin's), or a 404
    /// (<paramref name="notFoundMessage"/>), recorded.
    /// </summary>
    Task<TeamAccessKind> EnsureRecordVisibleAsync(
        string targetKind, Guid targetId, Guid teamId, Guid? pathwayId, TeamScope scope, string notFoundMessage, CancellationToken ct);

    /// <summary>For a change to a record with no pathway: its team the viewer's, else a 409 naming whose it is.</summary>
    Task EnsureCanChangeTeamAsync(Guid teamId, TeamScope scope, string what, CancellationToken ct);

    /// <summary>
    /// Records a refusal across a wall (<c>TeamBoundaryRefused</c>, <c>{targetKind, targetId}</c>) and returns the 404 to
    /// throw, with <paramref name="notFoundMessage"/> (the words of a missing one).
    /// </summary>
    Task<Exceptions.NotFoundException> RefuseAsync(
        string targetKind, Guid targetId, string notFoundMessage, CancellationToken ct);
}
