using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MediatR;

namespace MedApp.Application.Specimens.Commands.MarkSpecimenSent;

/// <summary>Records a taken specimen sent to the lab (now if <paramref name="AtUtc"/> is null), with the courier's or batch's reference.</summary>
public sealed record MarkSpecimenSentCommand(Guid SpecimenId, DateTime? AtUtc, string? CourierReference, int ExpectedVersion, PathwayActor Actor)
    : IRequest<SpecimenResponse>;

public sealed class MarkSpecimenSentValidator : AbstractValidator<MarkSpecimenSentCommand>
{
    public MarkSpecimenSentValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Specimen id is required.");
        RuleFor(x => x.CourierReference).Reference();
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Records a specimen sent: 409 unless it's taken and not yet sent, if the record is anonymised, or if it's changed
/// since the caller's version (a restricted record's can still go to the lab: decision 75); 400 for a time in the
/// future, more than 7 days back or before it was taken.
/// </summary>
public sealed class MarkSpecimenSentHandler(
    ISpecimenRepository repository,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    ITeamBoundary boundary,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<MarkSpecimenSentCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(MarkSpecimenSentCommand request, CancellationToken cancellationToken)
    {
        var specimen = await repository.GetVisibleForUpdateOrThrowAsync(
            request.SpecimenId, boundary, teams, request.Actor.Teams, cancellationToken);
        if (SpecimenWorkflow.WhyCantMoveTo(specimen, SpecimenStatus.Sent) is { } why)
            throw new ConflictException(why);
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var at = SpecimenRules.StepTime(request.AtUtc, specimen.TakenAtUtc, now, "sent", nameof(request.AtUtc));

        var change = new SpecimenChange(specimen, specimen.Pathway, request.Actor, now);
        change.Send(at, SpecimenRules.Optional(request.CourierReference));
        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await repository.ToResponseAsync(specimen, identity, qrCodes, request.Actor, now, cancellationToken);
    }
}
