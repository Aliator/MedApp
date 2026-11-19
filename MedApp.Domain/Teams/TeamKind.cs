namespace MedApp.Domain.Teams;

/// <summary>What kind of team it is. Stored as a number: never renumber; add new values with new numbers.</summary>
public enum TeamKind
{
    /// <summary>A clinic: Dermatology, Gynaecology and so on.</summary>
    Clinic = 0,

    /// <summary>The lab, which serves every clinic (at most one that isn't archived).</summary>
    Lab = 1
}
