using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class BookingTimesTests
{
    [Test]
    public void WhyNot_FutureWorkingTime_IsNull()
    {
        BookingTimes.WhyNot(At(NextMonday, 10), At(NextMonday, 10, 30), Now).Should().BeNull();
    }

    [Test]
    public void WhyNot_LaterToday_IsNull()
    {
        BookingTimes.WhyNot(At(Today, 9, 5), At(Today, 9, 30), Now).Should().BeNull();
    }

    [Test]
    public void WhyNot_StartingNow_IsInThePast()
    {
        BookingTimes.WhyNot(Now, Now.AddMinutes(30), Now).Should().Be(BookingTimes.InThePast);
    }

    [Test]
    public void WhyNot_EarlierToday_IsInThePast()
    {
        BookingTimes.WhyNot(At(Today, 8), At(Today, 8, 30), Now).Should().Be(BookingTimes.InThePast);
    }

    [Test]
    public void WhyNot_OutsideTheWorkingDayAndInThePast_SaysTheWorkingDayFirst()
    {
        BookingTimes.WhyNot(At(Today, 7), At(Today, 7, 30), Now).Should().Be(WorkingHours.OutsideWorkingDay);
    }

    [Test]
    public void WhyNot_Weekend_SaysSo()
    {
        BookingTimes.WhyNot(At(NextSaturday, 10), At(NextSaturday, 11), Now).Should().Be(WorkingHours.Weekend);
    }

    [Test]
    public void WhyNot_TimesWithoutAKind_AreTakenAsUtc()
    {
        var start = DateTime.SpecifyKind(At(NextMonday, 10), DateTimeKind.Unspecified);

        BookingTimes.WhyNot(start, start.AddMinutes(30), Now).Should().BeNull();
    }

    [Test]
    public void WhyNot_SummerWorkingDay_UsesUkTime()
    {
        // 07:00 UTC is 08:00 BST, the start of the working day; the clock is the day before.
        var start = new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc);

        BookingTimes.WhyNot(start, start.AddHours(1), start.AddDays(-1)).Should().BeNull();
        BookingTimes.WhyNot(start.AddHours(-1), start, start.AddDays(-1)).Should().Be(WorkingHours.OutsideWorkingDay);
    }
}
