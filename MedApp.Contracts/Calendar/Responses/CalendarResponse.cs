using MedApp.Contracts.Appointments.Responses;

namespace MedApp.Contracts.Calendar.Responses;

/// <summary>
/// One person's calendar over a range of UK dates: each day, the blocks on it (stored, and from templates), their
/// appointments (Booked, Attended and Did not attend) and the meetings they're going to. Times are UTC; each day gives its working day in UTC, so the
/// grid steps through slots without time zone arithmetic.
/// </summary>
public sealed class CalendarResponse
{
    public Guid StaffUserId { get; set; }

    public string StaffUsername { get; set; } = string.Empty;

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    /// <summary>The grid's start on every day, UK time: "08:00" (each day's <c>gridStartUtc</c>).</summary>
    public string WorkingDayStart { get; set; } = string.Empty;

    /// <summary>The grid's end on every day, UK time: "18:00".</summary>
    public string WorkingDayEnd { get; set; } = string.Empty;

    /// <summary>The grid's rows, in minutes.</summary>
    public int SlotMinutes { get; set; }

    /// <summary>Whether the signed-in user can change this calendar's blocks, day templates and default week (owner or admin).</summary>
    public bool CanEdit { get; set; }

    /// <summary>The person's default week template today, if any.</summary>
    public Guid? DefaultWeekTemplateId { get; set; }

    public string? DefaultWeekTemplateName { get; set; }

    public List<CalendarDayResponse> Days { get; set; } = [];

    /// <summary>By start time.</summary>
    public List<CalendarBlockResponse> Blocks { get; set; } = [];

    /// <summary>By start time.</summary>
    public List<AppointmentResponse> Appointments { get; set; } = [];

    /// <summary>The meetings the person is going to, by start time.</summary>
    public List<MeetingResponse> Meetings { get; set; } = [];
}
