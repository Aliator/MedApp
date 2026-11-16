using MedApp.Application.Activity.Common;

namespace MedApp.UnitTests.Common.Constants;

public static class ActivityTestConstants
{
    public static readonly Guid UserId = Guid.Parse("8d6d6f3e-0b5a-4c55-9d43-31a1f1a10001");

    public const string Username = "staff-user";

    /// <summary>A documentation address (RFC 5737), never a real one.</summary>
    public const string IpAddress = "192.0.2.10";

    public static readonly ActivityActor Actor = new(UserId, Username, IpAddress);

    public static readonly DateTime Now = new(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc);
}
