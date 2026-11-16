namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Query string for <c>GET api/calendar</c>: one person's calendar over a range of UK dates (at most 31 days).</summary>
public sealed class GetCalendarRequest
{
    /// <summary>Whose calendar; the signed-in user's when left out.</summary>
    public Guid? StaffUserId { get; set; }

    /// <summary>The first UK date.</summary>
    public DateOnly From { get; set; }

    /// <summary>The last UK date (inclusive).</summary>
    public DateOnly To { get; set; }
}
