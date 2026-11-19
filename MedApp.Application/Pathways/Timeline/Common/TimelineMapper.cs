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
        Referrals.Common.ReferralChange.TeamField,
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
                // Accepted from a referral: a link to it.
                response.ReferralId = Guid.TryParse(Text(details, "referralId"), out var fromReferral) ? fromReferral : null;
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

            // The reason is a fixed choice, for everyone; its detail is free text, for admins only.
            case StageSkipped:
                response.SkipReason = Text(details, "skipReason");
                response.Reason = isAdmin ? Text(details, "detail") : null;
                break;

            // The outcome (or, before choice nodes, the branch) and the stages skipped are names the template's author wrote: everyone sees them.
            case BranchChosen:
                response.Branch = Text(details, "outcome") ?? Text(details, "branch");
                response.SkippedStageNames = details.TryGetValue("skippedStages", out var skippedStages)
                                             && skippedStages.ValueKind == JsonValueKind.Array
                    ? skippedStages.EnumerateArray()
                        .Where(s => s.ValueKind == JsonValueKind.String)
                        .Select(s => s.GetString()!)
                        .ToList()
                    : [];
                break;

            // Why it was reinstated is free text, for admins only; how it had been skipped is a fixed choice.
            case StageReinstated:
                response.SkipReason = Text(details, "skipReason");
                response.Reason = isAdmin ? Text(details, "reason") : null;
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
                // A target date the waiting-time clock moved, rather than someone by hand.
                response.FromClock = Text(details, "source") == Referrals.Common.WaitingClock.AuditSource;
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

            case >= LetterDrafted and <= LetterWithdrawn:
                Letter(response, entry.Action, details, isAdmin ? row.Letter : null);
                break;

            case >= SpecimenTaken and <= SpecimenDetailsEdited:
                Specimen(response, entry.Action, details, isAdmin ? row.Specimen : null);
                break;

            // A lab case's entries (MC15): its number, and a step's name and outcome (admin-written or fixed words);
            // never the results or a detail typed, which the details don't hold.
            case >= LabCaseOpened and <= LabCaseClosed:
                response.SpecimenNumber = Text(details, "number");
                response.ToStatus = Text(details, "outcome") ?? Text(details, "step");
                break;

            case >= ResultRecorded and <= ResultEnteredInError:
                Result(response, entry.Action, details, isAdmin ? row.Result : null);
                break;

            // MDT items: the team (an admin-written name), the meetings' times and who was present, for everyone.
            // The question and the outcome are never in the details, so neither is ever here (decision 70).
            case MdtListed or MdtOutcomeRecorded or MdtDeferred or MdtWithdrawn:
                response.MdtItemId = Guid.TryParse(Text(details, "mdtItemId"), out var mdtItemId) ? mdtItemId : null;
                response.TeamName = Text(details, "specialty");
                response.MeetingStartUtc = Time(details, "meetingStartUtc");
                response.PreviousMeetingStartUtc = Time(details, "fromMeetingStartUtc");
                response.PresentUsernames = details.TryGetValue("present", out var present) && present.ValueKind == JsonValueKind.Array
                    ? present.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).ToList()
                    : [];
                break;

            case ConsentRecorded or ConsentWithdrawn or ConsentEnteredInError:
                Consent(response, entry.Action, details, isAdmin ? row.ConsentRecord : null);
                break;
        }

        return response;
    }

    /// <summary>
    /// A letter entry: which letter, its kind and template (an admin-written name), its referral, whom it was submitted
    /// to, and for a sending the recipient's kind, the method and whether it reached them. Never free text for staff:
    /// the return comment and the withdrawal reason are for admins only, read from the letter
    /// (<paramref name="letter"/>, given only for admins), as the audit holds none; the letter's text is on its page.
    /// </summary>
    private static void Letter(
        PatientTimelineEntryResponse response,
        PatientPathwayAuditAction action,
        IReadOnlyDictionary<string, JsonElement> details,
        Domain.Letters.Letter? letter)
    {
        response.LetterId = Guid.TryParse(Text(details, "letterId"), out var letterId) ? letterId : null;
        response.LetterKind = Text(details, "kind");
        response.LetterTemplateName = Text(details, "templateName");
        response.ReferralId = Guid.TryParse(Text(details, "referralId"), out var referralId) ? referralId : null;

        switch (action)
        {
            case LetterSubmitted:
                response.ApproverUsername = Text(details, "approver");
                break;

            case LetterApproved:
                response.AttachmentId = Guid.TryParse(Text(details, "attachmentId"), out var attachmentId) ? attachmentId : null;
                break;

            case LetterSent:
                response.RecipientKind = Text(details, "recipientKind");
                response.DispatchMethod = Text(details, "method");
                response.DispatchStatus = Text(details, "dispatchStatus");
                break;

            case LetterReturned when letter is not null:
                response.Reason = letter.ReturnedAtUtc is not null ? letter.ReturnComment : null;
                break;

            case LetterWithdrawn when letter is not null:
                response.Reason = letter.WithdrawnReason;
                break;
        }
    }

    /// <summary>
    /// A specimen entry: which specimen (its number), its type and lab (admin-written names), the status before and
    /// after, the report's file, and for an edit the fields changed, with values for the structured ones only. Never
    /// free text for staff: the site, request notes, rejection reason and every detail typed are for admins only, read
    /// from the specimen (<paramref name="specimen"/>, given only for admins), as the audit holds none.
    /// </summary>
    private static void Specimen(
        PatientTimelineEntryResponse response,
        PatientPathwayAuditAction action,
        IReadOnlyDictionary<string, JsonElement> details,
        Domain.Specimens.Specimen? specimen)
    {
        response.SpecimenId = Guid.TryParse(Text(details, "specimenId"), out var specimenId) ? specimenId : null;
        response.SpecimenNumber = Text(details, "number");
        response.SpecimenTypeName = Text(details, "specimenType");
        response.LaboratoryName = Text(details, "laboratory");
        response.FromStatus = Text(details, "from");
        response.ToStatus = Text(details, "to");

        switch (action)
        {
            case SpecimenReported:
                response.AttachmentId = Guid.TryParse(Text(details, "attachmentId"), out var attachmentId) ? attachmentId : null;
                break;

            case SpecimenDetailsEdited:
                var changes = details.TryGetValue("changes", out var c) ? ObjectOf(c) : new Dictionary<string, JsonElement>();
                response.Changes = (details.TryGetValue("fields", out var fields) && fields.ValueKind == JsonValueKind.Array
                        ? fields.EnumerateArray().Select(f => f.GetString()).OfType<string>()
                        : [])
                    .Select(field => Change(
                        field,
                        changes.TryGetValue(field, out var values) ? ObjectOf(values) : new Dictionary<string, JsonElement>(),
                        Specimens.Common.SpecimenChange.StructuredFields.Contains(field)))
                    .ToList();
                break;

            case SpecimenTaken when specimen is not null:
                response.Changes =
                [
                    .. specimen.Site is { } site ? [new PatientTimelineChangeResponse { Field = Specimens.Common.SpecimenChange.SiteField, To = site }] : Array.Empty<PatientTimelineChangeResponse>(),
                    .. specimen.RequestNotes is { } notes ? [new PatientTimelineChangeResponse { Field = Specimens.Common.SpecimenChange.RequestNotesField, To = notes }] : Array.Empty<PatientTimelineChangeResponse>(),
                ];
                break;

            case SpecimenRejected when specimen is not null:
                var reason = PathwayEnums.TryParse<Domain.Specimens.SpecimenRejectionReason>(Text(details, "rejectionReason"), out var r)
                    ? Specimens.Common.SpecimenWorkflow.Describe(r)
                    : null;
                response.Reason = string.IsNullOrWhiteSpace(specimen.ClosedDetail) ? reason : $"{reason}: {specimen.ClosedDetail}";
                break;

            case SpecimenLost or SpecimenEnteredInError when specimen is not null:
                response.Reason = specimen.ClosedDetail;
                break;
        }
    }

    /// <summary>
    /// A result entry: which result, where it came from (and its specimen's number and type), the status before and
    /// after, the report's file, the action by name on sign-off, and on a reassignment the clinicians (usernames). Never
    /// free text for staff: the title, the action's detail and the error detail are for admins only, read from the
    /// result (<paramref name="result"/>, given only for admins), as the audit holds none.
    /// </summary>
    private static void Result(
        PatientTimelineEntryResponse response,
        PatientPathwayAuditAction action,
        IReadOnlyDictionary<string, JsonElement> details,
        Domain.Results.Result? result)
    {
        response.ResultId = Guid.TryParse(Text(details, "resultId"), out var resultId) ? resultId : null;
        response.ResultSource = Text(details, "source");
        response.SpecimenId = Guid.TryParse(Text(details, "specimenId"), out var specimenId) ? specimenId : null;
        response.SpecimenNumber = Text(details, "number");
        response.SpecimenTypeName = Text(details, "specimenType");
        response.FromStatus = Text(details, "from");
        response.ToStatus = Text(details, "to");
        response.AttachmentId = Guid.TryParse(Text(details, "attachmentId"), out var attachmentId) ? attachmentId : null;

        switch (action)
        {
            case ResultRecorded:
                response.AssigneeUsername = Text(details, "responsible");
                if (result is not null)
                    response.Changes = [new PatientTimelineChangeResponse { Field = Results.Common.ResultChange.TitleField, To = result.Title }];
                break;

            case ResultSignedOff:
                response.ResultAction = Text(details, "action");
                if (result is not null)
                    response.Reason = result.ActionDetail;
                break;

            case ResultReassigned:
                var changes = details.TryGetValue("changes", out var c) ? ObjectOf(c) : new Dictionary<string, JsonElement>();
                response.Changes =
                [
                    Change(
                        Results.Common.ResultChange.ResponsibleField,
                        changes.TryGetValue(Results.Common.ResultChange.ResponsibleField, out var values)
                            ? ObjectOf(values)
                            : new Dictionary<string, JsonElement>(),
                        showValues: true)
                ];
                break;

            case ResultEnteredInError when result is not null:
                response.Reason = result.ErrorDetail;
                break;
        }
    }

    /// <summary>
    /// A consent entry: which record, the form's name and version (admin-written), who signed (by kind), the date signed,
    /// the status before and after, and the scan's file. Never free text for staff: a withdrawal's or correction's detail
    /// is for admins only, read from the record (<paramref name="record"/>, given only for admins), as the audit holds
    /// none.
    /// </summary>
    private static void Consent(
        PatientTimelineEntryResponse response,
        PatientPathwayAuditAction action,
        IReadOnlyDictionary<string, JsonElement> details,
        Domain.Consent.ConsentRecord? record)
    {
        response.ConsentRecordId = Guid.TryParse(Text(details, "consentRecordId"), out var recordId) ? recordId : null;
        response.ConsentFormName = Text(details, "formName");
        response.ConsentFormVersion = Text(details, "formVersion");
        response.ConsentSignedBy = Text(details, "signedBy");
        response.ConsentSignedOn = DateOnly.TryParse(Text(details, "signedOn"), System.Globalization.CultureInfo.InvariantCulture, out var signedOn)
            ? signedOn
            : null;
        response.AttachmentId = Guid.TryParse(Text(details, "scanAttachmentId"), out var scanId) ? scanId : null;
        response.FromStatus = Text(details, "from");
        response.ToStatus = Text(details, "to");

        if (action is ConsentWithdrawn or ConsentEnteredInError && record is not null)
            response.Reason = record.StatusDetail;
    }

    /// <summary>A GUID a JSON object's property holds, or null (for details that aren't JSON, or lack it).</summary>
    public static Guid? GuidIn(string details, string property) =>
        Guid.TryParse(Text(ParseDetails(details), property), out var id) ? id : null;

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

            case AppointmentEventAction.DnaFollowUpRecorded:
                // The decision is a fixed choice; its detail is free text, for admins only.
                response.DnaFollowUp = Text(details, "decision");
                response.Reason = isAdmin ? Text(details, "detail") : null;
                break;
        }

        return response;
    }

    /// <summary>
    /// One referral history entry: its action is "Referral" and what happened (e.g. ReferralRegistered), with its
    /// team, the priority asked for, the status after it, and for an edit each field changed, with values for the
    /// structured ones only (the team, date received and priority); for triage, the outcome, the template and
    /// priority chosen, the pathway, the rejection reason by name and the team redirected from. The free text the
    /// referral holds (its reason, referring practice, the detail given when it was triaged or closed, and where it was
    /// redirected elsewhere) is left out unless <paramref name="isAdmin"/>, and even then the history keeps no old text,
    /// so an edit's free-text fields come by name only.
    /// </summary>
    public static PatientTimelineEntryResponse ToResponse(ReferralTimelineRow row, bool isAdmin)
    {
        var entry = row.Event;
        var details = Referrals.Common.ReferralEventDetails.Parse(entry.Details);
        var response = new PatientTimelineEntryResponse
        {
            Id = entry.Id,
            Kind = Queries.GetPatientTimeline.ReferralTimelineSearch.KindOf(entry.Action).ToString(),
            Action = ReferralAction + entry.Action,
            AtUtc = entry.AtUtc,
            Username = entry.Username,
            ReferralId = entry.ReferralId,
            TeamName = details.TeamName,
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

            // More information asked of the referrer, or come: the status, structured.
            case Domain.Referrals.ReferralEventAction.InformationRequested or Domain.Referrals.ReferralEventAction.InformationReceived:
                response.ToStatus = details.ToStatus;
                break;

            case Domain.Referrals.ReferralEventAction.Withdrawn or Domain.Referrals.ReferralEventAction.EnteredInError:
                response.ToStatus = details.ToStatus;
                response.Reason = isAdmin ? row.DecisionDetail : null;
                break;

            // Triage: the outcome, the template and priority chosen and the pathway, or the rejection reason, by name;
            // the detail and where it was sent are free text, for admins only.
            case Domain.Referrals.ReferralEventAction.Accepted:
                response.ToStatus = details.ToStatus;
                response.Priority = details.Priority;
                response.TemplateName = details.TemplateName;
                response.PathwayId = details.PathwayId;
                break;

            case Domain.Referrals.ReferralEventAction.Rejected:
                response.ToStatus = details.ToStatus;
                response.RejectionReason = details.RejectionReason;
                response.Reason = isAdmin ? row.DecisionDetail : null;
                break;

            case Domain.Referrals.ReferralEventAction.RedirectedElsewhere:
                response.ToStatus = details.ToStatus;
                response.RedirectedTo = isAdmin ? row.RedirectedTo : null;
                response.Reason = isAdmin ? row.DecisionDetail : null;
                break;

            case Domain.Referrals.ReferralEventAction.TeamChanged:
                response.PreviousTeamName = details.PreviousTeamName;
                // The referral keeps the latest redirection's detail only.
                response.Reason = isAdmin ? row.RedirectDetail : null;
                break;

            // The waiting-time clock: reasons by name, dates and days; a pause's detail is free text, for admins only.
            case Domain.Referrals.ReferralEventAction.ClockPaused:
                response.PauseReason = details.PauseReason;
                response.ReviewOn = details.ReviewOn;
                response.Reason = isAdmin ? row.PauseDetail : null;
                break;

            case Domain.Referrals.ReferralEventAction.ClockResumed:
                response.PauseReason = details.PauseReason;
                response.IsAutomatic = details.Automatic;
                break;

            case Domain.Referrals.ReferralEventAction.ClockStopped:
                response.StopReason = details.StopReason;
                response.DaysWaited = details.DaysWaited;
                response.TargetDays = details.TargetDays;
                response.Breached = details.Breached;
                break;

            case Domain.Referrals.ReferralEventAction.TargetChanged:
                response.PreviousTargetDays = details.PreviousTargetDays;
                response.TargetDays = details.TargetDays;
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
        TeamName = referral.Team?.Name ?? string.Empty,
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
