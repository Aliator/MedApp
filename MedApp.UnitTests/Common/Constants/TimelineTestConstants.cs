using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic data for the patient timeline's tests.</summary>
public static class TimelineTestConstants
{
    public static readonly Guid PatientId = Guid.Parse("00000022-0000-0000-0000-000000000001");
    public static readonly Guid OtherPatientId = Guid.Parse("00000022-0000-0000-0000-000000000002");
    public static readonly Guid ProcedureId = Guid.Parse("00000022-0000-0000-0000-000000000003");
    public static readonly Guid DeletedProcedureId = Guid.Parse("00000022-0000-0000-0000-000000000004");

    public const string CurrentTitle = "Renamed procedure";
    public const string DeletedTitle = "Deleted procedure";
    public const string NotesText = "Synthetic notes text.";
    public const int PageSize = 20;

    /// <summary>The first page of the patient's timeline, unfiltered, as staff or an admin.</summary>
    public static GetPatientTimelineQuery CreateTimelineQuery(bool isAdmin = false, int pageSize = PageSize) =>
        new(PatientId, null, pageSize, null, null, isAdmin);

    /// <summary>An audit entry on one of the patient's procedures.</summary>
    public static PatientProcedureAuditEntry CreateEntry(
        PatientProcedureAuditAction action,
        DateTime atUtc,
        string details = "{}",
        Guid? procedureId = null,
        Guid? patientId = null,
        Guid? id = null,
        string procedureTitle = ProceduresTestConstants.ProcedureTitle,
        string? stageName = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        PatientProcedureId = procedureId ?? ProcedureId,
        ProcedureTitle = procedureTitle,
        PatientId = patientId ?? PatientId,
        StageId = stageName is null ? null : Guid.NewGuid(),
        StageName = stageName,
        Action = action,
        UserId = ProceduresTestConstants.Staff.UserId,
        Username = ProceduresTestConstants.Staff.Username,
        AtUtc = atUtc,
        Details = details,
    };

    /// <summary>A procedure of the patient's, with just what the timeline reads.</summary>
    public static PatientProcedure CreateTimelineProcedure(
        Guid? id = null,
        string title = CurrentTitle,
        PatientProcedureStatus status = PatientProcedureStatus.InProgress,
        DateTime? dueDateUtc = null,
        DateTime? createdAt = null) => new()
    {
        Id = id ?? ProcedureId,
        Title = title,
        PatientId = PatientId,
        Status = status,
        Priority = PatientProcedurePriority.High,
        DueDateUtc = dueDateUtc ?? ProceduresTestConstants.Now.AddDays(3),
        CreatedAt = createdAt ?? ProceduresTestConstants.Now.AddDays(-10),
    };

    public static readonly Guid AppointmentId = Guid.Parse("00000022-0000-0000-0000-000000000005");
    public const string AppointmentStageName = "Consultation";
    public const string CancelReasonText = "Synthetic cancellation reason.";

    /// <summary>An appointment history entry for the patient (for a procedure unless <paramref name="withProcedure"/> is false).</summary>
    public static AppointmentEvent CreateAppointmentEvent(
        AppointmentEventAction action,
        DateTime atUtc,
        string details = "{}",
        bool withProcedure = true,
        Guid? procedureId = null,
        Guid? patientId = null,
        Guid? id = null,
        string procedureTitle = ProceduresTestConstants.ProcedureTitle) => new()
    {
        Id = id ?? Guid.NewGuid(),
        AppointmentId = AppointmentId,
        PatientId = patientId ?? PatientId,
        ProcedureId = withProcedure ? procedureId ?? ProcedureId : null,
        ProcedureTitle = withProcedure ? procedureTitle : string.Empty,
        StageName = withProcedure ? AppointmentStageName : null,
        Action = action,
        UserId = ProceduresTestConstants.Staff.UserId,
        Username = ProceduresTestConstants.Staff.Username,
        AtUtc = atUtc,
        StaffUserId = ProceduresTestConstants.OtherUserId,
        StaffUsername = ProceduresTestConstants.OtherUsername,
        StartUtc = atUtc.AddDays(1),
        EndUtc = atUtc.AddDays(1).AddMinutes(30),
        Details = details,
    };
}
