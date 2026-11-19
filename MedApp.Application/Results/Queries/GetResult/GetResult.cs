using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Results.Queries.GetResult;

/// <summary>One result, with what <paramref name="Viewer"/> may do with it.</summary>
public sealed record GetResultQuery(Guid ResultId, PathwayActor Viewer) : IRequest<ResultResponse>;

/// <summary>
/// One result. Behind the viewer's team wall (not their team's, nor on a pathway they see in full) it's a 404 with the
/// words of a missing one, recorded (<see cref="ITeamBoundary"/>). Opening it is recorded against its patient as
/// <see cref="ActivityAction.ResultViewed"/>, with the result's ID and why the viewer could (<c>access</c>) in the details
/// (once per person and result every few minutes); the log fails closed.
/// </summary>
public sealed class GetResultHandler(
    IResultRepository repository,
    ITeamBoundary boundary,
    IActivityLog activityLog,
    IIdentityReadService identity,
    IPermissionHolders permissionHolders,
    TimeProvider timeProvider)
    : IRequestHandler<GetResultQuery, ResultResponse>
{
    public async Task<ResultResponse> Handle(GetResultQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.GetOrThrowAsync(request.ResultId, cancellationToken);
        var access = await boundary.EnsureResultVisibleAsync(result, request.Viewer.Teams, cancellationToken);

        await activityLog.RecordViewAsync(
            ActivityAction.ResultViewed, result.PatientId, new { resultId = result.Id, access = access.ToString() }, cancellationToken);

        return await repository.ToResponseAsync(
            result, identity, permissionHolders, request.Viewer, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
