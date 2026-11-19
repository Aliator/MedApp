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

    /// <summary>
    /// <c>didNotAttend</c>: the Did not attend list, appointments marked Did not attend with no later appointment for the
    /// same stage (or pathway) and no decision of no further appointment, each with how many were missed on its pathway.
    /// </summary>
    public string? View { get; set; }

    /// <summary>
    /// The clinic picker: only this team's appointments (by the appointment's own team). Admins may pick any team;
    /// anyone else only one of their own (another is a 400). All the viewer's teams when left out.
    /// </summary>
    public Guid? TeamId { get; set; }
}
