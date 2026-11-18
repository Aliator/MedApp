using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MedApp.Domain.Patients;

namespace MedApp.Application.Letters.Common;

/// <summary>What a letter's page can offer its viewer, worked out by <see cref="LetterRecords"/>.</summary>
/// <param name="SecureEmailAvailable">Whether MedApp can send letters by secure email here.</param>
/// <param name="AllowedDomains">The email domains a secure email may go to.</param>
/// <param name="MissingMergeFields">The merge fields with no value now, by name.</param>
/// <param name="CorrectionId">The correction written for it, if any.</param>
public sealed record LetterPageFacts(
    bool SecureEmailAvailable,
    IReadOnlyList<string> AllowedDomains,
    IReadOnlyList<string> MissingMergeFields,
    Guid? CorrectionId);

/// <summary>Letters to their responses: staff by username, with what the viewer may do.</summary>
public static class LetterMapper
{
    /// <summary>Everyone a letter names (for their usernames).</summary>
    public static IEnumerable<Guid> PeopleIn(Letter letter) =>
        new[] { letter.AuthorUserId, letter.ApproverUserId, letter.ReturnedByUserId, letter.WithdrawnByUserId }
            .Concat(letter.Recipients.Select(r => r.SentByUserId))
            .OfType<Guid>()
            .Distinct();

    public static LetterSummaryResponse ToSummary(
        Letter letter, string patientName, string? pathwayTitle, IReadOnlyDictionary<Guid, string> usernames) =>
        Fill(new LetterSummaryResponse(), letter, patientName, pathwayTitle, usernames);

    public static LetterResponse ToResponse(
        LetterRecord record,
        IReadOnlyDictionary<Guid, string> usernames,
        PathwayActor viewer,
        LetterPageFacts view)
    {
        var letter = record.Letter;
        var patient = record.Patient;
        var response = Fill(new LetterResponse(), letter, PatientMapper.FullName(patient), record.Pathway?.Title, usernames);

        var whyNot = LetterWorkflow.WhyRecordStopsLetters(patient);
        var canChange = whyNot is null;

        response.Patient = PathwayMapper.ToPatient(patient);
        response.PatientFlags = PatientMapper.ToFlags(patient);
        response.Letterhead = letter.Letterhead;
        response.Body = letter.Body;
        response.Closing = letter.Closing;
        response.LargePrint = letter.LargePrint;
        response.ReturnComment = letter.ReturnComment;
        response.ReturnedAtUtc = letter.ReturnedAtUtc;
        response.ReturnedByUsername = Username(letter.ReturnedByUserId, usernames);
        response.WithdrawnByUsername = Username(letter.WithdrawnByUserId, usernames);
        response.WithdrawnReason = letter.WithdrawnReason;
        response.PdfAttachmentId = letter.PdfAttachmentId;
        response.ReplacesLetterId = letter.ReplacesLetterId;
        response.ReplacedByLetterId = view.CorrectionId;
        response.MissingMergeFields = letter.Status is LetterStatus.Draft or LetterStatus.AwaitingApproval
            ? view.MissingMergeFields
            : [];
        response.Version = letter.Version;

        var authorsDraft = LetterWorkflow.IsAuthorsDraft(letter, viewer);
        response.CanEdit = canChange && authorsDraft;
        response.CanSubmit = canChange && authorsDraft;
        response.CanDiscard = authorsDraft && !PatientRules.IsAnonymised(patient);
        response.CanApprove = canChange && LetterWorkflow.CanApprove(letter, viewer);
        response.CanReturn = LetterWorkflow.CanReturn(letter, viewer) && !PatientRules.IsAnonymised(patient);
        response.CanWithdraw = LetterWorkflow.CanWithdraw(letter, viewer) && !PatientRules.IsAnonymised(patient);
        response.CanWriteCorrection = canChange
                                      && viewer.Has(MedApp.Application.Common.Identity.Permission.WriteLetters)
                                      && LetterWorkflow.CanBeCorrected(letter, view.CorrectionId is not null);
        response.CanPreview = LetterWorkflow.CanPreview(letter, viewer);
        response.ReadOnlyReason = whyNot;
        response.SecureEmailAvailable = view.SecureEmailAvailable;

        var canSend = canChange && viewer.Has(MedApp.Application.Common.Identity.Permission.SendLetters);
        response.Recipients = letter.Recipients
            .OrderBy(r => r.Position)
            .Select(r =>
            {
                var awaits = LetterWorkflow.AwaitsSending(letter, r);
                var whyNoEmail = awaits ? LetterWorkflow.WhyNoSecureEmail(r, view.SecureEmailAvailable, view.AllowedDomains) : null;
                return ToResponse(r, usernames, canSend && awaits && whyNoEmail is null, whyNoEmail, canSend && awaits);
            })
            .ToList();
        return response;
    }

