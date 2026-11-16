using MedApp.Domain.Monitoring;

namespace MedApp.Application.Monitoring.Repositories;

public interface IMonitoringRuleRepository
{
    /// <summary>An admin's rules, oldest first, read-only.</summary>
    Task<IReadOnlyList<MonitoringRule>> GetForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>One of an admin's rules, read-only; null if there's no such rule or it's someone else's.</summary>
    Task<MonitoringRule?> GetAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken);

    Task<int> CountForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Every switched-on rule, of any admin, that watches for one of these events, read-only.</summary>
    Task<IReadOnlyList<MonitoringRule>> GetEnabledAsync(IReadOnlyCollection<MonitoringEvent> events, CancellationToken cancellationToken);

    /// <summary>Which of these accounts have the Admin role now: one read-only query.</summary>
    Task<IReadOnlySet<Guid>> GetAdminsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task AddAsync(MonitoringRule rule, CancellationToken cancellationToken);

    Task UpdateAsync(MonitoringRule rule, CancellationToken cancellationToken);

    Task DeleteAsync(MonitoringRule rule, CancellationToken cancellationToken);
}
