using FluentValidation;

namespace MedApp.Application.Mdt.Commands.ChooseMdtPresenter;

public sealed class ChooseMdtPresenterValidator : AbstractValidator<ChooseMdtPresenterCommand>
{
    public ChooseMdtPresenterValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("MDT item id is required.");
        RuleFor(x => x.PresenterUserId).Must(id => id != Guid.Empty).WithMessage("Presenter id must not be empty: leave it out for nobody.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the MDT item you changed.");
    }
}
