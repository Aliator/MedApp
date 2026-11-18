using MedApp.Domain.Contacts;
using MedApp.Domain.Practices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// A patient's whole record (<see cref="PatientRecord"/>) as a subject access export. Staff appear by username only:
/// their account IDs, network addresses and when they read a notification are the staff's data, so they're left out.
/// So are admins' monitoring notifications (<see cref="Notification.IsMonitoring"/>): which admin watches which member
/// of staff is about the staff, and what they did is in the pathways' history already.
/// History entries' details are stored with some staff account IDs (an assignment's <c>userId</c>, a rescheduled
/// appointment's <c>previousStaffUserId</c>) beside their usernames, so the export removes them
/// (<see cref="WithoutStaffIds"/>) and keeps the usernames.
/// </summary>
public static class PatientExportMapper
{
    /// <summary>The record's notifications that belong in the patient's export.</summary>
    public static IEnumerable<Notification> ExportedNotifications(PatientRecord record) =>
        record.Notifications.Where(n => !Notification.IsMonitoring(n.Kind));

    /// <summary>
    /// Every user the export names: on pathways (assignees, assigners, stage workers, note authors), on appointments (who they're
    /// with, who asked for, booked and recorded them) and as notification recipients.
    /// </summary>
    public static IEnumerable<Guid> UserIds(PatientRecord record) =>
        record.Pathways.SelectMany(PathwayMapper.UserIds)
            .Concat(record.Appointments.SelectMany(AppointmentUserIds))
            .Concat(ExportedNotifications(record).Select(n => n.UserId))
            .Distinct();

    private static IEnumerable<Guid> AppointmentUserIds(Appointment appointment) =>
        new[] { appointment.StaffUserId, appointment.CreatedByUserId, appointment.BookedByUserId, appointment.OutcomeByUserId }
            .OfType<Guid>();

