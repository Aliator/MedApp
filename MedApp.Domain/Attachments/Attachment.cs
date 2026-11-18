namespace MedApp.Domain.Attachments;

/// <summary>
/// A file (a scan, photo, result or letter) attached to a patient, one of their pathways, a stage of
/// one, or one of their referrals (its letter). Only
/// its metadata is here; the bytes are in blob storage, under keys made from <see cref="Id"/>. Like the audit log, it
/// has no foreign keys: the file has a retention of its own, so its row outlives a deleted patient until the bytes are
/// purged. Deleting is soft (<see cref="DeletedAtUtc"/>): the row stays, hidden from staff, for admins and the purge.
/// </summary>
public sealed class Attachment
{
    public Guid Id { get; init; }

    /// <summary>The patient it belongs to, always set, whatever it's attached to.</summary>
    public Guid PatientId { get; init; }

    /// <summary>The pathway it's attached to (also set for a stage's file); null for a patient's own file.</summary>
    public Guid? PathwayId { get; init; }

    /// <summary>The stage it's attached to; null unless it's a stage's file.</summary>
    public Guid? StageId { get; init; }

    /// <summary>The referral it's attached to (its letter); null unless it's a referral's file.</summary>
    public Guid? ReferralId { get; init; }

    /// <summary>
    /// The name it was uploaded with, cleaned (no folders or control characters). Free text, so anonymising the
    /// patient replaces it.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>The type found from its contents (not its extension or what the browser said), e.g. application/pdf.</summary>
    public string ContentType { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    /// <summary>The SHA-256 of its bytes, as 64 lower-case hex digits.</summary>
    public string Sha256 { get; init; } = string.Empty;

    public AttachmentScanStatus ScanStatus { get; init; }

    /// <summary>Whether a thumbnail was made (images only), stored beside the file.</summary>
    public bool HasThumbnail { get; init; }

    public Guid UploadedByUserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted.</summary>
    public string UploadedByUsername { get; init; } = string.Empty;

    public DateTime UploadedAtUtc { get; init; }

    /// <summary>When it was deleted (hidden); null while it's live. The bytes are purged after the retention window.</summary>
    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedByUserId { get; set; }

    public string? DeletedByUsername { get; set; }

    public bool IsDeleted => DeletedAtUtc is not null;
}
