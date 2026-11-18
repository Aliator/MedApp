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

namespace MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;

public sealed class FinishPathwayStageHandler(
    IPatientPathwayRepository repository,
    IAppointmentRepository appointmentRepository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<FinishPathwayStageCommand, PatientPathwayResponse>
{
    public const string ReasonRequired = "A reason is required to finish a stage without its visit.";

    public async Task<PatientPathwayResponse> Handle(FinishPathwayStageCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);
        var stage = pathway.GetStageOrThrow(request.StageId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);

        if (PathwayWorkflow.WhyStageCantBeEdited(pathway, stage) is { } reason)
            throw new ConflictException(reason);

        // A branch point finished for the first time needs one of its branches, chosen by the clinician (never assumed).
        if (PathwayWorkflow.WhyBranchChoiceInvalid(pathway, stage, request.Branch) is { } branchProblem)
            throw new ValidationException([new ValidationFailure(nameof(request.Branch), branchProblem)]);

        // The finished stage's appointments, those of any stage the branch choice skips, and the next stage's (which
        // becomes current and may need its visit asked for).
        var appointments = await appointmentRepository.GetForStagesForUpdateAsync(
            StageFinishing.StagesTouchedByFinish(pathway, stage, request.Branch).ToList(), cancellationToken);
        var stageAppointments = appointments.Where(a => a.StageId == stage.Id).ToList();

        // A Required visit: only an admin can finish without it, on purpose and with a reason.
        if (PathwayWorkflow.WhyStageNeedsVisit(stage, stageAppointments) is { } needsVisit)
        {
            if (!request.CanOverrideStages || !request.WithoutVisit)
                throw new ConflictException(needsVisit);

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ValidationException([new ValidationFailure(nameof(request.Reason), ReasonRequired)]);
        }

        StageFinishing.Finish(change, stage, appointments, request.Reason, request.Branch);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
        }

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions);
    }
}
