using FluentValidation;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

public sealed class UpdatePathwayTemplateValidator : AbstractValidator<UpdatePathwayTemplateCommand>
{
    public UpdatePathwayTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("Template id is required.");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .When(x => x.Name is not null)
            .WithMessage("Name must not be blank.")
            .MaximumLength(150)
            .WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.SpecialtyId)
            .Must(id => id != Guid.Empty)
            .When(x => x.SpecialtyId is not null)
            .WithMessage("Specialty must not be empty.");

        RuleFor(x => x.StageDefinitionIdsInOrder)
            .Must(ids => ids is null || ids.Count > 0)
            .WithMessage("A template needs at least one stage.")
            .Must(ids => ids is null || ids.Count <= CreatePathwayTemplateValidator.MaxStages)
            .WithMessage($"A template can have at most {CreatePathwayTemplateValidator.MaxStages} stages.");

        RuleForEach(x => x.StageDefinitionIdsInOrder)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");

        RuleFor(x => x.StageOptions)
            .Must((command, options) => options is null || options.Count == (command.StageDefinitionIdsInOrder?.Count ?? -1))
            .WithMessage(TemplateBranchRules.CountMismatchMessage)
            .DependentRules(() =>
                RuleFor(x => x.StageOptions)
                    .Custom((options, context) =>
                    {
                        if (options is not null && TemplateBranchRules.WhyInvalid(options) is { } problem)
                            context.AddFailure(problem);
                    }));
        RuleFor(x => x.ClockStopStageOrder)
            .Must((command, order) => order is null || command.StageDefinitionIdsInOrder is not null)
            .WithMessage("Send the stage list with the stage that stops the waiting-time clock.")
            .Must((command, order) => order is null || command.StageDefinitionIdsInOrder is null
                                      || (order >= 1 && order <= command.StageDefinitionIdsInOrder.Count))
            .WithMessage(CreatePathwayTemplateValidator.ClockStopStageMessage);
    }
}
