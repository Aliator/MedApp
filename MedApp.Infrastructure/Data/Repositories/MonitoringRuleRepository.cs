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
