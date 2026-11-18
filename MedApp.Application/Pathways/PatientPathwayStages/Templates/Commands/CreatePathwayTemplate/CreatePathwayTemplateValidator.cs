using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

public sealed class CreatePathwayTemplateValidator : AbstractValidator<CreatePathwayTemplateCommand>
{
    public const int MaxStages = 50;

    public CreatePathwayTemplateValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(150)
            .WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.SpecialtyId)
            .NotEmpty()
            .WithMessage("Specialty is required.");

        RuleFor(x => x.StageDefinitionIdsInOrder)
            .NotNull()
            .WithMessage("Stage definition ids are required.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("A template needs at least one stage.")
            .Must(ids => ids is null || ids.Count <= MaxStages)
            .WithMessage($"A template can have at most {MaxStages} stages.");

        RuleForEach(x => x.StageDefinitionIdsInOrder)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");

        RuleFor(x => x.StageOptions)
            .Must((command, options) => options is null || options.Count == (command.StageDefinitionIdsInOrder?.Count ?? 0))
            .WithMessage(TemplateBranchRules.CountMismatchMessage)
            .DependentRules(() =>
                RuleFor(x => x.StageOptions)
                    .Custom((options, context) =>
                    {
                        if (options is not null && TemplateBranchRules.WhyInvalid(options) is { } problem)
                            context.AddFailure(problem);
                    }));
        RuleFor(x => x.ClockStopStageOrder)
            .Must((command, order) => order is null || (order >= 1 && order <= (command.StageDefinitionIdsInOrder?.Count ?? 0)))
            .WithMessage(ClockStopStageMessage);
    }

    /// <summary>The clock-stop stage must be one of the template's stages, by its position.</summary>
    public const string ClockStopStageMessage = "The stage that stops the waiting-time clock must be one of the template's stages.";
}
