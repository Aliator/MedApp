using MedApp.Domain.Appointments;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Referrals;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Monitoring.ServiceAlerts;

/// <summary>
/// Which items a service alert rule reports now (decision 134): dates and statuses against the days an admin chose,
/// never a clinical value. Plain LINQ that EF translates, so the same code runs in SQL and in the unit tests. Each
/// leaves out what the rule has already reported (<see cref="MonitoringAlertSent"/>), so an item is reported once.
/// <list type="bullet">
/// <item><b>ReferralNotTriaged:</b> Awaiting triage, received more than N whole UK days ago. One waiting for more
/// information is waiting for the referrer, so it isn't counted.</item>
/// <item><b>ResultNotSignedOff:</b> awaiting sign-off (seen or not), reported more than N days ago; the person is the
/// responsible clinician.</item>
/// <item><b>VisitNotBooked:</b> Requested (Requires appointment) more than N days ago.</item>
/// <item><b>SpecimenOverdue:</b> open (taken, sent or received) more than N days past taken plus its type's expected
/// turnaround (0: the lab worklist's Overdue).</item>
/// <item><b>WaitBreached:</b> a running clock (not paused or stopped) whose target is less than N days away, or passed
/// (0: breached, as the waiting list shows it).</item>
/// <item><b>PauseReviewPassed:</b> an open pause whose review date passed more than N days ago (0: passed); reported per
/// pause, so a clock paused again is reported again.</item>
/// </list>
/// The specialty is the referral's, else the pathway's; the priority the pathway's (a referral without one: its
/// requested priority); the template the pathway's. A condition about a pathway leaves out what has none.
/// </summary>
public static class ServiceAlertCandidates
{
    public static IQueryable<Guid> Find(ServiceAlertSources sources, MonitoringRule rule, DateTime nowUtc, DateOnly today)
    {
        var days = rule.ThresholdDays ?? 0;
        var items = rule.Event switch
        {
            MonitoringEvent.ReferralNotTriaged => ReferralsNotTriaged(sources, rule, today.AddDays(-days)),
            MonitoringEvent.ResultNotSignedOff => ResultsNotSignedOff(sources, rule, nowUtc.AddDays(-days)),
            MonitoringEvent.VisitNotBooked => VisitsNotBooked(sources, rule, nowUtc.AddDays(-days)),
            MonitoringEvent.SpecimenOverdue => SpecimensOverdue(sources, rule, nowUtc, days),
            MonitoringEvent.WaitBreached => WaitsBreached(sources, rule, today.AddDays(days)),
            MonitoringEvent.PauseReviewPassed => PauseReviewsPassed(sources, rule, today.AddDays(-days)),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Event, "Not a service alert.")
        };

