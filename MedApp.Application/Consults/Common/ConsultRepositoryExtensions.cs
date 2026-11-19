using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Repositories;
using MedApp.Domain.Consults;

namespace MedApp.Application.Consults.Common;

/// <summary>Loading consults through the team walls (the light demo's <c>findConsult</c>).</summary>
public static class ConsultRepositoryExtensions
{
    /// <summary>
    /// A consult to change, tracked: a 404 if there's none, or if its pathway is behind the viewer's wall (a closed
    /// consult's asked team, say), recorded with the words of a missing one.
    /// </summary>
    public static async Task<Consult> GetForChangeOrThrowAsync(
        this IConsultRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var consult = await repository.GetForUpdateAsync(id, ct)
                      ?? throw new NotFoundException(ConsultRules.NotFoundMessage(id));
        if (await boundary.GetPathwayAccessAsync(consult.PathwayId, scope, ct) is null)
            throw await boundary.RefuseAsync(ConsultRules.TeamWallTarget, id, ConsultRules.NotFoundMessage(id), ct);
        return consult;
    }
}
