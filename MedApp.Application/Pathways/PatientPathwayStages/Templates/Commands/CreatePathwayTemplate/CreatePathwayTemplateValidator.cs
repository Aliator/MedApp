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
    }
}
