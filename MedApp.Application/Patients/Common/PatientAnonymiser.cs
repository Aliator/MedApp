using System.Text.Json;
using System.Text.Json.Nodes;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// Anonymises a patient's record in place (<see cref="PatientRecord"/>, loaded for update): their identifying details
/// and every piece of free text become placeholders, and everything else stays, so the record's structure, its
/// statistics (pathways, templates, statuses, dates, who did what and when) and its audit trail survive.
/// <list type="bullet">
/// <item>Name and email become placeholders; the NHS number, the postal address, postcode and mobile, and the link to
/// their GP practice are removed (the practice stays in the directory); the date of birth keeps only its year
/// (1 January), for age statistics.</item>
/// <item>Pathway titles become the template's name; pathway notes (every version of each, those of deleted
/// pathways too, and the old single notes field) and stage notes become <see cref="RemovedText"/>, and so do the
/// details given when notes were deleted, or asked to be (the reasons themselves are a fixed choice, kept). A request still waiting is closed, by whoever
/// anonymises: the record can't change any more.</item>
/// <item>The pathway audit's titles, notes, reasons and changed text, and the activity log's reasons, are replaced the same way.</item>
/// <item>Appointments' notes and cancellation reasons, and their history's pathway titles, notes and reasons, are replaced the same way.</item>
/// <item>Notifications about the pathways take the new titles, except those whose title is fixed (the appointment
/// kinds' "Appointment booked", monitoring's "Stage finished"), which never held free text; their messages hold none
/// either.</item>
/// <item>Referrals' reasons, referring practices and clinicians, decision and redirect details, redirect destinations and clock pauses' details become
/// <see cref="RemovedText"/>, and their ODS codes and links to the directory's practices are removed; their dates, statuses, priorities and specialties stay.
/// Their history holds no free text, but its details are scrubbed as the others' are.</item>
/// <item>Attached files' names become <see cref="FileName"/> with their type's usual extension, in their records and in
/// the audit's details, and every file still live is deleted (hidden), by whoever anonymises, so its bytes go with the
/// purge of deleted files.</item>
/// </list>
/// The patient's ID, and every link to it, stays: on its own it identifies nobody.
/// </summary>
public static class PatientAnonymiser
{
    public const string FirstName = "Anonymised";
    public const string LastName = "Patient";
    public const string Email = "anonymised@anonymised.invalid";

    /// <summary>Takes the place of free text that had something in it; empty text stays empty.</summary>
    public const string RemovedText = "[Removed when the record was anonymised]";

    /// <summary>The title of a pathway with no template name, or one that has been deleted.</summary>
    public const string PathwayTitle = "Anonymised pathway";

    /// <summary>An attached file's name, before its type's usual extension.</summary>
    public const string FileName = "Anonymised file";

    /// <summary>Keys whose text is free text wherever they appear in an entry's details (a file's name included).</summary>
    private static readonly HashSet<string> FreeTextKeys = ["title", "notes", "reason", "fileName", "detail"];

