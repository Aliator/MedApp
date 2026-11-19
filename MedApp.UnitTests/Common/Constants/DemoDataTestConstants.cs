namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic values for the demo data set's helpers (tools/seed-demo-data.sh, and medapp-dev's reset_demo_data).</summary>
public static class DemoDataTestConstants
{
    /// <summary>"Now": 09:40 on Thursday 19 November 2026, UTC and UK time alike (GMT).</summary>
    public static readonly DateTime Now = new(2026, 11, 19, 9, 40, 0, DateTimeKind.Utc);

    /// <summary>Thursday 19 November 2026, and that week's Monday.</summary>
    public static readonly DateOnly Thursday = new(2026, 11, 19);
    public static readonly DateOnly Monday = new(2026, 11, 16);

    /// <summary>A Saturday morning, and a Monday before the working day (07:00 UK).</summary>
    public static readonly DateTime SaturdayMorning = new(2026, 11, 14, 11, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime MondayEarly = new(2026, 11, 16, 7, 0, 0, DateTimeKind.Utc);

    /// <summary>Christmas Eve, 2026: the centre is closed the next two days.</summary>
    public static readonly DateTime ChristmasEve = new(2026, 12, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The template most of the story's pathways use.</summary>
    public const string SuspectedSkinCancer = "Suspected skin cancer";
}
