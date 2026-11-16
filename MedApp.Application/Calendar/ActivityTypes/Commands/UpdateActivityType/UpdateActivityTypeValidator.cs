using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;

public sealed class UpdateActivityTypeValidator : AbstractValidator<UpdateActivityTypeCommand>
{
    public UpdateActivityTypeValidator()
    {
        RuleFor(x => x.ActivityTypeId).NotEmpty().WithMessage("Activity type id is required.");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .When(x => x.Name is not null)
            .WithMessage("Name must not be blank.")
            .MaximumLength(CalendarLimits.MaxActivityTypeNameLength)
            .WithMessage($"Name must not exceed {CalendarLimits.MaxActivityTypeNameLength} characters.");

        RuleFor(x => x.Colour)
            .Must(CalendarColours.IsColour)
            .When(x => x.Colour is not null)
            .WithMessage($"Colour must be one of: {CalendarColours.Names}.");
    }
}
