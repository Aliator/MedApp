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
    TimeProvider timeProvider)
    : IRequestHandler<SkipPathwayStageCommand, PatientPathwayResponse>
{
    public const string DetailRequiredMessage = "Say why: skipping a stage whose visit is required needs a detail.";

    public async Task<PatientPathwayResponse> Handle(SkipPathwayStageCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, boundary, request.Actor.Teams, cancellationToken);
        var stage = pathway.GetStageOrThrow(request.StageId);

        if (PathwayWorkflow.WhySkipNotAllowed(pathway, stage, request.CanFinishWithoutVisit) is { } reason)
            throw new ConflictException(reason);

        // A Required visit skipped, like one finished without, says why.
        if (PathwayWorkflow.SkipNeedsOverride(stage) && string.IsNullOrWhiteSpace(request.Detail))
            throw new ValidationException([new ValidationFailure(nameof(request.Detail), DetailRequiredMessage)]);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);

        // The skipped stage's appointments (its Requested ones are cancelled), and the next stage's (it becomes current
        // and may need its visit asked for).
        var next = StageFinishing.NextStage(pathway, stage);
        var appointments = await appointmentRepository.GetForStagesForUpdateAsync(
            next is null ? [stage.Id] : [stage.Id, next.Id], cancellationToken);

        StageSkipReasons.TryParseChosen(request.Reason, out var skipReason);
        StageFinishing.Skip(change, stage, skipReason, request.Detail, appointments);

        await repository.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions);
    }
}
