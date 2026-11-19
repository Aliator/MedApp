using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consent;
using MediatR;

namespace MedApp.Application.Consent.Commands.AttachConsentScan;

/// <summary>Gives a consent record that has none its scanned, signed form: a file already attached to the pathway.</summary>
public sealed record AttachConsentScanCommand(Guid ConsentRecordId, Guid AttachmentId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ConsentRecordResponse>;

public sealed class AttachConsentScanValidator : AbstractValidator<AttachConsentScanCommand>
{
    public AttachConsentScanValidator()
    {
        RuleFor(x => x.ConsentRecordId).NotEmpty().WithMessage("Consent record id is required.");
        RuleFor(x => x.AttachmentId).NotEmpty().WithMessage("Choose the scan.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Attaches the scan: 409 if the record has one, isn't Valid, can't change (restricted or anonymised), or has changed
/// since the caller's version; 400 unless the file is live, on the pathway (or one of its stages) and no other record's
/// scan. No audit entry of its own: the file's <c>AttachmentAdded</c> is its history.
/// <para>Only the pathway's own team changes its consent: another team's is a 404 (behind the viewer's wall,
/// recorded) or a 409 (seen, as an admin or the lab do: <see cref="ConsentRecords.GetForChangeOrThrowAsync"/>).</para>
/// </summary>
public sealed class AttachConsentScanHandler(
    IConsentRepository repository,
    ITeamBoundary boundary,
    IIdentityReadService identity,
    TimeProvider timeProvider)
    : IRequestHandler<AttachConsentScanCommand, ConsentRecordResponse>
{
    public async Task<ConsentRecordResponse> Handle(AttachConsentScanCommand request, CancellationToken cancellationToken)
    {
        var record = await repository.GetForChangeOrThrowAsync(
            request.ConsentRecordId, boundary, request.Actor.Teams, cancellationToken);
        if (ConsentWorkflow.WhyCantAttachScan(record) is { } why)
            throw new ConflictException(why);
        ConsentWorkflow.EnsureVersion(record, request.ExpectedVersion);

        await ConsentRecords.CheckScanAsync(repository, request.AttachmentId, record.Pathway, nameof(request.AttachmentId), cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        new ConsentChange(record, record.Pathway, request.Actor, now).AttachScan(request.AttachmentId);
        await repository.SaveAsync([], cancellationToken);

        return await ConsentRecords.ToResponseAsync(record, identity, request.Actor, now, cancellationToken);
    }
}
