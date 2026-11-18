using FluentValidation;

namespace MedApp.Application.Mdt.Commands.DeferMdtItem;

public sealed class DeferMdtItemValidator : AbstractValidator<DeferMdtItemCommand>
{
    public DeferMdtItemValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("MDT item id is required.");
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("Choose an MDT.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the MDT item you changed.");
    }
}
