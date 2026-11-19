using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;

/// <summary>
/// Adds a template's addable branch (MC10, the light demo's <c>POST …/branches</c>): only the pathway's own team (the
/// lab works its stages, but the route is the clinic's choice), only from the template's list as the pathway keeps it,
/// after a stage it may follow, with a reason. Its stages run side by side with what's left; its first is reached at
/// once when the stage it follows is done (handed over if another team's, MC7), and its visit asked for.
/// </summary>
public sealed class AddPathwayBranchHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IAppointmentRepository appointmentRepository,
    ITeamRepository teams,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    IPathwayHandovers handovers,
    TimeProvider timeProvider)
    : IRequestHandler<AddPathwayBranchCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(AddPathwayBranchCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, boundary, request.Actor.Teams, cancellationToken);
        if (!PathwayWorkflow.CanWorkOnStages(pathway))
            throw new ConflictException(PathwayBranches.OnHoldOrClosedMessage);

        var branch = pathway.Plan?.Branches.FirstOrDefault(b => b.Id == request.TemplateBranchId)
                     ?? throw new ValidationException(
                         [new ValidationFailure(nameof(request.TemplateBranchId), PathwayBranches.NotTheTemplatesMessage)]);
        var after = pathway.Stages.FirstOrDefault(s => s.Id == request.AfterStageId)
                    ?? throw new ValidationException(
                        [new ValidationFailure(nameof(request.AfterStageId), PathwayBranches.ChooseAfterStageMessage)]);
        if (PathwayBranches.AfterStagesFor(pathway, branch).All(s => s.Id != after.Id))
            throw new ConflictException(PathwayBranches.CantGoThereMessage(branch.Name, after.Name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var teamNames = (await teams.GetAllAsync(includeArchived: true, cancellationToken)).ToDictionary(t => t.Id, t => t.Name);
        var change = new PathwayChange(pathway, request.Actor, now) { TeamNames = teamNames };
        change.AddBranch(branch, after, request.Reason!.Trim());

        // Its first stage, current at once, may need its visit asked for (it's new, so it has no appointments).
        StageVisits.RequestForCurrentStage(change, await appointmentRepository.GetForStagesForUpdateAsync(
            pathway.Stages.Where(PathwayWorkflow.IsToDo).Select(s => s.Id).ToList(), cancellationToken));

        await repository.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);
        await handovers.NotifyAsync(change, cancellationToken);

        return await PathwayMapper.ToResponseAsync(
            pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId,
            viewerPermissions: request.Actor.Permissions, viewerScope: request.Actor.Teams, teamNames: teamNames);
    }
}
