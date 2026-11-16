namespace MedApp.Contracts.Calendar.Requests;

public sealed class CreateActivityTypeRequest
{
    /// <summary>At most 60 characters, unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One of red, orange, yellow, green, teal, cyan, blue, purple, pink or gray.</summary>
    public string Colour { get; set; } = string.Empty;
}
