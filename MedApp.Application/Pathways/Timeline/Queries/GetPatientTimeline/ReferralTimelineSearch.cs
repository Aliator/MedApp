using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// The referrals side of <see cref="GetPatientTimelineQuery"/>: a patient's referral history, as plain LINQ that EF
/// translates (mirroring <see cref="AppointmentTimelineSearch"/>), so the same code runs in SQL and in unit tests.
/// </summary>
public static class ReferralTimelineSearch
{
    /// <summary>
    /// The page's entries, newest first, then by ID, taking one more than the page size. Every entry is of the
    /// Referrals kind, so a kinds filter without it leaves nothing (the handler doesn't ask then). Filtering by pathways
    /// keeps only entries for those pathways (a referral has one once it's accepted). Only the full view
    /// (<see cref="GetPatientTimelineQuery.FullView"/>) gets the free text the referral holds now (its reason, referring
    /// practice and the detail given when it was closed); staff get none of it.
    /// </summary>
    public static IQueryable<ReferralTimelineRow> Page(
        IQueryable<ReferralEvent> events,
        IQueryable<Referral> referrals,
        GetPatientTimelineQuery query)
    {
        var patientId = query.PatientId;
        var filtered = events.Where(e => e.PatientId == patientId);

        var kinds = PatientTimelineKinds.Parse(query.Kinds);
        if (kinds.Count > 0 && !kinds.Contains(PatientTimelineKind.Referrals))
            filtered = filtered.Where(e => false);

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
                referrals.Where(r => r.Id == e.ReferralId).Select(r => r.DecisionDetail).FirstOrDefault()))
            : page.Select(e => new ReferralTimelineRow(e, null, null, null));
    }
}
