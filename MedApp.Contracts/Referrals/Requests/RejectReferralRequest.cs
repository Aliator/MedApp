namespace MedApp.Contracts.Referrals.Requests;

/// <summary>A triager rejecting a referral awaiting triage.</summary>
public sealed class RejectReferralRequest
{
    /// <summary>NotOfferedHere, DoesNotMeetCriteria, InsufficientInformation, Duplicate or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why (500 characters): required for Other, optional otherwise.</summary>
    public string? Detail { get; set; }

    /// <summary>The referral's <c>version</c> the decision was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
