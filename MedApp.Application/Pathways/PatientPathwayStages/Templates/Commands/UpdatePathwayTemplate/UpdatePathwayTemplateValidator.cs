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
    }
}
