namespace MedApp.Contracts.Patients.Requests;

public sealed class UpdatePatientRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = "";

    /// <summary>
    /// Ten digits with a valid check digit, with or without spaces. Left out (null), it's unchanged; empty, it's
    /// removed.
    /// </summary>
    public string? NhsNumber { get; set; }

    /// <summary>
    /// The postal address's lines: at most three, each at most 100 characters. Left out (null), unchanged; an empty list
    /// removes them.
    /// </summary>
    public List<string>? AddressLines { get; set; }

    /// <summary>A UK postcode. Left out (null), unchanged; empty, removed.</summary>
    public string? Postcode { get; set; }

    /// <summary>A UK mobile (<c>07…</c> or <c>+44 7…</c>). Left out (null), unchanged; empty, removed.</summary>
    public string? MobilePhone { get; set; }

    /// <summary>
    /// The GP practice they're registered with, from the directory. Left out (null), unchanged; the empty ID
    /// (<c>00000000-0000-0000-0000-000000000000</c>) removes it. A newly chosen practice mustn't be archived.
    /// </summary>
    public Guid? PracticeId { get; set; }

    /// <summary>How they want appointment messages: None, Text, Email or TextAndEmail. Left out (null), unchanged.</summary>
    public string? ReminderChannel { get; set; }
}