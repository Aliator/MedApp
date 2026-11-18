using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Domain.Monitoring;
using Microsoft.Extensions.Logging;

namespace MedApp.Application.Monitoring.ServiceAlerts;

/// <summary>
/// The service alerts (centre plan decisions 134 to 137), every 15 minutes: for each switched-on service alert rule
/// whose owner still holds ManageMonitoring, finds what it reports and hasn't yet (<see cref="ServiceAlertCandidates"/>)
/// and, if anything, sends its owner one notification with the count (<see cref="ServiceAlertNotifications"/>), saved
/// in the same save as the items' <see cref="MonitoringAlertSent"/> rows, so a run cut short repeats nothing.
/// <para>
/// <b>A convenience, never the safety net</b> (hazard H-29): the lists and their counts are. Clinical follow-up never
/// relies on an alert, and a stopped job is caught by the Scheduled Jobs page and its failure notification. Logs counts
/// only.
/// </para>
/// </summary>
public sealed class ServiceAlertsJob(
    IMonitoringRuleRepository rules,
    IServiceAlertRepository alerts,
    IIdentityReadService identityReadService,
    INotificationPublisher publisher,
    ILogger<ServiceAlertsJob> logger) : IScheduledJob
{
    public const string JobName = "service-alerts";

    public string Name => JobName;

    public string Title => "Service alerts";

    public string Description =>
        "Tells admins, by their monitoring rules, of work waiting longer than they chose: referrals not triaged, results not "
        + "signed off, visits not booked, specimens overdue, waiting-time targets near and pause reviews passed. Each item once per rule.";

    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(15));

    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var enabled = (await rules.GetEnabledAsync(MonitoringEvents.ServiceAlerts, cancellationToken))
            .Where(rule => rule.ThresholdDays is not null)
            .ToList();
        if (enabled.Count == 0)
            return 0;

        // Rules of owners who no longer hold ManageMonitoring are skipped (kept, and nothing marked reported).
        var owners = await rules.GetOwnersWhoCanMonitorAsync(enabled.Select(r => r.OwnerUserId).ToList(), cancellationToken);
        enabled = enabled.Where(rule => owners.Contains(rule.OwnerUserId)).ToList();
        if (enabled.Count == 0)
            return 0;

        var teamIds = enabled.Select(r => r.TeamId).OfType<Guid>().Distinct().ToList();
        IReadOnlyDictionary<Guid, string> teams = teamIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await alerts.GetTeamNamesAsync(teamIds, cancellationToken);
        var userIds = enabled.Select(r => r.SubjectUserId).OfType<Guid>().Distinct().ToList();
        IReadOnlyDictionary<Guid, string> usernames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await identityReadService.GetUsernamesByIdAsync(userIds, cancellationToken);

        var today = UkTime.DateOf(nowUtc);
        var reported = 0;
        var sent = 0;
        foreach (var rule in enabled)
        {
            var items = await alerts.FindNewAsync(rule, nowUtc, today, cancellationToken);
            if (items.Count == 0)
                continue;

            var notification = ServiceAlertNotifications.Create(
                rule,
                items.Count,
                rule.TeamId is { } teamId ? teams.GetValueOrDefault(teamId) : null,
                rule.SubjectUserId is { } userId ? usernames.GetValueOrDefault(userId) : null,
                nowUtc);
            await alerts.RecordAsync(rule.Id, items, notification, nowUtc, cancellationToken);
            reported += items.Count;
            sent++;

            // Live delivery is best effort: the notification is stored, and shows on the next load.
            try
            {
                await publisher.PublishAsync(notification.UserId, NotificationMapper.ToResponse(notification), cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning("A service alert couldn't be pushed live ({ExceptionType}).", e.GetType().Name);
            }
        }

        logger.LogInformation(
            "Service alerts: {Rules} rules checked, {Notifications} notifications sent, {Items} items reported.",
            enabled.Count, sent, reported);
        return reported;
    }
}
