namespace MedApp.Contracts.Calendar.Requests;

public sealed class GetClosedDaysRequest
{
    /// <summary>The first UK date to list, if any.</summary>
    public DateOnly? From { get; set; }

    /// <summary>The last UK date to list, if any.</summary>
    public DateOnly? To { get; set; }
}
