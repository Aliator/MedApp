using FluentValidation;
using MedApp.Application.Teams.Common;

namespace MedApp.Application.Teams.Commands.CreateTeam;

public sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator()
    {
        RuleFor(x => x.Name).TeamName();
        RuleFor(x => x.TargetDaysUrgent).TargetDays();
        RuleFor(x => x.TargetDaysHigh).TargetDays();
        RuleFor(x => x.TargetDaysMedium).TargetDays();
        RuleFor(x => x.TargetDaysLow).TargetDays();
    }
}
