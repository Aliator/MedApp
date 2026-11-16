using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Domain.Appointments;

/// <summary>
/// A patient seeing a member of staff, optionally for one of their procedures and one of its stages. It goes from
/// Requested (needs booking) to Booked (a member of staff and a time), then Attended or Did not attend, or Cancelled.
/// Every change is recorded in its history (<see cref="AppointmentEvent"/>).
/// </summary>
public sealed class Appointment
{
    public Guid Id { get; init; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The procedure it's for, if any. Set to none when the procedure is deleted (its history keeps it).</summary>
    public Guid? ProcedureId { get; set; }
    public PatientProcedure? Procedure { get; set; }

    /// <summary>The procedure stage it's for, if any: a stage with a visit (see <see cref="VisitRequirement"/>).</summary>
    public Guid? StageId { get; set; }
    public PatientProcedureStage? Stage { get; set; }

    /// <summary>Who the patient sees; null while Requested. No foreign key to the users table.</summary>
    public Guid? StaffUserId { get; set; }

    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    public AppointmentStatus Status { get; set; }

    /// <summary>Free text about the patient, treated like procedure notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Why it was cancelled, once it is.</summary>
    public string? CancelReason { get; set; }

    /// <summary>Requested by the app, when a stage with a visit became current, rather than by someone.</summary>
    public bool IsAutomatic { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>Who asked for it (for an automatic one, whoever made the change that caused it).</summary>
    public Guid CreatedByUserId { get; init; }

    public DateTime? BookedAtUtc { get; set; }
    public Guid? BookedByUserId { get; set; }

    /// <summary>When and by whom Attended or Did not attend was last recorded.</summary>
    public DateTime? OutcomeAtUtc { get; set; }
    public Guid? OutcomeByUserId { get; set; }

    public DateTime LastUpdated { get; set; }
}
