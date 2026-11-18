using FluentAssertions;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class WorkingHoursTests
{
    [TestCase(2026, 11, 23, true)] // Monday
    [TestCase(2026, 11, 27, true)] // Friday
    [TestCase(2026, 11, 28, false)] // Saturday
    [TestCase(2026, 11, 29, false)] // Sunday
    public void IsWorkingDay_Date_IsMondayToFriday(int year, int month, int day, bool expected)
    {
        WorkingHours.IsWorkingDay(new DateOnly(year, month, day)).Should().Be(expected);
    }

    [Test]
    public void DayStartAndEndUtc_WinterDate_Are0800And1800Utc()
    {
        WorkingHours.DayStartUtc(NextMonday).Should().Be(new DateTime(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc));
        WorkingHours.DayEndUtc(NextMonday).Should().Be(new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void DayStartAndEndUtc_SummerDate_AreAnHourEarlierInUtc()
    {
        WorkingHours.DayStartUtc(SummerMonday).Should().Be(new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc));
        WorkingHours.DayEndUtc(SummerMonday).Should().Be(new DateTime(2026, 6, 15, 17, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void WhyOutside_InsideTheWorkingDay_IsNull()
    {
        WorkingHours.WhyOutside(At(NextMonday, 10), At(NextMonday, 10, 30), ClosedDaySet.None).Should().BeNull();
    }

    [Test]
    public void WhyOutside_TheWholeWorkingDay_IsNull()
    {
        WorkingHours.WhyOutside(At(NextMonday, 8), At(NextMonday, 18), ClosedDaySet.None).Should().BeNull();
    }

    [Test]
    public void WhyOutside_SummerWorkingDayInUtc_IsNull()
    {
        // 08:00 to 18:00 BST.
        WorkingHours.WhyOutside(
                new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 17, 0, 0, DateTimeKind.Utc), ClosedDaySet.None)
            .Should().BeNull();
    }

    [Test]
    public void WhyOutside_SummerUtcHoursThatAreOutsideUkHours_SaysSo()
    {
        // 08:00Z is 09:00 BST, but 22:30Z is 23:30 BST, past the bounds; and on the default day, 17:30Z is 18:30 BST.
        WorkingHours.WhyOutside(
                new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc), ClosedDaySet.None)
            .Should().Be(WorkingHours.OutsideBounds);
        WorkingTimeSet.None.For(Guid.NewGuid(), SummerMonday, ClosedDaySet.None)
            .WhyOutside(new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 15, 17, 30, 0, DateTimeKind.Utc))
            .Should().Be(WorkingHours.OutsideWorkingDay);
    }

    [Test]
    public void WhyOutside_EndBeforeStart_SaysTheEndMustBeAfterTheStart()
    {
        WorkingHours.WhyOutside(At(NextMonday, 11), At(NextMonday, 10), ClosedDaySet.None).Should().Be(WorkingHours.EndNotAfterStart);
    }

    [Test]
    public void WhyOutside_EndAtTheStart_SaysTheEndMustBeAfterTheStart()
    {
        WorkingHours.WhyOutside(At(NextMonday, 10), At(NextMonday, 10), ClosedDaySet.None).Should().Be(WorkingHours.EndNotAfterStart);
    }

    [Test]
    public void WhyOutside_EndsTheNextDay_SaysItMustBeOneDay()
    {
        WorkingHours.WhyOutside(At(NextMonday, 17), At(NextMonday.AddDays(1), 9), ClosedDaySet.None).Should().Be(WorkingHours.NotSameDay);
    }

    [Test]
    public void WhyOutside_Weekend_IsForThePersonsHoursToSay()
    {
        // Someone may work Saturdays: whether this person does is their WorkingTime's to say.
        WorkingHours.WhyOutside(At(NextSaturday, 10), At(NextSaturday, 11), ClosedDaySet.None).Should().BeNull();
    }

    [Test]
    public void WhyOutside_StartsBefore0600_SaysItMustBeInTheBounds()
    {
        WorkingHours.WhyOutside(At(NextMonday, 5, 45), At(NextMonday, 8, 30), ClosedDaySet.None).Should().Be(WorkingHours.OutsideBounds);
        WorkingHours.WhyOutside(At(NextMonday, 6), At(NextMonday, 8, 30), ClosedDaySet.None).Should().BeNull();
    }

    [Test]
    public void WhyOutside_EndsAfter2300_SaysItMustBeInTheBounds()
    {
        WorkingHours.WhyOutside(At(NextMonday, 22, 30), At(NextMonday, 23, 15), ClosedDaySet.None).Should().Be(WorkingHours.OutsideBounds);
        WorkingHours.WhyOutside(At(NextMonday, 22, 30), At(NextMonday, 23), ClosedDaySet.None).Should().BeNull();
    }

    [Test]
    public void WhyOutside_OffA5MinuteBoundary_SaysSo()
    {
        WorkingHours.WhyOutside(At(NextMonday, 10, 2), At(NextMonday, 10, 30), ClosedDaySet.None).Should().Be(WorkingHours.NotOnBoundary);
    }

    [Test]
    public void WhyOutside_WithSeconds_SaysItsOffABoundary()
    {
        WorkingHours.WhyOutside(At(NextMonday, 10), At(NextMonday, 10, 30).AddSeconds(1), ClosedDaySet.None).Should().Be(WorkingHours.NotOnBoundary);
    }

    [Test]
    public void WhyOutside_UnspecifiedKind_IsTakenAsUtc()
    {
        WorkingHours.WhyOutside(
                new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Unspecified),
                new DateTime(2026, 11, 23, 10, 30, 0, DateTimeKind.Unspecified), ClosedDaySet.None)
            .Should().BeNull();
    }

    [TestCase(8, 0, 18, 0, null)]
    [TestCase(12, 0, 13, 0, null)]
    [TestCase(12, 0, 12, 0, WorkingHours.EndNotAfterStart)]
    [TestCase(13, 0, 12, 0, WorkingHours.EndNotAfterStart)]
    [TestCase(7, 30, 9, 0, null)]
    [TestCase(17, 0, 20, 0, null)]
    [TestCase(6, 0, 23, 0, null)]
    [TestCase(5, 30, 9, 0, WorkingHours.OutsideBounds)]
    [TestCase(17, 0, 23, 30, WorkingHours.OutsideBounds)]
    [TestCase(12, 3, 13, 0, WorkingHours.NotOnBoundary)]
    [TestCase(12, 0, 12, 59, WorkingHours.NotOnBoundary)]
    public void WhyOutside_TemplateTimes_ChecksTheWorkingDayAndBoundaries(
        int startHour, int startMinute, int endHour, int endMinute, string? expected)
    {
        WorkingHours.WhyOutside(new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute)).Should().Be(expected);
    }

    [Test]
    public void Constants_AreTheWorkingDayAndGrid()
    {
        WorkingHours.Start.Should().Be(new TimeOnly(8, 0));
        WorkingHours.End.Should().Be(new TimeOnly(18, 0));
        WorkingHours.SlotMinutes.Should().Be(15);
        WorkingHours.GranularityMinutes.Should().Be(5);
        WorkingHours.MaxDays.Should().Be(31);
    }
}
