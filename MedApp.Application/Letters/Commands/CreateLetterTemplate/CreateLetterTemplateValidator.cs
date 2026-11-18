using FluentValidation;
using MedApp.Application.Letters.Common;

namespace MedApp.Application.Letters.Commands.CreateLetterTemplate;

public sealed class CreateLetterTemplateValidator : AbstractValidator<CreateLetterTemplateCommand>
{
    public CreateLetterTemplateValidator(ILetterRenderer renderer)
    {
        this.NameAndKind(x => x.Name, x => x.Kind);

        RuleFor(x => x.TeamId)
            .Must(id => id != Guid.Empty)
            .WithMessage("Team id must be a team's, or left out for every team.");

        this.TemplateText(renderer, x => x.Letterhead, x => x.BodySkeleton, x => x.Closing);
    }
}
