using MedApp.Domain.Sites;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic sites and rooms: invented names, a postcode in the ZZ area (none exists) and a drama-range phone.</summary>
public static class SitesTestConstants
{
    public static readonly DateTime Now = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Earlier = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

    public static readonly Guid SiteId = Guid.Parse("5173e000-0000-4000-8000-0000000000a1");
    public static readonly Guid OtherSiteId = Guid.Parse("5173e000-0000-4000-8000-0000000000a2");
    public static readonly Guid RoomId = Guid.Parse("5173e000-0000-4000-8000-0000000000b1");
    public static readonly Guid OtherRoomId = Guid.Parse("5173e000-0000-4000-8000-0000000000b2");

    public const string SiteName = "Northgate outreach clinic";
    public const string OtherSiteName = "Riverside centre";
    public const string RoomName = "Room 2";
    public const string OtherRoomName = "Treatment room";

    public const string AddressLine1 = "1 Example Street";
    public const string AddressLine2 = "Testville";
    public const string Postcode = "ZZ99 3AA";
    public const string Phone = "07700 900123";

    public static Site NewSite(Guid? id = null, string name = SiteName, bool isArchived = false) => new()
    {
        Id = id ?? SiteId,
        Name = name,
        AddressLines = $"{AddressLine1}\n{AddressLine2}",
        Postcode = Postcode,
        Phone = Phone,
        IsArchived = isArchived,
        CreatedAt = Earlier,
        LastUpdated = Earlier
    };

    public static Room NewRoom(Guid? id = null, Guid? siteId = null, string name = RoomName, bool isArchived = false) => new()
    {
        Id = id ?? RoomId,
        SiteId = siteId ?? SiteId,
        Name = name,
        IsArchived = isArchived,
        CreatedAt = Earlier,
        LastUpdated = Earlier
    };
}
