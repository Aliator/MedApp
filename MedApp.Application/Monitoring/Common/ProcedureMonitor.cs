using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using Microsoft.Extensions.Logging;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// Reads what a saved change did from its audit entries, loads the switched-on rules for those events (one query, and
/// none when the change did nothing a rule can watch for), and sends each admin whose rules match one notification.
/// Rules of someone who is no longer an admin are skipped, and nobody is told about their own change.
/// <para>
/// It runs after the change is saved, not in the same transaction (no outbox): a failure is logged and the change
/// stands, so at worst an admin misses a notification. Monitoring is administrative, not a safety net.
/// </para>
/// </summary>
public sealed class ProcedureMonitor(
    IMonitoringRuleRepository rules,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    ILogger<ProcedureMonitor> logger)
    : IProcedureMonitor
{
    public async Task NotifyAsync(ProcedureChange change, CancellationToken cancellationToken)
    {
        IReadOnlyList<MonitoringMatch> matches;
        try
        {
            matches = await FindMatchesAsync(change, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Monitoring rules couldn't be checked for procedure {ProcedureId}.", change.Procedure.Id);
            return;
        }

        foreach (var match in matches)
        {
            try
            {
                await notificationSender.SendAsync(
                    MonitoringNotifications.Create(match.OwnerUserId, match.Activity, change.Procedure, change.NowUtc),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "A monitoring notification for procedure {ProcedureId} couldn't be sent to user {UserId}.",
                    change.Procedure.Id, match.OwnerUserId);
            }
        }
    }

    private async Task<IReadOnlyList<MonitoringMatch>> FindMatchesAsync(ProcedureChange change, CancellationToken cancellationToken)
    {
        var activities = MonitoredActivity.From(change.Procedure, change.AuditEntries);
        if (activities.Count == 0)
            return [];

        var events = activities.Select(a => a.Event).Distinct().ToList();
        var enabled = await rules.GetEnabledAsync(events, cancellationToken);
        var matches = MonitoringRuleMatcher.Match(enabled, activities, change.Procedure, change.Actor.UserId);
        if (matches.Count == 0)
            return [];

        // A rule only counts while its owner is an admin: someone whose Admin role was removed stops being told.
        var admins = await identityReadService.GetUserIdsInRoleAsync(AppRoles.Admin, cancellationToken);
        return matches.Where(m => admins.Contains(m.OwnerUserId)).ToList();
    }
}
