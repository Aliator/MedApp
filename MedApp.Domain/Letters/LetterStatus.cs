namespace MedApp.Domain.Letters;

/// <summary>
/// Where a letter is. Stored by number, so never renumber it (docs/centre-phases-3-5-plan.md, section 5). A returned
/// letter is a <see cref="Draft"/> with a return comment (<see cref="Letter.ReturnComment"/>).
/// </summary>
public enum LetterStatus
{
    /// <summary>Being written by its author, who alone edits it.</summary>
    Draft = 0,

    /// <summary>Submitted to a named approver; frozen until they approve or return it.</summary>
    AwaitingApproval = 1,

    /// <summary>Approved: its PDF is made and kept as a file, and it can be sent to its recipients.</summary>
    Approved = 2,

    /// <summary>Withdrawn: an approved letter found wrong, or a draft its author discarded. Final.</summary>
    Withdrawn = 3
}

/// <summary>Who a letter goes to. Stored by number, so never renumber it.</summary>
public enum LetterRecipientKind
{
    /// <summary>A GP practice from the directory (usually the patient's registered practice).</summary>
    GpPractice = 0,

    /// <summary>The patient, at their address on record.</summary>
    Patient = 1,

    /// <summary>Whoever referred the patient: the letter's referral's (or its pathway's referral's) practice and clinician.</summary>
    Referrer = 2,

    /// <summary>Anyone else, with a name and address typed by the author.</summary>
    Other = 3,

    /// <summary>
    /// A hospital (MC20, MC28: an onward referral, a lab report), with its name and address typed by the author as for
    /// <see cref="Other"/>.
    /// </summary>
    Hospital = 4
}

/// <summary>Whether a letter has reached one recipient. Stored by number, so never renumber it.</summary>
public enum LetterDispatchStatus
{
    NotSent = 0,
    Sent = 1,

    /// <summary>
    /// Secure email from MedApp was tried and the mail server didn't accept it; it can be tried again, or sent another
    /// way and recorded.
    /// </summary>
    Failed = 2
}

/// <summary>How a letter reached one recipient. Stored by number, so never renumber it.</summary>
public enum LetterDispatchMethod
{
    /// <summary>Printed and posted (recorded by staff).</summary>
    Post = 0,

    /// <summary>Given to the patient, in clinic (recorded by staff).</summary>
    HandedToPatient = 1,

    /// <summary>Sent by MedApp itself, by secure email to an address in an allowed domain.</summary>
    SecureEmail = 2,

    /// <summary>Emailed from the organisation's own secure mailbox, outside MedApp (recorded by staff).</summary>
    EmailOutsideMedApp = 3,

    /// <summary>Any other way (recorded by staff).</summary>
    Other = 4
}
