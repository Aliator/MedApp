namespace MedApp.Contracts.Referrals.Requests;

/// <summary>
/// A triager redirecting a referral awaiting triage: either to another specialty here (<see cref="SpecialtyId"/>: it
/// stays awaiting triage, in that specialty's inbox) or elsewhere (<see cref="Elsewhere"/>: another provider, which
/// closes it). Exactly one of the two.
/// </summary>
public sealed class RedirectReferralRequest
{
    /// <summary>Another specialty here, not archived.</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>Where else it was sent (another provider; 200 characters).</summary>
    public string? Elsewhere { get; set; }

    /// <summary>Why (500 characters, required).</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The referral's <c>version</c> the decision was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
