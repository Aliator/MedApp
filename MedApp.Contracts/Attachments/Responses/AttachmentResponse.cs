namespace MedApp.Contracts.Attachments.Responses;

/// <summary>A file attached to a patient, a pathway or a stage, without its bytes.</summary>
public sealed class AttachmentResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>What it's attached to: Patient, Pathway or Stage.</summary>
    public string AttachedTo { get; set; } = string.Empty;

    /// <summary>The pathway it's attached to (a stage's file too); null for the patient's own file.</summary>
    public Guid? PathwayId { get; set; }

    /// <summary>That pathway's title now; null for the patient's own file, or if the pathway has gone.</summary>
    public string? PathwayTitle { get; set; }

    /// <summary>The stage it's attached to, if any.</summary>
    public Guid? StageId { get; set; }

    /// <summary>That stage's name, if any.</summary>
    public string? StageName { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>The type found from the file's contents, e.g. application/pdf.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>The type in words, e.g. PDF, JPEG image, Word document.</summary>
    public string TypeName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>The SHA-256 of its bytes (64 hex digits), to check a downloaded copy.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>The malware scan's result: NotScanned (no scanner is set up yet), Clean or Infected.</summary>
    public string ScanStatus { get; set; } = string.Empty;

    /// <summary>Whether it has a thumbnail (some images), at <c>api/attachments/{id}/thumbnail</c>.</summary>
    public bool HasThumbnail { get; set; }

    public string UploadedByUsername { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    /// <summary>Whether the signed-in user may delete it: their own file, or any file for an admin.</summary>
    public bool CanDelete { get; set; }

    /// <summary>Deleted (only admins see deleted files, and only their details).</summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public string? DeletedByUsername { get; set; }
}
