namespace MedApp.Domain.Teams;

/// <summary>
/// Someone's role in one of their teams. Stored as a number: never renumber; add new values with new numbers. A clinic
/// admin is staff with minor extra rights for their own team (none yet beyond staff's: Stage 2's S2-9).
/// </summary>
public enum TeamMemberRole
{
    Member = 0,

    /// <summary>The clinic's admin. The lab has none.</summary>
    ClinicAdmin = 1
}
