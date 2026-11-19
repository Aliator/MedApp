using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

public static class PathwayRepositoryExtensions
{
    /// <summary>The words of a missing pathway's 404, and of one behind the viewer's team wall (the same, decision 204).</summary>
    public static string NotFoundMessage(Guid id) => $"Pathway '{id}' was not found.";

    /// <summary>
    /// The pathway, tracked, for a change by the viewer: behind their team wall a 404 (recorded), seen but another
    /// team's a 409 (the team walls, <see cref="ITeamBoundary"/>), then a 409 if its patient's record is restricted or
    /// anonymised (<see cref="PatientRules"/>). Every change a team makes to a pathway loads it here (or, to block or
    /// cancel it, through <see cref="GetForPauseOrThrowAsync(IPatientPathwayRepository, Guid, ITeamBoundary, TeamScope, CancellationToken)"/>).
    /// </summary>
    public static async Task<PatientPathway> GetForUpdateOrThrowAsync(
        this IPatientPathwayRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var pathway = await repository.GetForUpdateAsync(id, ct) ?? throw new NotFoundException(NotFoundMessage(id));
        await boundary.EnsureCanChangePathwayAsync(pathway, scope, NotFoundMessage(id), ct);
        PatientRules.EnsureCanChange(pathway.Patient);
        return pathway;
    }

    /// <summary>
    /// The pathway, tracked, to block or cancel it: the team walls as <see cref="GetForUpdateOrThrowAsync(IPatientPathwayRepository, Guid, ITeamBoundary, TeamScope, CancellationToken)"/>,
    /// then allowed while its patient's record is restricted (a 409 if it's anonymised).
    /// </summary>
    public static async Task<PatientPathway> GetForPauseOrThrowAsync(
        this IPatientPathwayRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var pathway = await repository.GetForUpdateAsync(id, ct) ?? throw new NotFoundException(NotFoundMessage(id));
        await boundary.EnsureCanChangePathwayAsync(pathway, scope, NotFoundMessage(id), ct);
        PatientRules.EnsureCanPause(pathway.Patient);
        return pathway;
    }

    /// <summary>
    /// The pathway, tracked, for a correction that isn't the team's own work (an admin deleting a note, deciding a
    /// deletion request): seen by the viewer (else a 404, recorded), then a 409 if its patient's record is restricted or
    /// anonymised. Admins see every team's pathways.
    /// </summary>
    public static async Task<PatientPathway> GetForCorrectionOrThrowAsync(
        this IPatientPathwayRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var pathway = await repository.GetForUpdateAsync(id, ct) ?? throw new NotFoundException(NotFoundMessage(id));
        await boundary.EnsurePathwayVisibleAsync(pathway, scope, NotFoundMessage(id), ct);
        PatientRules.EnsureCanChange(pathway.Patient);
        return pathway;
    }

    /// <summary>
    /// The pathway, tracked for changes, or a 404, without the team walls: for system work and for areas the walls
    /// don't reach yet (MC5). A 409 if its patient's record is restricted or anonymised (<see cref="PatientRules"/>),
    /// since then nothing about it can change. A team's change to a pathway loads it with its scope instead.
    /// </summary>
    public static async Task<PatientPathway> GetForUpdateOrThrowAsync(
        this IPatientPathwayRepository repository, Guid id, CancellationToken ct)
    {
        var pathway = await repository.GetForUpdateAsync(id, ct)
                        ?? throw new NotFoundException($"Pathway '{id}' was not found.");
        PatientRules.EnsureCanChange(pathway.Patient);
        return pathway;
    }

    /// <summary>
    /// The pathway, tracked for changes, to block or cancel it (<see cref="PatientRules.PausesWhileRestricted"/>), or a
    /// 404. Allowed while its patient's record is restricted, so care can be paused safely; a 409 if it's anonymised.
    /// </summary>
    public static async Task<PatientPathway> GetForPauseOrThrowAsync(
        this IPatientPathwayRepository repository, Guid id, CancellationToken ct)
    {
        var pathway = await repository.GetForUpdateAsync(id, ct)
                        ?? throw new NotFoundException($"Pathway '{id}' was not found.");
        PatientRules.EnsureCanPause(pathway.Patient);
        return pathway;
    }

    /// <summary>One of the pathway's stages, or a 404.</summary>
    public static PatientPathwayStage GetStageOrThrow(this PatientPathway pathway, Guid stageId) =>
        pathway.Stages.FirstOrDefault(s => s.Id == stageId)
        ?? throw new NotFoundException($"Stage '{stageId}' was not found on this pathway.");
}
