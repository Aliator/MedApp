using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.Domain.Letters.LetterStatus;

namespace MedApp.Application.Letters.Common;

/// <summary>
/// Applies one request's change to a letter and writes its audit entries (<see cref="PatientPathwayAuditEntry"/>,
/// actions 29 to 34, decision 70), to save in the same <c>SaveChangesAsync</c>, so its history can't miss one. Each
/// change moves the letter's <see cref="Letter.Version"/> on once and its <see cref="Letter.LastUpdated"/>. Handlers
/// check first that the change is allowed (<see cref="LetterWorkflow"/>); this only applies it.
/// <para>
/// A letter isn't a change to its pathway, so this doesn't go through <see cref="PathwayChange"/> and doesn't move the
/// pathway's LastUpdated, as attached files don't. A letter about a referral has no pathway
/// (<see cref="PatientPathwayAuditEntry.NoPathway"/>).
/// </para>
/// <para>
/// Details hold the letter's ID, kind and template name (admin-written), its referral's ID, usernames, recipients'
/// kinds, methods and dispatch statuses, dates and the PDF's file ID; <b>never free text</b>: the body, the return
/// comment, the withdrawal reason and recipients' names and addresses live only on the letter (DPIA risk 30). Drafts
/// aren't audited edit by edit: the approved PDF is the record (decision 100).
/// </para>
/// </summary>
public sealed class LetterChange(Letter letter, PatientPathway? pathway, PathwayActor actor, DateTime nowUtc)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];

    public Letter Letter => letter;

    public IReadOnlyList<PatientPathwayAuditEntry> AuditEntries => _auditEntries;

    /// <summary>Records a new draft (its version is 1).</summary>
    public void Draft()
    {
        letter.Version = 0;
        letter.Status = LetterStatus.Draft;
        Record(PatientPathwayAuditAction.LetterDrafted, new
        {
            replacesLetterId = letter.ReplacesLetterId,
            recipients = letter.Recipients.OrderBy(r => r.Position).Select(r => r.Kind.ToString()).ToList(),
        });
    }

    /// <summary>A draft's text and recipients saved: no audit entry (decision 100), but a new version.</summary>
    public void Edit() => Touch();

    /// <summary>Submits it to <paramref name="approverUserId"/>, clearing any earlier return comment.</summary>
    public void Submit(Guid approverUserId, string approverUsername)
    {
        letter.Status = AwaitingApproval;
        letter.ApproverUserId = approverUserId;
        letter.SubmittedAtUtc = nowUtc;
        letter.ReturnComment = null;
        letter.ReturnedAtUtc = null;
        letter.ReturnedByUserId = null;
        Record(PatientPathwayAuditAction.LetterSubmitted, new { approverUserId, approver = approverUsername });
    }

    /// <summary>Returns it to its author as a draft, with a comment (kept only on the letter).</summary>
    public void Return(string comment)
    {
        letter.Status = LetterStatus.Draft;
        letter.ReturnComment = comment;
        letter.ReturnedAtUtc = nowUtc;
        letter.ReturnedByUserId = actor.UserId;
        Record(PatientPathwayAuditAction.LetterReturned, new { });
    }

    /// <summary>Approves it, with its PDF (an attachment already made), by the actor.</summary>
    public void Approve(Guid pdfAttachmentId)
    {
        var selfApproved = letter.Status == LetterStatus.Draft;
        letter.Status = Approved;
        letter.ApproverUserId = actor.UserId;
        letter.ApprovedAtUtc = nowUtc;
        letter.PdfAttachmentId = pdfAttachmentId;
        letter.ReturnComment = null;
        Record(PatientPathwayAuditAction.LetterApproved, new
        {
            attachmentId = pdfAttachmentId,
            selfApproved,
            largePrint = letter.LargePrint,
            recipients = letter.Recipients.OrderBy(r => r.Position).Select(r => r.Kind.ToString()).ToList(),
        });
    }

    /// <summary>
    /// Records a sending to one recipient: sent (by staff, on <paramref name="sentOn"/>; or by secure email, with the
    /// mail server's reference), or a secure email that failed.
    /// </summary>
    public void Send(
        LetterRecipient recipient,
        LetterDispatchMethod method,
        LetterDispatchStatus status,
        DateOnly? sentOn,
        string? transportReference = null)
    {
        recipient.DispatchStatus = status;
        recipient.Method = method;
        recipient.SentAtUtc = nowUtc;
        recipient.SentByUserId = actor.UserId;
        recipient.SentOn = status == LetterDispatchStatus.Sent ? sentOn : null;
        recipient.TransportReference = transportReference;
        Record(PatientPathwayAuditAction.LetterSent, new
        {
            recipientId = recipient.Id,
            recipientKind = recipient.Kind.ToString(),
            method = method.ToString(),
            dispatchStatus = status.ToString(),
            sentOn = recipient.SentOn,
        });
    }

    /// <summary>
    /// Withdraws an approved letter, with a reason (kept only on the letter), or discards a draft (no reason). The
    /// details name the recipients it had already reached, by kind.
    /// </summary>
    public void Withdraw(string? reason)
    {
        var wasDraft = letter.Status != Approved;
        var sentTo = letter.Recipients
            .Where(r => r.DispatchStatus == LetterDispatchStatus.Sent)
            .OrderBy(r => r.Position)
            .Select(r => r.Kind.ToString())
            .ToList();
        letter.Status = Withdrawn;
        letter.WithdrawnAtUtc = nowUtc;
        letter.WithdrawnByUserId = actor.UserId;
        letter.WithdrawnReason = reason;
        Record(PatientPathwayAuditAction.LetterWithdrawn, new { wasDraft, sentTo });
    }

    /// <summary>A new version, now.</summary>
    private void Touch()
    {
        letter.Version++;
        letter.LastUpdated = nowUtc;
    }

    private void Record(PatientPathwayAuditAction action, object values)
    {
        Touch();

        // The letter's identity first, then what this change did.
        var details = new Dictionary<string, object?>
        {
            ["letterId"] = letter.Id,
            ["kind"] = letter.Kind.ToString(),
            ["templateName"] = letter.TemplateName,
        };
        if (letter.ReferralId is { } referralId)
            details["referralId"] = referralId;
        foreach (var property in JsonSerializer.SerializeToElement(values, Json).EnumerateObject())
            details[property.Name] = property.Value;

        _auditEntries.Add(new PatientPathwayAuditEntry
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = letter.PathwayId ?? PatientPathwayAuditEntry.NoPathway,
            PathwayTitle = pathway?.Title ?? string.Empty,
            PatientId = letter.PatientId,
            Action = action,
            UserId = actor.UserId,
            Username = actor.Username,
            // One change's entries are a tick apart, so they sort in order (as PathwayChange's).
            AtUtc = nowUtc.AddTicks(_auditEntries.Count),
            Details = JsonSerializer.Serialize(details, Json),
        });
    }
}
