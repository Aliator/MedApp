namespace MedApp.Application.Specialties.Common;

/// <summary>What has a specialty: one that anything has is archived, never deleted.</summary>
public sealed record SpecialtyUsage(int TemplateCount, int PathwayCount, int MemberCount)
{
    public static readonly SpecialtyUsage None = new(0, 0, 0);

    public bool IsInUse => TemplateCount > 0 || PathwayCount > 0 || MemberCount > 0;
}
