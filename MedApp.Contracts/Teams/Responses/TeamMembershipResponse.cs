namespace MedApp.Contracts.Teams.Responses;

/// <summary>A team someone belongs to.</summary>
public sealed class TeamMembershipResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>They still belong to it, but it can't be given to anyone new.</summary>
    public bool IsArchived { get; set; }
}
