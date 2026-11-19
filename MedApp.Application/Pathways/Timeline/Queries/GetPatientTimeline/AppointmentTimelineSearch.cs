using MedApp.Application.Common.Teams;
using MedApp.Domain.Teams;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// The appointments side of <see cref="GetPatientTimelineQuery"/>: a patient's appointment history, as plain LINQ that
/// EF translates (see <see cref="PatientTimelineSearch"/>, the audit log's side, which it mirrors), so the same code
/// runs in SQL and in unit tests.
/// </summary>
public static class AppointmentTimelineSearch
{
    /// <summary>
    /// The page's entries, newest first, then by ID, each with whether its pathway still exists and its title now,
    /// taking one more than the page size. Every entry is of the Appointments kind, so a kinds filter without it
    /// leaves nothing (the handler doesn't ask then). Filtering by pathways leaves out appointments not for one.
    /// Staff (not <see cref="GetPatientTimelineQuery.FullView"/>) only get entries not for a pathway, or for one that
    /// still exists.
    /// </summary>
    /// <remarks>
    /// The team walls (<see cref="GetPatientTimelineQuery.Scope"/>): an appointment's entry for a pathway shows when the
    /// viewer sees that pathway in full (<paramref name="grants"/>, the grants table); one for no pathway, when its team
    /// is theirs.
    /// </remarks>
    public static IQueryable<AppointmentTimelineRow> Page(
        IQueryable<AppointmentEvent> events,
        IQueryable<PatientPathway> pathways,
        GetPatientTimelineQuery query,
        IQueryable<PathwayAccess>? grants = null)
    {
        var patientId = query.PatientId;
        var filtered = events.Where(e => e.PatientId == patientId);

        if (!query.Teams.SeesAllTeams)
        {
            var visible = TeamAccess.VisiblePathwayIds(query.Teams, grants ?? throw new ArgumentNullException(nameof(grants)));
            var teamIds = query.Teams.TeamIds.ToList();
            filtered = filtered.Where(e => (e.PathwayId == null && teamIds.Contains(e.TeamId))
                                           || (e.PathwayId != null && visible.Contains(e.PathwayId.Value)));
        }

        var kinds = PatientTimelineKinds.Parse(query.Kinds);
        if (kinds.Count > 0 && !kinds.Contains(PatientTimelineKind.Appointments))
            filtered = filtered.Where(e => false);

        if (!query.FullView)
            filtered = filtered.Where(e => e.PathwayId == null || pathways.Any(p => p.Id == e.PathwayId));

        var pathwayIds = (query.PathwayIds ?? []).Distinct().ToList();
        if (pathwayIds.Count > 0)
            filtered = filtered.Where(e => e.PathwayId != null && pathwayIds.Contains(e.PathwayId.Value));

        if (TimelineCursor.TryDecode(query.Before, out var cursor))
        {
            var at = cursor.AtUtc;
            var id = cursor.Id;
            filtered = filtered.Where(e => e.AtUtc < at || (e.AtUtc == at && e.Id.CompareTo(id) < 0));
        }

        return filtered
            .OrderByDescending(e => e.AtUtc)
            .ThenByDescending(e => e.Id)
            .Take(query.PageSize + 1)
            .Select(e => new AppointmentTimelineRow(
                e,
                pathways.Any(p => p.Id == e.PathwayId),
                pathways.Where(p => p.Id == e.PathwayId).Select(p => p.Title).FirstOrDefault()));
    }
}
