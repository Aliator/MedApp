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
    IPathwayHandovers handovers,
    MedApp.Application.Teams.Repositories.ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<FinishPathwayStageCommand, PatientPathwayResponse>
{
    public const string ReasonRequired = "A reason is required to finish a stage without its visit.";

    public async Task<PatientPathwayResponse> Handle(FinishPathwayStageCommand request, CancellationToken cancellationToken)
    {
        // The team that works the stage (the relay: the lab's stages are the lab's) works it.
        var (pathway, stage) = await repository.GetForStageWorkOrThrowAsync(
            request.PathwayId, request.StageId, boundary, request.Actor.Teams, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // Teams are named in a handover's entry (MC7).
        var teamNames = (await teams.GetAllAsync(includeArchived: true, cancellationToken)).ToDictionary(t => t.Id, t => t.Name);
        var change = new PathwayChange(pathway, request.Actor, now) { TeamNames = teamNames };

        if (PathwayWorkflow.WhyStageCantBeEdited(pathway, stage) is { } reason)
            throw new ConflictException(reason);

        // MC16: a lab stage waits for its lab case's report to be authorised (no override).
        if (StageGates.WhyLabCaseOpen(stage, (await teams.GetLabAsync(cancellationToken))?.Id) is { } labCaseOpen)
            throw new ConflictException(labCaseOpen);

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

        // A choice node finished for the first time needs one of its outcomes, recorded by the clinician (never assumed).
        if (PathwayWorkflow.WhyOutcomeInvalid(pathway, stage, request.Outcome) is { } outcomeProblem)
            throw new ValidationException([new ValidationFailure(nameof(request.Outcome), outcomeProblem)]);

        // The finished stage's appointments, and those of the stages still to do (any may become current, on any route,
        // and need its visit asked for).
        var appointments = await appointmentRepository.GetForStagesForUpdateAsync(
            StageFinishing.StagesTouchedByFinish(pathway, stage, request.Outcome).ToList(), cancellationToken);
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

        StageFinishing.Finish(change, stage, appointments, request.Reason, request.Outcome);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
            await handovers.NotifyAsync(change, cancellationToken);
        }

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions, viewerScope: request.Actor.Teams,
            teamNames: teamNames);
    }
}
