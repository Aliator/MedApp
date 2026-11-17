namespace MedApp.Contracts.Calendar.Responses;

public sealed class WeekTemplateResponse
{
    public Guid Id { get; set; }

    /// <summary>The member of staff whose template it is.</summary>
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Archived week templates can't be chosen again, but stay the default of whoever has them.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Always five, Monday to Friday, as they are now (from today).</summary>
    public List<WeekTemplateDayResponse> Days { get; set; } = [];

    /// <summary>How many people have it as their default now.</summary>
    public int StaffCount { get; set; }

    /// <summary>Whether it can be deleted: true only if it has never been anyone's default.</summary>
    public bool CanBeDeleted { get; set; }

    /// <summary>Whether the signed-in user can change it: its owner, or an admin.</summary>
    public bool CanEdit { get; set; }
}

public sealed class WeekTemplateDayResponse
{
    /// <summary>"Monday" to "Friday".</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    /// <summary>Null when the day has no day template.</summary>
    public Guid? DayTemplateId { get; set; }

    public string? DayTemplateName { get; set; }
}
