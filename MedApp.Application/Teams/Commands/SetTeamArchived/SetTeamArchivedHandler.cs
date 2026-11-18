using MedApp.Application.Common.Exceptions;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Teams.Commands.SetTeamArchived;

public sealed class SetTeamArchivedHandler(ITeamRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SetTeamArchivedCommand, TeamResponse>
{
    public async Task<TeamResponse> Handle(SetTeamArchivedCommand request, CancellationToken cancellationToken)
    {
        var team = await repository.GetOrThrowAsync(request.TeamId, cancellationToken);
        TeamRules.EnsureVersion(team, request.ExpectedVersion);

        if (request.IsArchived && !team.IsArchived)
        {
            var usage = (await repository.GetUsageAsync([team.Id], cancellationToken)).GetValueOrDefault(team.Id);
            if (usage is { AwaitingTriageCount: > 0 })
                throw new ConflictException(TeamRules.AwaitingTriageMessage(team, usage.AwaitingTriageCount));
        }

        if (team.IsArchived != request.IsArchived)
        {
            var originalVersion = team.Version;
            team.IsArchived = request.IsArchived;
            team.Version++;
            team.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateAsync(team, originalVersion, cancellationToken);
        }

        return await TeamMapper.ToResponseAsync(team, repository, cancellationToken);
    }
}
