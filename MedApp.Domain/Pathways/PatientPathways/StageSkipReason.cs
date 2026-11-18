namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>Why a stage was skipped. Stored as a number, so values never change.</summary>
public enum StageSkipReason
{
    /// <summary>Another branch was chosen at the branch point before it (never chosen by hand).</summary>
    BranchNotChosen = 0,

    NotNeeded = 1,
    PatientDeclined = 2,

    /// <summary>Needs a detail.</summary>
    Other = 3
}
