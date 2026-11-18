using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Attachments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// Everything MedApp holds about one patient, from every table: what a subject access export contains, and what
/// anonymising the patient changes. Every list is required, so code that builds one can't leave a table out; to change
/// one list, copy the rest with a <c>with</c> expression.
/// </summary>
public sealed record PatientRecord
{
    public required Patient Patient { get; init; }

    /// <summary>Their pathways, with stages (in order), assignments and notes.</summary>
    public required IReadOnlyList<PatientPathway> Pathways { get; init; }

    /// <summary>The pathway audit entries with the patient's ID, oldest first, including those of deleted pathways.</summary>
    public required IReadOnlyList<PatientPathwayAuditEntry> PathwayHistory { get; init; }

    /// <summary>
    /// Every version of every note on the patient's pathways, oldest first, including those of pathways since
    /// deleted (versions have no foreign keys and outlive them).
    /// </summary>
    public required IReadOnlyList<PatientPathwayNoteVersion> NoteVersions { get; init; }

    /// <summary>
    /// Requests from staff to delete notes on the patient's pathways, oldest first, with their reasons, including those
    /// about pathways since deleted (they have no foreign keys).
    /// </summary>
    public required IReadOnlyList<PathwayNoteDeletionRequest> NoteDeletionRequests { get; init; }

    /// <summary>The activity log entries about the patient, oldest first.</summary>
    public required IReadOnlyList<ActivityEntry> Activity { get; init; }

    /// <summary>The patient's appointments, in any status, oldest first.</summary>
    public required IReadOnlyList<Appointment> Appointments { get; init; }

    /// <summary>Every change to the patient's appointments, oldest first.</summary>
    public required IReadOnlyList<AppointmentEvent> AppointmentHistory { get; init; }

    /// <summary>Notifications about any of the patient's pathways (including deleted ones) or appointments.</summary>
    public required IReadOnlyList<Notification> Notifications { get; init; }

    /// <summary>
    /// The files attached to the patient, their pathways and stages, oldest first, deleted ones included (their
    /// records, and bytes, are kept until the purge). Their bytes are in the file store, not here.
    /// </summary>
    public required IReadOnlyList<Attachment> Attachments { get; init; }
}
