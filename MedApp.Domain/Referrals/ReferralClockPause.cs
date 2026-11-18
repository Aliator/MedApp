namespace MedApp.Domain.Referrals;

/// <summary>
/// A pause in a referral's waiting-time clock, for a delay the patient chose or caused, after the referral was
/// accepted. It starts and ends on the day it's recorded (no backdating), and has a review date (at most 90 days
/// ahead), which becomes the pathway's target date while it lasts, so a forgotten pause shows as overdue (hazard
/// H-42). One open pause per referral. The detail is free text: it never reaches a staff timeline, a notification, an
/// email or a log line.
/// </summary>
public sealed class ReferralClockPause
{
    public const int MaxDetailLength = 500;

    /// <summary>The furthest ahead a review date may be.</summary>
    public const int MaxReviewDays = 90;

    public Guid Id { get; init; }

    public Guid ReferralId { get; init; }
    public Referral Referral { get; set; } = null!;

    /// <summary>The patient's ID, copied, so the export and anonymisation find it directly.</summary>
    public Guid PatientId { get; init; }

    /// <summary>The UK date it began: the clock's days stop counting from here.</summary>
    public DateOnly PausedOn { get; init; }

    public DateTime PausedAtUtc { get; init; }
    public Guid PausedByUserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted.</summary>
    public string PausedByUsername { get; init; } = string.Empty;

    public ClockPauseReason Reason { get; init; }

    /// <summary>More about why, required for Other. Free text.</summary>
    public string? Detail { get; set; }

    /// <summary>When someone must look at it again: the pathway's target date while it lasts.</summary>
    public DateOnly ReviewOn { get; init; }

    /// <summary>The UK date it ended, or null while it lasts.</summary>
    public DateOnly? ResumedOn { get; set; }

    public DateTime? ResumedAtUtc { get; set; }
    public Guid? ResumedByUserId { get; set; }
    public string? ResumedByUsername { get; set; }

    /// <summary>Whole days it lasted, once ended (a pause started and ended the same day counts 0).</summary>
    public int Days => ResumedOn is { } resumed ? resumed.DayNumber - PausedOn.DayNumber : 0;
}
