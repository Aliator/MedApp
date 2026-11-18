namespace MedApp.Contracts.Letters.Requests;

/// <summary>
/// Starts a letter (a draft) from a template, about one of a patient's pathways or referrals (exactly one of the two).
/// The template must be the pathway's or referral's team's, or for every team, and not archived; from a
/// referral, it must be a referral response or a request for more information. It copies the template's text, and
/// starts with the patient's registered practice and the patient as recipients.
/// </summary>
public sealed class CreateLetterRequest
{
    public Guid TemplateId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? ReferralId { get; set; }
}

/// <summary>One recipient of a draft, as its author chose it.</summary>
public sealed class LetterRecipientRequest
{
    /// <summary><c>GpPractice</c>, <c>Patient</c>, <c>Referrer</c> or <c>Other</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>GpPractice: the practice from the directory (required).</summary>
    public Guid? PracticeId { get; set; }

    /// <summary>Other: the name (required, 200).</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Other (required), or a referrer whose referral names no practice from the directory: up to three lines of 100,
    /// separated by line breaks.
    /// </summary>
    public string? AddressLines { get; set; }

    /// <summary>Other, or a referrer as above: a UK postcode, optional.</summary>
    public string? Postcode { get; set; }

    /// <summary>Other: a secure email address, optional, in an allowed domain (<c>Letters:SecureEmailDomains</c>).</summary>
    public string? Email { get; set; }
}

/// <summary>Saves a draft: its text, large print and recipients. Only its author, only while it's a draft.</summary>
public sealed class UpdateLetterRequest
{
    public string Letterhead { get; set; } = string.Empty;

    /// <summary>Markdown, at most 20,000 characters; may hold merge fields (<c>{{patient.name}}</c>).</summary>
    public string Body { get; set; } = string.Empty;

    public string Closing { get; set; } = string.Empty;

    public bool LargePrint { get; set; }

    /// <summary>In order: the first is the one it's addressed to; at most 10.</summary>
    public List<LetterRecipientRequest> Recipients { get; set; } = [];

    public int ExpectedVersion { get; set; }
}

/// <summary>Submits a draft to a named holder of ApproveLetters (not its author).</summary>
public sealed class SubmitLetterRequest
{
    public Guid ApproverUserId { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>A change that needs only the version the caller saw (approving, discarding, a secure email).</summary>
public sealed class LetterVersionRequest
{
    public int ExpectedVersion { get; set; }
}

/// <summary>Returns a letter to its author, with a comment (required, 500).</summary>
public sealed class ReturnLetterRequest
{
    public string Comment { get; set; } = string.Empty;

    public int ExpectedVersion { get; set; }
}

/// <summary>Withdraws an approved letter found wrong, with a reason (required, 500).</summary>
public sealed class WithdrawLetterRequest
{
    public string Reason { get; set; } = string.Empty;

    public int ExpectedVersion { get; set; }
}

/// <summary>Records a letter as sent to one recipient outside MedApp.</summary>
public sealed class RecordLetterSentRequest
{
    /// <summary><c>Post</c>, <c>HandedToPatient</c>, <c>EmailOutsideMedApp</c> or <c>Other</c>.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>The UK date it was sent: today, or up to 7 days back, and not before it was approved.</summary>
    public DateOnly SentOn { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>
/// The letters list's views and filters. With a view: <c>MyDrafts</c>, <c>AwaitingMyApproval</c>, <c>ReturnedToMe</c>,
/// <c>ToSend</c> (approved, with a recipient not yet sent) or <c>Sent</c> (approved and sent to everyone, newest
/// first). Without one, every letter of the patient, pathway or referral given (one is then required).
/// </summary>
public sealed class SearchLettersRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? View { get; set; }

    public Guid? PatientId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? ReferralId { get; set; }

    /// <summary>The patient's full name contains this, ignoring case. At most 200 characters.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// <c>patient</c>, <c>letter</c> (kind, then template), <c>status</c> or <c>latest</c> (the latest step). Without
    /// one, the view's own order.
    /// </summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;
}