    public static PatientExportResponse ToResponse(
        PatientRecord record,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        string exportedBy)
    {
        string Username(Guid? userId) =>
            userId is { } id ? usernames.TryGetValue(id, out var name) ? name : PathwayMapper.DeletedUser : string.Empty;

        return new PatientExportResponse
        {
            ExportedAtUtc = nowUtc,
            ExportedBy = exportedBy,
            Patient = ToExported(record.Patient, record.Practice),
            Pathways = record.Pathways
                .OrderBy(p => p.CreatedAt)
                .Select(p => new ExportedPathwayResponse
                {
                    Id = p.Id,
                    Title = p.Title,
                    TemplateName = p.TemplateName,
                    Status = p.Status.ToString(),
                    Priority = p.Priority.ToString(),
                    DueDate = DueDates.ToUkDate(p.DueDateUtc),
                    CreatedAt = p.CreatedAt,
                    LastUpdated = p.LastUpdated,
                    Notes = p.Notes
                        .OrderBy(n => n.CreatedAtUtc).ThenBy(n => n.Id)
                        .Select(n => new ExportedPathwayNoteResponse
                        {
                            Id = n.Id,
                            Author = PathwayNoteMapper.AuthorName(n.AuthorUserId, usernames),
                            Text = n.Text,
                            CreatedAtUtc = n.CreatedAtUtc,
                            LastEditedAtUtc = n.LastEditedAtUtc,
                            Version = n.Version,
                            // A deleted note is hidden from the pathway page, but kept: the export says when, who and why.
                            DeletedAtUtc = n.DeletedAtUtc,
                            DeletedBy = n.DeletedByUserId is null ? null : Username(n.DeletedByUserId),
                            DeletionReason = n.DeletionReason?.ToString(),
                            DeletionDetail = n.DeletionDetail,
                            IsAboutAnotherPatient = n.DeletionReason == NoteDeletionReason.WrongPatient
                        })
                        .ToList(),
                    AssignedTo = p.Assignments.OrderBy(a => a.AssignedAtUtc).Select(a => Username(a.UserId)).ToList(),
                    Stages = p.Stages
                        .OrderBy(s => s.StageOrder)
                        .Select(s => new ExportedStageResponse
                        {
                            Order = s.StageOrder,
                            Name = s.Name,
                            Notes = s.Notes,
                            StartedAtUtc = s.StartedAtUtc,
                            StartedBy = s.StartedByUserId is null ? null : Username(s.StartedByUserId),
                            FinishedAtUtc = s.CompletedAtUtc,
                            FinishedBy = s.CompletedByUserId is null ? null : Username(s.CompletedByUserId)
                        })
                        .ToList()
                })
                .ToList(),
            // Staff by the username recorded at the time; never their account IDs or network addresses.
            PathwayHistory = record.PathwayHistory
                .Select(e => new ExportedHistoryEntryResponse
                {
                    AtUtc = e.AtUtc,
                    Action = e.Action.ToString(),
                    Username = e.Username,
                    PathwayId = e.PatientPathwayId,
                    PathwayTitle = e.PathwayTitle,
                    StageName = e.StageName,
                    Details = WithoutStaffIds(e.Details)
                })
                .ToList(),
            // Every version of every note, by the username recorded at the time (none when it wasn't recorded).
            NoteHistory = record.NoteVersions
                .Select(v => new ExportedNoteVersionResponse
                {
                    NoteId = v.NoteId,
                    PathwayId = v.PatientPathwayId,
                    Version = v.Version,
                    Text = v.Text,
                    Username = v.UserId is null ? PathwayNoteMapper.UnknownAuthor : v.Username,
                    AtUtc = v.AtUtc
                })
                .ToList(),
            // Staff's requests to delete notes, by the usernames recorded at the time.
            NoteDeletionRequests = record.NoteDeletionRequests
                .Select(r => new ExportedNoteDeletionRequestResponse
                {
                    Id = r.Id,
                    NoteId = r.NoteId,
                    PathwayId = r.PatientPathwayId,
                    RequestedBy = r.RequestedByUsername,
                    RequestedAtUtc = r.RequestedAtUtc,
                    Reason = r.Reason.ToString(),
                    Detail = r.Detail,
                    Status = r.Status.ToString(),
                    DecidedBy = r.DecidedByUsername,
                    DecidedAtUtc = r.DecidedAtUtc
                })
                .ToList(),
            Appointments = record.Appointments
                .Select(a => new ExportedAppointmentResponse
                {
                    Id = a.Id,
                    PathwayId = a.PathwayId,
                    StageName = a.StageId is { } stageId
                        ? record.Pathways.SelectMany(p => p.Stages).FirstOrDefault(s => s.Id == stageId)?.Name
                        : null,
                    Status = a.Status.ToString(),
                    StaffUsername = a.StaffUserId is null ? null : Username(a.StaffUserId),
                    StartUtc = a.StartUtc,
                    EndUtc = a.EndUtc,
                    Notes = a.Notes,
                    CancelReason = a.CancelReason,
                    IsAutomatic = a.IsAutomatic,
                    IsRecordedAfterEvent = a.IsRecordedAfterEvent,
                    CreatedAtUtc = a.CreatedAtUtc,
                    CreatedBy = Username(a.CreatedByUserId),
                    BookedAtUtc = a.BookedAtUtc,
                    BookedBy = a.BookedByUserId is null ? null : Username(a.BookedByUserId),
                    OutcomeAtUtc = a.OutcomeAtUtc,
                    OutcomeBy = a.OutcomeByUserId is null ? null : Username(a.OutcomeByUserId)
                })
                .ToList(),
            // As the pathways' history: staff by the username recorded at the time.
            AppointmentHistory = record.AppointmentHistory
                .Select(e => new ExportedAppointmentHistoryEntryResponse
                {
                    AtUtc = e.AtUtc,
                    Action = e.Action.ToString(),
                    Username = e.Username,
                    AppointmentId = e.AppointmentId,
                    PathwayId = e.PathwayId,
                    PathwayTitle = e.PathwayTitle,
                    StageName = e.StageName,
                    StaffUsername = e.StaffUsername,
                    StartUtc = e.StartUtc,
                    EndUtc = e.EndUtc,
                    Details = WithoutStaffIds(e.Details)
                })
                .ToList(),
            AccessLog = record.Activity
                .Select(e => new ExportedAccessEntryResponse
                {
                    AtUtc = e.AtUtc,
                    Action = e.Action.ToString(),
                    Username = e.Username,
                    PathwayId = e.PathwayId,
                    Details = WithoutStaffIds(e.Details)
                })
                .ToList(),
            Notifications = ExportedNotifications(record)
                .Select(n => new ExportedNotificationResponse
                {
                    CreatedAtUtc = n.CreatedAtUtc,
                    Kind = n.Kind.ToString(),
                    Username = Username(n.UserId),
                    Title = n.Title,
                    Message = n.Message,
                    PathwayId = n.PathwayId,
                    AppointmentId = n.AppointmentId
                })
                .ToList(),
            // Who by the username recorded at the time, as the pathway history does.
            Attachments = record.Attachments
                .Select(a => new ExportedAttachmentResponse
                {
                    Id = a.Id,
                    AttachedTo = AttachmentTarget.KindOf(a).ToString(),
                    PathwayId = a.PathwayId,
                    StageName = a.StageId is { } stageId
                        ? record.Pathways.SelectMany(p => p.Stages).FirstOrDefault(s => s.Id == stageId)?.Name
                        : null,
                    ReferralId = a.ReferralId,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    TypeName = AttachmentFileTypes.FromContentType(a.ContentType)?.Name ?? a.ContentType,
                    SizeBytes = a.SizeBytes,
                    Sha256 = a.Sha256,
                    ScanStatus = a.ScanStatus.ToString(),
                    UploadedBy = a.UploadedByUsername,
                    UploadedAtUtc = a.UploadedAtUtc,
                    DeletedAtUtc = a.DeletedAtUtc,
                    DeletedBy = a.DeletedByUsername
                })
                .ToList(),
            // Everything the referrer sent and what was decided; staff by the usernames recorded at the time.
            Referrals = record.Referrals
                .Select(r => new ExportedReferralResponse
                {
                    Id = r.Id,
                    SpecialtyName = r.Specialty?.Name ?? string.Empty,
                    ReceivedOn = r.ReceivedOn,
                    ReferrerPractice = r.ReferrerPractice,
                    ReferrerClinician = r.ReferrerClinician,
                    ReferrerOdsCode = r.ReferrerOdsCode,
                    RequestedPriority = r.RequestedPriority.ToString(),
                    Reason = r.Reason,
                    Status = r.Status.ToString(),
                    RegisteredAtUtc = r.RegisteredAtUtc,
                    RegisteredBy = r.RegisteredByUsername,
                    DecidedAtUtc = r.DecidedAtUtc,
                    DecidedBy = r.DecidedByUsername,
                    RejectionReason = r.RejectionReason?.ToString(),
                    DecisionDetail = r.DecisionDetail,
                    RedirectedTo = r.RedirectedTo,
                    PathwayId = r.PathwayId
                })
                .ToList(),
            ReferralHistory = record.ReferralHistory
                .Select(e => new ExportedReferralHistoryEntryResponse
                {
                    AtUtc = e.AtUtc,
                    Action = e.Action.ToString(),
                    Username = e.Username,
                    ReferralId = e.ReferralId,
                    PathwayId = e.PathwayId,
                    Details = WithoutStaffIds(e.Details)
                })
                .ToList()
        };
    }

