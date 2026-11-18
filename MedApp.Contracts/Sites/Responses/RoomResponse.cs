namespace MedApp.Contracts.Sites.Responses;

/// <summary>A room at a site.</summary>
public sealed class RoomResponse
{
    public Guid Id { get; set; }

    public Guid SiteId { get; set; }

    /// <summary>The site's name, e.g. "Main site".</summary>
    public string SiteName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Archived rooms can't be chosen for anything new; what names one keeps it.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Whether it can be deleted: true only if nothing has used it.</summary>
    public bool CanBeDeleted { get; set; }
}
