namespace MedApp.Domain.Calendar;

/// <summary>
/// A template row that applies over a range of UK dates, so a template keeps its history: editing it closes the rows
/// that changed (they end the day before) and adds new ones from that day, and a past day projects the rows valid then.
/// </summary>
public interface IDatedRow
{
    /// <summary>The first UK date it applies to.</summary>
    DateOnly ValidFrom { get; }

    /// <summary>The last UK date it applies to; null while it still applies.</summary>
    DateOnly? ValidTo { get; set; }
}

public static class DatedRows
{
    /// <summary>Whether the row applies on a UK date.</summary>
    public static bool IsValidOn(this IDatedRow row, DateOnly date) =>
        row.ValidFrom <= date && (row.ValidTo is null || row.ValidTo >= date);
}