    public static LetterRecipientResponse ToResponse(
        LetterRecipient recipient,
        IReadOnlyDictionary<Guid, string> usernames,
        bool canSendSecureEmail = false,
        string? whyNoSecureEmail = null,
        bool canRecordSent = false) => new()
    {
        Id = recipient.Id,
        Kind = recipient.Kind.ToString(),
        PracticeId = recipient.PracticeId,
        Name = recipient.Name,
        AddressLines = recipient.AddressLines,
        Postcode = recipient.Postcode,
        Email = recipient.Email,
        DispatchStatus = recipient.DispatchStatus.ToString(),
        Method = recipient.Method?.ToString(),
        SentOn = recipient.SentOn,
        SentAtUtc = recipient.SentAtUtc,
        SentByUsername = Username(recipient.SentByUserId, usernames),
        CanSendSecureEmail = canSendSecureEmail,
        WhyNoSecureEmail = whyNoSecureEmail,
        CanRecordSent = canRecordSent,
    };

    private static T Fill<T>(
        T response, Letter letter, string patientName, string? pathwayTitle, IReadOnlyDictionary<Guid, string> usernames)
        where T : LetterSummaryResponse
    {
        response.Id = letter.Id;
        response.PatientId = letter.PatientId;
        response.PatientName = patientName;
        response.PathwayId = letter.PathwayId;
        response.PathwayTitle = pathwayTitle;
        response.ReferralId = letter.ReferralId;
        response.Kind = letter.Kind.ToString();
        response.TemplateName = letter.TemplateName;
        response.Status = letter.Status.ToString();
        response.IsReturned = LetterWorkflow.IsReturned(letter);
        response.AuthorUserId = letter.AuthorUserId;
        response.AuthorUsername = Username(letter.AuthorUserId, usernames) ?? string.Empty;
        response.ApproverUserId = letter.ApproverUserId;
        response.ApproverUsername = Username(letter.ApproverUserId, usernames);
        response.CreatedAt = letter.CreatedAt;
        response.LastUpdated = letter.LastUpdated;
        response.SubmittedAtUtc = letter.SubmittedAtUtc;
        response.ApprovedAtUtc = letter.ApprovedAtUtc;
        response.WithdrawnAtUtc = letter.WithdrawnAtUtc;
        response.RecipientCount = letter.Recipients.Count;
        response.UnsentCount = letter.Recipients.Count(r => r.DispatchStatus != LetterDispatchStatus.Sent);
        return response;
    }

    /// <summary>A username, or "Deleted user" for an account that's gone; null for nobody.</summary>
    private static string? Username(Guid? id, IReadOnlyDictionary<Guid, string> usernames) =>
        id is { } known ? usernames.GetValueOrDefault(known, "Deleted user") : null;
}

/// <summary>A letter with what its rules and response need: its patient, and its pathway or referral.</summary>
public sealed record LetterRecord(Letter Letter, Patient Patient, Domain.Pathways.PatientPathways.PatientPathway? Pathway, Domain.Referrals.Referral? Referral);
