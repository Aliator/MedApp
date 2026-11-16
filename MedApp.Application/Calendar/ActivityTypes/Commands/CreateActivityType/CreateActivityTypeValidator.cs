using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;

public sealed class CreateActivityTypeValidator : AbstractValidator<CreateActivityTypeCommand>
{
    public CreateActivityTypeValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(CalendarLimits.MaxActivityTypeNameLength)
            .WithMessage($"Name must not exceed {CalendarLimits.MaxActivityTypeNameLength} characters.");

        RuleFor(x => x.Colour)
            .Must(CalendarColours.IsColour)
            .WithMessage($"Colour must be one of: {CalendarColours.Names}.");
    }
}
