namespace MedApp.Application.Specialties.Common;

/// <summary>
/// What has a specialty: one that anything has is archived, never deleted. <paramref name="AwaitingTriageCount"/> is
/// how many of its <paramref name="ReferralCount"/> referrals await triage: while any do, it can't be archived either.
/// <paramref name="TriagerCount"/> is how many of its <paramref name="MemberCount"/> members can triage (they hold
/// <c>TriageReferrals</c>): with none, its referrals can't be triaged (hazard H-40).
/// </summary>
public sealed record SpecialtyUsage(
    int TemplateCount,
    int PathwayCount,
    int MemberCount,
    int ReferralCount = 0,
    int AwaitingTriageCount = 0,
    int TriagerCount = 0)
{
    public static readonly SpecialtyUsage None = new(0, 0, 0);

    public bool IsInUse => TemplateCount > 0 || PathwayCount > 0 || MemberCount > 0 || ReferralCount > 0;
}
