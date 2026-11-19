using MedApp.Contracts.Referrals.Responses;

namespace MedApp.Contracts.Pathways.PatientPathways.Responses;

/// <summary>A pathway as a table row: lists, a patient's pathways, and task lists.</summary>
public sealed class PatientPathwaySummaryResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public DateTime DueDateUtc { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public int CompletedStageCount { get; set; }

    /// <summary>How many stages were skipped (an optional stage, or another branch chosen).</summary>
    public int SkippedStageCount { get; set; }
    public int StageCount { get; set; }

    /// <summary>The names of the stages being worked on ("A and B" for routes side by side), or null when none is.</summary>
    public string? CurrentStageName { get; set; }

    /// <summary>The teams that have its current stages (MC7), as names ("Dermatology and Lab"), or null with none.</summary>
    public string? CurrentStageTeamName { get; set; }

    /// <summary>Whether one of its current stages was handed over and not picked up yet.</summary>
    public bool AwaitingPickUp { get; set; }

    public List<string> Assignees { get; set; } = [];
    public DateTime LastUpdated { get; set; }

    /// <summary>Its referral's waiting-time clock, for the Wait column; null for a pathway started without a referral.</summary>
    public WaitingClockResponse? Clock { get; set; }
}
