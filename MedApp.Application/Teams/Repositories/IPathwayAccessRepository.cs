using MedApp.Application.Common.Teams;
using MedApp.Domain.Teams;

namespace MedApp.Application.Teams.Repositories;

/// <summary>
/// Pathway access grants (the team walls, <see cref="PathwayAccess"/>), read only: grants are written in the same save as
/// the event that causes them (a pathway's change, a sample recorded), by those repositories.
/// </summary>
public interface IPathwayAccessRepository
{
    /// <summary>The viewer's live grants (for one of their teams, or to them) on these pathways.</summary>
    Task<IReadOnlyList<PathwayAccess>> GetLiveAsync(IReadOnlyCollection<Guid> pathwayIds, TeamScope scope, CancellationToken ct);

    /// <summary>Whether this team already has a grant on this pathway for this reason, live or not.</summary>
    Task<bool> HasGrantAsync(Guid pathwayId, Guid teamId, PathwayAccessReason reason, CancellationToken ct);
}
