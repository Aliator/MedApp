namespace MedApp.Contracts.Appointments.Responses;

/// <summary>
/// An appointment. Requested ones have no member of staff or time yet. The <c>Can...</c> flags say what can be done
/// with it now.
/// </summary>
public sealed class AppointmentResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public Guid? ProcedureId { get; set; }

    public string? ProcedureTitle { get; set; }

    /// <summary>The procedure's due date (UK), for ordering what needs booking.</summary>
    public DateOnly? ProcedureDueDate { get; set; }

    public Guid? StageId { get; set; }

    public string? StageName { get; set; }

    /// <summary>The stage's: None, Suggested or Required.</summary>
    public string? VisitRequirement { get; set; }

    /// <summary>Requested, Booked, Attended, DidNotAttend or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public Guid? StaffUserId { get; set; }

    public string? StaffUsername { get; set; }

    public DateTime? StartUtc { get; set; }

    public DateTime? EndUtc { get; set; }

    public int? DurationMinutes { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string? CancelReason { get; set; }

    /// <summary>Requested by the app when a stage with a visit became current.</summary>
    public bool IsAutomatic { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Who asked for it (for an automatic one, whoever made the change that caused it).</summary>
    public string CreatedByUsername { get; set; } = string.Empty;

    /// <summary>
    /// Recorded after the event (a walk-in, or one booked outside MedApp) rather than booked ahead: then
    /// <see cref="BookedAtUtc"/> and <see cref="BookedByUsername"/> say when and by whom it was recorded.
    /// </summary>
    public bool IsRecordedAfterEvent { get; set; }

    public DateTime? BookedAtUtc { get; set; }

    public string? BookedByUsername { get; set; }

    public DateTime? OutcomeAtUtc { get; set; }

    public string? OutcomeByUsername { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>It's Requested: it can be booked.</summary>
    public bool CanBook { get; set; }

    /// <summary>It's Booked: it can be moved to another time or member of staff.</summary>
    public bool CanReschedule { get; set; }

    /// <summary>It's Booked (or its outcome is recorded) and has started: Attended or Did not attend can be recorded.</summary>
    public bool CanRecordOutcome { get; set; }

    /// <summary>It's Requested or Booked.</summary>
    public bool CanCancel { get; set; }

    /// <summary>It isn't Cancelled.</summary>
    public bool CanEditNotes { get; set; }
}
