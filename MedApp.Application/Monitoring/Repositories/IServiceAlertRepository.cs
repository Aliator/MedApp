using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Monitoring.Repositories;

/// <summary>What the service-alerts job reads and saves.</summary>
public interface IServiceAlertRepository
{
    /// <summary>The items a service alert rule reports now and hasn't before (<c>ServiceAlertCandidates</c>), read-only.</summary>
    Task<IReadOnlyList<Guid>> FindNewAsync(MonitoringRule rule, DateTime nowUtc, DateOnly today, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the items reported under the rule and stores the notification about them, in one save, so a run cut short
    /// repeats nothing and loses nothing.
    /// </summary>
    Task RecordAsync(Guid ruleId, IReadOnlyCollection<Guid> subjectIds, Notification notification, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>The names of these specialties, archived or not; one that no longer exists is left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetSpecialtyNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
