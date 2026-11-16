namespace MedApp.Contracts.Procedures.Timeline.Requests;

/// <summary>
/// Query string for <c>GET api/patients/{id}/timeline</c>: one page of the patient's timeline, newest first. Every
/// filter is optional.
/// </summary>
public sealed class GetPatientTimelineRequest
{
    /// <summary>The previous page's <c>NextCursor</c>, for the page after it; leave it out for the newest.</summary>
    public string? Before { get; set; }

    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Any of these kinds: Created, Stages, Notes, Assignments, Blocks, Status, DueDates, Details, Deleted or
    /// Appointments.
    /// </summary>
    public List<string>? Kinds { get; set; }

    /// <summary>Only these procedures' entries (appointments not for a procedure are left out).</summary>
    public List<Guid>? ProcedureIds { get; set; }
}
