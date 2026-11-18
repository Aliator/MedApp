namespace MedApp.Contracts.Patients.Requests;

public sealed class CreatePatientRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = "";

    /// <summary>
    /// Optional: ten digits with a valid check digit, with or without spaces. Left out or empty, the patient has none.
    /// </summary>
    public string? NhsNumber { get; set; }

    /// <summary>The postal address's lines: at most three, each at most 100 characters. Left out or empty, none.</summary>
    public List<string>? AddressLines { get; set; }

    /// <summary>A UK postcode (any case, spaces anywhere). Left out or empty, none.</summary>
    public string? Postcode { get; set; }

    /// <summary>A UK mobile (<c>07…</c> or <c>+44 7…</c>), stored as <c>+447…</c>. Left out or empty, none.</summary>
    public string? MobilePhone { get; set; }

    /// <summary>The GP practice they're registered with, from the directory (not archived). Left out, none.</summary>
    public Guid? PracticeId { get; set; }

    /// <summary>How they want appointment messages: None, Text, Email or TextAndEmail. Left out, None.</summary>
    public string? ReminderChannel { get; set; }
}