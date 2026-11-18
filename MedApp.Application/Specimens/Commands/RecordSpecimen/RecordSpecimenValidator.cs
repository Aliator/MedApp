using FluentValidation;
using MedApp.Application.Specimens.Common;

namespace MedApp.Application.Specimens.Commands.RecordSpecimen;

public sealed class RecordSpecimenValidator : AbstractValidator<RecordSpecimenCommand>
{
    public RecordSpecimenValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Choose the stage that took the specimen.");
        RuleFor(x => x.SpecimenTypeId).NotEmpty().WithMessage(SpecimenChoices.TypeMessage);
        RuleFor(x => x.LaboratoryId).NotEmpty().WithMessage(SpecimenChoices.LaboratoryMessage);
        RuleFor(x => x.RequestingUserId).NotEmpty().WithMessage(SpecimenRules.RequestingUserMessage);
        RuleFor(x => x.Site).Site();
        RuleFor(x => x.RequestNotes).RequestNotes();
    }
}
