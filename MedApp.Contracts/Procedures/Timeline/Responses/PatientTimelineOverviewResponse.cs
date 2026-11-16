namespace MedApp.Contracts.Procedures.Timeline.Responses;

/// <summary>What sits above a patient's timeline: what's due next, and the procedures to filter it by.</summary>
public sealed class PatientTimelineOverviewResponse
{
    /// <summary>The soonest due procedures that aren't closed (overdue first, as they're due soonest).</summary>
    public List<PatientTimelineUpcomingResponse> Upcoming { get; set; } = [];

    /// <summary>How many procedures aren't closed, of which <see cref="Upcoming"/> are the first few.</summary>
    public int OpenCount { get; set; }

    /// <summary>
    /// Every procedure in the patient's history, newest first: those that exist, then (admins only) deleted ones.
    /// </summary>
    public List<PatientTimelineProcedureResponse> Procedures { get; set; } = [];
}
