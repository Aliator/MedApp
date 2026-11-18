namespace MedApp.Domain.Referrals;

/// <summary>Why a referral was rejected. Stored as its number, so never renumber these.</summary>
public enum ReferralRejectionReason
{
    NotOfferedHere = 0,
    DoesNotMeetCriteria = 1,
    InsufficientInformation = 2,
    Duplicate = 3,
    Other = 4
}
