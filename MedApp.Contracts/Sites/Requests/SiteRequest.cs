namespace MedApp.Contracts.Sites.Requests;

/// <summary>A site's details, for adding one or replacing an existing one's.</summary>
public sealed class SiteRequest
{
    /// <summary>At most 100 characters, unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The postal address, at most three lines of 100 characters each (none for no address).</summary>
    public List<string>? AddressLines { get; set; }

    /// <summary>A UK postcode, with or without its space, in any case; null or empty for none.</summary>
    public string? Postcode { get; set; }

    /// <summary>At most 30 characters: digits, spaces and + ( ) -; null or empty for none.</summary>
    public string? Phone { get; set; }
}
