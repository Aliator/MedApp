namespace MedApp.Contracts.Patients.Requests;

/// <summary>
/// A GP practice that isn't in the directory ("Not listed here"): held on the patient only, never added to the
/// directory.
/// </summary>
public sealed class UnlistedPracticeRequest
{
    /// <summary>The practice's name: required, at most 200 characters.</summary>
    public string Name { get; set; } = "";

    /// <summary>Its address's lines: at most three, each at most 100 characters. Left out or empty, none.</summary>
    public List<string>? AddressLines { get; set; }

    /// <summary>A UK postcode. Left out or empty, none.</summary>
    public string? Postcode { get; set; }

    /// <summary>A UK phone number. Left out or empty, none.</summary>
    public string? Phone { get; set; }
}
