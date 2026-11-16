namespace MedApp.Contracts.Attachments.Requests;

/// <summary>
/// What a file is attached to: exactly one of a patient, a procedure or a stage. Listing a patient's files lists
/// everything attached to them anywhere; a procedure's lists only its own (not its stages'); a stage's, only its own.
/// </summary>
public sealed class AttachmentTargetRequest
{
    public Guid? PatientId { get; set; }

    public Guid? ProcedureId { get; set; }

    public Guid? StageId { get; set; }
}
