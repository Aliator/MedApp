using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;

/// <summary>The filtering and sorting behind <see cref="SearchPathwayAuditQuery"/>, as plain LINQ (see <c>PatientSearch</c>).</summary>
public static class PathwayAuditSearch
{
    /// <summary>Entries matching every filter given. <c>From</c> and <c>To</c> are whole UK days, inclusive.</summary>
    public static IQueryable<PatientPathwayAuditEntry> Filter(
        this IQueryable<PatientPathwayAuditEntry> entries,
        SearchPathwayAuditQuery query)
    {
        if (query.PathwayId is { } pathwayId)
            entries = entries.Where(e => e.PatientPathwayId == pathwayId);

        if (query.PatientId is { } patientId)
            entries = entries.Where(e => e.PatientId == patientId);

        if (query.UserId is { } userId)
            entries = entries.Where(e => e.UserId == userId);

        if (PathwayEnums.TryParse<PatientPathwayAuditAction>(query.Action, out var action))
            entries = entries.Where(e => e.Action == action);

        var actions = (query.Actions ?? [])
            .Select(s => PathwayEnums.TryParse<PatientPathwayAuditAction>(s, out var a) ? a : (PatientPathwayAuditAction?)null)
            .OfType<PatientPathwayAuditAction>()
            .Distinct()
            .ToList();
        if (actions.Count > 0)
            entries = entries.Where(e => actions.Contains(e.Action));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            entries = entries.Where(e =>
                e.PathwayTitle.ToLower().Contains(term)
                || e.Username.ToLower().Contains(term)
                || (e.StageName != null && e.StageName.ToLower().Contains(term)));
        }

        if (query.From is { } from)
        {
            var fromUtc = DueDates.StartOfDayUtc(from);
            entries = entries.Where(e => e.AtUtc >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = DueDates.EndOfDayUtc(to);
            entries = entries.Where(e => e.AtUtc <= toUtc);
        }

        return entries;
    }

    /// <summary>Newest first by default; ties broken by ID so paging is stable.</summary>
    public static IOrderedQueryable<PatientPathwayAuditEntry> Sort(
        this IQueryable<PatientPathwayAuditEntry> entries,
        bool ascending) =>
        ascending
            ? entries.OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            : entries.OrderByDescending(e => e.AtUtc).ThenBy(e => e.Id);
}
