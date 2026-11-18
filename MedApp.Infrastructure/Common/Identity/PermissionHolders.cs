using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class PermissionHolders(MedAppDbContext context) : IPermissionHolders
{
    public async Task<IReadOnlyList<Guid>> GetUserIdsAsync(Permission permission, CancellationToken ct) =>
        await RolePermissionQueries.HoldersOf(context, permission).ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> WhoHoldsAsync(IReadOnlyCollection<Guid> userIds, Permission permission, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new HashSet<Guid>();

        var holders = await RolePermissionQueries.HoldersOf(context, permission)
            .Where(id => ids.Contains(id))
            .ToListAsync(ct);
        return holders.ToHashSet();
    }
}
