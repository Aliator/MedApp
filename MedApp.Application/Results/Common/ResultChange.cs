using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using static MedApp.Domain.Results.ResultStatus;

namespace MedApp.Application.Results.Common;

/// <summary>
/// Applies one request's change to a result and writes its audit entries (<see cref="PatientPathwayAuditEntry"/>,
/// actions 24 to 28), to save in the same <c>SaveChangesAsync</c>, so the history can't miss one. It moves the result's
/// <see cref="Result.Version"/> on once per change. Handlers check first that the change is allowed
/// (<see cref="ResultWorkflow"/>); this only applies it.
/// <para>
/// A result isn't a change to its pathway, so this doesn't go through <see cref="PathwayChange"/> (decision 70). A
/// result with no pathway is recorded against the patient (<see cref="PatientPathwayAuditEntry.NoPathway"/>), as files
/// on the patient are.
/// </para>
/// <para>
/// Details hold the result's ID and source, its specimen's ID, number and type name (admin-written), statuses, the
/// action by name, the report file's ID and usernames; <b>never free text</b>: the title, action detail and error detail
/// live only on the result.
/// </para>
/// </summary>
public sealed class ResultChange(Result result, PatientPathway? pathway, PathwayActor actor, DateTime nowUtc)
{
    /// <summary>The field a reassignment's change names on the timeline (usernames, structured).</summary>
    public const string ResponsibleField = "responsibleClinician";

    /// <summary>The field the full view's Recorded entry shows the title in (admins only: free text).</summary>
    public const string TitleField = "title";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];

    public Result Result => result;

    public IReadOnlyList<PatientPathwayAuditEntry> AuditEntries => _auditEntries;

    /// <summary>Records a new result as awaiting sign-off (its version is 1).</summary>
    public void Record(string responsibleUsername)
    {
        result.Version = 0;
        result.Status = AwaitingSignOff;
        Audit(PatientPathwayAuditAction.ResultRecorded, new
        {
            to = AwaitingSignOff.ToString(),
            attachmentId = result.ReportAttachmentId,
            responsible = responsibleUsername,
        });
    }

    public void MarkSeen()
    {
        var from = result.Status;
        result.Status = Seen;
        result.SeenAtUtc = nowUtc;
        result.SeenByUserId = actor.UserId;
        Audit(PatientPathwayAuditAction.ResultSeen, new { from = from.ToString(), to = Seen.ToString() });
    }

    /// <summary>Signs it off with the clinician's action; the detail is kept only on the result.</summary>
    public void SignOff(ResultAction action, string? detail)
    {
        var from = result.Status;
        result.Status = SignedOff;
        result.SignedOffAtUtc = nowUtc;
        result.SignedOffByUserId = actor.UserId;
        result.Action = action;
        result.ActionDetail = detail;
        Audit(PatientPathwayAuditAction.ResultSignedOff,
            new { from = from.ToString(), to = SignedOff.ToString(), action = action.ToString() });
    }

    /// <summary>Gives it to another responsible clinician (or the actor, taking it over).</summary>
    public void Reassign(Guid responsibleUserId, string? previousUsername, string? newUsername)
    {
        result.ResponsibleUserId = responsibleUserId;
        Audit(PatientPathwayAuditAction.ResultReassigned, new
        {
            changes = new Dictionary<string, object?>
            {
                [ResponsibleField] = new { from = previousUsername, to = newUsername },
            },
        });
    }

    /// <summary>
    /// Closes it as entered in error: the detail kept only on the result. A specimen's goes back to Received (its report
    /// cleared, its version moved on) to be reported again; the caller deletes the file.
    /// </summary>
    public void MarkEnteredInError(string detail)
    {
        var from = result.Status;
        result.Status = EnteredInError;
        result.EnteredInErrorAtUtc = nowUtc;
        result.EnteredInErrorByUserId = actor.UserId;
        result.ErrorDetail = detail;

        var specimen = result.Specimen;
        var specimenBack = specimen is not null && specimen.Status == SpecimenStatus.Reported;
        if (specimenBack)
        {
            specimen!.Status = SpecimenStatus.Received;
            specimen.ReportedAtUtc = null;
            specimen.ReportedByUserId = null;
            specimen.ReportAttachmentId = null;
            specimen.Version++;
        }

        Audit(PatientPathwayAuditAction.ResultEnteredInError, new
        {
            from = from.ToString(),
            to = EnteredInError.ToString(),
            attachmentId = result.ReportAttachmentId,
            specimenStatus = specimenBack ? SpecimenStatus.Received.ToString() : null,
        });
    }

    private void Audit(PatientPathwayAuditAction action, object values)
    {
        result.Version++;

        var details = new Dictionary<string, object?>
        {
            ["resultId"] = result.Id,
            ["source"] = result.Source.ToString(),
            ["specimenId"] = result.SpecimenId,
            ["number"] = result.Specimen?.Number,
            ["specimenType"] = result.Specimen?.SpecimenType?.Name,
        };
        foreach (var property in JsonSerializer.SerializeToElement(values, Json).EnumerateObject())
            details[property.Name] = property.Value;

        _auditEntries.Add(new PatientPathwayAuditEntry
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = pathway?.Id ?? PatientPathwayAuditEntry.NoPathway,
            PathwayTitle = pathway?.Title ?? string.Empty,
            PatientId = result.PatientId,
            TeamId = result.TeamId,
            StageId = result.Specimen?.StageId,
            StageName = result.Specimen is { } s ? pathway?.Stages.FirstOrDefault(st => st.Id == s.StageId)?.Name : null,
            Action = action,
            UserId = actor.UserId,
            Username = actor.Username,
            AtUtc = nowUtc.AddTicks(_auditEntries.Count),
            Details = JsonSerializer.Serialize(details, Json),
        });
    }
}
