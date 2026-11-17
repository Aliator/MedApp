namespace MedApp.Contracts.Calendar.Requests;

/// <summary>
/// A week template: a day template (or none) for each weekday. To add one, or to replace one with (an edit applies
/// from today; past days keep what they had).
/// </summary>
public sealed class WeekTemplateRequest
{
    /// <summary>At most 100 characters, unique among the owner's templates.</summary>
    public string Name { get; set; } = string.Empty;

    public Guid? Monday { get; set; }
    public Guid? Tuesday { get; set; }
    public Guid? Wednesday { get; set; }
    public Guid? Thursday { get; set; }
    public Guid? Friday { get; set; }
}
