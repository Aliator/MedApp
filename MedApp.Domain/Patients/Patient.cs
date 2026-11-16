namespace MedApp.Domain.Patients;

public sealed class Patient
{
    public Guid Id { get; init; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// When an admin restricted the record (UK GDPR Article 18), or null. While it's restricted, nothing about the
    /// patient or their procedures can be changed, until an admin lifts it.
    /// </summary>
    public DateTime? RestrictedAtUtc { get; set; }

    /// <summary>Why it's restricted, shown on the patient's banner.</summary>
    public string? RestrictionReason { get; set; }

    /// <summary>When an admin marked the record as disputed by the patient (Article 16), or null.</summary>
    public DateTime? DisputedAtUtc { get; set; }

    /// <summary>What the patient disputes, shown on the patient's banner.</summary>
    public string? DisputeReason { get; set; }

    /// <summary>When an admin anonymised the record, or null. Its identifying details and free text are placeholders.</summary>
    public DateTime? AnonymisedAtUtc { get; set; }

    /// <summary>
    /// When an admin last decided to keep the record past its retention period, the date to review it again; the
    /// record isn't due for review until then.
    /// </summary>
    public DateOnly? RetentionKeptUntil { get; set; }

    /// <summary>Why the record is being kept past its retention period.</summary>
    public string? RetentionKeptReason { get; set; }

    /// <summary>
    /// The database's version of the row, a concurrency token: a save made from a copy read before someone else's save
    /// is refused (409) rather than writing old values back over theirs, such as undoing a restriction or an
    /// anonymisation.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
