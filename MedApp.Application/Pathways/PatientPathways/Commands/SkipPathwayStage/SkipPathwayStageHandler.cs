using MedApp.Application.Common.Teams;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;

public sealed class SkipPathwayStageHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IAppointmentRepository appointmentRepository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    IPathwayHandovers handovers,
    TimeProvider timeProvider)
    : IRequestHandler<SkipPathwayStageCommand, PatientPathwayResponse>
{
    public const string DetailRequiredMessage = "Say why: skipping a stage whose visit is required needs a detail.";

    public async Task<PatientPathwayResponse> Handle(SkipPathwayStageCommand request, CancellationToken cancellationToken)
    {
        // The team that works the stage (the relay: the lab's stages are the lab's) works it.
        var (pathway, stage) = await repository.GetForStageWorkOrThrowAsync(
            request.PathwayId, request.StageId, boundary, request.Actor.Teams, cancellationToken);

        if (PathwayWorkflow.WhySkipNotAllowed(pathway, stage, request.CanFinishWithoutVisit) is { } reason)
            throw new ConflictException(reason);

        // A Required visit skipped, like one finished without, says why.
        if (PathwayWorkflow.SkipNeedsOverride(stage) && string.IsNullOrWhiteSpace(request.Detail))
            throw new ValidationException([new ValidationFailure(nameof(request.Detail), DetailRequiredMessage)]);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);

        // The skipped stage's appointments (its Requested ones are cancelled), and those of the stages still to do (any
        // may become current, on any route, and need its visit asked for).
        var appointments = await appointmentRepository.GetForStagesForUpdateAsync(
            StageFinishing.StagesTouchedByFinish(pathway, stage).ToList(), cancellationToken);

        StageSkipReasons.TryParseChosen(request.Reason, out var skipReason);
        StageFinishing.Skip(change, stage, skipReason, request.Detail, appointments);

        await repository.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);
        await handovers.NotifyAsync(change, cancellationToken);

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions, viewerScope: request.Actor.Teams);
    }
}
