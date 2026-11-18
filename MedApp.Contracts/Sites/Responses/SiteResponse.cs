namespace MedApp.Contracts.Sites.Responses;

/// <summary>A place the centre works from, with its rooms.</summary>
public sealed class SiteResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The postal address's lines (none when it has no address).</summary>
    public List<string> AddressLines { get; set; } = [];

    /// <summary>"ZZ99 3AA", or null.</summary>
    public string? Postcode { get; set; }

    public string? Phone { get; set; }

    /// <summary>Archived sites can't be chosen for anything new (rooms, closed days); what names one keeps it.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Whether it can be deleted: true only while it has no rooms and no closed days, and isn't the last open site.</summary>
    public bool CanBeDeleted { get; set; }

    /// <summary>Its rooms, by name (archived ones only when they were asked for).</summary>
    public List<RoomResponse> Rooms { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdated { get; set; }
}
