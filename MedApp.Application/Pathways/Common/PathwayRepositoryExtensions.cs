using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

public static class PathwayRepositoryExtensions
{
    /// <summary>
    /// The pathway, tracked for changes, or a 404. A 409 if its patient's record is restricted or anonymised
    /// (<see cref="PatientRules"/>), since then nothing about it can change. Every change to a pathway loads it here,
    /// or, only to block or cancel it, through <see cref="GetForPauseOrThrowAsync"/>.
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
