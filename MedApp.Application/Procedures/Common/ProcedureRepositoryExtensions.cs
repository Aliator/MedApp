using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

public static class ProcedureRepositoryExtensions
{
    /// <summary>The procedure, tracked for changes, or a 404.</summary>
    public static async Task<PatientProcedure> GetForUpdateOrThrowAsync(
        this IPatientProcedureRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct)
        ?? throw new NotFoundException($"Procedure '{id}' was not found.");

    /// <summary>One of the procedure's stages, or a 404.</summary>
    public static PatientProcedureStage GetStageOrThrow(this PatientProcedure procedure, Guid stageId) =>
        procedure.Stages.FirstOrDefault(s => s.Id == stageId)
        ?? throw new NotFoundException($"Stage '{stageId}' was not found on this procedure.");
}
