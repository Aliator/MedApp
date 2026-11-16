using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Procedures.PatientProcedures;

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
            .Must(value => ProcedureEnums.TryParse<MonitoringEvent>(value, out _))
            .WithMessage($"Event must be one of: {EventNames}.");

        RuleFor(x => x.MinimumPriority)
            .Must(ProcedureEnums.IsPriority)
            .When(x => x.MinimumPriority is not null)
            .WithMessage($"Priority must be one of: {ProcedureEnums.PriorityNames}.");

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
            .When(x => ProcedureEnums.TryParse<MonitoringEvent>(x.Event, out var e) && !MonitoringRuleInput.HasStage(e))
            .WithMessage("A stage can only be chosen for a stage being finished or unlocked.");
    }
}

public static class MonitoringRuleInput
{
    /// <summary>The most rules one admin can have: plenty, and it keeps matching cheap.</summary>
    public const int MaxRulesPerAdmin = 50;

    /// <summary>Whether a rule for this event can name a stage.</summary>
    public static bool HasStage(MonitoringEvent monitoringEvent) =>
        monitoringEvent is MonitoringEvent.StageFinished or MonitoringEvent.StageUnlocked;

    /// <summary>
    /// Checks that what the input names exists (404 otherwise): the person (any account; their role isn't checked,
    /// since roles change), the template and the stage, archived or not.
    /// </summary>
    public static async Task CheckNamedThingsExistAsync(
        IMonitoringRuleInput input,
        IIdentityReadService identityReadService,
        IPatientProcedureStagesRepository stagesRepository,
        CancellationToken cancellationToken)
    {
        if (input.SubjectUserId is { } userId
            && !(await identityReadService.GetUsernamesByIdAsync([userId], cancellationToken)).ContainsKey(userId))
            throw new NotFoundException($"User '{userId}' was not found.");

        if (input.TemplateId is { } templateId
            && !(await stagesRepository.GetStageTemplateNamesAsync([templateId], cancellationToken)).ContainsKey(templateId))
            throw new NotFoundException($"Template '{templateId}' was not found.");

        if (input.StageDefinitionId is { } stageId
            && await stagesRepository.GetStageDefinitionByIdAsync(stageId, cancellationToken) is null)
            throw new NotFoundException($"Stage '{stageId}' was not found.");
    }

    /// <summary>Sets a rule's fields from validated input.</summary>
    public static void Apply(MonitoringRule rule, IMonitoringRuleInput input, DateTime nowUtc)
    {
        rule.SubjectUserId = input.SubjectUserId;
        rule.Event = ProcedureEnums.Parse<MonitoringEvent>(input.Event);
        rule.MinimumPriority = input.MinimumPriority is null ? null : ProcedureEnums.Parse<PatientProcedurePriority>(input.MinimumPriority);
        rule.TemplateId = input.TemplateId;
        rule.StageDefinitionId = input.StageDefinitionId;
        rule.IsEnabled = input.IsEnabled;
        rule.UpdatedAtUtc = nowUtc;
    }
}
