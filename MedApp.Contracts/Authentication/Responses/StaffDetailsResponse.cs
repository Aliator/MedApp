namespace MedApp.Contracts.Authentication.Responses;

/// <summary>Someone's full name and job title, as they go on letters; null for one not given.</summary>
public sealed class StaffDetailsResponse
{
    public string? FullName { get; set; }
    public string? JobTitle { get; set; }
}
