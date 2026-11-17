using MedApp.DevMcp.DemoData;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic values for the demo data set's helpers (medapp-dev's reset_demo_data).</summary>
public static class DemoDataTestConstants
{
    /// <summary>"Now": 09:00 on Monday 16 November 2026, UTC and UK time alike (GMT).</summary>
    public static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Monday 16 November 2026, and that week's Saturday.</summary>
    public static readonly DateOnly Monday = new(2026, 11, 16);
    public static readonly DateOnly Saturday = new(2026, 11, 21);

    public const int Seed = 2026;

    public static readonly Guid StaffId = Guid.Parse("5eed0000-0000-0000-0000-000000000001");
    public static readonly Guid OtherStaffId = Guid.Parse("5eed0000-0000-0000-0000-000000000002");
    public static readonly Guid TheatreDayId = Guid.Parse("5eed0000-0000-0000-0000-0000000000d1");

    /// <summary>A kind of procedure with a side, for the note builder.</summary>
    public static ProcedureKind Knee => ProcedureKinds.KneeReplacement;

    public static ProcedureKind Colonoscopy => ProcedureKinds.Colonoscopy;

    /// <summary>How many notes to build when checking they vary.</summary>
    public const int Samples = 40;
}
