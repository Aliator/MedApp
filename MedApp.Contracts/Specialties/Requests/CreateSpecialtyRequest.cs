namespace MedApp.Contracts.Specialties.Requests;

public sealed class CreateSpecialtyRequest
{
    /// <summary>Unique, ignoring case; at most 100 characters.</summary>
    public string Name { get; set; } = string.Empty;
}
