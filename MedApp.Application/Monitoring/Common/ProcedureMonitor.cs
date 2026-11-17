using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.Extensions.Logging;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// Reads what a saved change did from its audit entries, loads the switched-on rules for those events (one query, and
/// none when the change did nothing a rule can watch for), and sends each admin whose rules match one notification
/// (one more query, for which owners are admins now, then an insert and a push for each admin told). Rules of someone
/// who is no longer an admin are skipped, not deleted, and nobody is told about their own change.
/// <para>
/// It runs after the change is saved, not in the same transaction (no outbox): a failure is logged and the change
/// stands, so at worst an admin misses a notification. Monitoring is administrative, not a safety net.
/// </para>
/// </summary>
public sealed class ProcedureMonitor(
    IMonitoringRuleRepository rules,
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
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
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
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "A monitoring notification for procedure {ProcedureId} couldn't be sent to user {UserId}.",
                    change.Procedure.Id, match.OwnerUserId);
            }
        }
    }

    private Task<IReadOnlyList<MonitoringMatch>> FindMatchesAsync(ProcedureChange change, CancellationToken cancellationToken) =>
        FindMatchesAsync(change.Procedure, change.AuditEntries, change.Actor.UserId, rules.GetEnabledAsync, rules.GetAdminsAsync,
            cancellationToken);

    /// <summary>
    /// Whom a change's audit entries should be told about, and what: the activities read from the entries, the
    /// switched-on rules for those events (from <paramref name="enabledRules"/>, not asked when the change did nothing a
    /// rule can watch for), matched by <see cref="MonitoringRuleMatcher"/>, keeping only owners who are admins (from
    /// <paramref name="admins"/>, not asked when nothing matched). Shared with medapp-dev's demo data, which answers the
    /// two questions with the rules and admins as they were at a moment in its history.
    /// </summary>
    public static async Task<IReadOnlyList<MonitoringMatch>> FindMatchesAsync(
        PatientProcedure procedure,
        IReadOnlyList<PatientProcedureAuditEntry> entries,
        Guid actorUserId,
        Func<IReadOnlyCollection<MonitoringEvent>, CancellationToken, Task<IReadOnlyList<MonitoringRule>>> enabledRules,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlySet<Guid>>> admins,
        CancellationToken cancellationToken)
    {
        var activities = MonitoredActivity.From(procedure, entries);
        if (activities.Count == 0)
            return [];

        var events = activities.Select(a => a.Event).Distinct().ToList();
        var enabled = await enabledRules(events, cancellationToken);
        var matches = MonitoringRuleMatcher.Match(enabled, activities, procedure, actorUserId);
        if (matches.Count == 0)
            return [];

        // A rule only counts while its owner is an admin: someone whose Admin role was removed stops being told (their
        // rules stay, and count again if they're made an admin again).
        var adminIds = await admins(matches.Select(m => m.OwnerUserId).ToList(), cancellationToken);
        return matches.Where(m => adminIds.Contains(m.OwnerUserId)).ToList();
    }
}
