using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

public static class ProcedureRepositoryExtensions
{
    /// <summary>
    /// The procedure, tracked for changes, or a 404. A 409 if its patient's record is restricted or anonymised
    /// (<see cref="PatientRules"/>), since then nothing about it can change. Every change to a procedure loads it here,
    /// or, only to block or cancel it, through <see cref="GetForPauseOrThrowAsync"/>.
    /// </summary>
    public static async Task<PatientProcedure> GetForUpdateOrThrowAsync(
        this IPatientProcedureRepository repository, Guid id, CancellationToken ct)
    {
        var procedure = await repository.GetForUpdateAsync(id, ct)
                        ?? throw new NotFoundException($"Procedure '{id}' was not found.");
        PatientRules.EnsureCanChange(procedure.Patient);
        return procedure;
    }

    /// <summary>
    /// The procedure, tracked for changes, to block or cancel it (<see cref="PatientRules.PausesWhileRestricted"/>), or a
    /// 404. Allowed while its patient's record is restricted, so care can be paused safely; a 409 if it's anonymised.
    /// </summary>
    public static async Task<PatientProcedure> GetForPauseOrThrowAsync(
        this IPatientProcedureRepository repository, Guid id, CancellationToken ct)
    {
        var procedure = await repository.GetForUpdateAsync(id, ct)
                        ?? throw new NotFoundException($"Procedure '{id}' was not found.");
        PatientRules.EnsureCanPause(procedure.Patient);
        return procedure;
    }

    /// <summary>One of the procedure's stages, or a 404.</summary>
    public static PatientProcedureStage GetStageOrThrow(this PatientProcedure procedure, Guid stageId) =>
        procedure.Stages.FirstOrDefault(s => s.Id == stageId)
        ?? throw new NotFoundException($"Stage '{stageId}' was not found on this procedure.");
}
