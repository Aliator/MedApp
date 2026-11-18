namespace MedApp.Contracts.Consent;

/// <summary>A consent form's name and its own version, as printed on it.</summary>
public sealed class ConsentFormRequest
{
    public string Name { get; set; } = string.Empty;
    public string FormVersion { get; set; } = string.Empty;
}

/// <summary>
/// Records a patient's consent to one of the pathway's stages, as the clinician states it. MedApp doesn't assess
/// capacity or validity.
/// </summary>
public sealed class RecordConsentRequest
{
    public Guid StageId { get; set; }
    public Guid ConsentFormId { get; set; }

    /// <summary>The UK date it was signed (or the best-interests decision made): not in the future, at most a year back.</summary>
    public DateOnly SignedOn { get; set; }

    /// <summary>Patient, PersonWithParentalResponsibility or BestInterestsDecision.</summary>
    public string SignedBy { get; set; } = string.Empty;

    /// <summary>The scanned, signed form, already attached to the pathway or one of its stages; optional.</summary>
    public Guid? ScanAttachmentId { get; set; }
}

/// <summary>Consent withdrawn: the UK date, and what was said (500 characters).</summary>
public sealed class WithdrawConsentRequest
{
    public DateOnly WithdrawnOn { get; set; }
    public string Detail { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}

/// <summary>A consent record entered in error, with what was wrong (500 characters).</summary>
public sealed class MarkConsentEnteredInErrorRequest
{
    public string Detail { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}

/// <summary>The scanned, signed form for a record that has none: a file already attached to the pathway or one of its stages.</summary>
public sealed class AttachConsentScanRequest
{
    public Guid AttachmentId { get; set; }
    public int ExpectedVersion { get; set; }
}
