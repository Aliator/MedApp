using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// What replacing a set of dated rows from today does (see <see cref="TemplateHistory.Replace{TRow,TValue}"/>): rows
/// to delete, rows to close (end yesterday, <see cref="ClosedValidTo"/>), and new rows from today. The rest are kept.
/// </summary>
public sealed record TemplateHistoryChange<TRow>(
    IReadOnlyList<TRow> Delete,
    IReadOnlyList<TRow> Close,
    IReadOnlyList<TRow> Add,
    DateOnly ClosedValidTo)
    where TRow : IDatedRow
{
    public bool HasChanges => Delete.Count > 0 || Close.Count > 0 || Add.Count > 0;
}

/// <summary>
/// Template history is kept by dating rows, not by copying them (a day template's blocks, a week template's days and
/// a person's default week). Changing a set from today keeps the rows that stay the same; a row that's no longer
/// wanted ends the day before today, so past days still project it, unless it only started today, when it's simply
/// deleted (a row added today and changed again today is just replaced). New values start today.
/// </summary>
public static class TemplateHistory
{
    /// <summary>
    /// The change that makes <paramref name="desired"/> the set in force from <paramref name="today"/>.
    /// </summary>
    /// <param name="currentRows">The rows in force today or later (others are ignored).</param>
    /// <param name="desired">The values wanted from today; equal values each match one row.</param>
    /// <param name="valueOf">A row's value, to compare with the wanted ones.</param>
    /// <param name="create">A new row for a value, valid from the date given.</param>
    /// <param name="comparer">How values compare (by default, their own equality).</param>
    public static TemplateHistoryChange<TRow> Replace<TRow, TValue>(
        IEnumerable<TRow> currentRows,
        IEnumerable<TValue> desired,
        DateOnly today,
        Func<TRow, TValue> valueOf,
        Func<TValue, DateOnly, TRow> create,
        IEqualityComparer<TValue>? comparer = null)
        where TRow : IDatedRow
    {
        comparer ??= EqualityComparer<TValue>.Default;
        var unmatched = desired.ToList();
        var delete = new List<TRow>();
        var close = new List<TRow>();

        foreach (var row in currentRows.Where(r => r.ValidTo is null || r.ValidTo >= today).OrderBy(r => r.ValidFrom))
        {
            var value = valueOf(row);
            var match = unmatched.FindIndex(v => comparer.Equals(v, value));
            if (match >= 0)
            {
                unmatched.RemoveAt(match);
                continue;
            }

            if (row.ValidFrom >= today)
                delete.Add(row);
            else
                close.Add(row);
        }

        return new TemplateHistoryChange<TRow>(
            delete,
            close,
            unmatched.Select(value => create(value, today)).ToList(),
            today.AddDays(-1));
    }
}
