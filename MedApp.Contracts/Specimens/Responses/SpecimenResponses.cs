using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Contracts.Specimens.Responses;

/// <summary>
/// One specimen, as its page, label and request sheet show it: the patient (for the banner and the label), where it
/// came from, where it is, when each step happened and who did it, and what the caller may do next. Free text (the
/// site, request notes and closing detail) is here for anyone who can see patients, as on a pathway's page; it never
/// reaches a staff timeline or a notification.
/// </summary>
public sealed class SpecimenResponse
{
    public Guid Id { get; set; }

    /// <summary>MedApp's specimen number, e.g. <c>MA26-000123</c>.</summary>
    public string Number { get; set; } = string.Empty;

    public Guid PatientId { get; set; }

    /// <summary>The patient, for the banner and the label.</summary>
    public PathwayPatientResponse? Patient { get; set; }

    /// <summary>The patient's restricted, disputed and anonymised flags, for the banners.</summary>
    public PatientFlagsResponse? PatientFlags { get; set; }

    public Guid PathwayId { get; set; }

    public string PathwayTitle { get; set; } = string.Empty;

    public Guid StageId { get; set; }

    public string StageName { get; set; } = string.Empty;

    public Guid SpecimenTypeId { get; set; }

    public string SpecimenTypeName { get; set; } = string.Empty;

    /// <summary>The type's expected days from taken to reported.</summary>
    public int ExpectedTurnaroundDays { get; set; }

    public Guid LaboratoryId { get; set; }

    public string LaboratoryName { get; set; } = string.Empty;

    public string? Site { get; set; }

    public string? RequestNotes { get; set; }

    public Guid RequestingUserId { get; set; }

    /// <summary>The requesting clinician's username, or "Deleted user".</summary>
    public string RequestingUsername { get; set; } = string.Empty;

    /// <summary>Taken, Sent, Received, Reported, RejectedByLab, Lost or EnteredInError.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Taken, Sent or Received: still to come back from the lab.</summary>
    public bool IsOpen { get; set; }

    public DateTime TakenAtUtc { get; set; }

    public string TakenByUsername { get; set; } = string.Empty;

    public DateTime? SentAtUtc { get; set; }

    public string? SentByUsername { get; set; }

    public string? CourierReference { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    public string? ReceivedByUsername { get; set; }

    public string? LabReference { get; set; }

    public DateTime? ReportedAtUtc { get; set; }

    public string? ReportedByUsername { get; set; }

    /// <summary>The report's attachment, to download (or see its thumbnail).</summary>
    public Guid? ReportAttachmentId { get; set; }

    /// <summary>Rejected by the lab: why (Unlabelled, Mislabelled, Leaked, InsufficientSample, WrongContainer or Other).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>The detail given when it was rejected, lost or entered in error.</summary>
    public string? ClosedDetail { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosedByUsername { get; set; }

    /// <summary>When the report is expected: taken plus the type's expected days.</summary>
    public DateTime ExpectedByUtc { get; set; }

    /// <summary>Still open after <see cref="ExpectedByUtc"/>: a date comparison, nothing clinical.</summary>
    public bool IsOverdueAtLab { get; set; }

    /// <summary>One more for each change: send it back as <c>expectedVersion</c>.</summary>
    public int Version { get; set; }

    /// <summary>Its details can be corrected (before it's sent, by a holder of RecordSpecimens, the record not restricted).</summary>
    public bool CanEdit { get; set; }

    public bool CanMarkSent { get; set; }

    public bool CanMarkReceived { get; set; }

    public bool CanReject { get; set; }

    public bool CanReport { get; set; }

    public bool CanMarkLost { get; set; }

    public bool CanMarkEnteredInError { get; set; }

    /// <summary>Why it can't change now (the record restricted or anonymised, or the specimen closed), or null.</summary>
    public string? ReadOnlyReason { get; set; }

    /// <summary>The label's QR code, holding the specimen number only.</summary>
    public SpecimenLabelResponse Label { get; set; } = new();

    /// <summary>The other specimens the same stage took (a rejected or lost one's retake, say), oldest first.</summary>
    public List<SpecimenSummaryResponse> OtherSpecimensFromStage { get; set; } = [];
}

/// <summary>
/// The label's QR code, drawn on the server: one SVG path in a square of <see cref="Size"/> modules (the quiet zone
/// included). It encodes the specimen number only, never patient details.
/// </summary>
public sealed class SpecimenLabelResponse
{
    public string QrPath { get; set; } = string.Empty;

    public int Size { get; set; }
}

/// <summary>A specimen in a list (the lab's worklist, a pathway's specimens): dates, names and statuses only.</summary>
public sealed class SpecimenSummaryResponse
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid PatientId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string? PatientNhsNumber { get; set; }

    public DateOnly PatientDateOfBirth { get; set; }

    public Guid PathwayId { get; set; }

    public Guid StageId { get; set; }

    public string SpecimenTypeName { get; set; } = string.Empty;

    public string LaboratoryName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsOpen { get; set; }

    public DateTime TakenAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    public DateTime? ReportedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime ExpectedByUtc { get; set; }

    public bool IsOverdueAtLab { get; set; }
}

/// <summary>A specimen type, for the setup page and the Record Specimen dialog.</summary>
public sealed class SpecimenTypeResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ExpectedTurnaroundDays { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>No specimen has it, so it can be deleted; otherwise it's archived.</summary>
    public bool CanBeDeleted { get; set; }
}

/// <summary>A lab, for the setup page and the Record Specimen dialog.</summary>
public sealed class LaboratoryResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool UsesMedApp { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>No specimen was sent to it, so it can be deleted; otherwise it's archived.</summary>
    public bool CanBeDeleted { get; set; }
}
