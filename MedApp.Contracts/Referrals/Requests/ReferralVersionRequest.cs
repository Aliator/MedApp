namespace MedApp.Contracts.Referrals.Requests;

/// <summary>A change to a referral that needs only the version the caller saw: asking for more information, or recording it came.</summary>
public sealed class ReferralVersionRequest
{
    /// <summary>The referral's <c>version</c> the change was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
