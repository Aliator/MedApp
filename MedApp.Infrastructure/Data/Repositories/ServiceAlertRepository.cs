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
            context.MonitoringAlertsSent.AsNoTracking(),
            context.LabCases.AsNoTracking());

        // MC19's lab clocks follow each case's test and UK dates: narrowed in SQL, the clock worked out in memory.
        if (MonitoringEvents.IsLabClock(rule.Event))
            return (await ServiceAlertCandidates.LabCasesToCheck(sources, rule)
                    .Include(c => c.LabTest)
                    .ToListAsync(cancellationToken))
                .Where(c => ServiceAlertCandidates.IsLabCaseDue(c, rule, nowUtc, today))
                .Select(c => c.Id)
                .ToList();

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

    public async Task<IReadOnlyDictionary<Guid, string>> GetTeamNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await context.Teams
            .AsNoTracking()
            .Where(s => list.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
    }
}
