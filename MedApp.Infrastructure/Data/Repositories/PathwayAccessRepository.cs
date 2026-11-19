using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Pathway access grants, read only (see <see cref="IPathwayAccessRepository"/>).</summary>
public sealed class PathwayAccessRepository(MedAppDbContext context) : IPathwayAccessRepository
{
    public async Task<IReadOnlyList<PathwayAccess>> GetLiveAsync(
        IReadOnlyCollection<Guid> pathwayIds, TeamScope scope, CancellationToken ct)
    {
        var ids = pathwayIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await context.PathwayAccess
            .AsNoTracking()
            .Where(a => ids.Contains(a.PathwayId))
            .Where(TeamAccess.LiveGrantFor(scope))
            .ToListAsync(ct);
    }

    public Task<bool> HasGrantAsync(Guid pathwayId, Guid teamId, PathwayAccessReason reason, CancellationToken ct) =>
        context.PathwayAccess.AnyAsync(a => a.PathwayId == pathwayId && a.TeamId == teamId && a.Reason == reason, ct);
}
