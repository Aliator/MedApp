using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MediatR;

namespace MedApp.Application.Specimens.Commands.RejectSpecimen;

/// <summary>Records that the lab rejected a specimen, with a reason (by name) and a detail (required for Other).</summary>
public sealed record RejectSpecimenCommand(Guid SpecimenId, string Reason, string? Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<SpecimenResponse>;

public sealed class RejectSpecimenValidator : AbstractValidator<RejectSpecimenCommand>
{
    public RejectSpecimenValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Specimen id is required.");
        RuleFor(x => x.Reason)
            .Must(r => PathwayEnums.TryParse<SpecimenRejectionReason>(r, out _))
            .WithMessage($"The reason must be one of: {string.Join(", ", Enum.GetNames<SpecimenRejectionReason>())}.");
        RuleFor(x => x.Detail)
            .Must(d => !string.IsNullOrWhiteSpace(d))
            .When(x => PathwayEnums.TryParse<SpecimenRejectionReason>(x.Reason, out var r) && r == SpecimenRejectionReason.Other)
            .WithMessage("Say why the lab rejected it.");
        RuleFor(x => x.Detail)
            .MaximumLength(Specimen.MaxClosedDetailLength)
            .WithMessage($"The detail must be at most {Specimen.MaxClosedDetailLength} characters.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Records that the lab rejected a specimen, closing it: 409 unless it's been sent or received and isn't closed, if
/// the record is anonymised, or if it's changed since the caller's version (allowed while restricted: decision 75).
/// The requesting clinician and whoever took it are told (<see cref="SpecimenNotifications"/>), after the save, so
/// someone takes another.
/// </summary>
public sealed class RejectSpecimenHandler(
    ISpecimenRepository repository,
    INotificationSender notifications,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    ITeamBoundary boundary,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<RejectSpecimenCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(RejectSpecimenCommand request, CancellationToken cancellationToken)
    {
        var specimen = await repository.GetVisibleForUpdateOrThrowAsync(
            request.SpecimenId, boundary, teams, request.Actor.Teams, cancellationToken);
        if (SpecimenWorkflow.WhyCantMoveTo(specimen, SpecimenStatus.RejectedByLab) is { } why)
            throw new ConflictException(why);
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        PathwayEnums.TryParse<SpecimenRejectionReason>(request.Reason, out var reason);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new SpecimenChange(specimen, specimen.Pathway, request.Actor, now);
        change.Reject(reason, SpecimenRules.Optional(request.Detail));
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        foreach (var notification in SpecimenNotifications.Rejected(specimen, request.Actor, now))
            await notifications.SendAsync(notification, cancellationToken);

        return await repository.ToResponseAsync(specimen, identity, qrCodes, request.Actor, now, cancellationToken);
    }
}
