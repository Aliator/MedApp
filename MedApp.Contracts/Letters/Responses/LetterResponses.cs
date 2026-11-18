using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Contracts.Letters.Responses;

/// <summary>A letter in a list: who it's about, what it is and where it is. No text.</summary>
public class LetterSummaryResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>The patient's full name, for the list.</summary>
    public string PatientName { get; set; } = string.Empty;

    public Guid? PathwayId { get; set; }

    /// <summary>The pathway's title, if it's about one that still exists.</summary>
    public string? PathwayTitle { get; set; }

    public Guid? ReferralId { get; set; }

    /// <summary>By name: <c>ClinicLetter</c>, <c>DischargeSummary</c>, <c>ReferralResponse</c>, ...</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The template it was written from, by its name then.</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary><c>Draft</c>, <c>AwaitingApproval</c>, <c>Approved</c> or <c>Withdrawn</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>A draft its approver returned (it has a return comment).</summary>
    public bool IsReturned { get; set; }

    public Guid AuthorUserId { get; set; }

    public string AuthorUsername { get; set; } = string.Empty;

    public Guid? ApproverUserId { get; set; }

    /// <summary>Whom it's awaiting approval from, or who approved it.</summary>
    public string? ApproverUsername { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime? WithdrawnAtUtc { get; set; }

    public int RecipientCount { get; set; }

    /// <summary>Recipients it hasn't reached yet (not sent, or a failed secure email).</summary>
    public int UnsentCount { get; set; }
}

/// <summary>
/// A letter, as its page shows it: its text (with merge fields still in braces: they're filled when it's approved),
/// recipients and where it is, the patient for the banner, and what the caller may do. The text is here for anyone who
/// can see patients, as notes are; it never reaches a staff timeline, a notification or an email.
/// </summary>
public sealed class LetterResponse : LetterSummaryResponse
{
    /// <summary>The patient, for the banner.</summary>
    public PathwayPatientResponse? Patient { get; set; }

    /// <summary>The patient's restricted, disputed and anonymised flags, for the banners.</summary>
    public PatientFlagsResponse? PatientFlags { get; set; }

    public string Letterhead { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Closing { get; set; } = string.Empty;

    public bool LargePrint { get; set; }

    /// <summary>Why it was returned, while it's a returned draft.</summary>
    public string? ReturnComment { get; set; }

    public DateTime? ReturnedAtUtc { get; set; }

    public string? ReturnedByUsername { get; set; }

    public string? WithdrawnByUsername { get; set; }

    /// <summary>Why an approved letter was withdrawn.</summary>
    public string? WithdrawnReason { get; set; }

    /// <summary>The approved PDF, a file: download it with <c>GET api/attachments/{id}/content</c>.</summary>
    public Guid? PdfAttachmentId { get; set; }

    /// <summary>The withdrawn letter this one corrects.</summary>
    public Guid? ReplacesLetterId { get; set; }

    /// <summary>The correction written for this one, once withdrawn, if any.</summary>
    public Guid? ReplacedByLetterId { get; set; }

    /// <summary>
    /// The merge fields its text uses that have no value for this patient now (e.g. <c>patient.address</c>): it can't be
    /// approved until they have one or are removed. Its approver's are filled when it's approved, so they're never here.
    /// </summary>
    public IReadOnlyList<string> MissingMergeFields { get; set; } = [];

    /// <summary>Pass back as <c>expectedVersion</c> with a change.</summary>
    public int Version { get; set; }

    public List<LetterRecipientResponse> Recipients { get; set; } = [];

    /// <summary>The caller may edit it and its recipients: its author, while it's a draft and the record can change.</summary>
    public bool CanEdit { get; set; }

    /// <summary>The caller may submit it to an approver (as for editing).</summary>
    public bool CanSubmit { get; set; }

    /// <summary>
    /// The caller may approve it: its named approver while it awaits approval, or its author while it's a draft, if they
    /// may approve letters.
    /// </summary>
    public bool CanApprove { get; set; }

    /// <summary>The caller may return it to its author: its named approver, while it awaits approval.</summary>
    public bool CanReturn { get; set; }

    /// <summary>The caller may discard it: its author, while it's a draft.</summary>
    public bool CanDiscard { get; set; }

    /// <summary>The caller may withdraw it: its approver, or someone who can correct records, once it's approved.</summary>
    public bool CanWithdraw { get; set; }

    /// <summary>The caller may write a correction: it's withdrawn after approval, they write letters, and none is written yet.</summary>
    public bool CanWriteCorrection { get; set; }

    /// <summary>The caller may download the draft's preview: its author or approver, or anyone who may approve letters.</summary>
    public bool CanPreview { get; set; }

    /// <summary>Why it can't change now (the record restricted or anonymised), or null.</summary>
    public string? ReadOnlyReason { get; set; }

    /// <summary>Whether MedApp can send letters by secure email here (it's configured, and the relay declared to meet DCB1596).</summary>
    public bool SecureEmailAvailable { get; set; }
}

/// <summary>One recipient of a letter, and whether and how it reached them.</summary>
public sealed class LetterRecipientResponse
{
    public Guid Id { get; set; }

    /// <summary><c>GpPractice</c>, <c>Patient</c>, <c>Referrer</c> or <c>Other</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid? PracticeId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Address lines, separated by line feeds.</summary>
    public string? AddressLines { get; set; }

    public string? Postcode { get; set; }

    /// <summary>The secure email address it can be sent to, if any.</summary>
    public string? Email { get; set; }

    /// <summary><c>NotSent</c>, <c>Sent</c> or <c>Failed</c> (a secure email the mail server didn't accept).</summary>
    public string DispatchStatus { get; set; } = string.Empty;

    /// <summary>How it was sent: <c>Post</c>, <c>HandedToPatient</c>, <c>SecureEmail</c>, <c>EmailOutsideMedApp</c> or <c>Other</c>.</summary>
    public string? Method { get; set; }

    public DateOnly? SentOn { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string? SentByUsername { get; set; }

    /// <summary>The caller may send it by secure email from MedApp now.</summary>
    public bool CanSendSecureEmail { get; set; }

    /// <summary>Why it can't go by secure email from MedApp, when it's approved and not yet sent.</summary>
    public string? WhyNoSecureEmail { get; set; }

    /// <summary>The caller may record it as sent another way now.</summary>
    public bool CanRecordSent { get; set; }
}

/// <summary>The side nav's count: the caller's letters to approve, and every approved letter still to send.</summary>
public sealed class LetterCountResponse
{
    public int ToApprove { get; set; }

    public int ToSend { get; set; }
}
