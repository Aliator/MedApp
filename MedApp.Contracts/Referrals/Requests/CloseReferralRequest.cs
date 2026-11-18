namespace MedApp.Contracts.Referrals.Requests;

/// <summary>Withdrawing a referral, or marking it entered in error, while it awaits triage.</summary>
public sealed class CloseReferralRequest
{
    /// <summary>What happened (500 characters, required): who withdrew it, or what was wrong.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The referral's <c>version</c> the change was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
