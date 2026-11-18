using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consent;
using MediatR;

namespace MedApp.Application.Consent.Commands.WithdrawConsent;

/// <summary>Records that consent was withdrawn, on a date, with what was said.</summary>
public sealed record WithdrawConsentCommand(
    Guid ConsentRecordId,
    DateOnly WithdrawnOn,
    string Detail,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<ConsentRecordResponse>;

public sealed class WithdrawConsentValidator : AbstractValidator<WithdrawConsentCommand>
{
    public WithdrawConsentValidator()
    {
        RuleFor(x => x.ConsentRecordId).NotEmpty().WithMessage("Consent record id is required.");
        RuleFor(x => x.WithdrawnOn).NotEmpty().WithMessage("Give the date it was withdrawn.");
        RuleFor(x => x.Detail).RequiredDetail();
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Withdraws consent (decision 147): 409 unless it's Valid, if the record is anonymised, or if it's changed since the
/// caller's version; allowed while the record is restricted and in any pathway status (it protects the patient). A
/// withdrawal after the stage finished is flagged on the pathway; nothing else changes by itself, and the scan stays
/// (it's what was signed).
/// </summary>
public sealed class WithdrawConsentHandler(
    IConsentRepository repository,
    IIdentityReadService identity,
    TimeProvider timeProvider)
    : IRequestHandler<WithdrawConsentCommand, ConsentRecordResponse>
{
    public async Task<ConsentRecordResponse> Handle(WithdrawConsentCommand request, CancellationToken cancellationToken)
    {
        var record = await repository.GetForUpdateOrThrowAsync(request.ConsentRecordId, cancellationToken);
        if (ConsentWorkflow.WhyCantWithdraw(record) is { } why)
            throw new ConflictException(why);
        ConsentWorkflow.EnsureVersion(record, request.ExpectedVersion);

        if (ConsentWorkflow.WhyWithdrawnOnInvalid(record, request.WithdrawnOn, UkTime.Today(timeProvider)) is { } badDate)
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.WithdrawnOn), badDate)]);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ConsentChange(record, record.Pathway, request.Actor, now);
        change.Withdraw(request.WithdrawnOn, request.Detail.Trim());
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await ConsentRecords.ToResponseAsync(record, identity, request.Actor, now, cancellationToken);
    }
}
