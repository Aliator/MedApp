using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Results.Responses;
using MediatR;

namespace MedApp.Application.Results.Commands.MarkResultSeen;

/// <summary>Marks a result seen: the clinician has read it, and action is under way. Optional before sign-off.</summary>
public sealed record MarkResultSeenCommand(Guid ResultId, int ExpectedVersion, PathwayActor Actor) : IRequest<ResultResponse>;

public sealed class MarkResultSeenValidator : AbstractValidator<MarkResultSeenCommand>
{
    public MarkResultSeenValidator()
    {
        RuleFor(x => x.ResultId).NotEmpty().WithMessage("Result id is required.");
        RuleFor(x => x.ExpectedVersion).ResultVersion();
    }
}

/// <summary>
/// Marks a result seen (decision 93; holders of SignOffResults, checked by the endpoint): 409 unless it awaits sign-off
/// and isn't already seen, if the record is anonymised, or if it's changed since the caller's version (allowed while
/// restricted: a result must never be stuck).
/// </summary>
public sealed class MarkResultSeenHandler(
    IResultRepository repository,
    IIdentityReadService identity,
    IPermissionHolders permissionHolders,
    TimeProvider timeProvider)
    : IRequestHandler<MarkResultSeenCommand, ResultResponse>
{
    public async Task<ResultResponse> Handle(MarkResultSeenCommand request, CancellationToken cancellationToken)
    {
        var result = await repository.GetForUpdateOrThrowAsync(request.ResultId, cancellationToken);
        if (ResultWorkflow.WhyCantMarkSeen(result) is { } why)
            throw new ConflictException(why);
        ResultWorkflow.EnsureVersion(result, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ResultChange(result, result.Pathway, request.Actor, now);
        change.MarkSeen();
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await repository.ToResponseAsync(result, identity, permissionHolders, request.Actor, now, cancellationToken);
    }
}
