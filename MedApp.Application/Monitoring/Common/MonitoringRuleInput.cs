using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Monitoring.Common;

/// <summary>What an admin sets on a rule, as the add and edit commands take it.</summary>
public interface IMonitoringRuleInput
{
    Guid? SubjectUserId { get; }
    string Event { get; }
    string? MinimumPriority { get; }
    Guid? TemplateId { get; }
    Guid? StageDefinitionId { get; }
    bool IsEnabled { get; }
    int? ThresholdDays { get; }
    Guid? TeamId { get; }
}

/// <summary>The checks every rule passes, for adding one and for editing one.</summary>
public abstract class MonitoringRuleInputValidator<T> : AbstractValidator<T> where T : IMonitoringRuleInput
{
    public static string EventNames => string.Join(", ", Enum.GetNames<MonitoringEvent>());

    protected MonitoringRuleInputValidator()
    {
        RuleFor(x => x.Event)
            .Must(value => PathwayEnums.TryParse<MonitoringEvent>(value, out _))
            .WithMessage($"Event must be one of: {EventNames}.");

        RuleFor(x => x.MinimumPriority)
            .Must(PathwayEnums.IsPriority)
            .When(x => x.MinimumPriority is not null)
            .WithMessage($"Priority must be one of: {PathwayEnums.PriorityNames}.");

        RuleFor(x => x.SubjectUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Choose a staff member, or anyone.");

        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .WithMessage("Choose a template, or any.");

        RuleFor(x => x.StageDefinitionId)
            .NotEqual(Guid.Empty)
            .WithMessage("Choose a stage, or any.");

        RuleFor(x => x.StageDefinitionId)
            .Null()
            .When(x => PathwayEnums.TryParse<MonitoringEvent>(x.Event, out var e) && !MonitoringRuleInput.HasStage(e))
            .WithMessage("A stage can only be chosen for a stage being finished, finished without its visit, or unlocked, a visit not booked or a specimen overdue.");

        RuleFor(x => x.TeamId)
            .NotEqual(Guid.Empty)
            .WithMessage("Choose a team, or any.");

        RuleFor(x => x.ThresholdDays)
            .NotNull()
            .WithMessage("Choose how many days.")
            .InclusiveBetween(MonitoringRuleInput.MinThresholdDays, MonitoringRuleInput.MaxThresholdDays)
            .WithMessage($"Days must be from {MonitoringRuleInput.MinThresholdDays} to {MonitoringRuleInput.MaxThresholdDays}.")
            .When(x => IsServiceAlert(x.Event));

        RuleFor(x => x.ThresholdDays)
            .Null()
            .When(x => PathwayEnums.TryParse<MonitoringEvent>(x.Event, out var e) && !MonitoringEvents.IsServiceAlert(e))
            .WithMessage("Days are only for a service alert.");

        RuleFor(x => x.SubjectUserId)
            .Null()
            .When(x => PathwayEnums.TryParse<MonitoringEvent>(x.Event, out var e) && !MonitoringRuleInput.HasPerson(e))
            .WithMessage("This alert isn't about a member of staff, so leave it on anyone.");

        RuleFor(x => x.TemplateId)
            .Null()
            .When(x => PathwayEnums.TryParse<MonitoringEvent>(x.Event, out var e) && !MonitoringRuleInput.HasTemplate(e))
            .WithMessage("A referral awaiting triage has no pathway yet, so it has no template: leave it on any.");
    }

    private static bool IsServiceAlert(string value) =>
        PathwayEnums.TryParse<MonitoringEvent>(value, out var e) && MonitoringEvents.IsServiceAlert(e);
}

public static class MonitoringRuleInput
{
    /// <summary>
    /// The most rules one admin can have: plenty, and it keeps matching cheap. It's a count then an insert, so two adds
    /// at the same moment at 49 can make 51; accepted, as a rule or two over harms nothing.
    /// </summary>
    public const int MaxRulesPerAdmin = 50;

    /// <summary>The fewest and most days a service alert can wait (decision 134).</summary>
    public const int MinThresholdDays = 0;

    public const int MaxThresholdDays = 90;

    /// <summary>
    /// Whether a rule for this event can name a stage: the stage events, a visit not booked (its stage) and a specimen
    /// overdue (the stage it was taken at).
    /// </summary>
    public static bool HasStage(MonitoringEvent monitoringEvent) =>
        monitoringEvent is MonitoringEvent.StageFinished or MonitoringEvent.StageUnlocked or MonitoringEvent.StageFinishedWithoutVisit
            or MonitoringEvent.VisitNotBooked or MonitoringEvent.SpecimenOverdue;

