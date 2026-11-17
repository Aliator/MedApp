using FluentValidation;
using MedApp.Domain.Notepads;

namespace MedApp.Application.Notepads.Commands.SaveNotepad;

public sealed class SaveNotepadValidator : AbstractValidator<SaveNotepadCommand>
{
    public SaveNotepadValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User id is required.");

        RuleFor(x => x.Text)
            .NotNull()
            .WithMessage("Text must not be null.")
            .MaximumLength(Notepad.MaxTextLength)
            .WithMessage($"The notepad must not exceed {Notepad.MaxTextLength} characters.");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("The notepad's version must not be negative.");
    }
}
