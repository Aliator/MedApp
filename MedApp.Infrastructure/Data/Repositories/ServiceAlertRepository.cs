using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Monitoring.ServiceAlerts;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class ServiceAlertRepository(MedAppDbContext context) : IServiceAlertRepository
{
    public async Task<IReadOnlyList<Guid>> FindNewAsync(MonitoringRule rule, DateTime nowUtc, DateOnly today, CancellationToken cancellationToken)
    {
        var sources = new ServiceAlertSources(
            context.Referrals.AsNoTracking(),
            context.ReferralClockPauses.AsNoTracking(),
            context.Results.AsNoTracking(),
            context.Appointments.AsNoTracking(),
            context.Specimens.AsNoTracking(),
            context.PatientPathwayStages.AsNoTracking(),
            context.MonitoringAlertsSent.AsNoTracking());

        return await ServiceAlertCandidates.Find(sources, rule, nowUtc, today).ToListAsync(cancellationToken);
    }

    public async Task RecordAsync(
        Guid ruleId, IReadOnlyCollection<Guid> subjectIds, Notification notification, DateTime nowUtc, CancellationToken cancellationToken)
    {
        context.MonitoringAlertsSent.AddRange(subjectIds.Distinct().Select(id => new MonitoringAlertSent
        {
            RuleId = ruleId,
            SubjectId = id,
            SentAtUtc = nowUtc
        }));
        context.Notifications.Add(notification);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            // Saved or not, nothing stays tracked for a later save in the same run to retry.
            context.ChangeTracker.Clear();
        }
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetSpecialtyNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await context.Specialties
            .AsNoTracking()
            .Where(s => list.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
    }
}
