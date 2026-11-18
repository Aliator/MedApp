namespace MedApp.Contracts.DataRequests;

/// <summary>Logs a patient's subject access request as received.</summary>
public sealed class CreateDataRequestRequest
{
    public Guid PatientId { get; set; }

    /// <summary>The UK date it was received: not in the future, at most a year back. The month starts here.</summary>
    public DateOnly ReceivedOn { get; set; }

    /// <summary>Whether the patient asked themselves; otherwise give who asked and their relationship.</summary>
    public bool RequestedByPatient { get; set; }

    /// <summary>Who asked on the patient's behalf (200 characters); required when the patient didn't ask.</summary>
    public string? RequesterName { get; set; }

    /// <summary>Their relationship to the patient, or their authority (100 characters); required when the patient didn't ask.</summary>
    public string? RequesterRelationship { get; set; }
}

/// <summary>The requester's identity was checked: when, how (InPerson, Documents, KnownToService or Other) and, for Other, in words.</summary>
public sealed class RecordIdentityCheckRequest
{
    public DateOnly CheckedOn { get; set; }
    public string Method { get; set; } = string.Empty;

    /// <summary>How it was checked, in words (500 characters); required for Other.</summary>
    public string? Detail { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>Extends the due date by 1 or 2 months (in all) beyond the first month, with the reason given to the requester.</summary>
public sealed class ExtendDataRequestRequest
{
    public int Months { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}

/// <summary>An item the clinician withholds from the pack under the serious harm test, with why.</summary>
public sealed class WithheldItemRequest
{
    /// <summary>Note, File, Letter, Result, MdtItem or Referral.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    /// <summary>Why (500 characters).</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>The clinician review: what's withheld (empty for nothing), replacing any earlier review.</summary>
public sealed class RecordDataRequestReviewRequest
{
    public List<WithheldItemRequest> Withheld { get; set; } = [];
    public int ExpectedVersion { get; set; }
}

/// <summary>The response was sent: the UK date and how (Post, HandedOver, SecureEmail, Email or Other).</summary>
public sealed class RecordDataRequestSentRequest
{
    public DateOnly SentOn { get; set; }
    public string Method { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}

/// <summary>Refuses the request, with the reason given to the requester (500 characters).</summary>
public sealed class RefuseDataRequestRequest
{
    public string Reason { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}

/// <summary>The requester withdrew the request.</summary>
public sealed class WithdrawDataRequestRequest
{
    public int ExpectedVersion { get; set; }
}

/// <summary>
/// Filters for the data requests list: a status (Open, Sent, Refused or Withdrawn), one patient's, or the open ones a
/// clinician hasn't reviewed yet. Open ones first by due date, then the rest, newest received first.
/// </summary>
public sealed class SearchDataRequestsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public Guid? PatientId { get; set; }
    public bool AwaitingReview { get; set; }

    /// <summary>The patient's full name contains this, ignoring case. At most 200 characters.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// <c>patient</c>, <c>received</c>, <c>due</c> or <c>status</c>. Without one, open ones first by due date, then
    /// the rest, newest received first.
    /// </summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;
}
