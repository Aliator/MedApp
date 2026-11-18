using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consent;
using MediatR;

namespace MedApp.Application.Consent.Commands.MarkConsentEnteredInError;

/// <summary>Marks a consent record entered in error (the wrong patient, stage or form), with what was wrong.</summary>
public sealed record MarkConsentEnteredInErrorCommand(Guid ConsentRecordId, string Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ConsentRecordResponse>;

public sealed class MarkConsentEnteredInErrorValidator : AbstractValidator<MarkConsentEnteredInErrorCommand>
{
    public MarkConsentEnteredInErrorValidator()
    {
        RuleFor(x => x.ConsentRecordId).NotEmpty().WithMessage("Consent record id is required.");
        RuleFor(x => x.Detail).RequiredDetail();
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Marks a consent record entered in error: 403 unless the caller recorded it, still holds RecordConsent and recorded
/// it less than 24 hours ago, or holds CorrectRecords; 409 if it already is, if the record is anonymised, or if it's
/// changed since the caller's version (allowed while restricted, as a correction). Its scan is soft-deleted in the same
/// save, with an <c>AttachmentDeleted</c> entry (decision 71).
/// </summary>
public sealed class MarkConsentEnteredInErrorHandler(
    IConsentRepository repository,
    IIdentityReadService identity,
    TimeProvider timeProvider)
    : IRequestHandler<MarkConsentEnteredInErrorCommand, ConsentRecordResponse>
{
    public async Task<ConsentRecordResponse> Handle(MarkConsentEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        var record = await repository.GetForUpdateOrThrowAsync(request.ConsentRecordId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!ConsentWorkflow.MayMarkEnteredInError(record, request.Actor, now))
            throw new ForbiddenException(ConsentWorkflow.EnteredInErrorNotAllowedMessage);
        if (ConsentWorkflow.WhyCantMarkEnteredInError(record) is { } why)
            throw new ConflictException(why);
        ConsentWorkflow.EnsureVersion(record, request.ExpectedVersion);

        var change = new ConsentChange(record, record.Pathway, request.Actor, now);
        change.MarkEnteredInError(request.Detail.Trim());
        var scanDeleted = await ConsentRecords.DeleteScanAsync(repository, record, request.Actor, now, cancellationToken);
        await repository.SaveAsync(scanDeleted is null ? change.AuditEntries : [.. change.AuditEntries, scanDeleted], cancellationToken);

        return await ConsentRecords.ToResponseAsync(record, identity, request.Actor, now, cancellationToken);
    }
}
