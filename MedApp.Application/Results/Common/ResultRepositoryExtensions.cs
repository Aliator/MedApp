using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
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

    public static NotFoundException NotFound(Guid id) => new($"Result '{id}' was not found.");
}
