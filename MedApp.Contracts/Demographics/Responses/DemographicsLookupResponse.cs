namespace MedApp.Contracts.Demographics.Responses;

/// <summary>A person found by NHS number in the demographics service, to fill in a patient's form.</summary>
public sealed class DemographicsLookupResponse
{
    /// <summary>The NHS number looked up, as its ten digits.</summary>
    public string NhsNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }

    /// <summary>Which service answered, e.g. "Offline test data (synthetic 999 numbers)".</summary>
    public string Source { get; set; } = string.Empty;
}
