namespace MedApp.Contracts.Consent;

public sealed class ConsentFormResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FormVersion { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>Whether it can be deleted: no consent record has used it (a used one is archived instead).</summary>
    public bool CanBeDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// A consent record, as the clinician recorded it. MedApp records what the clinician states; it never assesses capacity,
/// validity or whether the form fits.
/// </summary>
public sealed class ConsentRecordResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid PathwayId { get; set; }
    public Guid StageId { get; set; }

    /// <summary>The stage's name on this pathway.</summary>
    public string StageName { get; set; } = string.Empty;

    public Guid ConsentFormId { get; set; }

    /// <summary>The form's name and version when the consent was recorded.</summary>
    public string FormName { get; set; } = string.Empty;
    public string FormVersion { get; set; } = string.Empty;

    public DateOnly SignedOn { get; set; }

    /// <summary>Patient, PersonWithParentalResponsibility or BestInterestsDecision.</summary>
    public string SignedBy { get; set; } = string.Empty;

    public Guid RecordedByUserId { get; set; }
    public string? RecordedByUsername { get; set; }
    public DateTime RecordedAtUtc { get; set; }

    /// <summary>The scanned, signed form (a file on the pathway), or null: shown as "No scan".</summary>
    public Guid? ScanAttachmentId { get; set; }

    /// <summary>Valid, Withdrawn or EnteredInError.</summary>
    public string Status { get; set; } = string.Empty;

    public DateOnly? WithdrawnOn { get; set; }

    /// <summary>What was said when it was withdrawn, or what was wrong (free text).</summary>
    public string? StatusDetail { get; set; }

    public string? StatusChangedByUsername { get; set; }
    public DateTime? StatusChangedAtUtc { get; set; }

    /// <summary>
    /// Withdrawn after its stage was finished: flagged on the pathway (decision 147). Nothing changes by itself; the
    /// clinician decides what to do.
    /// </summary>
    public bool WithdrawnAfterStageFinished { get; set; }

    /// <summary>One more for each change; every change sends the version it started from.</summary>
    public int Version { get; set; }

    /// <summary>Whether the viewer can withdraw it now (RecordConsent; it's Valid; the record isn't anonymised).</summary>
    public bool CanWithdraw { get; set; }

    /// <summary>
    /// Whether the viewer can mark it entered in error now: whoever recorded it within 24 hours, or a holder of
    /// CorrectRecords, while it isn't already entered in error and the record isn't anonymised.
    /// </summary>
    public bool CanMarkEnteredInError { get; set; }

    /// <summary>Whether the viewer can attach its scan now (RecordConsent; it has none; it's Valid; the record can change).</summary>
    public bool CanAttachScan { get; set; }
}
