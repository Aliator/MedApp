using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Specimens.Commands.MarkSpecimenReceived;

/// <summary>Records a sent specimen received by the lab (now if <paramref name="AtUtc"/> is null), with the lab's own reference.</summary>
public sealed record MarkSpecimenReceivedCommand(Guid SpecimenId, DateTime? AtUtc, string? LabReference, int ExpectedVersion, PathwayActor Actor)
    : IRequest<SpecimenResponse>;

public sealed class MarkSpecimenReceivedValidator : AbstractValidator<MarkSpecimenReceivedCommand>
{
    public MarkSpecimenReceivedValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Specimen id is required.");
        RuleFor(x => x.LabReference).Reference();
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Records a specimen received by the lab: 409 unless it's been sent and not yet received, if the record is
/// anonymised, or if it's changed since the caller's version (allowed while restricted: decision 75); 400 for a time
/// in the future, more than 7 days back or before it was sent.
/// </summary>
public sealed class MarkSpecimenReceivedHandler(
    ISpecimenRepository repository,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    TimeProvider timeProvider)
    : IRequestHandler<MarkSpecimenReceivedCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(MarkSpecimenReceivedCommand request, CancellationToken cancellationToken)
    {
        var specimen = await repository.GetForUpdateOrThrowAsync(request.SpecimenId, cancellationToken);
        if (SpecimenWorkflow.WhyCantMoveTo(specimen, SpecimenStatus.Received) is { } why)
            throw new ConflictException(why);
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var at = SpecimenRules.StepTime(request.AtUtc, specimen.SentAtUtc, now, "received", nameof(request.AtUtc));

        var change = new SpecimenChange(specimen, specimen.Pathway, request.Actor, now);
        change.Receive(at, SpecimenRules.Optional(request.LabReference));
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await repository.ToResponseAsync(specimen, identity, qrCodes, request.Actor, now, cancellationToken);
    }
}
