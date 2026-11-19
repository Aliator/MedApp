using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Common.Teams;

/// <summary>
/// The team walls at a single record (see <see cref="ITeamBoundary"/>): grants read through
/// <see cref="IPathwayAccessRepository"/>, the rules from <see cref="TeamAccess"/>, refusals recorded in the activity log.
/// </summary>
public sealed class TeamBoundary(
    IPathwayAccessRepository access,
    ITeamRepository teams,
    IActivityLog activityLog)
    : ITeamBoundary
{
    public async Task<TeamAccessKind?> GetPathwayAccessAsync(Guid pathwayId, TeamScope scope, CancellationToken ct)
    {
        // Admins (and the system) see every team's records, but a grant of their own is still the stronger reason.
        var grants = scope.TeamIds.Count == 0 && scope.SeesAllTeams
            ? []
            : await access.GetLiveAsync([pathwayId], scope, ct);
        return TeamAccess.Strongest(grants.Where(g => g.PathwayId == pathwayId), scope);
    }

    public async Task<IReadOnlySet<Guid>> VisiblePathwaysAsync(IEnumerable<Guid> pathwayIds, TeamScope scope, CancellationToken ct)
    {
        var ids = pathwayIds.Distinct().ToList();
        if (scope.SeesAllTeams || ids.Count == 0)
            return ids.ToHashSet();

        var grants = await access.GetLiveAsync(ids, scope, ct);
        return grants.Where(TeamAccess.LiveGrantFor(scope).Compile()).Select(g => g.PathwayId).ToHashSet();
    }

    public async Task<TeamAccessKind> EnsurePathwayVisibleAsync(
        PatientPathway pathway, TeamScope scope, string notFoundMessage, CancellationToken ct) =>
        await GetPathwayAccessAsync(pathway.Id, scope, ct)
        ?? throw await RefuseAsync(TeamWalls.PathwayTarget, pathway.Id, notFoundMessage, ct);

    public async Task EnsureCanChangePathwayAsync(
        PatientPathway pathway, TeamScope scope, string notFoundMessage, CancellationToken ct)
    {
        await EnsurePathwayVisibleAsync(pathway, scope, notFoundMessage, ct);
        if (!TeamAccess.CanChange(pathway, scope))
            throw new ConflictException(TeamWalls.NotYourTeams(await TeamNameAsync(pathway.TeamId, ct)));
    }

    public async Task EnsureTeamVisibleAsync(
        string targetKind, Guid targetId, Guid teamId, TeamScope scope, string notFoundMessage, CancellationToken ct)
    {
        if (!TeamAccess.CanSeeTeam(teamId, scope))
            throw await RefuseAsync(targetKind, targetId, notFoundMessage, ct);
    }

    public async Task<TeamAccessKind> EnsureRecordVisibleAsync(
        string targetKind, Guid targetId, Guid teamId, Guid? pathwayId, TeamScope scope, string notFoundMessage, CancellationToken ct)
    {
        if (scope.IsMember(teamId))
            return TeamAccessKind.Owner;
        if (pathwayId is { } id && await GetPathwayAccessAsync(id, scope, ct) is { } access)
            return access;
        if (scope.SeesAllTeams)
            return scope.IsSystem ? TeamAccessKind.System : TeamAccessKind.Admin;
        throw await RefuseAsync(targetKind, targetId, notFoundMessage, ct);
    }

    public async Task EnsureCanChangeTeamAsync(Guid teamId, TeamScope scope, string what, CancellationToken ct)
    {
        if (!TeamAccess.CanChangeTeam(teamId, scope))
            throw new ConflictException(TeamWalls.RecordNotYours(what, await TeamNameAsync(teamId, ct)));
    }

    public async Task<NotFoundException> RefuseAsync(string targetKind, Guid targetId, string notFoundMessage, CancellationToken ct)
    {
        await activityLog.RecordOnceAsync(ActivityAction.TeamBoundaryRefused, new { targetKind, targetId }, ct);
        return new NotFoundException(notFoundMessage);
    }

    private async Task<string?> TeamNameAsync(Guid teamId, CancellationToken ct) =>
        (await teams.GetAsync(teamId, ct))?.Name;
}
