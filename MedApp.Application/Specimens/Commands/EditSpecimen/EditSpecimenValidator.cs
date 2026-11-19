using FluentValidation;
using MedApp.Application.Specimens.Common;

namespace MedApp.Application.Specimens.Commands.EditSpecimen;

public sealed class EditSpecimenValidator : AbstractValidator<EditSpecimenCommand>
{
    public EditSpecimenValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Specimen id is required.");
        RuleFor(x => x.SpecimenTypeId).NotEmpty().WithMessage(SpecimenChoices.TypeMessage);
        RuleFor(x => x.RequestingUserId).NotEmpty().WithMessage(SpecimenRules.RequestingUserMessage);
        RuleFor(x => x.Site).Site();
        RuleFor(x => x.RequestNotes).RequestNotes();
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}
