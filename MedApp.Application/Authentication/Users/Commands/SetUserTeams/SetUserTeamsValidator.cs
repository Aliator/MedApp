using FluentValidation;
using MedApp.Application.Teams.Common;

namespace MedApp.Application.Authentication.Users.Commands.SetUserTeams;

public sealed class SetUserTeamsValidator : AbstractValidator<SetUserTeamsCommand>
{
    public SetUserTeamsValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");

        RuleFor(x => x.TeamIds)
            .NotNull()
            .WithMessage("Team ids must be given (an empty list removes them all).")
            .Must(ids => ids is null || ids.Count <= TeamLimits.MaxPerUser)
            .WithMessage($"At most {TeamLimits.MaxPerUser} teams can be given.");

        RuleForEach(x => x.TeamIds)
            .NotEmpty()
            .WithMessage("Team id must not be empty.");
    }
}
