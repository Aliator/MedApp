namespace MedApp.Application.Practices.Common;

/// <summary>
/// How many patients are registered with a practice, and how many referrals came from it: one either has is archived,
/// never deleted.
/// </summary>
public sealed record PracticeUsage(int PatientCount, int ReferralCount)
{
    public static readonly PracticeUsage None = new(0, 0);

    public bool IsInUse => PatientCount > 0 || ReferralCount > 0;
}
