namespace MedApp.Contracts.Calendar.Responses;

/// <summary>
/// What's booked on one day, for closing it: Booked, Attended and Did not attend appointments, stored blocks, and
/// meetings, in all and for each member of staff. Counts only, never a patient.
/// </summary>
public sealed class DayBookingsResponse
{
    public DateOnly Date { get; set; }

    public int Appointments { get; set; }

    public int Blocks { get; set; }

    public int Meetings { get; set; }

    /// <summary>Each member of staff with something on that day, by username.</summary>
    public List<DayBookingsStaffResponse> Staff { get; set; } = [];
}

public sealed class DayBookingsStaffResponse
{
    public Guid UserId { get; set; }

    /// <summary>Their username, or "Deleted user".</summary>
    public string Username { get; set; } = string.Empty;

    public int Appointments { get; set; }

    public int Blocks { get; set; }

    public int Meetings { get; set; }
}
