using MedApp.Domain.Contacts;

namespace MedApp.Domain.Patients;

public sealed class Patient
{
    public Guid Id { get; init; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The patient's NHS number, as its ten digits (no spaces), or null when it isn't known. Optional, and unique
    /// when present. Checked with its Modulus 11 check digit (<see cref="NhsNumbers"/>).
    /// </summary>
    public string? NhsNumber { get; set; }

    /// <summary>
    /// The postal address's lines (at most three), stored as one value (<see cref="PostalAddress"/>), or null
    /// when it isn't known.
    /// </summary>
    public string? AddressLines { get; set; }

    /// <summary>The postcode, in upper case with one space (<see cref="UkPostcodes"/>), or null.</summary>
    public string? Postcode { get; set; }

    /// <summary>A UK mobile, in international form (<c>+447…</c>: <see cref="UkPhoneNumbers"/>), or null.</summary>
    public string? MobilePhone { get; set; }

    /// <summary>The GP practice the patient is registered with, from the directory, or null when it isn't known.</summary>
    public Guid? PracticeId { get; set; }

    /// <summary>How they want appointment reminders; <see cref="ReminderChannel.None"/> until asked.</summary>
    public ReminderChannel ReminderChannel { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// When an admin restricted the record (UK GDPR Article 18), or null. While it's restricted, nothing about the
    /// patient or their pathways can be changed, until an admin lifts it.
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
