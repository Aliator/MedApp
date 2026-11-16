namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Sets a person's default week template, from today.</summary>
public sealed class SetStaffWeekTemplateRequest
{
    /// <summary>The week template; null for none.</summary>
    public Guid? WeekTemplateId { get; set; }
}
