namespace MedApp.Application.Sites.Common;

/// <summary>The lengths sites and rooms allow, as the database stores them.</summary>
public static class SiteLimits
{
    public const int MaxNameLength = 100;

    public const int MaxAddressLines = 3;

    public const int MaxAddressLineLength = 100;

    /// <summary>The stored address: three lines and the two line breaks between them.</summary>
    public const int MaxAddressLength = MaxAddressLines * MaxAddressLineLength + MaxAddressLines - 1;

    public const int MaxPhoneLength = 30;

    public const int MaxRoomNameLength = 100;
}
