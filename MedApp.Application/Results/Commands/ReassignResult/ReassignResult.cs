using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Results.Responses;
using MediatR;

namespace MedApp.Application.Results.Commands.ReassignResult;

/// <summary>
/// Gives a result to another responsible clinician, or takes it over (<paramref name="ResponsibleUserId"/> the actor's
/// own ID).
/// </summary>
public sealed record ReassignResultCommand(Guid ResultId, Guid ResponsibleUserId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ResultResponse>;

public sealed class ReassignResultValidator : AbstractValidator<ReassignResultCommand>
{
    public ReassignResultValidator()
    {
        RuleFor(x => x.ResultId).NotEmpty().WithMessage("Result id is required.");
        RuleFor(x => x.ResponsibleUserId).NotEmpty().WithMessage("Choose the clinician to give it to.");
        RuleFor(x => x.ExpectedVersion).ResultVersion();
    }
}

/// <summary>
/// Reassigns a result (decision 92; any holder of SignOffResults, checked by the endpoint): the new responsible
/// clinician must hold SignOffResults (400) and not be the current one (400); 409 unless it awaits sign-off, if the
/// record is anonymised, or if it's changed since (allowed while restricted). The new one is told
/// (<c>ResultReassigned</c>) unless they took it over themselves.
/// </summary>
public sealed class ReassignResultHandler(
    IResultRepository repository,
    IIdentityReadService identity,
    IPermissionHolders permissionHolders,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<ReassignResultCommand, ResultResponse>
{
    public async Task<ResultResponse> Handle(ReassignResultCommand request, CancellationToken cancellationToken)
    {
        var result = await repository.GetForUpdateOrThrowAsync(request.ResultId, cancellationToken);
        if (ResultWorkflow.WhyCantProgress(result) is { } why)
            throw new ConflictException(why);
        ResultWorkflow.EnsureVersion(result, request.ExpectedVersion);

        if (result.ResponsibleUserId == request.ResponsibleUserId)
            throw new ValidationException(
                [new FluentValidation.Results.ValidationFailure(nameof(request.ResponsibleUserId), ResultWorkflow.SameResponsibleMessage)]);
        await permissionHolders.EnsureCanBeResponsibleAsync(
            request.ResponsibleUserId, nameof(request.ResponsibleUserId), cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync([result.ResponsibleUserId, request.ResponsibleUserId], cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ResultChange(result, result.Pathway, request.Actor, now);
        change.Reassign(
            request.ResponsibleUserId,
            usernames.GetValueOrDefault(result.ResponsibleUserId, PathwayMapper.DeletedUser),
            usernames.GetValueOrDefault(request.ResponsibleUserId, PathwayMapper.DeletedUser));
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        if (ResultNotifications.Reassigned(result, request.Actor, now) is { } notification)
            await notifications.SendAsync(notification, cancellationToken);

        return await repository.ToResponseAsync(result, identity, permissionHolders, request.Actor, now, cancellationToken);
    }
}
