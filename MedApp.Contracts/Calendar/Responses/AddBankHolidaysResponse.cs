namespace MedApp.Contracts.Calendar.Responses;

/// <summary>What adding a year's England and Wales bank holidays did: the days added, and those left out and why.</summary>
public sealed class AddBankHolidaysResponse
{
    public int Year { get; set; }

    /// <summary>The closed days added, by date.</summary>
    public List<ClosedDayResponse> Added { get; set; } = [];

    /// <summary>The bank holidays not added, by date.</summary>
    public List<SkippedBankHolidayResponse> Skipped { get; set; } = [];
}

/// <summary>A bank holiday that wasn't added.</summary>
public sealed class SkippedBankHolidayResponse
{
    public DateOnly Date { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>AlreadyClosed (the date is in the list already) or Past (past days don't change).</summary>
    public string Reason { get; set; } = string.Empty;
}
