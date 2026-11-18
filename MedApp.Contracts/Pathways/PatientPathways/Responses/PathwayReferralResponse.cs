using MedApp.Contracts.Referrals.Responses;

namespace MedApp.Contracts.Pathways.PatientPathways.Responses;

/// <summary>
/// The referral a pathway was accepted from, for the pathway's page: a link back, and its waiting-time clock (the
/// "Referral and wait" card), with what the viewer may do to the clock.
/// </summary>
public sealed class PathwayReferralResponse
{
    public Guid Id { get; set; }

    /// <summary>When the referral arrived (a UK date).</summary>
    public DateOnly ReceivedOn { get; set; }

    /// <summary>The referral's team.</summary>
    public string TeamName { get; set; } = string.Empty;

    /// <summary>Pass back as <c>expectedVersion</c> to pause or resume the clock.</summary>
    public int Version { get; set; }

    /// <summary>The waiting-time clock.</summary>
    public WaitingClockResponse? Clock { get; set; }

    /// <summary>The clock's pauses, oldest first.</summary>
    public List<ClockPauseResponse> Pauses { get; set; } = [];

    /// <summary>The viewer may pause the clock: it runs, the referral was accepted, and they can work on pathways.</summary>
    public bool CanPauseClock { get; set; }

    /// <summary>The viewer may resume it: it's paused, the record can change, and they can work on pathways.</summary>
    public bool CanResumeClock { get; set; }
}
