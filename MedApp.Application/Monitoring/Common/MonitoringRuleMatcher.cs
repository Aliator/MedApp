using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Monitoring.Common;

/// <summary>An admin to notify about one change, and the activity to tell them about.</summary>
public sealed record MonitoringMatch(Guid OwnerUserId, MonitoredActivity Activity);

/// <summary>Which monitoring rules a change to a pathway matches, and so which admins to tell.</summary>
public static class MonitoringRuleMatcher
{
    /// <summary>
    /// Whether the rule matches this activity on this pathway: it's on, watches for this event, and every condition
    /// it has holds (the person, a priority at least its minimum, the template, the stage's definition). The template is
    /// the pathway's after the change; the priority is the higher of before and after (<see cref="PriorityOf"/>).
    /// </summary>
    public static bool Matches(MonitoringRule rule, MonitoredActivity activity, PatientPathway pathway) =>
        rule.IsEnabled
        && rule.Event == activity.Event
        && (rule.SubjectUserId is null || rule.SubjectUserId == activity.SubjectUserId)
        && (rule.MinimumPriority is null || PriorityOf(activity, pathway) >= rule.MinimumPriority)
        && (rule.TemplateId is null || rule.TemplateId == pathway.TemplateId)
        && (rule.StageDefinitionId is null || rule.StageDefinitionId == activity.StageDefinitionId);

    /// <summary>
    /// The priority a rule matches on: the pathway's after the change, or before it if that was higher, so an edit
    /// that moves an Urgent pathway's target date and lowers it to Low in one save still counts as Urgent.
    /// </summary>
    public static PatientPathwayPriority PriorityOf(MonitoredActivity activity, PatientPathway pathway) =>
        activity.PriorityBefore is { } before && before > pathway.Priority ? before : pathway.Priority;

    /// <summary>
    /// The admins to notify about one change, each once however many of their rules match (de-duplicated), about the
    /// first activity of the change that one of their rules matched. Nobody is told about their own change, nor told
    /// they were assigned twice: the person assigned already gets the assignment notification.
    /// </summary>
    public static IReadOnlyList<MonitoringMatch> Match(
        IEnumerable<MonitoringRule> rules,
        IReadOnlyList<MonitoredActivity> activities,
        PatientPathway pathway,
        Guid actorUserId)
    {
        var candidates = rules.Where(rule => rule.OwnerUserId != actorUserId).ToList();
        var matches = new List<MonitoringMatch>();
        var notified = new HashSet<Guid>();

        foreach (var activity in activities)
        foreach (var rule in candidates)
        {
            var isTheAssignee = activity.Event == MonitoringEvent.PathwayAssigned && rule.OwnerUserId == activity.SubjectUserId;
            if (!isTheAssignee && !notified.Contains(rule.OwnerUserId) && Matches(rule, activity, pathway))
            {
                notified.Add(rule.OwnerUserId);
                matches.Add(new MonitoringMatch(rule.OwnerUserId, activity));
            }
        }

        return matches;
    }
}
