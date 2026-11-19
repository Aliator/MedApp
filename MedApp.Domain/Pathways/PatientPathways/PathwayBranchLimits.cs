namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>The lengths of a stage's branch name and skip detail, shared by the database and the validators.</summary>
public static class PathwayBranchLimits
{
    public const int MaxBranchLength = 50;
    public const int MaxSkipDetailLength = 500;
    public const int MaxReinstateReasonLength = 500;

    /// <summary>The longest reason for adding a branch (MC10).</summary>
    public const int MaxBranchReasonLength = 500;

    /// <summary>The longest results text the lab enters on its stage (MC10b).</summary>
    public const int MaxResultTextLength = 4000;
}
