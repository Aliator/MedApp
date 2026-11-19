using MedApp.Application.Common.Teams;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;

public sealed class FinishPathwayStageHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IAppointmentRepository appointmentRepository,
    ILetterRepository letterRepository,
    ISpecimenRepository specimenRepository,
    MedApp.Application.Results.Repositories.IResultRepository resultRepository,
    IConsentRepository consentRepository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<FinishPathwayStageCommand, PatientPathwayResponse>
{
    public const string ReasonRequired = "A reason is required to finish a stage without its visit.";

    public async Task<PatientPathwayResponse> Handle(FinishPathwayStageCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, boundary, request.Actor.Teams, cancellationToken);
        var stage = pathway.GetStageOrThrow(request.StageId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);

        if (PathwayWorkflow.WhyStageCantBeEdited(pathway, stage) is { } reason)
            throw new ConflictException(reason);

        // The gates with no override (StageGates): the last stage waits for the pathway's open specimens.
        if (StageGates.IsLastToFinish(pathway, stage)
            && StageGates.WhyCantFinish(
                pathway, stage, await specimenRepository.GetOpenForPathwayAsync(pathway.Id, cancellationToken)) is { } gate)
            throw new ConflictException(gate);

        // A stage that waits for results, and the last stage, wait for the pathway's results to be signed off (no override).
        if ((stage.WaitsForResults || StageGates.IsLastToFinish(pathway, stage))
            && StageGates.WhyResultsAwaiting(
                pathway, stage, await resultRepository.GetAwaitingForPathwayAsync(pathway.Id, cancellationToken)) is { } awaiting)
            throw new ConflictException(awaiting);

        // A branch point finished for the first time needs one of its branches, chosen by the clinician (never assumed).
        if (PathwayWorkflow.WhyBranchChoiceInvalid(pathway, stage, request.Branch) is { } branchProblem)
            throw new ValidationException([new ValidationFailure(nameof(request.Branch), branchProblem)]);

        // The finished stage's appointments, those of any stage the branch choice skips, and the next stage's (which
        // becomes current and may need its visit asked for).
        var appointments = await appointmentRepository.GetForStagesForUpdateAsync(
            StageFinishing.StagesTouchedByFinish(pathway, stage, request.Branch).ToList(), cancellationToken);
        var stageAppointments = appointments.Where(a => a.StageId == stage.Id).ToList();

        // A Required visit: only a holder of FinishWithoutVisit can finish without it, on purpose and with a reason.
        if (PathwayWorkflow.WhyStageNeedsVisit(stage, stageAppointments) is { } needsVisit)
        {
            if (!request.CanFinishWithoutVisit || !request.WithoutVisit)
                throw new ConflictException(needsVisit);

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ValidationException([new ValidationFailure(nameof(request.Reason), ReasonRequired)]);
        }

        // A stage that needs a letter: one of its kind on the pathway, approved and sent to everyone (no override).
        if (stage.NeedsLetterKind is not null
            && StageGates.WhyLetterMissing(stage, await letterRepository.GetForPathwayAsync(pathway.Id, cancellationToken)) is { } needsLetter)
            throw new ConflictException(needsLetter);

        // A stage that needs consent: a Valid consent record for it (no override: recording it is the action).
        if (stage.NeedsConsent
            && StageGates.WhyConsentMissing(stage, await consentRepository.GetForStageAsync(stage.Id, cancellationToken)) is { } needsConsent)
            throw new ConflictException(needsConsent);

        StageFinishing.Finish(change, stage, appointments, request.Reason, request.Branch);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
        }

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions);
    }
}
