using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic data for the patient timeline's tests.</summary>
public static class TimelineTestConstants
{
    public static readonly Guid PatientId = Guid.Parse("00000022-0000-0000-0000-000000000001");
    public static readonly Guid OtherPatientId = Guid.Parse("00000022-0000-0000-0000-000000000002");
    public static readonly Guid PathwayId = Guid.Parse("00000022-0000-0000-0000-000000000003");
    public static readonly Guid DeletedPathwayId = Guid.Parse("00000022-0000-0000-0000-000000000004");

    public const string CurrentTitle = "Renamed pathway";
    public const string DeletedTitle = "Deleted pathway";
    public const string NotesText = "Synthetic notes text.";
    public const int PageSize = 20;

    /// <summary>The first page of the patient's timeline, unfiltered, as staff or an admin.</summary>
    public static GetPatientTimelineQuery CreateTimelineQuery(bool isAdmin = false, int pageSize = PageSize) =>
        new(PatientId, null, pageSize, null, null, isAdmin);

    /// <summary>An audit entry on one of the patient's pathways.</summary>
    public static PatientPathwayAuditEntry CreateEntry(
        PatientPathwayAuditAction action,
        DateTime atUtc,
        string details = "{}",
        Guid? pathwayId = null,
        Guid? patientId = null,
        Guid? id = null,
        string pathwayTitle = PathwaysTestConstants.PathwayTitle,
        string? stageName = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        PatientPathwayId = pathwayId ?? PathwayId,
        PathwayTitle = pathwayTitle,
        PatientId = patientId ?? PatientId,
        StageId = stageName is null ? null : Guid.NewGuid(),
        StageName = stageName,
        Action = action,
        UserId = PathwaysTestConstants.Staff.UserId,
        Username = PathwaysTestConstants.Staff.Username,
        AtUtc = atUtc,
        Details = details,
    };

    /// <summary>A pathway of the patient's, with just what the timeline reads.</summary>
    public static PatientPathway CreateTimelinePathway(
        Guid? id = null,
        string title = CurrentTitle,
        PatientPathwayStatus status = PatientPathwayStatus.InProgress,
        DateTime? dueDateUtc = null,
        DateTime? createdAt = null) => new()
    {
        Id = id ?? PathwayId,
        Title = title,
        PatientId = PatientId,
        Status = status,
        Priority = PatientPathwayPriority.High,
        DueDateUtc = dueDateUtc ?? PathwaysTestConstants.Now.AddDays(3),
        CreatedAt = createdAt ?? PathwaysTestConstants.Now.AddDays(-10),
    };

    public static readonly Guid AppointmentId = Guid.Parse("00000022-0000-0000-0000-000000000005");
    public const string AppointmentStageName = "Consultation";
    public const string CancelReasonText = "Synthetic cancellation reason.";

    /// <summary>An appointment history entry for the patient (for a pathway unless <paramref name="withPathway"/> is false).</summary>
    public static AppointmentEvent CreateAppointmentEvent(
        AppointmentEventAction action,
        DateTime atUtc,
        string details = "{}",
        bool withPathway = true,
        Guid? pathwayId = null,
        Guid? patientId = null,
        Guid? id = null,
        string pathwayTitle = PathwaysTestConstants.PathwayTitle) => new()
    {
        Id = id ?? Guid.NewGuid(),
        AppointmentId = AppointmentId,
        PatientId = patientId ?? PatientId,
        PathwayId = withPathway ? pathwayId ?? PathwayId : null,
        PathwayTitle = withPathway ? pathwayTitle : string.Empty,
        StageName = withPathway ? AppointmentStageName : null,
        Action = action,
        UserId = PathwaysTestConstants.Staff.UserId,
        Username = PathwaysTestConstants.Staff.Username,
        AtUtc = atUtc,
        StaffUserId = PathwaysTestConstants.OtherUserId,
        StaffUsername = PathwaysTestConstants.OtherUsername,
        StartUtc = atUtc.AddDays(1),
        EndUtc = atUtc.AddDays(1).AddMinutes(30),
        Details = details,
    };
}
