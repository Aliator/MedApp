using MedApp.Application.Common.Teams;
using MedApp.Domain.Teams;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// The query behind <see cref="GetPatientTimelineQuery"/>, as plain LINQ that EF translates (see <c>PatientSearch</c>),
/// so the same code runs in SQL and in unit tests.
/// </summary>
public static class PatientTimelineSearch
{
    /// <summary>
    /// The page's entries, newest first, then by ID so the order is unique (entries one change writes are a tick
    /// apart, <c>PathwayChange.Audit</c>, so they already sort in the order they happened), each with its
    /// pathway's title now. It takes one more than the page size, so the handler can tell whether there's more.
    /// Staff (not <see cref="GetPatientTimelineQuery.FullView"/>) only get entries for pathways that still exist, and
    /// for files attached to the patient themselves (<see cref="PatientPathwayAuditEntry.NoPathway"/>). Downloads
    /// are in the audit log, never on the timeline.
    /// </summary>
    /// <remarks>
    /// The team walls (<see cref="GetPatientTimelineQuery.Scope"/>): an entry about a pathway shows when the viewer sees
    /// that pathway in full (<paramref name="grants"/>, the grants table); one about no pathway, when its team is theirs.
    /// </remarks>
    public static IQueryable<PatientTimelineRow> Page(
        IQueryable<PatientPathwayAuditEntry> entries,
        IQueryable<PatientPathway> pathways,
        GetPatientTimelineQuery query,
        IQueryable<PathwayAccess>? grants = null)
    {
        var patientId = query.PatientId;
        var noPathway = PatientPathwayAuditEntry.NoPathway;
        // Downloads, and consults (MC25: the web app has no timeline kind for them yet), are in the audit log only.
        var filtered = entries.Where(e => e.PatientId == patientId
                                          && e.Action != PatientPathwayAuditAction.AttachmentDownloaded
                                          && e.Action != PatientPathwayAuditAction.ConsultRequested
                                          && e.Action != PatientPathwayAuditAction.ConsultReplied
                                          && e.Action != PatientPathwayAuditAction.ConsultClosed
                                          && e.Action != PatientPathwayAuditAction.ConsultWithdrawn);

        if (!query.Teams.SeesAllTeams)
        {
            var visible = TeamAccess.VisiblePathwayIds(query.Teams, grants ?? throw new ArgumentNullException(nameof(grants)));
            var teamIds = query.Teams.TeamIds.ToList();
            filtered = filtered.Where(e => (e.PatientPathwayId == noPathway && teamIds.Contains(e.TeamId))
                                           || (e.PatientPathwayId != noPathway && visible.Contains(e.PatientPathwayId)));
        }

        if (!query.FullView)
            filtered = filtered.Where(e => e.PatientPathwayId == noPathway || pathways.Any(p => p.Id == e.PatientPathwayId));

        var kinds = PatientTimelineKinds.Parse(query.Kinds);
        if (kinds.Count > 0)
            filtered = filtered.WhereKindIn(kinds);

        var pathwayIds = (query.PathwayIds ?? []).Distinct().ToList();
        if (pathwayIds.Count > 0)
            filtered = filtered.Where(e => pathwayIds.Contains(e.PatientPathwayId));

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
            .Select(e => new PatientTimelineRow(
                e,
                pathways.Where(p => p.Id == e.PatientPathwayId).Select(p => p.Title).FirstOrDefault()));
    }
}
