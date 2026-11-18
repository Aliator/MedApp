using FluentValidation;
using MedApp.Application.Teams.Common;

namespace MedApp.Application.Teams.Commands.UpdateTeam;

public sealed class UpdateTeamValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("Team id is required.");

        RuleFor(x => x.Name).TeamName();
        RuleFor(x => x.TargetDaysUrgent).TargetDays();
        RuleFor(x => x.TargetDaysHigh).TargetDays();
        RuleFor(x => x.TargetDaysMedium).TargetDays();
        RuleFor(x => x.TargetDaysLow).TargetDays();

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version is required: send the version you last saw.");
    }
}
