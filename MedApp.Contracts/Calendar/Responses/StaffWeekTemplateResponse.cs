namespace MedApp.Contracts.Calendar.Responses;

/// <summary>A person's default week template now.</summary>
public sealed class StaffWeekTemplateResponse
{
    public Guid StaffUserId { get; set; }

    /// <summary>Null when they have none.</summary>
    public Guid? WeekTemplateId { get; set; }

    public string? WeekTemplateName { get; set; }

    /// <summary>The UK date it has applied from.</summary>
    public DateOnly? Since { get; set; }
}
