using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// The query behind <see cref="GetPatientTimelineQuery"/>, as plain LINQ that EF translates (see <c>PatientSearch</c>),
/// so the same code runs in SQL and in unit tests.
/// </summary>
public static class PatientTimelineSearch
{
    /// <summary>
    /// The page's entries, newest first, then by ID so the order is unique (entries one change writes are a tick
    /// apart, <c>ProcedureChange.Audit</c>, so they already sort in the order they happened), each with its
    /// procedure's title now. It takes one more than the page size, so the handler can tell whether there's more.
    /// Staff (not <see cref="GetPatientTimelineQuery.IsAdmin"/>) only get entries for procedures that still exist.
    /// </summary>
    public static IQueryable<PatientTimelineRow> Page(
        IQueryable<PatientProcedureAuditEntry> entries,
        IQueryable<PatientProcedure> procedures,
        GetPatientTimelineQuery query)
    {
        var patientId = query.PatientId;
        var filtered = entries.Where(e => e.PatientId == patientId);

        if (!query.IsAdmin)
            filtered = filtered.Where(e => procedures.Any(p => p.Id == e.PatientProcedureId));

        var kinds = PatientTimelineKinds.Parse(query.Kinds);
        if (kinds.Count > 0)
            filtered = filtered.WhereKindIn(kinds);

        var procedureIds = (query.ProcedureIds ?? []).Distinct().ToList();
        if (procedureIds.Count > 0)
            filtered = filtered.Where(e => procedureIds.Contains(e.PatientProcedureId));

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
                procedures.Where(p => p.Id == e.PatientProcedureId).Select(p => p.Title).FirstOrDefault()));
    }
}
