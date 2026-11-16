namespace MedApp.Contracts.Calendar.Responses;

/// <summary>A member of staff (anyone with a role), whose calendar can be shown, with their default week template now.</summary>
public sealed class StaffMemberResponse
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public Guid? WeekTemplateId { get; set; }

    public string? WeekTemplateName { get; set; }
}
