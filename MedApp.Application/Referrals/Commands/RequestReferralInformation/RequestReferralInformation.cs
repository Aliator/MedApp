using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RequestReferralInformation;

/// <summary>
/// Triage's "Ask for more information" (<paramref name="Asked"/> true) and "Information received" (false), decision 106
/// of docs/centre-phases-3-5-plan.md.
/// </summary>
public sealed record SetReferralInformationRequestedCommand(Guid ReferralId, bool Asked, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ReferralResponse>;

public sealed class SetReferralInformationRequestedValidator : AbstractValidator<SetReferralInformationRequestedCommand>
{
    public SetReferralInformationRequestedValidator()
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");
        RuleFor(x => x.ExpectedVersion)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Expected version must be the referral's version, 1 or more.");
    }
}

/// <summary>
/// Asks the referrer for more information, or records that it came (decision 106): the same checks as triage (403
/// without <c>TriageReferrals</c> or membership of its team; 409 once it's closed, on a restricted or anonymised
/// record, or if it changed since the caller's version), then 409 if it's already waiting (asking) or isn't (receiving).
/// Asking keeps it open in the inbox's Information requested view, its clock still running (decision 106: the
/// centre's own wait counts); receiving puts it back awaiting triage. The letter asking is written from the referral's
/// page (a request for more information). Nobody is told: the inbox is the signal.
/// </summary>
public sealed class SetReferralInformationRequestedHandler(IReferralRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SetReferralInformationRequestedCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(SetReferralInformationRequestedCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForTriageOrThrowAsync(
            request.ReferralId, request.Actor, request.ExpectedVersion, cancellationToken);

        var waiting = referral.Status == ReferralStatus.InformationRequested;
        if (request.Asked && waiting)
            throw new ConflictException(ReferralWorkflow.AlreadyAskedMessage);
        if (!request.Asked && !waiting)
            throw new ConflictException(ReferralWorkflow.NotAskedMessage);

        var change = new ReferralChange(referral, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        if (request.Asked)
            change.AskForInformation();
        else
            change.InformationReceived();

        try
        {
            await repository.SaveAsync(change.Events, cancellationToken);
        }
        catch (ConflictException)
        {
            throw await repository.TriageConflictAsync(referral.Id, cancellationToken);
        }

        return await ReferralResponses.ForAsync(referral, repository, request.Actor, timeProvider, cancellationToken);
    }
}
