namespace MedApp.Domain.Teams;

/// <summary>
/// A member of staff belonging to a team, with their role in it: admins set them on the Users page, any number each.
/// A team decides what its members see (the team walls); their roles decide what they do. An admin is in no team.
/// Goes with the account.
/// </summary>
public sealed class TeamMembership
{
    public Guid UserId { get; init; }

    public Guid TeamId { get; init; }
    public Team Team { get; init; } = null!;

    /// <summary>A member, or the clinic's admin.</summary>
    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
}
