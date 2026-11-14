using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;

/// <summary>The filtering and sorting behind <see cref="SearchProcedureAuditQuery"/>, as plain LINQ (see <c>PatientSearch</c>).</summary>
public static class ProcedureAuditSearch
{
    /// <summary>Entries matching every filter given. <c>From</c> and <c>To</c> are whole UK days, inclusive.</summary>
    public static IQueryable<PatientProcedureAuditEntry> Filter(
        this IQueryable<PatientProcedureAuditEntry> entries,
        SearchProcedureAuditQuery query)
    {
        if (query.ProcedureId is { } procedureId)
            entries = entries.Where(e => e.PatientProcedureId == procedureId);

        if (query.PatientId is { } patientId)
            entries = entries.Where(e => e.PatientId == patientId);

        if (query.UserId is { } userId)
            entries = entries.Where(e => e.UserId == userId);

        if (ProcedureEnums.TryParse<PatientProcedureAuditAction>(query.Action, out var action))
            entries = entries.Where(e => e.Action == action);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            entries = entries.Where(e =>
                e.ProcedureTitle.ToLower().Contains(term)
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
    public static IOrderedQueryable<PatientProcedureAuditEntry> Sort(
        this IQueryable<PatientProcedureAuditEntry> entries,
        bool ascending) =>
        ascending
            ? entries.OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            : entries.OrderByDescending(e => e.AtUtc).ThenBy(e => e.Id);
}
