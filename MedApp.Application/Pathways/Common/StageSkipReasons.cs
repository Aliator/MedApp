using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>Why a stage was skipped, by name and in words.</summary>
public static class StageSkipReasons
{
    /// <summary>
    /// A reason staff may choose (NotNeeded, PatientDeclined or Other), by name only, ignoring case. BranchNotChosen is
    /// the app's own, when another branch is chosen.
    /// </summary>
    public static bool TryParseChosen(string? value, out StageSkipReason reason) =>
        PathwayEnums.TryParse(value, out reason) && reason != StageSkipReason.BranchNotChosen;

    /// <summary>A reason in words, as the pages show it.</summary>
    public static string Describe(StageSkipReason reason) => reason switch
    {
        StageSkipReason.BranchNotChosen => "Another branch was chosen",
        StageSkipReason.NotNeeded => "Not needed",
        StageSkipReason.PatientDeclined => "Patient declined",
        _ => "Other"
    };
}
