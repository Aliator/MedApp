namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Gives one date a day template of its own instead of the week template's.</summary>
public sealed class ApplyDayTemplateRequest
{
    /// <summary>The day template; null for none (an empty day).</summary>
    public Guid? DayTemplateId { get; set; }
}
