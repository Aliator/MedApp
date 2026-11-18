using FluentValidation;
using MedApp.Application.Specialties.Common;

namespace MedApp.Application.Authentication.Users.Commands.SetUserSpecialties;

public sealed class SetUserSpecialtiesValidator : AbstractValidator<SetUserSpecialtiesCommand>
{
    public SetUserSpecialtiesValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");

        RuleFor(x => x.SpecialtyIds)
            .NotNull()
            .WithMessage("Specialty ids must be given (an empty list removes them all).")
            .Must(ids => ids is null || ids.Count <= SpecialtyLimits.MaxPerUser)
            .WithMessage($"At most {SpecialtyLimits.MaxPerUser} specialties can be given.");

        RuleForEach(x => x.SpecialtyIds)
            .NotEmpty()
            .WithMessage("Specialty id must not be empty.");
    }
}
