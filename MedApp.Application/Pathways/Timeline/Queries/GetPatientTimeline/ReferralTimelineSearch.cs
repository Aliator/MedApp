using MedApp.Application.Common.Teams;
using MedApp.Domain.Teams;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// The referrals side of <see cref="GetPatientTimelineQuery"/>: a patient's referral history, as plain LINQ that EF
/// translates (mirroring <see cref="AppointmentTimelineSearch"/>), so the same code runs in SQL and in unit tests.
/// </summary>
public static class ReferralTimelineSearch
{
    /// <summary>The waiting-time clock's actions: the Waiting time kind.</summary>
    public static readonly IReadOnlyList<ReferralEventAction> ClockActions =
    [
        ReferralEventAction.ClockPaused,
        ReferralEventAction.ClockResumed,
        ReferralEventAction.ClockStopped,
        ReferralEventAction.TargetChanged
    ];

    /// <summary>The kind of a referral history entry: Waiting time for the clock's, Referrals for the rest.</summary>
    public static PatientTimelineKind KindOf(ReferralEventAction action) =>
        ClockActions.Contains(action) ? PatientTimelineKind.WaitingTime : PatientTimelineKind.Referrals;

    /// <summary>
    /// The page's entries, newest first, then by ID, taking one more than the page size. Every entry is of the
    /// Referrals or Waiting time kind (<see cref="KindOf"/>), so a kinds filter with neither leaves nothing (the handler
    /// doesn't ask then). Filtering by pathways
    /// keeps only entries for those pathways (a referral has one once it's accepted). Only the full view
    /// (<see cref="GetPatientTimelineQuery.FullView"/>) gets the free text the referral holds now (its reason, referring
    /// practice and the detail given when it was closed); staff get none of it.
    /// </summary>
    /// <remarks>
    /// The team walls (<see cref="GetPatientTimelineQuery.Scope"/>): a referral's entry shows when its team is the
    /// viewer's, or its pathway is one they see in full (<paramref name="grants"/>, the grants table).
    /// </remarks>
    public static IQueryable<ReferralTimelineRow> Page(
        IQueryable<ReferralEvent> events,
        IQueryable<Referral> referrals,
        GetPatientTimelineQuery query,
        IQueryable<PathwayAccess>? grants = null)
    {
        var patientId = query.PatientId;
        var filtered = events.Where(e => e.PatientId == patientId);

        if (!query.Teams.SeesAllTeams)
        {
            var visible = TeamAccess.VisiblePathwayIds(query.Teams, grants ?? throw new ArgumentNullException(nameof(grants)));
            var teamIds = query.Teams.TeamIds.ToList();
            filtered = filtered.Where(e => teamIds.Contains(e.TeamId)
                                           || (e.PathwayId != null && visible.Contains(e.PathwayId.Value)));
        }

        // Two kinds: the clock's entries are Waiting time, the rest Referrals.
        var kinds = PatientTimelineKinds.Parse(query.Kinds);
        var clockActions = ClockActions.ToList();
        var wantsReferrals = kinds.Count == 0 || kinds.Contains(PatientTimelineKind.Referrals);
        var wantsClock = kinds.Count == 0 || kinds.Contains(PatientTimelineKind.WaitingTime);
        if (!wantsReferrals && !wantsClock)
            filtered = filtered.Where(e => false);
        else if (!wantsClock)
            filtered = filtered.Where(e => !clockActions.Contains(e.Action));
        else if (!wantsReferrals)
            filtered = filtered.Where(e => clockActions.Contains(e.Action));

        var pathwayIds = (query.PathwayIds ?? []).Distinct().ToList();
        if (pathwayIds.Count > 0)
            filtered = filtered.Where(e => e.PathwayId != null && pathwayIds.Contains(e.PathwayId.Value));

        if (TimelineCursor.TryDecode(query.Before, out var cursor))
        {
            var at = cursor.AtUtc;
            var id = cursor.Id;
            filtered = filtered.Where(e => e.AtUtc < at || (e.AtUtc == at && e.Id.CompareTo(id) < 0));
        }

        var page = filtered
            .OrderByDescending(e => e.AtUtc)
            .ThenByDescending(e => e.Id)
            .Take(query.PageSize + 1);

        return query.FullView
            ? page.Select(e => new ReferralTimelineRow(
                e,
                referrals.Where(r => r.Id == e.ReferralId).Select(r => r.Reason).FirstOrDefault(),
                referrals.Where(r => r.Id == e.ReferralId).Select(r => r.ReferrerPractice).FirstOrDefault(),
                referrals.Where(r => r.Id == e.ReferralId).Select(r => r.DecisionDetail).FirstOrDefault(),
                referrals.Where(r => r.Id == e.ReferralId).Select(r => r.RedirectedTo).FirstOrDefault(),
                referrals.Where(r => r.Id == e.ReferralId).Select(r => r.RedirectDetail).FirstOrDefault()))
            : page.Select(e => new ReferralTimelineRow(e, null, null, null, null, null));
    }
}
