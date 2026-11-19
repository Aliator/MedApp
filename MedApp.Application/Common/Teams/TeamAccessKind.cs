namespace MedApp.Application.Common.Teams;

/// <summary>
/// Why a viewer may see something in full, the strongest that applied (decision 207): the activity log's views record it
/// by name as <c>access</c>. Not stored as a number.
/// </summary>
public enum TeamAccessKind
{
    /// <summary>Their team owns it.</summary>
    Owner,

    /// <summary>Their team owns one of its stages (the lab, once passed it).</summary>
    StageOwner,

    /// <summary>Their team was asked a consult on it.</summary>
    Consult,

    /// <summary>They attend an MDT it's listed for.</summary>
    Mdt,

    /// <summary>They see every team's records (an admin), read only.</summary>
    Admin,

    /// <summary>The system itself (a job, the seed).</summary>
    System
}
