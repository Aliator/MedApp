namespace MedApp.Contracts.Attachments.Requests;

/// <summary>
/// What a file is attached to: exactly one of a patient, a pathway, a stage or a referral. Listing a patient's files
/// lists everything attached to them anywhere; a pathway's lists only its own (not its stages'); a stage's or a
/// referral's, only its own.
/// </summary>
public sealed class AttachmentTargetRequest
{
    public Guid? PatientId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? StageId { get; set; }

    /// <summary>A referral (its letter).</summary>
    public Guid? ReferralId { get; set; }
}
