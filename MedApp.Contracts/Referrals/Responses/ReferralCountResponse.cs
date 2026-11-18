namespace MedApp.Contracts.Referrals.Responses;

/// <summary>How many referrals await triage, for the side nav. A count only: it names no patients.</summary>
public sealed class ReferralCountResponse
{
    public int Count { get; set; }

    /// <summary>
    /// Whether <see cref="Count"/> is of the caller's specialties only; false when they belong to none, and it's every
    /// specialty's.
    /// </summary>
    public bool InYourSpecialties { get; set; }
}
