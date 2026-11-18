namespace MedApp.Contracts.Pathways.PatientPathways.Responses;

/// <summary>The referral a pathway was accepted from, for a link from the pathway's page.</summary>
public sealed class PathwayReferralResponse
{
    public Guid Id { get; set; }

    /// <summary>When the referral arrived (a UK date).</summary>
    public DateOnly ReceivedOn { get; set; }

    /// <summary>The referral's specialty.</summary>
    public string SpecialtyName { get; set; } = string.Empty;
}
