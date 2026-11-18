using FluentValidation;
using MedApp.Application.Letters.Common;

namespace MedApp.Application.Letters.Commands.UpdateLetterTemplate;

public sealed class UpdateLetterTemplateValidator : AbstractValidator<UpdateLetterTemplateCommand>
{
    public UpdateLetterTemplateValidator(ILetterRenderer renderer)
    {
        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("Letter template id is required.");

        this.NameAndKind(x => x.Name, x => x.Kind);

        RuleFor(x => x.SpecialtyId)
            .Must(id => id != Guid.Empty)
            .WithMessage("Specialty id must be a specialty's, or left out for every specialty.");

        this.TemplateText(renderer, x => x.Letterhead, x => x.BodySkeleton, x => x.Closing);

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version is required: send the version you last saw.");
    }
}
