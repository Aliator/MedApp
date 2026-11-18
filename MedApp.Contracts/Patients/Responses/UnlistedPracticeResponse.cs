namespace MedApp.Contracts.Patients.Responses;

/// <summary>A patient's GP practice that isn't in the directory: its name, address and phone, held on the patient only.</summary>
public sealed class UnlistedPracticeResponse
{
    public string Name { get; init; } = "";

    /// <summary>Its address's lines (at most three), without the postcode.</summary>
    public IReadOnlyList<string> AddressLines { get; init; } = [];

    public string? Postcode { get; init; }

    public string? Phone { get; init; }
}
