namespace MedApp.Domain.Letters;

/// <summary>
/// A letter about a patient (decisions 100 to 108 of docs/centre-phases-3-5-plan.md): clinical correspondence written
/// by a member of staff, from a template whose text it copies when it's started, so editing the template changes only
/// new letters. It's about one of the patient's pathways or referrals. MedApp fills its merge fields with identity and
/// administrative data only; <b>the body is its author's own text</b>, and nothing in it is generated.
/// <para>
/// A draft is its author's alone. It's submitted to a named approver (or approved by its author, if they may approve),
/// who approves it (its PDF is made, kept as a file, and the letter frozen) or returns it with a comment. An approved
/// letter is sent to each recipient, by secure email from MedApp or recorded as sent another way, and can be withdrawn
/// if it's found wrong, and a correction written. Its history is in the pathway audit log (actions 29 to 34), whose
/// details hold no free text.
/// </para>
/// </summary>
public sealed class Letter
{
    /// <summary>A body's longest (decision 100), the same as a template's skeleton.</summary>
    public const int MaxBodyLength = LetterTemplate.MaxBodyLength;

    public const int MaxLetterheadLength = LetterTemplate.MaxLetterheadLength;

    public const int MaxClosingLength = LetterTemplate.MaxClosingLength;

    /// <summary>A return comment's or withdrawal reason's longest.</summary>
    public const int MaxCommentLength = 500;

    /// <summary>The most recipients one letter has.</summary>
    public const int MaxRecipients = 10;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }

    /// <summary>The pathway it's about, if any; at least one of this and <see cref="ReferralId"/> is set.</summary>
    public Guid? PathwayId { get; init; }

    /// <summary>The referral it's about (a reply to the referrer, or a request for more information), if any.</summary>
    public Guid? ReferralId { get; init; }

    /// <summary>The template it was written from (no foreign key: a template is deleted only while no letter has it).</summary>
    public Guid? TemplateId { get; init; }

    /// <summary>The template's name when it was started (admin-written), so the letter still reads well if it's renamed.</summary>
    public string TemplateName { get; init; } = string.Empty;

    public LetterKind Kind { get; init; }

    /// <summary>Plain lines at the top of the first page; may hold merge fields.</summary>
    public string Letterhead { get; set; } = string.Empty;

    /// <summary>The author's text, in Markdown (at most <see cref="MaxBodyLength"/>); may hold merge fields.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Plain lines after the body: the sign-off; may hold merge fields.</summary>
    public string Closing { get; set; } = string.Empty;

    /// <summary>Made in 16 point instead of 11, for the Accessible Information Standard.</summary>
    public bool LargePrint { get; set; }

    public LetterStatus Status { get; set; }

    public Guid AuthorUserId { get; init; }

    /// <summary>
    /// Whom it was submitted to, while awaiting approval; once approved, who approved it. Null for a draft never
    /// submitted (and kept, after a return, as who returned it until it's submitted again).
    /// </summary>
    public Guid? ApproverUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    /// <summary>Why the approver returned it (free text, 500), while it's a returned draft; cleared when it's submitted again.</summary>
    public string? ReturnComment { get; set; }

    public DateTime? ReturnedAtUtc { get; set; }

    public Guid? ReturnedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    /// <summary>The approved PDF, an attachment on the pathway (or the referral): the record of what was sent.</summary>
    public Guid? PdfAttachmentId { get; set; }

    public DateTime? WithdrawnAtUtc { get; set; }

    public Guid? WithdrawnByUserId { get; set; }

    /// <summary>Why an approved letter was withdrawn (free text, 500); null for a discarded draft.</summary>
    public string? WithdrawnReason { get; set; }

    /// <summary>The withdrawn letter this one corrects, if it's a correction.</summary>
    public Guid? ReplacesLetterId { get; init; }

    /// <summary>
    /// One more for each change. A change names the version it started from, so a page loaded before someone else's
    /// change is refused (409).
    /// </summary>
    public int Version { get; set; }

    /// <summary>SQL Server's row version: two saves at once can't both win.</summary>
    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }

    public List<LetterRecipient> Recipients { get; set; } = [];

    /// <summary>Whether it has been sent to every recipient (and has at least one).</summary>
    public bool IsSentToEveryone =>
        Recipients.Count > 0 && Recipients.All(r => r.DispatchStatus == LetterDispatchStatus.Sent);
}

/// <summary>
/// One recipient of a letter, and whether and how it reached them. Its name and address are the ones on record when
/// the draft is saved, and are copied again when it's approved, so a later move doesn't change what was sent.
/// </summary>
public sealed class LetterRecipient
{
    public const int MaxNameLength = 200;

    /// <summary>Up to three lines of 100, joined by line feeds (as a patient's).</summary>
    public const int MaxAddressLength = 302;

    public const int MaxPostcodeLength = 8;

    public const int MaxEmailLength = 254;

    public const int MaxTransportReferenceLength = 100;

    public Guid Id { get; init; }

    public Guid LetterId { get; init; }

    /// <summary>Its order on the letter: the first is the one it's addressed to; the rest are sent copies.</summary>
    public int Position { get; set; }

    public LetterRecipientKind Kind { get; init; }

    /// <summary>The practice from the directory, for a GP practice (and a referrer whose referral names one).</summary>
    public Guid? PracticeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? AddressLines { get; set; }

    public string? Postcode { get; set; }

    /// <summary>A secure email address (a practice's, or one typed for another recipient), in an allowed domain; never a patient's.</summary>
    public string? Email { get; set; }

    public LetterDispatchStatus DispatchStatus { get; set; }

    public LetterDispatchMethod? Method { get; set; }

    /// <summary>The UK date it was sent (today, or up to 7 days back for one recorded).</summary>
    public DateOnly? SentOn { get; set; }

    /// <summary>When it was recorded as sent, or the last secure email attempt.</summary>
    public DateTime? SentAtUtc { get; set; }

    public Guid? SentByUserId { get; set; }

    /// <summary>The mail server's reference for a secure email (its message ID), for tracing it with the relay.</summary>
    public string? TransportReference { get; set; }
}
