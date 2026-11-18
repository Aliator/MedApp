namespace MedApp.Contracts.Attachments.Requests;

/// <summary>
/// What a file is attached to: exactly one of a patient, a pathway or a stage. Listing a patient's files lists
/// everything attached to them anywhere; a pathway's lists only its own (not its stages'); a stage's, only its own.
/// </summary>
public sealed class AttachmentTargetRequest
{
    public Guid? PatientId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? StageId { get; set; }
}
