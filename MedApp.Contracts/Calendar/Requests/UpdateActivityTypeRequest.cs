namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Edits an activity type. Only the fields that are set change.</summary>
public sealed class UpdateActivityTypeRequest
{
    public string? Name { get; set; }

    public string? Colour { get; set; }
}
