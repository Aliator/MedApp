namespace MedApp.Domain.Sites;

/// <summary>
/// A room at a site ("Room 2", "Treatment room"), where clinic sessions and appointments will take place. Admins keep
/// the list; names are unique within a site. One that has been used is archived, not deleted.
/// </summary>
public sealed class Room
{
    public Guid Id { get; init; }

    /// <summary>The site it's at. A room never moves to another site.</summary>
    public Guid SiteId { get; init; }
    public Site Site { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Archived rooms can't be chosen for anything new; what already names one keeps it.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }
}
