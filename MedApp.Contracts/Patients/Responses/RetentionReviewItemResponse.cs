namespace MedApp.Contracts.Patients.Responses;

/// <summary>A patient whose record is past its minimum retention period, for an admin to review.</summary>
public sealed class RetentionReviewItemResponse
{
    public Guid PatientId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }

    /// <summary>The conclusion of treatment: the later of the last pathway closing and the record last changing (UK date).</summary>
    public DateOnly ConclusionOfTreatment { get; set; }

    /// <summary>Adult or Child, which sets the period.</summary>
    public string RetentionClass { get; set; } = string.Empty;

    /// <summary>When the minimum period ended.</summary>
    public DateOnly DueOn { get; set; }

    /// <summary>If an admin decided to keep it, the date to review it again.</summary>
    public DateOnly? KeptUntil { get; set; }

    public string? KeptReason { get; set; }
}
