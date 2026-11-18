namespace MedApp.Contracts.Referrals.Responses;

/// <summary>One pause of a referral's waiting-time clock: why, when it's to be reviewed, and who started and ended it.</summary>
public sealed class ClockPauseResponse
{
    public Guid Id { get; set; }

    public DateOnly PausedOn { get; set; }

    public DateTime PausedAtUtc { get; set; }

    public string PausedByUsername { get; set; } = string.Empty;

    /// <summary>PatientChoseToWait, PatientUnavailable or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why (free text, shown on the referral's and pathway's pages only).</summary>
    public string? Detail { get; set; }

    public DateOnly ReviewOn { get; set; }

    /// <summary>When it ended; null while it lasts.</summary>
    public DateOnly? ResumedOn { get; set; }

    public string? ResumedByUsername { get; set; }

    /// <summary>Whole days it lasted, once ended.</summary>
    public int Days { get; set; }
}
