using FluentValidation;
using MedApp.Application.Clinics.Common;

namespace MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;

public sealed class CreateSessionsFromPatternValidator : AbstractValidator<CreateSessionsFromPatternCommand>
{
    public CreateSessionsFromPatternValidator() =>
        RuleFor(x => x.Weeks)
            .InclusiveBetween(1, ClinicLimits.MaxWeeksAhead)
            .WithMessage(ClinicPatternRules.WeeksMessage);
}
