namespace MedApp.Domain.Sites;

/// <summary>
/// A place the centre works from (the main building, an outreach clinic), with its rooms (<see cref="Room"/>). Admins
/// keep the list; one that has been used (it has rooms or closed days) is archived, not deleted. Every database starts
/// with one, <see cref="MainSiteId"/>, which admins rename. Its details are the organisation's, never a person's.
/// </summary>
public sealed class Site
{
    /// <summary>The site every database starts with ("Main site"), added by the migration that added sites.</summary>
    public static readonly Guid MainSiteId = Guid.Parse("5173e000-0000-4000-8000-000000000001");

    /// <summary>The name the main site starts with.</summary>
    public const string MainSiteName = "Main site";

    public Guid Id { get; init; }

    /// <summary>Unique, e.g. "Main site" or "Northgate outreach clinic".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The postal address's lines (at most three), separated by line breaks, or null.</summary>
    public string? AddressLines { get; set; }

    /// <summary>A UK postcode, upper case with one space ("ZZ99 3AA"), or null.</summary>
    public string? Postcode { get; set; }

    /// <summary>The site's phone number as typed (digits, spaces and + ( ) -), or null.</summary>
    public string? Phone { get; set; }

    /// <summary>Archived sites can't be chosen for anything new; what already names one keeps it.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }
}
