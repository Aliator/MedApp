namespace MedApp.Domain.Appointments;

/// <summary>
/// One change to an appointment: who did what, and when, with the appointment as it was after it. Written in the
/// same save as the change. Like the procedure audit log, it has no foreign keys, so it outlives the appointment,
/// procedure and accounts it's about; what it shows is copied in.
/// </summary>
public sealed class AppointmentEvent
{
    public Guid Id { get; init; }

    public Guid AppointmentId { get; init; }

    public Guid PatientId { get; init; }

    public Guid? ProcedureId { get; init; }

    /// <summary>The procedure's title at the time, or empty for an appointment not for a procedure.</summary>
    public string ProcedureTitle { get; init; } = string.Empty;

    /// <summary>The stage's name at the time, if it was for a stage.</summary>
    public string? StageName { get; init; }

    public AppointmentEventAction Action { get; init; }

    /// <summary>Who made the change.</summary>
    public Guid UserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted.</summary>
    public string Username { get; init; } = string.Empty;

    public DateTime AtUtc { get; init; }

    /// <summary>The member of staff after the change, if any.</summary>
    public Guid? StaffUserId { get; init; }

    public string? StaffUsername { get; init; }

    /// <summary>The time after the change, if booked.</summary>
    public DateTime? StartUtc { get; init; }

    public DateTime? EndUtc { get; init; }

    /// <summary>What changed, as camelCase JSON: the previous time and member of staff, a reason, notes, and so on.</summary>
    public string Details { get; init; } = "{}";
}
