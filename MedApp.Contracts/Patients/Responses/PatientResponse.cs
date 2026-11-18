using MedApp.Contracts.Practices.Responses;

namespace MedApp.Contracts.Patients.Responses;

public sealed record PatientResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Email
)
{
    /// <summary>The NHS number, as its ten digits (shown as <c>XXX XXX XXXX</c>), or null when it isn't known.</summary>
    public string? NhsNumber { get; init; }

    /// <summary>The postal address's lines (none when it isn't known).</summary>
    public IReadOnlyList<string> AddressLines { get; init; } = [];

    /// <summary>The postcode, upper case with one space, or null.</summary>
    public string? Postcode { get; init; }

    /// <summary>A UK mobile in international form (<c>+447…</c>), or null.</summary>
    public string? MobilePhone { get; init; }

    /// <summary>How they want appointment messages: None, Text, Email or TextAndEmail.</summary>
    public string ReminderChannel { get; init; } = "None";

    /// <summary>The ID of the GP practice they're registered with, or null.</summary>
    public Guid? PracticeId { get; init; }

    /// <summary>
    /// The practice itself, on a single patient's response (their page, and after adding or editing them); null in
    /// lists, and when they have none.
    /// </summary>
    public PracticeSummaryResponse? Practice { get; init; }

    /// <summary>
    /// Their GP practice when it isn't in the directory, held on this patient only, or null. A patient has this or
    /// <see cref="PracticeId"/>, never both.
    /// </summary>
    public UnlistedPracticeResponse? UnlistedPractice { get; init; }

    /// <summary>An admin has restricted the record (Article 18): nothing about it can be changed until they lift it.</summary>
    public bool IsRestricted { get; init; }

    public string? RestrictionReason { get; init; }

    public DateTime? RestrictedAtUtc { get; init; }

    /// <summary>The patient disputes something in the record (Article 16).</summary>
    public bool IsDisputed { get; init; }

    public string? DisputeReason { get; init; }

    public DateTime? DisputedAtUtc { get; init; }

    /// <summary>The record has been anonymised: its details are placeholders, and it can't be changed.</summary>
    public bool IsAnonymised { get; init; }

    public DateTime? AnonymisedAtUtc { get; init; }
}
