namespace MedApp.Contracts.DataRequests;

/// <summary>A data request in a list: the patient, the dates and where it stands, never who asked or why.</summary>
public sealed class DataRequestSummaryResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateOnly ReceivedOn { get; set; }
    public DateOnly DueOn { get; set; }

    /// <summary>Open, Sent, Refused or Withdrawn.</summary>
    public string Status { get; set; } = string.Empty;

    public bool RequestedByPatient { get; set; }
    public bool IsIdentityChecked { get; set; }
    public bool IsReviewed { get; set; }

    /// <summary>Open and past its due date (UK).</summary>
    public bool IsOverdue { get; set; }
}

/// <summary>A data request, in full, with what the caller may do with it.</summary>
public sealed class DataRequestResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateOnly ReceivedOn { get; set; }
    public bool RequestedByPatient { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterRelationship { get; set; }

    public DateOnly? IdentityCheckedOn { get; set; }
    public string? IdentityCheckedByUsername { get; set; }

    /// <summary>InPerson, Documents, KnownToService or Other; null until checked.</summary>
    public string? IdentityCheckMethod { get; set; }

    public string? IdentityCheckDetail { get; set; }

    /// <summary>One calendar month from receipt (the last day of the month when there's no matching day).</summary>
    public DateOnly FirstDueOn { get; set; }

    /// <summary>When it's due now: <see cref="FirstDueOn"/> plus any extension.</summary>
    public DateOnly DueOn { get; set; }

    public int ExtensionMonths { get; set; }
    public string? ExtensionReason { get; set; }

    /// <summary>Open, Sent, Refused or Withdrawn.</summary>
    public string Status { get; set; } = string.Empty;

    public string? RefusalReason { get; set; }
    public DateOnly? SentOn { get; set; }

    /// <summary>Post, HandedOver, SecureEmail, Email or Other, once sent.</summary>
    public string? SendMethod { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByUsername { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedByUsername { get; set; }

    /// <summary>What the review withheld, with why.</summary>
    public List<WithheldItemResponse> Withheld { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; }
    public int Version { get; set; }

    public bool IsOverdue { get; set; }

    /// <summary>Whether the caller may record the identity check, extend, mark sent, refuse or withdraw it (ManagePrivacy, while open).</summary>
    public bool CanManage { get; set; }

    /// <summary>Whether the caller may record the clinician review (SignOffResults, while open).</summary>
    public bool CanReview { get; set; }

    /// <summary>Whether the caller may make and download the pack now (ManagePrivacy, open, identity checked and reviewed).</summary>
    public bool CanMakePack { get; set; }

    /// <summary>Why the pack can't be made yet, for a holder of ManagePrivacy while it's open, or null.</summary>
    public string? PackBlockedReason { get; set; }
}

/// <summary>An item the review withheld.</summary>
public sealed class WithheldItemResponse
{
    /// <summary>Note, File, Letter, Result, MdtItem or Referral.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid ItemId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// An item of the record the clinician reviewing a data request may withhold: what it is, its date and its text as
/// held, so they can judge the serious harm test.
/// </summary>
public sealed class DataRequestReviewItemResponse
{
    /// <summary>Note, File, Letter, Result, MdtItem or Referral.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid ItemId { get; set; }
    public DateTime AtUtc { get; set; }

    /// <summary>What it is: the pathway's title for a note, the file's name, the letter's kind and template, and so on.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Its text as held (a note's, a letter's body, an MDT item's question and outcome, a referral's reason), or empty.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Who wrote or added it, by username.</summary>
    public string? By { get; set; }

    /// <summary>Whether it's deleted (a note or file): still in the record, so still in the pack unless withheld.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Whether it's withheld now: by the last review, or (before any review) because it starts withheld.</summary>
    public bool IsWithheld { get; set; }

    /// <summary>The reason it's withheld now, or the reason it starts withheld (a note about another patient).</summary>
    public string? WithheldReason { get; set; }
}

/// <summary>
/// The home page's counts of data requests: the open ones, and the open ones due within seven days or already past due
/// (UK dates).
/// </summary>
public sealed class DataRequestCountResponse
{
    public int Open { get; set; }

    public int DueSoon { get; set; }
}
