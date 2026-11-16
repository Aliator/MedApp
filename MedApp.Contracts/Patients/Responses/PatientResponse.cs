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
