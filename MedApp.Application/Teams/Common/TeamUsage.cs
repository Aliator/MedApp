namespace MedApp.Application.Teams.Common;

/// <summary>
/// What has a team: one that anything has is archived, never deleted. <paramref name="AwaitingTriageCount"/> is
/// how many of its <paramref name="ReferralCount"/> referrals await triage: while any do, it can't be archived either.
/// <paramref name="TriagerCount"/> is how many of its <paramref name="MemberCount"/> members can triage (they hold
/// <c>TriageReferrals</c>): with none, its referrals can't be triaged (hazard H-40).
/// </summary>
public sealed record TeamUsage(
    int TemplateCount,
    int PathwayCount,
    int MemberCount,
    int ReferralCount = 0,
    int AwaitingTriageCount = 0,
    int TriagerCount = 0,
    int ClinicCount = 0,
    int LetterTemplateCount = 0,
    int MdtCount = 0)
{
    public static readonly TeamUsage None = new(0, 0, 0);

    public bool IsInUse => TemplateCount > 0 || PathwayCount > 0 || MemberCount > 0 || ReferralCount > 0 || ClinicCount > 0
                           || LetterTemplateCount > 0 || MdtCount > 0;
}
