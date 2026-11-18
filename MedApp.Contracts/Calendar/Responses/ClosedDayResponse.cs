namespace MedApp.Contracts.Calendar.Responses;

/// <summary>A day the clinic is closed (a bank holiday, a staff training day): nothing new can go on it.</summary>
public sealed class ClosedDayResponse
{
    public Guid Id { get; set; }

    /// <summary>The UK date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>What it is, e.g. "Christmas Day".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Before today (UK): it can't be removed, as past days don't change.</summary>
    public bool IsPast { get; set; }
}
