using MedApp.Domain.Activity;
using MedApp.Domain.Attachments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// Everything MedApp holds about one patient, from every table: what a subject access export contains, and what
/// anonymising the patient changes.
/// </summary>
public sealed class PatientRecord
{
    public required Patient Patient { get; init; }

    /// <summary>Their procedures, with stages (in order) and assignments.</summary>
    public required IReadOnlyList<PatientProcedure> Procedures { get; init; }

    /// <summary>The procedure audit entries with the patient's ID, oldest first, including those of deleted procedures.</summary>
    public required IReadOnlyList<PatientProcedureAuditEntry> ProcedureHistory { get; init; }

    /// <summary>The activity log entries about the patient, oldest first.</summary>
    public required IReadOnlyList<ActivityEntry> Activity { get; init; }

    /// <summary>Notifications about any of the patient's procedures (including deleted ones).</summary>
    public required IReadOnlyList<Notification> Notifications { get; init; }

    /// <summary>
    /// The files attached to the patient, their procedures and stages, oldest first, deleted ones included (their
    /// records, and bytes, are kept until the purge). Their bytes are in the file store, not here.
    /// </summary>
    public IReadOnlyList<Attachment> Attachments { get; init; } = [];
}