        var ruleId = rule.Id;
        var sent = sources.Sent;
        return items.Where(id => !sent.Any(s => s.RuleId == ruleId && s.SubjectId == id));
    }

    private static IQueryable<Guid> ReferralsNotTriaged(ServiceAlertSources sources, MonitoringRule rule, DateOnly receivedBefore)
    {
        var referrals = sources.Referrals.Where(r => r.Status == ReferralStatus.AwaitingTriage && r.ReceivedOn < receivedBefore);
        if (rule.SpecialtyId is { } specialtyId)
            referrals = referrals.Where(r => r.SpecialtyId == specialtyId);
        if (rule.MinimumPriority is { } priority)
            referrals = referrals.Where(r => r.RequestedPriority >= priority);
        return referrals.Select(r => r.Id);
    }

    private static IQueryable<Guid> ResultsNotSignedOff(ServiceAlertSources sources, MonitoringRule rule, DateTime reportedBefore)
    {
        var results = sources.Results.Where(r =>
            (r.Status == ResultStatus.AwaitingSignOff || r.Status == ResultStatus.Seen) && r.ReportedAtUtc < reportedBefore);
        if (rule.SubjectUserId is { } userId)
            results = results.Where(r => r.ResponsibleUserId == userId);
        if (rule.SpecialtyId is { } specialtyId)
            results = results.Where(r => r.Pathway != null && r.Pathway.SpecialtyId == specialtyId);
        if (rule.MinimumPriority is { } priority)
            results = results.Where(r => r.Pathway != null && r.Pathway.Priority >= priority);
        if (rule.TemplateId is { } templateId)
            results = results.Where(r => r.Pathway != null && r.Pathway.TemplateId == templateId);
        return results.Select(r => r.Id);
    }

    private static IQueryable<Guid> VisitsNotBooked(ServiceAlertSources sources, MonitoringRule rule, DateTime requestedBefore)
    {
        var visits = sources.Appointments.Where(a => a.Status == AppointmentStatus.Requested && a.CreatedAtUtc < requestedBefore);
        if (rule.SpecialtyId is { } specialtyId)
            visits = visits.Where(a => a.Pathway != null && a.Pathway.SpecialtyId == specialtyId);
        if (rule.MinimumPriority is { } priority)
            visits = visits.Where(a => a.Pathway != null && a.Pathway.Priority >= priority);
        if (rule.TemplateId is { } templateId)
            visits = visits.Where(a => a.Pathway != null && a.Pathway.TemplateId == templateId);
        if (rule.StageDefinitionId is { } stageDefinitionId)
            visits = visits.Where(a => a.Stage != null && a.Stage.StageDefinitionId == stageDefinitionId);
        return visits.Select(a => a.Id);
    }

    private static IQueryable<Guid> SpecimensOverdue(ServiceAlertSources sources, MonitoringRule rule, DateTime nowUtc, int days)
    {
        var specimens = sources.Specimens.Where(s =>
            (s.Status == SpecimenStatus.Taken || s.Status == SpecimenStatus.Sent || s.Status == SpecimenStatus.Received)
            && s.TakenAtUtc.AddDays(s.SpecimenType.ExpectedTurnaroundDays + days) < nowUtc);
        if (rule.SpecialtyId is { } specialtyId)
            specimens = specimens.Where(s => s.Pathway.SpecialtyId == specialtyId);
        if (rule.MinimumPriority is { } priority)
            specimens = specimens.Where(s => s.Pathway.Priority >= priority);
        if (rule.TemplateId is { } templateId)
            specimens = specimens.Where(s => s.Pathway.TemplateId == templateId);
        if (rule.StageDefinitionId is { } stageDefinitionId)
        {
            var stages = sources.Stages;
            specimens = specimens.Where(s => stages.Any(st => st.Id == s.StageId && st.StageDefinitionId == stageDefinitionId));
        }

        return specimens.Select(s => s.Id);
    }

    private static IQueryable<Guid> WaitsBreached(ServiceAlertSources sources, MonitoringRule rule, DateOnly targetBefore)
    {
        var referrals = sources.Referrals.Where(r =>
            (r.Status == ReferralStatus.AwaitingTriage || r.Status == ReferralStatus.InformationRequested || r.Status == ReferralStatus.Accepted)
            && r.StoppedOn == null
            && r.PausedSinceOn == null
            && r.TargetOn < targetBefore);
        if (rule.SpecialtyId is { } specialtyId)
            referrals = referrals.Where(r => r.SpecialtyId == specialtyId);
        if (rule.MinimumPriority is { } priority)
            referrals = referrals.Where(r => (r.Pathway != null ? r.Pathway.Priority : r.RequestedPriority) >= priority);
        if (rule.TemplateId is { } templateId)
            referrals = referrals.Where(r => r.Pathway != null && r.Pathway.TemplateId == templateId);
        return referrals.Select(r => r.Id);
    }

    private static IQueryable<Guid> PauseReviewsPassed(ServiceAlertSources sources, MonitoringRule rule, DateOnly reviewBefore)
    {
        var pauses = sources.Pauses.Where(p =>
            p.ResumedOn == null
            && p.ReviewOn < reviewBefore
            && p.Referral.StoppedOn == null
            && p.Referral.PausedSinceOn != null);
        if (rule.SpecialtyId is { } specialtyId)
            pauses = pauses.Where(p => p.Referral.SpecialtyId == specialtyId);
        if (rule.MinimumPriority is { } priority)
            pauses = pauses.Where(p => (p.Referral.Pathway != null ? p.Referral.Pathway.Priority : p.Referral.RequestedPriority) >= priority);
        if (rule.TemplateId is { } templateId)
            pauses = pauses.Where(p => p.Referral.Pathway != null && p.Referral.Pathway.TemplateId == templateId);
        return pauses.Select(p => p.Id);
    }
}