    /// <summary>
    /// Anonymises the record. <paramref name="by"/> is who's doing it, recorded as having deleted the files still
    /// live; without one, they're deleted by nobody in particular. Returns the audit entries for those deletions
    /// (<see cref="PatientPathwayAuditAction.AttachmentDeleted"/>, with the file's new name), for the caller to save
    /// with the record.
    /// </summary>
    public static IReadOnlyList<PatientPathwayAuditEntry> Apply(PatientRecord record, DateTime nowUtc, ActivityActor? by = null)
    {
        var patient = record.Patient;
        patient.FirstName = FirstName;
        patient.LastName = LastName;
        patient.Email = Email;
        patient.NhsNumber = null;
        patient.AddressLines = null;
        patient.Postcode = null;
        patient.MobilePhone = null;
        patient.PracticeId = null;
        patient.ReminderChannel = ReminderChannel.None;
        patient.DateOfBirth = new DateOnly(patient.DateOfBirth.Year, 1, 1);
        patient.RestrictedAtUtc = null;
        patient.RestrictionReason = null;
        patient.DisputedAtUtc = null;
        patient.DisputeReason = null;
        patient.RetentionKeptUntil = null;
        patient.RetentionKeptReason = null;
        patient.AnonymisedAtUtc = nowUtc;
        patient.LastUpdated = nowUtc;

        var titles = new Dictionary<Guid, string>();
        foreach (var pathway in record.Pathways)
        {
            pathway.Title = string.IsNullOrWhiteSpace(pathway.TemplateName) ? PathwayTitle : pathway.TemplateName;
            pathway.LegacyNotes = Removed(pathway.LegacyNotes);
            foreach (var note in pathway.Notes)
            {
                note.Text = Removed(note.Text);
                if (note.DeletionDetail is not null)
                    note.DeletionDetail = Removed(note.DeletionDetail);
                note.PendingDeletionRequestId = null;
            }
            foreach (var stage in pathway.Stages)
            {
                stage.Notes = Removed(stage.Notes);
                if (stage.SkipDetail is not null)
                    stage.SkipDetail = Removed(stage.SkipDetail);
            }
            titles[pathway.Id] = pathway.Title;
        }

        foreach (var version in record.NoteVersions)
            version.Text = Removed(version.Text);

        foreach (var request in record.NoteDeletionRequests)
        {
            if (request.Detail is not null)
                request.Detail = Removed(request.Detail);
            if (request.IsPending)
                request.Close(NoteDeletionRequestStatus.Closed, by?.UserId, by?.Username, nowUtc);
        }

        foreach (var entry in record.PathwayHistory)
        {
            entry.PathwayTitle = titles.GetValueOrDefault(entry.PatientPathwayId, PathwayTitle);
            entry.Details = ScrubDetails(entry.Details, entry.Action == PatientPathwayAuditAction.StageNotesUpdated);
        }

        foreach (var entry in record.Activity)
            entry.Details = ScrubDetails(entry.Details, isNotesChange: false);

        foreach (var appointment in record.Appointments)
        {
            appointment.Notes = Removed(appointment.Notes);
            if (appointment.CancelReason is not null)
                appointment.CancelReason = Removed(appointment.CancelReason);
        }

        foreach (var entry in record.AppointmentHistory)
        {
            // An appointment not for a pathway has no title to replace.
            if (entry.PathwayId is { } pathwayId)
                entry.PathwayTitle = titles.GetValueOrDefault(pathwayId, PathwayTitle);
            entry.Details = ScrubDetails(entry.Details, entry.Action == AppointmentEventAction.NotesUpdated);
        }

        foreach (var referral in record.Referrals)
        {
            referral.Reason = Removed(referral.Reason);
            referral.ReferrerPractice = Removed(referral.ReferrerPractice);
            if (referral.ReferrerClinician is not null)
                referral.ReferrerClinician = Removed(referral.ReferrerClinician);
            referral.ReferrerOdsCode = null;
            referral.PracticeId = null;
            if (referral.DecisionDetail is not null)
                referral.DecisionDetail = Removed(referral.DecisionDetail);
            if (referral.RedirectedTo is not null)
                referral.RedirectedTo = Removed(referral.RedirectedTo);
            if (referral.RedirectDetail is not null)
                referral.RedirectDetail = Removed(referral.RedirectDetail);
            foreach (var pause in referral.Pauses.Where(p => p.Detail is not null))
                pause.Detail = Removed(pause.Detail!);
        }

        foreach (var entry in record.ReferralHistory)
            entry.Details = ScrubDetails(entry.Details, isNotesChange: false);

        foreach (var notification in record.Notifications.Where(n => !Notification.HasFixedTitle(n.Kind)))
            notification.Title = notification.PathwayId is { } id ? titles.GetValueOrDefault(id, PathwayTitle) : PathwayTitle;

        var deletions = new List<PatientPathwayAuditEntry>();
        var actor = new PathwayActor(by?.UserId ?? Guid.Empty, by?.Username ?? string.Empty);
        foreach (var attachment in record.Attachments)
        {
            attachment.FileName = FileName + (AttachmentFileTypes.FromContentType(attachment.ContentType)?.Extensions[0] ?? string.Empty);
            if (attachment.IsDeleted)
                continue;

            attachment.DeletedAtUtc = nowUtc;
            attachment.DeletedByUserId = by?.UserId;
            attachment.DeletedByUsername = by?.Username;

            // Audited like any other deletion, each a tick after the last so they keep their order.
            var pathway = record.Pathways.FirstOrDefault(p => p.Id == attachment.PathwayId);
            var stage = pathway?.Stages.FirstOrDefault(s => s.Id == attachment.StageId);
            var target = new AttachmentTarget(attachment.PatientId, pathway, stage)
            {
                Referral = record.Referrals.FirstOrDefault(r => r.Id == attachment.ReferralId)
            };
            deletions.Add(AttachmentAudit.Entry(
                attachment, target, actor, PatientPathwayAuditAction.AttachmentDeleted, nowUtc.AddTicks(deletions.Count)));
        }

        return deletions;
    }

    /// <summary>
    /// An entry's details with their free text replaced: any <c>title</c>, <c>notes</c> or <c>reason</c> (or the
    /// <c>from</c> and <c>to</c> under one), and for a notes change the top-level <c>from</c> and <c>to</c>.
    /// </summary>
    public static string ScrubDetails(string details, bool isNotesChange)
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

        if (isNotesChange)
        {
            ReplaceText(root, "from");
            ReplaceText(root, "to");
        }

        ScrubKeys(root);
        return root.ToJsonString();
    }

    private static void ScrubKeys(JsonObject node)
    {
        foreach (var (key, value) in node.ToList())
        {
            if (FreeTextKeys.Contains(key))
            {
                if (value is JsonObject change)
                {
                    ReplaceText(change, "from");
                    ReplaceText(change, "to");
                }
                else
                {
                    ReplaceText(node, key);
                }
            }
            else if (value is JsonObject child)
            {
                ScrubKeys(child);
            }
        }
    }

    private static void ReplaceText(JsonObject node, string key)
    {
        if (node[key] is JsonValue value && value.TryGetValue<string>(out var text))
            node[key] = Removed(text);
    }

    private static string Removed(string text) => string.IsNullOrEmpty(text) ? text : RemovedText;
}
