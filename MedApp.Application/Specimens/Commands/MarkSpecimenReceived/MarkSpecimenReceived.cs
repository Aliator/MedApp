using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Common;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Teams.Repositories;
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
/// Records a sample received by the lab: 404 behind the viewer's team wall; 409 unless it's been sent and not yet
/// received, if the record is anonymised, or if it's changed since the caller's version (allowed while restricted:
/// decision 75); 400 for a time in the future, more than 7 days back or before it was sent. With its test known, its
/// lab case opens, or it joins its lab stage's (MC15, MC16: <see cref="LabReceipt"/>), picking the stage up; saved
/// together, in one save.
/// </summary>
public sealed class MarkSpecimenReceivedHandler(
    ISpecimenRepository repository,
    IPatientPathwayRepository pathways,
    ILabCaseRepository labCases,
    LabReceipt receipt,
    ITeamBoundary boundary,
    ITeamRepository teams,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    TimeProvider timeProvider)
    : IRequestHandler<MarkSpecimenReceivedCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(MarkSpecimenReceivedCommand request, CancellationToken cancellationToken)
    {
        var specimen = await repository.GetVisibleForUpdateOrThrowAsync(
            request.SpecimenId, boundary, teams, request.Actor.Teams, cancellationToken);
        if (SpecimenWorkflow.WhyCantMoveTo(specimen, SpecimenStatus.Received) is { } why)
            throw new ConflictException(why);
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var at = SpecimenRules.StepTime(request.AtUtc, specimen.SentAtUtc, now, "received", nameof(request.AtUtc));

        // The pathway with its links and lab stages' cases (tracked: the same instance the specimen holds).
        var pathway = specimen.PathwayId is { } pathwayId ? await pathways.GetForUpdateAsync(pathwayId, cancellationToken) : null;
        var change = new SpecimenChange(specimen, pathway, request.Actor, now);
        change.Receive(at, SpecimenRules.Optional(request.LabReference));

        var labTeamId = (await teams.GetLabAsync(cancellationToken))?.Id;
        var lab = await receipt.OnReceivedAsync(specimen, pathway, labTeamId, request.Actor, now.AddTicks(change.AuditEntries.Count), cancellationToken);
        labCases.Track(null, change.AuditEntries);
        if (lab.PathwayChange is { HasChanges: true } pathwayChange)
            await pathways.SaveAsync(pathwayChange, cancellationToken);
        else
            await labCases.SaveAsync(cancellationToken);

        return await repository.ToResponseAsync(
            specimen, identity, qrCodes, request.Actor, now, cancellationToken,
            canOpenLabCase: LabSamples.CanOpenCaseFor(specimen, request.Actor, labTeamId));
    }
}
