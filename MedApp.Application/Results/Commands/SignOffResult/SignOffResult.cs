using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Results;
using MediatR;

namespace MedApp.Application.Results.Commands.SignOffResult;

/// <summary>
/// Signs a result off with what was done (<paramref name="Action"/>, chosen by the clinician, by name) and their detail
/// (required for Other).
/// </summary>
public sealed record SignOffResultCommand(Guid ResultId, string Action, string? Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ResultResponse>;

public sealed class SignOffResultValidator : AbstractValidator<SignOffResultCommand>
{
    public SignOffResultValidator()
    {
        RuleFor(x => x.ResultId).NotEmpty().WithMessage("Result id is required.");
        RuleFor(x => x.Action).ResultAction();
        RuleFor(x => x.Detail).ActionDetail();
        RuleFor(x => x.Detail)
            .Must(d => !string.IsNullOrWhiteSpace(d))
            .When(x => PathwayEnums.TryParse<ResultAction>(x.Action, out var action) && action == ResultAction.Other)
            .WithMessage(ResultRules.OtherDetailMessage);
        RuleFor(x => x.ExpectedVersion).ResultVersion();
    }
}

/// <summary>
/// Signs a result off (decision 93; any holder of SignOffResults, checked by the endpoint): 409 unless it awaits
/// sign-off, if the record is anonymised, or if it's changed since the caller's version (allowed while restricted). The
/// action is the clinician's record: MedApp never checks it fits the result. Signed off is final. If it isn't the
/// responsible clinician, they're told (<c>ResultSignedOffByOther</c>), after the save.
/// </summary>
/// <remarks>
/// The team walls (the light demo's <c>findResult</c>): a result behind the viewer's wall is a 404, recorded; any
/// team that sees it (its own, or one its pathway was passed to, such as the lab) may act on it, by its permissions.
/// </remarks>
public sealed class SignOffResultHandler(
    IResultRepository repository,
    ITeamBoundary boundary,
    IIdentityReadService identity,
    IPermissionHolders permissionHolders,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<SignOffResultCommand, ResultResponse>
{
    public async Task<ResultResponse> Handle(SignOffResultCommand request, CancellationToken cancellationToken)
    {
        var result = await repository.GetForUpdateOrThrowAsync(request.ResultId, cancellationToken);
        // Behind the viewer's team wall: a 404, as for one that isn't there (recorded).
        await boundary.EnsureResultVisibleAsync(result, request.Actor.Teams, cancellationToken);
        if (ResultWorkflow.WhyCantProgress(result) is { } why)
            throw new ConflictException(why);
        ResultWorkflow.EnsureVersion(result, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ResultChange(result, result.Pathway, request.Actor, now);
        change.SignOff(PathwayEnums.Parse<ResultAction>(request.Action), ResultRules.Optional(request.Detail));
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        if (ResultNotifications.SignedOffByOther(result, request.Actor, now) is { } notification)
            await notifications.SendAsync(notification, cancellationToken);

        return await repository.ToResponseAsync(result, identity, permissionHolders, request.Actor, now, cancellationToken);
    }
}
