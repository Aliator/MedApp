using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Domain.Appointments;

/// <summary>
/// A patient seeing a member of staff, optionally for one of their pathways and one of its stages. It goes from
/// Requested (Requires appointment) to Booked (a member of staff and a time), then Attended or Did not attend, or Cancelled.
/// Every change is recorded in its history (<see cref="AppointmentEvent"/>).
/// </summary>
public sealed class Appointment
{
    public Guid Id { get; init; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The pathway it's for, if any. Set to none when the pathway is deleted (its history keeps it).</summary>
    public Guid? PathwayId { get; set; }
    public PatientPathway? Pathway { get; set; }

    /// <summary>The pathway stage it's for, if any: a stage with a visit (see <see cref="VisitRequirement"/>).</summary>
    public Guid? StageId { get; set; }
    public PatientPathwayStage? Stage { get; set; }

    /// <summary>Who the patient sees; null while Requested. No foreign key to the users table.</summary>
    public Guid? StaffUserId { get; set; }

    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    public AppointmentStatus Status { get; set; }

    /// <summary>Free text about the patient, treated like pathway notes.</summary>
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

    /// <summary>
    /// Recorded after the event (a walk-in, or one booked outside MedApp), with its outcome, rather than booked ahead.
    /// <see cref="BookedAtUtc"/> and <see cref="BookedByUserId"/> are then when and by whom it was recorded.
    /// </summary>
    public bool IsRecordedAfterEvent { get; set; }

    /// <summary>When and by whom Attended or Did not attend was last recorded.</summary>
    public DateTime? OutcomeAtUtc { get; set; }
    public Guid? OutcomeByUserId { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// The database's row version, a concurrency token: a save made from a copy read before someone else saved the
    /// appointment is refused (409), so two people changing it at once can't silently overwrite each other.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
