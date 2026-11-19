namespace MedApp.Domain.Teams;

/// <summary>
/// Why a team or person may see a pathway in full (<see cref="PathwayAccess"/>). Stored as a number: never renumber;
/// add new values with new numbers.
/// </summary>
public enum PathwayAccessReason
{
    /// <summary>The pathway's own team: it changes what it owns. Written with the pathway.</summary>
    Owner = 0,

    /// <summary>
    /// A team that owns one of its stages (the lab), from when it's first passed the pathway; never ended
    /// (decision 184): it keeps read access after handing back.
    /// </summary>
    StageOwner = 1,

    /// <summary>A team asked a consult (MC25): read only, ended when the consult closes.</summary>
    Consult = 2,

    /// <summary>An MDT's attendee, by person (MC26): read only, ended when the item is discussed or withdrawn.</summary>
    Mdt = 3
}
