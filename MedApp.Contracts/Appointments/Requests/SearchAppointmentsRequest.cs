namespace MedApp.Contracts.Appointments.Requests;

/// <summary>Query string for <c>GET api/appointments/search</c>. Every filter is optional.</summary>
public sealed class SearchAppointmentsRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>
    /// <c>start</c> (the default; Requested ones, which have none, last), <c>requested</c> (when it was asked for),
    /// <c>dueDate</c> (the pathway's; none last) or <c>patient</c>.
    /// </summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;

    public Guid? PatientId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? StageId { get; set; }

    public Guid? StaffUserId { get; set; }

    /// <summary>Any of these statuses: Requested, Booked, Attended, DidNotAttend or Cancelled.</summary>
    public List<string>? Statuses { get; set; }

    /// <summary>Starting on or after this UK date (dates, not times: a query string can't carry a UTC time safely).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Starting on or before this UK date.</summary>
    public DateOnly? To { get; set; }

    /// <summary>The patient's full name contains this, ignoring case.</summary>
    public string? Search { get; set; }
}
