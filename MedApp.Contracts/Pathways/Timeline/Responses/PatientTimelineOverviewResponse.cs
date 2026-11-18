namespace MedApp.Contracts.Pathways.Timeline.Responses;

/// <summary>What sits above a patient's timeline: what's due next, and the pathways to filter it by.</summary>
public sealed class PatientTimelineOverviewResponse
{
    /// <summary>The soonest due pathways that aren't closed (overdue first, as they're due soonest).</summary>
    public List<PatientTimelineUpcomingResponse> Upcoming { get; set; } = [];

    /// <summary>How many pathways aren't closed, of which <see cref="Upcoming"/> are the first few.</summary>
    public int OpenCount { get; set; }

    /// <summary>
    /// Every pathway in the patient's history, newest first: those that exist, then (admins only) deleted ones.
    /// </summary>
    public List<PatientTimelinePathwayResponse> Pathways { get; set; } = [];

    /// <summary>The patient's referrals awaiting triage, oldest first.</summary>
    public List<PatientTimelineReferralResponse> AwaitingReferrals { get; set; } = [];
}