    /// <summary>
    /// An entry's details without staff account IDs: every property, at any depth, named <c>userId</c> or ending in
    /// <c>UserId</c> or <c>UserIds</c> (in any case) is removed, and the usernames beside them stay. Details that aren't
    /// a JSON object become <c>{}</c>, as they can't be checked; details with no IDs are returned as stored.
    /// </summary>
    public static string WithoutStaffIds(string details)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(details) as JsonObject;
        }
        catch (JsonException)
        {
            root = null;
        }

        if (root is null)
            return "{}";

        return RemoveStaffIds(root) ? root.ToJsonString() : details;
    }

    /// <summary>Removes the staff ID properties under <paramref name="node"/>; whether it removed any.</summary>
    private static bool RemoveStaffIds(JsonNode? node)
    {
        var removed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (IsStaffIdKey(key))
                    {
                        obj.Remove(key);
                        removed = true;
                    }
                    else
                    {
                        removed |= RemoveStaffIds(value);
                    }
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    removed |= RemoveStaffIds(item);
                break;
        }

        return removed;
    }

    private static bool IsStaffIdKey(string key) =>
        key.EndsWith("UserId", StringComparison.OrdinalIgnoreCase)
        || key.EndsWith("UserIds", StringComparison.OrdinalIgnoreCase);

    public static ExportedPatientResponse ToExported(Patient patient, Practice? practice = null) => new()
    {
        Id = patient.Id,
        FirstName = patient.FirstName,
        LastName = patient.LastName,
        DateOfBirth = patient.DateOfBirth,
        Email = patient.Email,
        NhsNumber = patient.NhsNumber,
        AddressLines = PostalAddress.Split(patient.AddressLines).ToList(),
        Postcode = patient.Postcode,
        MobilePhone = patient.MobilePhone,
        PracticeName = practice?.Id == patient.PracticeId ? practice?.Name : null,
        PracticeOdsCode = practice?.Id == patient.PracticeId ? practice?.OdsCode : null,
        CreatedAt = patient.CreatedAt,
        LastUpdated = patient.LastUpdated,
        RestrictedAtUtc = patient.RestrictedAtUtc,
        RestrictionReason = patient.RestrictionReason,
        DisputedAtUtc = patient.DisputedAtUtc,
        DisputeReason = patient.DisputeReason,
        AnonymisedAtUtc = patient.AnonymisedAtUtc,
        RetentionKeptUntil = patient.RetentionKeptUntil,
        RetentionKeptReason = patient.RetentionKeptReason
    };
}
