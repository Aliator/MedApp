using MedApp.Application.Procedures.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Attachments.Common;

/// <summary>What a file is attached to. Stored by name only in responses and audit details.</summary>
public enum AttachmentTargetKind
{
    Patient,
    Procedure,
    Stage
}

/// <summary>
/// What a file is (or will be) attached to: a patient, one of their procedures, or a stage of one, with the
/// procedure and stage loaded, to check the workflow's rules and record their names.
/// </summary>
public sealed record AttachmentTarget(Guid PatientId, PatientProcedure? Procedure, PatientProcedureStage? Stage)
{
    public AttachmentTargetKind Kind =>
        Stage is not null ? AttachmentTargetKind.Stage
        : Procedure is not null ? AttachmentTargetKind.Procedure
        : AttachmentTargetKind.Patient;

    /// <summary>What an attachment is attached to, from its IDs alone.</summary>
    public static AttachmentTargetKind KindOf(Attachment attachment) =>
        attachment.StageId is not null ? AttachmentTargetKind.Stage
        : attachment.ProcedureId is not null ? AttachmentTargetKind.Procedure
        : AttachmentTargetKind.Patient;

    /// <summary>
    /// Why files can't be added to it, or removed from it by staff, now; null if they can. A patient's files can
    /// always change. A procedure's follow its details: not once it's cancelled or archived. A stage's follow its
    /// notes: only the stage being worked on, or one an admin has unlocked, of an open procedure.
    /// </summary>
    public string? WhyFilesCantChange() => Kind switch
    {
        AttachmentTargetKind.Stage => ProcedureWorkflow.WhyStageCantBeEdited(Procedure!, Stage!),
        AttachmentTargetKind.Procedure when !ProcedureWorkflow.CanEditDetails(Procedure!) =>
            $"Files can't be added to or removed from a procedure while it's {ProcedureWorkflow.Describe(Procedure!.Status)}.",
        _ => null
    };
}
