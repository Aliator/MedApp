namespace MedApp.Application.Activity.Common;

/// <summary>The activity log's column sizes, which entries are cut to fit.</summary>
public static class ActivityLimits
{
    public const int Username = 256;

    /// <summary>Long enough for any IPv6 address.</summary>
    public const int IpAddress = 45;
}
