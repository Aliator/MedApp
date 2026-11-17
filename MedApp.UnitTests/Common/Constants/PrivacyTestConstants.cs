using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic data for the privacy features: flags, anonymisation, exports and retention.</summary>
public static class PrivacyTestConstants
{
    public const string Reason = "Patient asked for this.";
    public const string NotesText = "Synthetic clinical note.";

    public static readonly DateTime FlaggedAtUtc = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>A patient whose record is restricted.</summary>
    public static Patient CreateRestrictedPatient()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = FlaggedAtUtc;
        patient.RestrictionReason = Reason;
        return patient;
    }

    /// <summary>A patient whose record has been anonymised.</summary>
    public static Patient CreateAnonymisedPatient()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AnonymisedAtUtc = FlaggedAtUtc;
        return patient;
    }

    /// <summary>
    /// A patient's record holding one of each kind of thing about them, with the procedures given. With a procedure, it
    /// has one appointment, for the first procedure's first stage, with notes, cancelled with a reason, and its history.
    /// </summary>
    /// <summary>The record with these note versions.</summary>
    public static PatientRecord WithNoteVersions(PatientRecord record, params PatientProcedureNoteVersion[] versions) => new()
    {
        Patient = record.Patient,
        Procedures = record.Procedures,
        ProcedureHistory = record.ProcedureHistory,
        NoteVersions = versions,
        Activity = record.Activity,
        Appointments = record.Appointments,
        AppointmentHistory = record.AppointmentHistory,
        Notifications = record.Notifications,
        Attachments = record.Attachments
    };

    public static PatientRecord CreateRecord(Patient? patient = null, params PatientProcedure[] procedures)
    {
        patient ??= PatientsTestConstants.CreateValidPatient();
        foreach (var procedure in procedures)
        {
            procedure.Patient = patient;
            procedure.PatientId = patient.Id;
        }

        var appointments = procedures.Take(1).Select(p => CreateAppointment(patient, p)).ToList();

        return new PatientRecord
        {
            Patient = patient,
            Procedures = procedures,
            ProcedureHistory = procedures
                .Select(p => new PatientProcedureAuditEntry
                {
                    Id = Guid.NewGuid(),
                    PatientProcedureId = p.Id,
                    ProcedureTitle = p.Title,
                    PatientId = patient.Id,
                    Action = PatientProcedureAuditAction.Created,
                    UserId = ProceduresTestConstants.Staff.UserId,
                    Username = ProceduresTestConstants.Staff.Username,
                    AtUtc = ProceduresTestConstants.Now.AddDays(-7),
                    Details = """{"template":"Test template"}"""
                })
                .ToList(),
            Activity =
            [
                new ActivityEntry
                {
                    Id = Guid.NewGuid(),
                    AtUtc = ProceduresTestConstants.Now.AddDays(-1),
                    Action = ActivityAction.PatientViewed,
                    UserId = ProceduresTestConstants.Staff.UserId,
                    Username = ProceduresTestConstants.Staff.Username,
                    PatientId = patient.Id
                }
            ],
            Appointments = appointments,
            AppointmentHistory = appointments.SelectMany(a => CreateAppointmentHistory(a, procedures[0])).ToList(),
            Notifications = procedures
                .Select(p => new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = ProceduresTestConstants.Staff.UserId,
                    Kind = NotificationKind.ProcedureAssigned,
                    Title = p.Title,
                    Message = "test.admin assigned you to this procedure.",
                    ProcedureId = p.Id,
                    CreatedAtUtc = ProceduresTestConstants.Now.AddDays(-6)
                })
                .ToList()
        };
    }

    /// <summary>An appointment for the procedure's first stage, with notes, cancelled with a reason.</summary>
    private static Appointment CreateAppointment(Patient patient, PatientProcedure procedure) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = patient.Id,
        Patient = patient,
        ProcedureId = procedure.Id,
        StageId = procedure.Stages.OrderBy(s => s.StageOrder).FirstOrDefault()?.Id,
        Status = AppointmentStatus.Cancelled,
        Notes = NotesText,
        CancelReason = Reason,
        CreatedAtUtc = ProceduresTestConstants.Now.AddDays(-5),
        CreatedByUserId = ProceduresTestConstants.Staff.UserId,
        LastUpdated = ProceduresTestConstants.Now.AddDays(-4)
    };

    /// <summary>The appointment's notes being written, then its cancellation.</summary>
    private static IEnumerable<AppointmentEvent> CreateAppointmentHistory(Appointment appointment, PatientProcedure procedure) =>
    [
        new AppointmentEvent
        {
            Id = Guid.NewGuid(), AppointmentId = appointment.Id, PatientId = appointment.PatientId, ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title, Action = AppointmentEventAction.NotesUpdated,
            UserId = ProceduresTestConstants.Staff.UserId, Username = ProceduresTestConstants.Staff.Username,
            AtUtc = ProceduresTestConstants.Now.AddDays(-5),
            Details = $$"""{"from":"","to":"{{NotesText}}"}"""
        },
        new AppointmentEvent
        {
            Id = Guid.NewGuid(), AppointmentId = appointment.Id, PatientId = appointment.PatientId, ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title, Action = AppointmentEventAction.Cancelled,
            UserId = ProceduresTestConstants.Staff.UserId, Username = ProceduresTestConstants.Staff.Username,
            AtUtc = ProceduresTestConstants.Now.AddDays(-4),
            Details = $$"""{"reason":"{{Reason}}","automatic":false}"""
        }
    ];

    /// <summary>A retention candidate, last changed on <paramref name="lastUpdatedUtc"/>.</summary>
    public static RetentionCandidate CreateCandidate(
        DateOnly dateOfBirth,
        DateTime lastUpdatedUtc,
        DateTime? lastProcedureChangedUtc = null,
        DateOnly? keptUntil = null,
        string firstName = "Test",
        string lastName = "Patient") =>
        new(Guid.NewGuid(), firstName, lastName, dateOfBirth, lastUpdatedUtc, lastProcedureChangedUtc, keptUntil,
            keptUntil is null ? null : Reason);
}
