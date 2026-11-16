using System.Text.Json;
using System.Text.Json.Nodes;
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// Anonymises a patient's record in place (<see cref="PatientRecord"/>, loaded for update): their identifying details
/// and every piece of free text become placeholders, and everything else stays, so the record's structure, its
/// statistics (procedures, templates, statuses, dates, who did what and when) and its audit trail survive.
/// <list type="bullet">
/// <item>Name and email become placeholders; the NHS number is removed; the date of birth keeps only its year (1 January), for age statistics.</item>
/// <item>Procedure titles become the template's name; procedure and stage notes become <see cref="RemovedText"/>.</item>
/// <item>The procedure audit's titles, notes, reasons and changed text, and the activity log's reasons, are replaced the same way.</item>
/// <item>Appointments' notes and cancellation reasons, and their history's procedure titles, notes and reasons, are replaced the same way.</item>
/// <item>Notifications about the procedures take the new titles, except those whose title is fixed (monitoring's "Stage
/// finished"), which never held the procedure's title; their messages hold no free text either. A notification about
/// an appointment not for a procedure keeps its fixed title ("Appointment").</item>
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

    /// <summary>The title of a procedure with no template name, or one that has been deleted.</summary>
    public const string ProcedureTitle = "Anonymised procedure";

    /// <summary>Keys whose text is free text wherever they appear in an entry's details.</summary>
    private static readonly HashSet<string> FreeTextKeys = ["title", "notes", "reason"];

    public static void Apply(PatientRecord record, DateTime nowUtc)
    {
        var patient = record.Patient;
        patient.FirstName = FirstName;
        patient.LastName = LastName;
        patient.Email = Email;
        patient.NhsNumber = null;
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
        foreach (var procedure in record.Procedures)
        {
            procedure.Title = string.IsNullOrWhiteSpace(procedure.TemplateName) ? ProcedureTitle : procedure.TemplateName;
            procedure.Notes = Removed(procedure.Notes);
            foreach (var stage in procedure.Stages)
                stage.Notes = Removed(stage.Notes);
            titles[procedure.Id] = procedure.Title;
        }

        foreach (var entry in record.ProcedureHistory)
        {
            entry.ProcedureTitle = titles.GetValueOrDefault(entry.PatientProcedureId, ProcedureTitle);
            entry.Details = ScrubDetails(entry.Details, entry.Action == PatientProcedureAuditAction.StageNotesUpdated);
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
            // An appointment not for a procedure has no title to replace.
            if (entry.ProcedureId is { } procedureId)
                entry.ProcedureTitle = titles.GetValueOrDefault(procedureId, ProcedureTitle);
            entry.Details = ScrubDetails(entry.Details, entry.Action == AppointmentEventAction.NotesUpdated);
        }

        foreach (var notification in record.Notifications.Where(n => !Notification.HasFixedTitle(n.Kind)))
            notification.Title = notification switch
            {
                { ProcedureId: { } id } => titles.GetValueOrDefault(id, ProcedureTitle),
                { AppointmentId: not null } => AppointmentNotifications.DefaultTitle,
                _ => ProcedureTitle
            };
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
