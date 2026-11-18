using MedApp.Application.Pathways.Common;
using MedApp.Domain.Activity;

namespace MedApp.Application.Activity.Queries.SearchActivity;

/// <summary>The filtering and sorting behind <see cref="SearchActivityQuery"/>, as plain LINQ (see <c>PatientSearch</c>).</summary>
public static class ActivitySearch
{
    public static string ActionNames => string.Join(", ", Enum.GetNames<ActivityAction>());

    public static bool IsAction(string? value) => PathwayEnums.TryParse<ActivityAction>(value, out _);

    /// <summary>Entries matching every filter given. <c>From</c> and <c>To</c> are whole UK days, inclusive.</summary>
    public static IQueryable<ActivityEntry> Filter(this IQueryable<ActivityEntry> entries, SearchActivityQuery query)
    {
        if (query.PatientId is { } patientId)
            entries = entries.Where(e => e.PatientId == patientId);

        if (query.UserId is { } userId)
            entries = entries.Where(e => e.UserId == userId);

        var actions = (query.Actions ?? [])
            .Select(s => PathwayEnums.TryParse<ActivityAction>(s, out var a) ? a : (ActivityAction?)null)
            .OfType<ActivityAction>()
            .Distinct()
            .ToList();
        if (actions.Count > 0)
            entries = entries.Where(e => actions.Contains(e.Action));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            entries = entries.Where(e =>
                e.Username.ToLower().Contains(term)
                || (e.TargetUsername != null && e.TargetUsername.ToLower().Contains(term))
                || (e.IpAddress != null && e.IpAddress.Contains(term)));
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
    public static IOrderedQueryable<ActivityEntry> Sort(this IQueryable<ActivityEntry> entries, bool ascending) =>
        ascending
            ? entries.OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            : entries.OrderByDescending(e => e.AtUtc).ThenBy(e => e.Id);
}
