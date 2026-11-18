namespace MedApp.Contracts.Referrals.Requests;

/// <summary>Resuming a referral's paused waiting-time clock.</summary>
public sealed class ResumeClockRequest
{
    /// <summary>The referral's <c>version</c> the change was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
