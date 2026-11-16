using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Attachments.Common;

/// <summary>Finds what a file is (or will be) attached to, or a 404.</summary>
public static class AttachmentTargets
{
    /// <summary>
    /// The patient, procedure or stage a request names (exactly one, which its validator checks), with the procedure
    /// and stage loaded.
    /// </summary>
    public static async Task<AttachmentTarget> ResolveAsync(
        Guid? patientId,
        Guid? procedureId,
        Guid? stageId,
        IPatientRepository patients,
        IPatientProcedureRepository procedures,
        IAttachmentRepository attachments,
        CancellationToken ct)
    {
        if (stageId is { } stage)
        {
            var stageProcedureId = await attachments.GetStageProcedureIdAsync(stage, ct)
                                   ?? throw new NotFoundException($"Stage '{stage}' was not found.");
            var procedure = await GetProcedureAsync(stageProcedureId, procedures, ct);
            return new AttachmentTarget(procedure.PatientId, procedure, procedure.GetStageOrThrow(stage));
        }

        if (procedureId is { } procedureIdValue)
        {
            var procedure = await GetProcedureAsync(procedureIdValue, procedures, ct);
            return new AttachmentTarget(procedure.PatientId, procedure, null);
        }

        var patient = patientId ?? throw new ArgumentException("A patient, procedure or stage is needed.");
        _ = await patients.GetByIdAsync(patient, ct) ?? throw new NotFoundException($"Patient '{patient}' was not found.");
        return new AttachmentTarget(patient, null, null);
    }

    /// <summary>
    /// What an existing attachment is attached to. Its procedure can't be deleted while it has files (a 409), but if
    /// it somehow has been, the target is the patient, so the file can still be dealt with.
    /// </summary>
    public static async Task<AttachmentTarget> OfAsync(
        Attachment attachment,
        IPatientProcedureRepository procedures,
        CancellationToken ct)
    {
        if (attachment.ProcedureId is not { } procedureId)
            return new AttachmentTarget(attachment.PatientId, null, null);

        var procedure = await procedures.GetByIdAsync(procedureId, ct);
        if (procedure is null)
            return new AttachmentTarget(attachment.PatientId, null, null);

        var stage = attachment.StageId is { } stageId ? procedure.Stages.FirstOrDefault(s => s.Id == stageId) : null;
        return new AttachmentTarget(attachment.PatientId, procedure, stage);
    }

    private static async Task<PatientProcedure> GetProcedureAsync(
        Guid id, IPatientProcedureRepository procedures, CancellationToken ct) =>
        await procedures.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Procedure '{id}' was not found.");
}
