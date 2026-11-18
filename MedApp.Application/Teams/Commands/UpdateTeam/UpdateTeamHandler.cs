using MedApp.Application.Common.Exceptions;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Teams.Commands.UpdateTeam;

public sealed class UpdateTeamHandler(ITeamRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateTeamCommand, TeamResponse>
{
    public async Task<TeamResponse> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await repository.GetOrThrowAsync(request.TeamId, cancellationToken);
        TeamRules.EnsureVersion(team, request.ExpectedVersion);

        var name = request.Name.Trim();
        var originalVersion = team.Version;
        var targetsChanged = TeamValidation.ApplyTargets(
            team, request.TargetDaysUrgent, request.TargetDaysHigh, request.TargetDaysMedium, request.TargetDaysLow);
        if (name == team.Name && !targetsChanged)
            return await TeamMapper.ToResponseAsync(team, repository, cancellationToken);

        if (name != team.Name && await repository.NameExistsAsync(name, team.Id, cancellationToken))
            throw new ConflictException(TeamRules.NameTakenMessage(name));

        team.Name = name;
        team.Version++;
        team.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateAsync(team, originalVersion, cancellationToken);

        return await TeamMapper.ToResponseAsync(team, repository, cancellationToken);
    }
}
