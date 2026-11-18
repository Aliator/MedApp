namespace MedApp.Contracts.Attachments.Requests;

/// <summary>The files attached to one patient, pathway, stage or referral (see <see cref="AttachmentTargetRequest"/>).</summary>
public sealed class GetAttachmentsRequest
{
    public Guid? PatientId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? StageId { get; set; }

    public Guid? ReferralId { get; set; }

    /// <summary>Admins only: include deleted files (their details; the files themselves can't be downloaded).</summary>
    public bool IncludeDeleted { get; set; }
}
