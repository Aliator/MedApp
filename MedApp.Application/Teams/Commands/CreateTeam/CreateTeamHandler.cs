using MedApp.Application.Common.Exceptions;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Teams;
using MediatR;

namespace MedApp.Application.Teams.Commands.CreateTeam;

public sealed class CreateTeamHandler(ITeamRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateTeamCommand, TeamResponse>
{
    public async Task<TeamResponse> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException(TeamRules.NameTakenMessage(name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            Version = 1,
            CreatedAt = now,
            LastUpdated = now
        };
        TeamValidation.ApplyTargets(
            team, request.TargetDaysUrgent, request.TargetDaysHigh, request.TargetDaysMedium, request.TargetDaysLow);

        await repository.AddAsync(team, cancellationToken);

        return TeamMapper.ToResponse(team, TeamUsage.None);
    }
}
