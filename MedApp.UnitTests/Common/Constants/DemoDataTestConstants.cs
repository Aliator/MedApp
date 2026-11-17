using MedApp.Application.Calendar.Common;
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

    /// <summary>
    /// Moments that change the calendars' shape (UK time): each weekday (the diary day and the days either side), a
    /// weekend, late on a Sunday and just after midnight, both 2026 clock changes, and a new year.
    /// </summary>
    public static IEnumerable<TestCaseData> CalendarMoments() =>
        new (string Name, DateTime Uk)[]
        {
            ("Monday afternoon", new(2026, 11, 16, 14, 0, 0)),
            ("Tuesday morning", new(2026, 11, 17, 10, 0, 0)),
            ("Wednesday morning", new(2026, 11, 18, 10, 0, 0)),
            ("Thursday morning (the lunch moves to a Friday admin day)", new(2026, 11, 19, 10, 0, 0)),
            ("Friday afternoon", new(2026, 11, 20, 16, 30, 0)),
            ("Saturday morning", new(2026, 11, 14, 11, 0, 0)),
            ("Sunday night", new(2026, 11, 15, 23, 30, 0)),
            ("Monday just after midnight", new(2026, 11, 16, 0, 20, 0)),
            ("Sunday of the autumn clock change", new(2026, 10, 25, 10, 0, 0)),
            ("Monday after the autumn clock change", new(2026, 10, 26, 7, 30, 0)),
            ("Monday after the spring clock change", new(2026, 3, 30, 8, 10, 0)),
            ("New year", new(2027, 1, 4, 9, 0, 0)),
        }.Select(m => new TestCaseData(UkTime.ToUtc(DateOnly.FromDateTime(m.Uk), TimeOnly.FromDateTime(m.Uk))).SetArgDisplayNames(m.Name));

    /// <summary>
    /// The clinic's staff as the seed makes them, with when each started and left and when the service manager became
    /// an admin, but no accounts.
    /// </summary>
    public static List<StaffMember> Staff(DemoTimeline timeline) =>
        DemoStaff.Roster
            .Select(seed => new StaffMember(seed, Guid.NewGuid())
            {
                StartedUtc = timeline.AccountCreatedUtc(seed.Kind),
                LeftUtc = seed.Kind == StaffKind.Leaver ? timeline.LeaverLeftUtc : null,
                AdminFromUtc = seed.Kind == StaffKind.ServiceManager ? timeline.ManagerMadeAdminUtc : null,
            })
            .Prepend(new StaffMember(DemoStaff.Lead, Guid.NewGuid())
            {
                StartedUtc = timeline.AppStartUtc.AddDays(-30),
                AdminFromUtc = DateTime.MinValue,
            })
            .ToList();
}
