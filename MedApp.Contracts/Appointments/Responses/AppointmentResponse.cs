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

    public Guid? PathwayId { get; set; }

    public string? PathwayTitle { get; set; }

    /// <summary>The pathway's target date (UK), for ordering what requires an appointment.</summary>
    public DateOnly? PathwayDueDate { get; set; }

    public Guid? StageId { get; set; }

    public string? StageName { get; set; }

    /// <summary>The stage's: None, Suggested or Required.</summary>
    public string? VisitRequirement { get; set; }

    /// <summary>Requested, Booked, Attended, DidNotAttend, Cancelled or EnteredInError.</summary>
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

    /// <summary>It isn't Cancelled or Entered in error.</summary>
    public bool CanEditNotes { get; set; }

    /// <summary>
    /// It was recorded after the event and still has its outcome: an admin can mark it entered in error (the server
    /// checks who). Whoever recorded it can too while <see cref="CanRecorderMarkEnteredInError"/>.
    /// </summary>
    public bool CanMarkEnteredInError { get; set; }

    /// <summary>
    /// As <see cref="CanMarkEnteredInError"/>, and it started within the last 30 days, so whoever recorded it can mark
    /// it entered in error too, not only an admin.
    /// </summary>
    public bool CanRecorderMarkEnteredInError { get; set; }
}
