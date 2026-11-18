using FluentValidation;

namespace MedApp.Application.Mdt.Commands.WithdrawMdtItem;

public sealed class WithdrawMdtItemValidator : AbstractValidator<WithdrawMdtItemCommand>
{
    public WithdrawMdtItemValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("MDT item id is required.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the MDT item you changed.");
    }
}
