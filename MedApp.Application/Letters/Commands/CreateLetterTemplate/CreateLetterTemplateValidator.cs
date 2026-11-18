using FluentValidation;
using MedApp.Application.Letters.Common;

namespace MedApp.Application.Letters.Commands.CreateLetterTemplate;

public sealed class CreateLetterTemplateValidator : AbstractValidator<CreateLetterTemplateCommand>
{
    public CreateLetterTemplateValidator(ILetterRenderer renderer)
    {
        this.NameAndKind(x => x.Name, x => x.Kind);

        RuleFor(x => x.SpecialtyId)
            .Must(id => id != Guid.Empty)
            .WithMessage("Specialty id must be a specialty's, or left out for every specialty.");

        this.TemplateText(renderer, x => x.Letterhead, x => x.BodySkeleton, x => x.Closing);
    }
}
