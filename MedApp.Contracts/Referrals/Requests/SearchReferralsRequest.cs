namespace MedApp.Contracts.Referrals.Requests;

/// <summary>Query string for <c>GET api/referrals/search</c>. Every filter is optional.</summary>
public sealed class SearchReferralsRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>
    /// <c>receivedOn</c> (the default: oldest first, so the longest wait is at the top), <c>patient</c>,
    /// <c>team</c>, <c>priority</c>, <c>registered</c>, <c>daysWaited</c> (the clock), <c>targetOn</c> or
    /// <c>reviewOn</c> (the open pause's review date; clocks not paused last).
    /// </summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;

    /// <summary>Any of these statuses: AwaitingTriage, Accepted, Rejected, RedirectedElsewhere, Withdrawn, EnteredInError.</summary>
    public List<string>? Statuses { get; set; }

    /// <summary>Any of these teams.</summary>
    public List<Guid>? TeamIds { get; set; }

    /// <summary>Only the teams the caller belongs to (all, if they belong to none).</summary>
    public bool Mine { get; set; }

    public Guid? PatientId { get; set; }

    /// <summary>The patient's full name contains this, ignoring case.</summary>
    public string? Search { get; set; }

    /// <summary>Only referrals whose waiting-time clock is past its target.</summary>
    public bool BreachedOnly { get; set; }

    /// <summary>True: only paused clocks; false: only clocks not paused.</summary>
    public bool? Paused { get; set; }

    /// <summary>True: only stopped clocks; false: only clocks still running or paused.</summary>
    public bool? ClockStopped { get; set; }

    /// <summary>
    /// The clinic picker: only this team's referrals (none: all the caller's). Anyone but an admin may pick only one of
    /// their own teams (400 otherwise).
    /// </summary>
    public Guid? TeamId { get; set; }
}
