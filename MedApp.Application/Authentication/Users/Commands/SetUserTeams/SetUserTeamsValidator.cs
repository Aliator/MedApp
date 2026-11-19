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

        RuleFor(x => x.Teams)
            .NotNull()
            .WithMessage("Teams must be given (an empty list removes them all).")
            .Must(teams => teams is null || teams.Count <= TeamLimits.MaxPerUser)
            .WithMessage($"At most {TeamLimits.MaxPerUser} teams can be given.")
            .Must(teams => teams is null || teams.All(t => t.TeamId != Guid.Empty))
            .WithMessage(TeamRules.ChooseTeamMessage)
            .Must(teams => teams is null || teams.All(t => TeamRules.ParseRole(t.Role) is not null))
            .WithMessage(TeamRules.ChooseRoleMessage);
    }
}
