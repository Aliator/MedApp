using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

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
    /// A patient's record holding one of each kind of thing about them, with the pathways given. With a pathway, it
    /// has one appointment, for the first pathway's first stage, with notes, cancelled with a reason, and its history.
    /// </summary>
    /// <summary>The record with these note versions.</summary>
    public static PatientRecord WithNoteVersions(PatientRecord record, params PatientPathwayNoteVersion[] versions) => new()
    {
        Patient = record.Patient,
        Pathways = record.Pathways,
        PathwayHistory = record.PathwayHistory,
        NoteVersions = versions,
        NoteDeletionRequests = record.NoteDeletionRequests,
        Activity = record.Activity,
        Appointments = record.Appointments,
        AppointmentHistory = record.AppointmentHistory,
        Notifications = record.Notifications,
        Attachments = record.Attachments,
        Referrals = [],
        ReferralHistory = [],
        Letters = []
    };

    /// <summary>The record with these requests to delete notes.</summary>
    public static PatientRecord WithNoteDeletionRequests(PatientRecord record, params PathwayNoteDeletionRequest[] requests) => new()
    {
        Patient = record.Patient,
        Pathways = record.Pathways,
        PathwayHistory = record.PathwayHistory,
        NoteVersions = record.NoteVersions,
        NoteDeletionRequests = requests,
        Activity = record.Activity,
        Appointments = record.Appointments,
        AppointmentHistory = record.AppointmentHistory,
        Notifications = record.Notifications,
        Attachments = record.Attachments,
        Referrals = [],
        ReferralHistory = [],
        Letters = []
    };

    public static PatientRecord CreateRecord(Patient? patient = null, params PatientPathway[] pathways)
    {
        patient ??= PatientsTestConstants.CreateValidPatient();
        foreach (var pathway in pathways)
        {
            pathway.Patient = patient;
            pathway.PatientId = patient.Id;
        }

        var appointments = pathways.Take(1).Select(p => CreateAppointment(patient, p)).ToList();

        return new PatientRecord
        {
            Patient = patient,
            Pathways = pathways,
            PathwayHistory = pathways
                .Select(p => new PatientPathwayAuditEntry
                {
                    Id = Guid.NewGuid(),
                    PatientPathwayId = p.Id,
                    PathwayTitle = p.Title,
                    PatientId = patient.Id,
                    Action = PatientPathwayAuditAction.Created,
                    UserId = PathwaysTestConstants.Staff.UserId,
                    Username = PathwaysTestConstants.Staff.Username,
                    AtUtc = PathwaysTestConstants.Now.AddDays(-7),
                    Details = """{"template":"Test template"}"""
                })
                .ToList(),
            Activity =
            [
                new ActivityEntry
                {
                    Id = Guid.NewGuid(),
                    AtUtc = PathwaysTestConstants.Now.AddDays(-1),
                    Action = ActivityAction.PatientViewed,
                    UserId = PathwaysTestConstants.Staff.UserId,
                    Username = PathwaysTestConstants.Staff.Username,
                    PatientId = patient.Id
                }
            ],
            Appointments = appointments,
            AppointmentHistory = appointments.SelectMany(a => CreateAppointmentHistory(a, pathways[0])).ToList(),
            Notifications = pathways
                .Select(p => new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = PathwaysTestConstants.Staff.UserId,
                    Kind = NotificationKind.PathwayAssigned,
                    Title = p.Title,
                    Message = "test.admin assigned you to this pathway.",
                    PathwayId = p.Id,
                    CreatedAtUtc = PathwaysTestConstants.Now.AddDays(-6)
                })
                .ToList(),
            NoteVersions = [],
            NoteDeletionRequests = [],
            Attachments = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = []
        };
    }

    /// <summary>An appointment for the pathway's first stage, with notes, cancelled with a reason.</summary>
    private static Appointment CreateAppointment(Patient patient, PatientPathway pathway) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = patient.Id,
        Patient = patient,
        PathwayId = pathway.Id,
        StageId = pathway.Stages.OrderBy(s => s.StageOrder).FirstOrDefault()?.Id,
        Status = AppointmentStatus.Cancelled,
        Notes = NotesText,
        CancelReason = Reason,
        CreatedAtUtc = PathwaysTestConstants.Now.AddDays(-5),
        CreatedByUserId = PathwaysTestConstants.Staff.UserId,
        LastUpdated = PathwaysTestConstants.Now.AddDays(-4)
    };

    /// <summary>The appointment's notes being written, then its cancellation.</summary>
    private static IEnumerable<AppointmentEvent> CreateAppointmentHistory(Appointment appointment, PatientPathway pathway) =>
    [
        new AppointmentEvent
        {
            Id = Guid.NewGuid(), AppointmentId = appointment.Id, PatientId = appointment.PatientId, PathwayId = pathway.Id,
            PathwayTitle = pathway.Title, Action = AppointmentEventAction.NotesUpdated,
            UserId = PathwaysTestConstants.Staff.UserId, Username = PathwaysTestConstants.Staff.Username,
            AtUtc = PathwaysTestConstants.Now.AddDays(-5),
            Details = $$"""{"from":"","to":"{{NotesText}}"}"""
        },
        new AppointmentEvent
        {
            Id = Guid.NewGuid(), AppointmentId = appointment.Id, PatientId = appointment.PatientId, PathwayId = pathway.Id,
            PathwayTitle = pathway.Title, Action = AppointmentEventAction.Cancelled,
            UserId = PathwaysTestConstants.Staff.UserId, Username = PathwaysTestConstants.Staff.Username,
            AtUtc = PathwaysTestConstants.Now.AddDays(-4),
            Details = $$"""{"reason":"{{Reason}}","automatic":false}"""
        }
    ];

    /// <summary>A retention candidate, last changed on <paramref name="lastUpdatedUtc"/>.</summary>
    public static RetentionCandidate CreateCandidate(
        DateOnly dateOfBirth,
        DateTime lastUpdatedUtc,
        DateTime? lastPathwayChangedUtc = null,
        DateOnly? keptUntil = null,
        string firstName = "Test",
        string lastName = "Patient") =>
        new(Guid.NewGuid(), firstName, lastName, dateOfBirth, lastUpdatedUtc, lastPathwayChangedUtc, keptUntil,
            keptUntil is null ? null : Reason);
}