    /// <summary>
    /// Whether a rule for this event can name a person: every change (who did it), and of the service alerts only a
    /// result not signed off (the responsible clinician); the other alerts are about the service, not someone's action.
    /// </summary>
    public static bool HasPerson(MonitoringEvent monitoringEvent) =>
        !MonitoringEvents.IsServiceAlert(monitoringEvent) || monitoringEvent == MonitoringEvent.ResultNotSignedOff;

    /// <summary>Whether a rule for this event can name a template: all but a referral awaiting triage (no pathway yet).</summary>
    public static bool HasTemplate(MonitoringEvent monitoringEvent) => monitoringEvent != MonitoringEvent.ReferralNotTriaged;

    /// <summary>The message when "Stage unlocked" names someone who can't unlock stages.</summary>
    public const string UnlockNeedsAdmin = "Only people whose roles let them unlock stages do, so choose one of them, or anyone.";

    /// <summary>
    /// Checks what the input newly names: every person, template and stage it names (all of them for a new rule; for an
    /// edit, only those that differ from the stored rule's, so a rule may keep naming something since deleted) must
    /// exist, archived or not, and so must a team (404 otherwise). And a person named for "Stage unlocked" must hold
    /// <see cref="Permission.UnlockStages"/>, as only they unlock (400 otherwise; checked again whenever the person or
    /// the event changes, as roles do).
    /// </summary>
    public static async Task CheckAsync(
        IMonitoringRuleInput input,
        MonitoringRule? existing,
        IIdentityReadService identityReadService,
        IPermissionHolders permissionHolders,
        IPatientPathwayStagesRepository stagesRepository,
        ITeamRepository teamRepository,
        CancellationToken cancellationToken)
    {
        Guid? Newly(Guid? id, Guid? stored) => existing is not null && id == stored ? null : id;
        var monitoringEvent = PathwayEnums.Parse<MonitoringEvent>(input.Event);

        if (Newly(input.SubjectUserId, existing?.SubjectUserId) is { } userId)
        {
            var usernames = await identityReadService.GetUsernamesByIdAsync([userId], cancellationToken);
            if (!usernames.ContainsKey(userId))
                throw new NotFoundException($"User '{userId}' was not found.");
            if (monitoringEvent == MonitoringEvent.StageUnlocked)
                await CheckCanUnlockAsync(userId, permissionHolders, cancellationToken);
        }
        else if (input.SubjectUserId is { } sameUser
                 && monitoringEvent == MonitoringEvent.StageUnlocked
                 && existing?.Event != MonitoringEvent.StageUnlocked
                 && (await identityReadService.GetUsernamesByIdAsync([sameUser], cancellationToken)).ContainsKey(sameUser))
            await CheckCanUnlockAsync(sameUser, permissionHolders, cancellationToken);

        if (Newly(input.TemplateId, existing?.TemplateId) is { } templateId
            && !(await stagesRepository.GetPathwayTemplateNamesAsync([templateId], cancellationToken)).ContainsKey(templateId))
            throw new NotFoundException($"Template '{templateId}' was not found.");

        if (Newly(input.StageDefinitionId, existing?.StageDefinitionId) is { } stageId
            && await stagesRepository.GetStageDefinitionByIdAsync(stageId, cancellationToken) is null)
            throw new NotFoundException($"Stage '{stageId}' was not found.");

        if (Newly(input.TeamId, existing?.TeamId) is { } teamId
            && await teamRepository.GetAsync(teamId, cancellationToken) is null)
            throw new NotFoundException($"Team '{teamId}' was not found.");
    }

    private static async Task CheckCanUnlockAsync(Guid userId, IPermissionHolders permissionHolders, CancellationToken cancellationToken)
    {
        var holders = await permissionHolders.WhoHoldsAsync([userId], Permission.UnlockStages, cancellationToken);
        if (!holders.Contains(userId))
            throw new ValidationException([new ValidationFailure(nameof(IMonitoringRuleInput.SubjectUserId), UnlockNeedsAdmin)]);
    }

    /// <summary>Sets a rule's fields from validated input.</summary>
    public static void Apply(MonitoringRule rule, IMonitoringRuleInput input, DateTime nowUtc)
    {
        rule.SubjectUserId = input.SubjectUserId;
        rule.Event = PathwayEnums.Parse<MonitoringEvent>(input.Event);
        rule.MinimumPriority = input.MinimumPriority is null ? null : PathwayEnums.Parse<PatientPathwayPriority>(input.MinimumPriority);
        rule.TemplateId = input.TemplateId;
        rule.StageDefinitionId = input.StageDefinitionId;
        rule.ThresholdDays = input.ThresholdDays;
        rule.TeamId = input.TeamId;
        rule.IsEnabled = input.IsEnabled;
        rule.UpdatedAtUtc = nowUtc;
    }
}
