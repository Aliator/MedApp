namespace MedApp.Contracts.Teams.Requests;

/// <summary>Archiving or restoring a team, from the version the caller saw.</summary>
public sealed class TeamVersionRequest
{
    /// <summary>The <c>version</c> the caller saw: if anyone has changed the team since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }
}
