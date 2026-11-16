using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class MonitoringRuleRepository(MedAppDbContext context) : IMonitoringRuleRepository
{
    public async Task<IReadOnlyList<MonitoringRule>> GetForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        await context.MonitoringRules
            .AsNoTracking()
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

    public async Task<MonitoringRule?> GetAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken) =>
        await context.MonitoringRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == ownerUserId, cancellationToken);

    public Task<int> CountForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        context.MonitoringRules.CountAsync(r => r.OwnerUserId == ownerUserId, cancellationToken);

    public async Task<IReadOnlyList<MonitoringRule>> GetEnabledAsync(
        IReadOnlyCollection<MonitoringEvent> events, CancellationToken cancellationToken)
    {
        var list = events.Distinct().ToList();
        return await context.MonitoringRules
            .AsNoTracking()
            .Where(r => r.IsEnabled && list.Contains(r.Event))
            .OrderBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetAdminsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new HashSet<Guid>();

        var admin = AppRoles.Admin.ToUpperInvariant();
        var admins = await (
                from userRole in context.UserRoles.AsNoTracking()
                join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.NormalizedName == admin && ids.Contains(userRole.UserId)
                select userRole.UserId)
            .ToListAsync(cancellationToken);
        return admins.ToHashSet();
    }

    public async Task AddAsync(MonitoringRule rule, CancellationToken cancellationToken)
    {
        context.MonitoringRules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MonitoringRule rule, CancellationToken cancellationToken)
    {
        context.MonitoringRules.Update(rule);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(MonitoringRule rule, CancellationToken cancellationToken)
    {
        context.MonitoringRules.Remove(rule);
        await context.SaveChangesAsync(cancellationToken);
    }
}
