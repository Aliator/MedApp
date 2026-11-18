using MedApp.Application.Common.Exceptions;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MediatR;

namespace MedApp.Application.Teams.Commands.DeleteTeam;

public sealed class DeleteTeamHandler(ITeamRepository repository) : IRequestHandler<DeleteTeamCommand>
{
    public async Task Handle(DeleteTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await repository.GetOrThrowAsync(request.TeamId, cancellationToken);

        var usage = (await repository.GetUsageAsync([team.Id], cancellationToken))
            .GetValueOrDefault(team.Id, TeamUsage.None);
        if (usage.IsInUse)
            throw new ConflictException(TeamRules.InUseMessage(team, usage));

        await repository.DeleteAsync(team, cancellationToken);
    }
}
