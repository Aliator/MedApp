using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
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
            .WithMessage("A stage can only be chosen for a stage being finished or unlocked.");
    }
}

public static class MonitoringRuleInput
{
    /// <summary>
    /// The most rules one admin can have: plenty, and it keeps matching cheap. It's a count then an insert, so two adds
    /// at the same moment at 49 can make 51; accepted, as a rule or two over harms nothing.
    /// </summary>
    public const int MaxRulesPerAdmin = 50;

    /// <summary>Whether a rule for this event can name a stage.</summary>
    public static bool HasStage(MonitoringEvent monitoringEvent) =>
        monitoringEvent is MonitoringEvent.StageFinished or MonitoringEvent.StageUnlocked;

    /// <summary>The message when "Stage unlocked" names someone who isn't an admin.</summary>
    public const string UnlockNeedsAdmin = "Only admins unlock stages, so choose an admin, or anyone.";

    /// <summary>
    /// Checks what the input newly names: every person, template and stage it names (all of them for a new rule; for an
    /// edit, only those that differ from the stored rule's, so a rule may keep naming something since deleted) must
    /// exist, archived or not (404 otherwise). And a person named for "Stage unlocked" must be an admin, as only admins
    /// unlock (400 otherwise; checked again whenever the person or the event changes, as roles do).
    /// </summary>
    public static async Task CheckAsync(
        IMonitoringRuleInput input,
        MonitoringRule? existing,
        IIdentityReadService identityReadService,
        IPatientPathwayStagesRepository stagesRepository,
        CancellationToken cancellationToken)
    {
        Guid? Newly(Guid? id, Guid? stored) => existing is not null && id == stored ? null : id;
        var monitoringEvent = PathwayEnums.Parse<MonitoringEvent>(input.Event);

        if (Newly(input.SubjectUserId, existing?.SubjectUserId) is { } userId)
        {
            var usernames = await identityReadService.GetUsernamesByIdAsync([userId], cancellationToken);
            if (!usernames.TryGetValue(userId, out var username))
                throw new NotFoundException($"User '{userId}' was not found.");
            if (monitoringEvent == MonitoringEvent.StageUnlocked)
                await CheckIsAdminAsync(username, identityReadService, cancellationToken);
        }
        else if (input.SubjectUserId is { } sameUser
                 && monitoringEvent == MonitoringEvent.StageUnlocked
                 && existing?.Event != MonitoringEvent.StageUnlocked
                 && (await identityReadService.GetUsernamesByIdAsync([sameUser], cancellationToken)).TryGetValue(sameUser, out var name))
            await CheckIsAdminAsync(name, identityReadService, cancellationToken);

        if (Newly(input.TemplateId, existing?.TemplateId) is { } templateId
            && !(await stagesRepository.GetPathwayTemplateNamesAsync([templateId], cancellationToken)).ContainsKey(templateId))
            throw new NotFoundException($"Template '{templateId}' was not found.");

        if (Newly(input.StageDefinitionId, existing?.StageDefinitionId) is { } stageId
            && await stagesRepository.GetStageDefinitionByIdAsync(stageId, cancellationToken) is null)
            throw new NotFoundException($"Stage '{stageId}' was not found.");
    }

    private static async Task CheckIsAdminAsync(string username, IIdentityReadService identityReadService, CancellationToken cancellationToken)
    {
        var roles = await identityReadService.GetRolesForUserAsync(username, cancellationToken);
        if (!roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase))
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
        rule.IsEnabled = input.IsEnabled;
        rule.UpdatedAtUtc = nowUtc;
    }
}
