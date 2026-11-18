namespace MedApp.Domain.Teams;

/// <summary>
/// A member of staff belonging to a team: admins set them on the Users page, any number each. Triage will use it
/// (a referral is triaged by someone of its team); it changes nothing about what anyone can see. Goes with the
/// account.
/// </summary>
public sealed class TeamMembership
{
    public Guid UserId { get; init; }

    public Guid TeamId { get; init; }
    public Team Team { get; init; } = null!;
}
