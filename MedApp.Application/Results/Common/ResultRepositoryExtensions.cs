using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Results;

namespace MedApp.Application.Results.Common;

public static class ResultRepositoryExtensions
{
    public static async Task<Result> GetOrThrowAsync(this IResultRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct) ?? throw NotFound(id);

    /// <summary>The result (and its specimen), tracked for changes, or a 404.</summary>
    public static async Task<Result> GetForUpdateOrThrowAsync(this IResultRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct) ?? throw NotFound(id);

    /// <summary>
    /// A result's page: the usernames it names, its report file, and whether its responsible clinician can still sign
    /// off, with what <paramref name="viewer"/> may do.
    /// </summary>
    public static async Task<ResultResponse> ToResponseAsync(
        this IResultRepository repository,
        Result result,
        IIdentityReadService identity,
        IPermissionHolders permissionHolders,
        PathwayActor viewer,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var usernames = await identity.GetUsernamesByIdAsync(ResultMapper.PeopleIn(result), ct);
        var report = await repository.GetReportAsync(result.ReportAttachmentId, ct);
        var holders = await permissionHolders.WhoHoldsAsync([result.ResponsibleUserId], Permission.SignOffResults, ct);
        return ResultMapper.ToResponse(result, usernames, report, holders.Contains(result.ResponsibleUserId), viewer, nowUtc);
    }

    /// <summary>
    /// Checks that <paramref name="userId"/> can be a result's responsible clinician (holds SignOffResults): a 400 on
    /// <paramref name="field"/> otherwise.
    /// </summary>
    public static async Task EnsureCanBeResponsibleAsync(
        this IPermissionHolders permissionHolders, Guid userId, string field, CancellationToken ct)
    {
        var holders = await permissionHolders.WhoHoldsAsync([userId], Permission.SignOffResults, ct);
        if (!holders.Contains(userId))
            throw new FluentValidation.ValidationException(
                [new FluentValidation.Results.ValidationFailure(field, ResultWorkflow.ResponsibleNotClinicianMessage)]);
    }

    /// <summary>What a refusal across a team wall records as <c>targetKind</c> for a result.</summary>
    public const string TeamWallTarget = "Result";

    /// <summary>
    /// The team walls at one result (the light demo's <c>findResult</c>): seen when it's the viewer's team's, or on a
    /// pathway they see in full (owner, stage owner, consult, MDT), or they see every team; otherwise a 404 with the words
    /// of a missing one, recorded. Returns why they see it (the activity log's <c>access</c>).
    /// </summary>
    public static Task<TeamAccessKind> EnsureResultVisibleAsync(
        this ITeamBoundary boundary, Result result, TeamScope scope, CancellationToken ct) =>
        boundary.EnsureRecordVisibleAsync(
            TeamWallTarget, result.Id, result.TeamId, result.PathwayId, scope, NotFound(result.Id).Message, ct);

    public static NotFoundException NotFound(Guid id) => new($"Result '{id}' was not found.");
}
