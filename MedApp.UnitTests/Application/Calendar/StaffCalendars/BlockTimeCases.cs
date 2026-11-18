using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars;

/// <summary>Times a block can't have (start, end, and the message saying why), for the block validators' tests.</summary>
public static class BlockTimeCases
{
    /// <summary>Times nobody's calendar can have, whoever it's for.</summary>
    public static IEnumerable<TestCaseData> Invalid()
    {
        yield return new TestCaseData(At(NextMonday, 11), At(NextMonday, 10), WorkingHours.EndNotAfterStart)
            .SetName("{m}_EndBeforeStart_Fails");
        yield return new TestCaseData(At(NextMonday, 10), At(NextMonday, 10), WorkingHours.EndNotAfterStart)
            .SetName("{m}_EndAtStart_Fails");
        yield return new TestCaseData(At(NextMonday, 5, 30), At(NextMonday, 9), WorkingHours.OutsideBounds)
            .SetName("{m}_StartsBefore0600_Fails");
        yield return new TestCaseData(At(NextMonday, 22), At(NextMonday, 23, 30), WorkingHours.OutsideBounds)
            .SetName("{m}_EndsAfter2300_Fails");
        yield return new TestCaseData(At(NextMonday, 10, 2), At(NextMonday, 11), WorkingHours.NotOnBoundary)
            .SetName("{m}_StartOffAFiveMinuteBoundary_Fails");
        yield return new TestCaseData(At(NextMonday, 10), At(NextMonday, 10, 59), WorkingHours.NotOnBoundary)
            .SetName("{m}_EndOffAFiveMinuteBoundary_Fails");
        yield return new TestCaseData(At(NextMonday, 17), At(NextMonday.AddDays(1), 9), WorkingHours.NotSameDay)
            .SetName("{m}_EndsOnAnotherDay_Fails");
    }

    /// <summary>
    /// Times outside the person's working day on the default hours (Monday to Friday, 08:00 to 18:00): refused by
    /// validators that know whose calendar it is, and otherwise by the handler.
    /// </summary>
    public static IEnumerable<TestCaseData> OutsideTheDefaultDay()
    {
        yield return new TestCaseData(At(NextSaturday, 10), At(NextSaturday, 11), WorkingHours.NotWorking(DayOfWeek.Saturday))
            .SetName("{m}_Saturday_Fails");
        yield return new TestCaseData(At(NextSunday, 10), At(NextSunday, 11), WorkingHours.NotWorking(DayOfWeek.Sunday))
            .SetName("{m}_Sunday_Fails");
        yield return new TestCaseData(At(NextMonday, 7, 30), At(NextMonday, 9), WorkingHours.OutsideWorkingDay)
            .SetName("{m}_StartsBeforeTheWorkingDay_Fails");
        yield return new TestCaseData(At(NextMonday, 17), At(NextMonday, 18, 30), WorkingHours.OutsideWorkingDay)
            .SetName("{m}_EndsAfterTheWorkingDay_Fails");
    }

    /// <summary>A summer (BST) day's working hours in UTC: 08:00 to 18:00 UK time is 07:00Z to 17:00Z.</summary>
    public static IEnumerable<TestCaseData> ValidInSummer()
    {
        yield return new TestCaseData(At(SummerMonday, 8), At(SummerMonday, 18)).SetName("{m}_WholeSummerWorkingDay_Passes");
    }
}
