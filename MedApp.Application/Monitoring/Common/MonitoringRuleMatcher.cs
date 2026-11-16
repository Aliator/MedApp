using MedApp.Domain.Monitoring;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Monitoring.Common;

/// <summary>An admin to notify about one change, and the activity to tell them about.</summary>
public sealed record MonitoringMatch(Guid OwnerUserId, MonitoredActivity Activity);

/// <summary>Which monitoring rules a change to a procedure matches, and so which admins to tell.</summary>
public static class MonitoringRuleMatcher
{
    /// <summary>
    /// Whether the rule matches this activity on this procedure: it's on, watches for this event, and every condition
    /// it has holds (the person, a priority at least its minimum, the template, the stage's definition). The priority
    /// and template are the procedure's after the change.
    /// </summary>
    public static bool Matches(MonitoringRule rule, MonitoredActivity activity, PatientProcedure procedure) =>
        rule.IsEnabled
        && rule.Event == activity.Event
        && (rule.SubjectUserId is null || rule.SubjectUserId == activity.SubjectUserId)
        && (rule.MinimumPriority is null || procedure.Priority >= rule.MinimumPriority)
        && (rule.TemplateId is null || rule.TemplateId == procedure.TemplateId)
        && (rule.StageDefinitionId is null || rule.StageDefinitionId == activity.StageDefinitionId);

    /// <summary>
    /// The admins to notify about one change, each once however many of their rules match (de-duplicated), about the
    /// first activity of the change that one of their rules matched. Nobody is told about their own change.
    /// </summary>
    public static IReadOnlyList<MonitoringMatch> Match(
        IEnumerable<MonitoringRule> rules,
        IReadOnlyList<MonitoredActivity> activities,
        PatientProcedure procedure,
        Guid actorUserId)
    {
        var candidates = rules.Where(rule => rule.OwnerUserId != actorUserId).ToList();
        var matches = new List<MonitoringMatch>();
        var notified = new HashSet<Guid>();

        foreach (var activity in activities)
        foreach (var rule in candidates)
        {
            if (!notified.Contains(rule.OwnerUserId) && Matches(rule, activity, procedure))
            {
                notified.Add(rule.OwnerUserId);
                matches.Add(new MonitoringMatch(rule.OwnerUserId, activity));
            }
        }

        return matches;
    }
}
