using System.Globalization;
using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;

namespace MedApp.Application.Pathways.Timeline.Common;

/// <summary>
/// A patient's timeline as the API returns it: audit entries and appointment history entries read into fields (from
/// the JSON details that <see cref="PathwayAudit"/> and <c>AppointmentChange</c> write), the pathways that are
/// due, and the pathways to filter by.
/// <para>
/// Staff see what the pathway and calendar pages show anyway (statuses, priorities, target dates, who is assigned,
/// appointment times and staff), but never the free text the logs keep (notes, titles, reasons), as only admins can
/// read the audit log itself.
/// </para>
/// </summary>
public static class TimelineMapper
{
    /// <summary>What an appointment entry's action starts with, before its history action (e.g. AppointmentBooked).</summary>
    public const string AppointmentAction = "Appointment";

    /// <summary>What a referral entry's action starts with, before its history action (e.g. ReferralRegistered).</summary>
    public const string ReferralAction = "Referral";

    /// <summary>The fields of a referral's edit whose values everyone sees (structured: never free text).</summary>
    private static readonly HashSet<string> ReferralValueFields =
    [
        Referrals.Common.ReferralChange.SpecialtyField,
        Referrals.Common.ReferralChange.ReceivedOnField,
        Referrals.Common.ReferralChange.RequestedPriorityField
    ];

    /// <summary>The field a pathway note's entry names in its change.</summary>
    public const string NoteField = "note";

    /// <summary>Fields whose values everyone sees in a change; the others (free text) only admins.</summary>
    private static readonly HashSet<string> PublicValueFields = ["dueDate", "priority"];

