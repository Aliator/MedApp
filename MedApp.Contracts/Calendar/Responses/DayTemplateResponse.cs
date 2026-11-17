namespace MedApp.Contracts.Calendar.Responses;

public sealed class DayTemplateResponse
{
    public Guid Id { get; set; }

    /// <summary>The member of staff whose template it is.</summary>
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Archived day templates can't be chosen again, but keep applying where they already do.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Its blocks as they are now (from today), by start time.</summary>
    public List<DayTemplateBlockResponse> Blocks { get; set; } = [];

    /// <summary>How many week templates use it now.</summary>
    public int WeekTemplateCount { get; set; }

    /// <summary>Whether it can be deleted: true only if no week template or day override has ever used it.</summary>
    public bool CanBeDeleted { get; set; }

    /// <summary>Whether the signed-in user can change it: its owner, or an admin.</summary>
    public bool CanEdit { get; set; }
}

/// <summary>One block of a day template, at UK clock times.</summary>
public sealed class DayTemplateBlockResponse
{
    public Guid Id { get; set; }

    public Guid ActivityTypeId { get; set; }

    public string ActivityName { get; set; } = string.Empty;

    public string Colour { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Note { get; set; } = string.Empty;
}
