namespace MedApp.Contracts.Referrals.Requests;

/// <summary>
/// A triager redirecting a referral awaiting triage: either to another team here (<see cref="TeamId"/>: it
/// stays awaiting triage, in that team's inbox) or elsewhere (<see cref="Elsewhere"/>: another provider, which
/// closes it). Exactly one of the two.
/// </summary>
public sealed class RedirectReferralRequest
{
    /// <summary>Another team here, not archived.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Where else it was sent (another provider; 200 characters).</summary>
    public string? Elsewhere { get; set; }

    /// <summary>Why (500 characters, required).</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The referral's <c>version</c> the decision was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
