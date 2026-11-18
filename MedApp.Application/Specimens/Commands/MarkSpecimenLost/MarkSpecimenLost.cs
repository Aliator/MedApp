using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Specimens.Commands.MarkSpecimenLost;

/// <summary>Marks an open specimen lost, with what happened.</summary>
public sealed record MarkSpecimenLostCommand(Guid SpecimenId, string Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<SpecimenResponse>;

public sealed class MarkSpecimenLostValidator : AbstractValidator<MarkSpecimenLostCommand>
{
    public MarkSpecimenLostValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Specimen id is required.");
        RuleFor(x => x.Detail).RequiredDetail();
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Marks a specimen lost, closing it: 403 unless the caller holds RecordSpecimens or ProcessSpecimens; 409 unless
/// it's open, if the record is anonymised, or if it's changed since the caller's version (allowed while restricted).
/// The requesting clinician and whoever took it are told, after the save.
/// </summary>
public sealed class MarkSpecimenLostHandler(
    ISpecimenRepository repository,
    INotificationSender notifications,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    TimeProvider timeProvider)
    : IRequestHandler<MarkSpecimenLostCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(MarkSpecimenLostCommand request, CancellationToken cancellationToken)
    {
        if (!SpecimenWorkflow.MayMarkLost(request.Actor))
            throw new ForbiddenException(SpecimenWorkflow.LostNotAllowedMessage);

        var specimen = await repository.GetForUpdateOrThrowAsync(request.SpecimenId, cancellationToken);
        if (SpecimenWorkflow.WhyCantMoveTo(specimen, SpecimenStatus.Lost) is { } why)
            throw new ConflictException(why);
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new SpecimenChange(specimen, specimen.Pathway, request.Actor, now);
        change.MarkLost(request.Detail.Trim());
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        foreach (var notification in SpecimenNotifications.Lost(specimen, request.Actor, now))
            await notifications.SendAsync(notification, cancellationToken);

        return await repository.ToResponseAsync(specimen, identity, qrCodes, request.Actor, now, cancellationToken);
    }
}