    /// <summary>
    /// One entry. Its pathway's title is the one it has now, or, once deleted, the one it had at the time. Free-text
    /// values are left out unless <paramref name="isAdmin"/>.
    /// </summary>
    public static PatientTimelineEntryResponse ToResponse(PatientTimelineRow row, bool isAdmin)
    {
        var entry = row.Entry;
        var response = new PatientTimelineEntryResponse
        {
            Id = entry.Id,
            Kind = PatientTimelineKinds.Of(entry).ToString(),
            Action = entry.Action.ToString(),
            AtUtc = entry.AtUtc,
            Username = entry.Username,
            PathwayId = entry.PatientPathwayId,
            PathwayTitle = row.CurrentPathwayTitle ?? entry.PathwayTitle,
            IsPathwayDeleted = row.CurrentPathwayTitle is null && !row.IsAboutPatient,
            StageName = entry.StageName,
        };

        var details = ParseDetails(entry.Details);
        switch (entry.Action)
        {
            case Created:
                response.TemplateName = Text(details, "template");
                response.StageCount = details.TryGetValue("stageCount", out var count)
                                      && count.ValueKind == JsonValueKind.Number
                                      && count.TryGetInt32(out var stages)
                    ? stages
                    : null;
                response.Priority = Text(details, "priority");
                response.DueDate = DateOnly.TryParseExact(Text(details, "dueDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate)
                    ? dueDate
                    : null;
                break;

            case StatusChanged:
                response.FromStatus = Text(details, "from");
                response.ToStatus = Text(details, "to");
                response.IsAutomatic = IsTrue(details, "automatic");
                response.Reason = isAdmin ? Text(details, "reason") : null;
                break;

            case Assigned or Unassigned:
                response.AssigneeUsername = Text(details, "username");
                break;

            case StageFinished when IsTrue(details, "withoutVisit"):
                response.FinishedWithoutVisit = true;
                response.VisitRequirement = Text(details, "visitRequirement");
                response.Reason = isAdmin ? Text(details, "reason") : null;
                break;

            case StageNotesUpdated:
                response.Changes = [Change("stageNotes", details, isAdmin)];
                break;

            // The entry holds only the note's ID and version; its text, from the note versions, is for admins only.
            case NoteAdded or NoteEdited:
                response.Changes =
                [
                    new PatientTimelineChangeResponse
                    {
                        Field = NoteField,
                        From = isAdmin ? row.PreviousNoteText : null,
                        To = isAdmin ? row.NoteText : null,
                    }
                ];
                break;

            // A deleted note: staff see that it was deleted, by whom and when; admins also see the text it had (from the
            // note versions) and the reason.
            case NoteDeleted:
                response.Changes =
                [
                    new PatientTimelineChangeResponse
                    {
                        Field = NoteField,
                        From = isAdmin ? row.NoteText : null,
                        To = null,
                    }
                ];
                response.Reason = isAdmin ? DeletionReason(details) : null;
                break;

            case DetailsUpdated when details.TryGetValue("changes", out var changes) && changes.ValueKind == JsonValueKind.Object:
                response.Changes = changes.EnumerateObject()
                    .Select(c => Change(c.Name, ObjectOf(c.Value), isAdmin || PublicValueFields.Contains(c.Name)))
                    .ToList();
                break;

            case Deleted:
                response.FromStatus = Text(details, "status");
                break;

            case AttachmentAdded or AttachmentDeleted or AttachmentDownloaded:
                response.AttachmentId = Guid.TryParse(Text(details, "attachmentId"), out var attachmentId) ? attachmentId : null;
                response.AttachedTo = Text(details, "attachedTo");
                response.ReferralId = Guid.TryParse(Text(details, "referralId"), out var referralId) ? referralId : null;
                // A file's name is free text: only admins see it, as with notes and reasons.
                response.FileName = isAdmin ? Text(details, "fileName") : null;
                break;
        }

        return response;
    }

    /// <summary>
    /// One appointment history entry: its action is "Appointment" and what happened (e.g. AppointmentBooked), with
    /// the appointment's time and member of staff after it. Its pathway's title is the one it has now, or, once
    /// deleted, the one it had at the time; none for an appointment not for a pathway. Cancellation reasons and
    /// notes are left out unless <paramref name="isAdmin"/>.
    /// </summary>
    public static PatientTimelineEntryResponse ToResponse(AppointmentTimelineRow row, bool isAdmin)
    {
        var entry = row.Event;
        var hasPathway = entry.PathwayId is not null;
        var details = ParseDetails(entry.Details);

        var response = new PatientTimelineEntryResponse
        {
            Id = entry.Id,
            Kind = nameof(PatientTimelineKind.Appointments),
            Action = AppointmentAction + entry.Action,
            AtUtc = entry.AtUtc,
            Username = entry.Username,
            PathwayId = entry.PathwayId,
            PathwayTitle = hasPathway ? row.CurrentPathwayTitle ?? entry.PathwayTitle : null,
            IsPathwayDeleted = hasPathway && !row.PathwayExists,
            StageName = entry.StageName,
            AppointmentId = entry.AppointmentId,
            AppointmentStartUtc = entry.StartUtc,
            AppointmentEndUtc = entry.EndUtc,
            AppointmentStaffUsername = entry.StaffUsername,
            IsAutomatic = IsTrue(details, "automatic"),
        };

        switch (entry.Action)
        {
            case AppointmentEventAction.Rescheduled:
                response.PreviousStartUtc = Time(details, "previousStartUtc");
                response.PreviousStaffUsername = Text(details, "previousStaffUsername");
                break;

            case AppointmentEventAction.Attended or AppointmentEventAction.DidNotAttend:
                response.FromStatus = Text(details, "previousStatus");
                break;

            case AppointmentEventAction.RecordedAfterEvent:
                // The outcome, and Requested when it was a stage's visit in Requires Appointment: structured values.
                response.FromStatus = Text(details, "previousStatus");
                response.ToStatus = Text(details, "outcome");
                break;

            case AppointmentEventAction.EnteredInError:
                // The outcome it had is structured; why it was withdrawn is free text, for admins only.
                response.FromStatus = Text(details, "previousStatus");
                response.Reason = isAdmin ? Text(details, "reason") : null;
                break;

            case AppointmentEventAction.Cancelled:
                response.Reason = isAdmin ? Text(details, "reason") : null;
                break;

            case AppointmentEventAction.NotesUpdated:
                response.Changes = [Change("appointmentNotes", details, isAdmin)];
                break;
        }

        return response;
    }

    /// <summary>
    /// One referral history entry: its action is "Referral" and what happened (e.g. ReferralRegistered), with its
    /// specialty, the priority asked for, the status after it, and for an edit each field changed, with values for the
    /// structured ones only (the specialty, date received and priority). The free text the referral holds (its reason,
    /// referring practice, and the detail given when it was closed) is left out unless <paramref name="isAdmin"/>, and
    /// even then the history keeps no old text, so an edit's free-text fields come by name only.
    /// </summary>
    public static PatientTimelineEntryResponse ToResponse(ReferralTimelineRow row, bool isAdmin)
    {
        var entry = row.Event;
        var details = Referrals.Common.ReferralEventDetails.Parse(entry.Details);
        var response = new PatientTimelineEntryResponse
        {
            Id = entry.Id,
            Kind = nameof(PatientTimelineKind.Referrals),
            Action = ReferralAction + entry.Action,
            AtUtc = entry.AtUtc,
            Username = entry.Username,
            ReferralId = entry.ReferralId,
            SpecialtyName = details.SpecialtyName,
            ReferrerPractice = isAdmin ? row.ReferrerPractice : null,
        };

        switch (entry.Action)
        {
            case Domain.Referrals.ReferralEventAction.Registered:
                response.Priority = details.RequestedPriority;
                response.ToStatus = details.ToStatus;
                response.Reason = isAdmin ? row.Reason : null;
                break;

            case Domain.Referrals.ReferralEventAction.DetailsEdited:
                response.Changes = details.ChangedFields
                    .Select(field =>
                    {
                        var values = ReferralValueFields.Contains(field) ? details.Changes.GetValueOrDefault(field) : default;
                        return new PatientTimelineChangeResponse { Field = field, From = values.From, To = values.To };
                    })
                    .ToList();
                break;

            case Domain.Referrals.ReferralEventAction.Withdrawn or Domain.Referrals.ReferralEventAction.EnteredInError:
                response.ToStatus = details.ToStatus;
                response.Reason = isAdmin ? row.DecisionDetail : null;
                break;

            default:
                response.ToStatus = details.ToStatus;
                break;
        }

        return response;
    }

    /// <summary>A referral awaiting triage, beside the open pathways above the timeline (no free text).</summary>
    public static PatientTimelineReferralResponse ToReferralResponse(Domain.Referrals.Referral referral) => new()
    {
        ReferralId = referral.Id,
        SpecialtyName = referral.Specialty?.Name ?? string.Empty,
        ReceivedOn = referral.ReceivedOn,
        RequestedPriority = referral.RequestedPriority.ToString(),
    };

    /// <summary>A note deletion's reason and detail in words ("Wrong patient: ..."), from its entry's details.</summary>
    private static string? DeletionReason(IReadOnlyDictionary<string, JsonElement> details) =>
        PathwayEnums.TryParse<NoteDeletionReason>(Text(details, "deletionReason"), out var reason)
            ? NoteDeletionReasons.Describe(reason, Text(details, "detail"))
            : Text(details, "detail");

    /// <summary>A pathway that's due, flagged when it's overdue at <paramref name="nowUtc"/>.</summary>
    public static PatientTimelineUpcomingResponse ToUpcomingResponse(PatientPathway pathway, DateTime nowUtc) => new()
    {
        PathwayId = pathway.Id,
        Title = pathway.Title,
        Status = pathway.Status.ToString(),
        Priority = pathway.Priority.ToString(),
        DueDate = DueDates.ToUkDate(pathway.DueDateUtc),
        IsOverdue = PathwayWorkflow.IsOverdue(pathway, nowUtc),
    };

    /// <summary>A pathway to filter the timeline by, with the UK day it was created.</summary>
    public static PatientTimelinePathwayResponse ToPathwayResponse(PatientPathway pathway) => new()
    {
        Id = pathway.Id,
        Title = pathway.Title,
        IsDeleted = false,
        CreatedOn = DueDates.ToUkDate(pathway.CreatedAt),
    };

    /// <summary>A deleted pathway to filter the timeline by, from the audit entry of its deletion (with its UK day).</summary>
    public static PatientTimelinePathwayResponse ToDeletedPathwayResponse(PatientPathwayAuditEntry deletion) => new()
    {
        Id = deletion.PatientPathwayId,
        Title = deletion.PathwayTitle,
        IsDeleted = true,
        DeletedOn = DueDates.ToUkDate(deletion.AtUtc),
    };

    private static PatientTimelineChangeResponse Change(string field, IReadOnlyDictionary<string, JsonElement> fromTo, bool showValues) => new()
    {
        Field = field,
        From = showValues ? Text(fromTo, "from") : null,
        To = showValues ? Text(fromTo, "to") : null,
    };

    /// <summary>The details' top-level values by name; empty if they aren't a JSON object.</summary>
    private static IReadOnlyDictionary<string, JsonElement> ParseDetails(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ObjectOf(document.RootElement.Clone());
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> ObjectOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.Last().Value)
            : new Dictionary<string, JsonElement>();

    private static bool IsTrue(IReadOnlyDictionary<string, JsonElement> values, string name) =>
        values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>A time written as JSON (ISO 8601, UTC), or null when missing or not a time.</summary>
    private static DateTime? Time(IReadOnlyDictionary<string, JsonElement> values, string name) =>
        values.TryGetValue(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.TryGetDateTime(out var time)
            ? time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc)
            : null;

    /// <summary>A value as text: a string as it is, a number or date as written, null when missing or null.</summary>
    private static string? Text(IReadOnlyDictionary<string, JsonElement> values, string name) =>
        !values.TryGetValue(name, out var value) ? null : value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText(),
        };
}
