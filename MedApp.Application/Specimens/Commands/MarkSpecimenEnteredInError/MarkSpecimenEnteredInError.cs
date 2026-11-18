using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Specimens.Commands.MarkSpecimenEnteredInError;

/// <summary>Marks an open specimen entered in error (the wrong patient, recorded twice), with what was wrong.</summary>
public sealed record MarkSpecimenEnteredInErrorCommand(Guid SpecimenId, string Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<SpecimenResponse>;

public sealed class MarkSpecimenEnteredInErrorValidator : AbstractValidator<MarkSpecimenEnteredInErrorCommand>
{
    public MarkSpecimenEnteredInErrorValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Specimen id is required.");
        RuleFor(x => x.Detail).RequiredDetail();
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Marks a specimen entered in error, closing it (decision 84): 403 unless the caller recorded it, still holds
/// RecordSpecimens and recorded it less than 24 hours ago, or holds CorrectRecords; 409 unless it's open, if the
/// record is anonymised, or if it's changed since the caller's version (allowed while restricted, as a correction).
/// </summary>
public sealed class MarkSpecimenEnteredInErrorHandler(
    ISpecimenRepository repository,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    TimeProvider timeProvider)
    : IRequestHandler<MarkSpecimenEnteredInErrorCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(MarkSpecimenEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        var specimen = await repository.GetForUpdateOrThrowAsync(request.SpecimenId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!SpecimenWorkflow.MayMarkEnteredInError(specimen, request.Actor, now))
            throw new ForbiddenException(SpecimenWorkflow.EnteredInErrorNotAllowedMessage);
        if (SpecimenWorkflow.WhyCantMoveTo(specimen, SpecimenStatus.EnteredInError) is { } why)
            throw new ConflictException(why);
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        var change = new SpecimenChange(specimen, specimen.Pathway, request.Actor, now);
        change.MarkEnteredInError(request.Detail.Trim());
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await repository.ToResponseAsync(specimen, identity, qrCodes, request.Actor, now, cancellationToken);
    }
}
