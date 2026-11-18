using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class BookingTimesTests
{
    private static readonly WorkingDays WorkingDays = InMemoryClosedDays.WorkingDays(CreateClosedDay());

    private static Task<string?> WhyNot(DateTime start, DateTime end, DateTime now) =>
        BookingTimes.WhyNotAsync(WorkingDays, start, end, now, CancellationToken.None);

    private static Task<string?> WhyNotRecorded(DateTime start, DateTime end, DateTime now) =>
        Task.FromResult(BookingTimes.WhyNotRecorded(start, end, now));

    [Test]
    public async Task WhyNot_FutureWorkingTime_IsNull()
    {
        (await WhyNot(At(NextMonday, 10), At(NextMonday, 10, 30), Now)).Should().BeNull();
    }

    [Test]
    public async Task WhyNot_LaterToday_IsNull()
    {
        (await WhyNot(At(Today, 9, 5), At(Today, 9, 30), Now)).Should().BeNull();
    }

    [Test]
    public async Task WhyNot_StartingNow_IsInThePast()
    {
        (await WhyNot(Now, Now.AddMinutes(30), Now)).Should().Be(BookingTimes.InThePast);
    }

    [Test]
    public async Task WhyNot_EarlierToday_IsInThePast()
    {
        (await WhyNot(At(Today, 8), At(Today, 8, 30), Now)).Should().Be(BookingTimes.InThePast);
    }

    [Test]
    public async Task WhyNot_OutsideTheWorkingDayAndInThePast_SaysTheWorkingDayFirst()
    {
        (await WhyNot(At(Today, 7), At(Today, 7, 30), Now)).Should().Be(WorkingHours.OutsideWorkingDay);
    }

    [Test]
    public async Task WhyNot_Weekend_SaysSo()
    {
        (await WhyNot(At(NextSaturday, 10), At(NextSaturday, 11), Now)).Should().Be(WorkingHours.Weekend);
    }

    [Test]
    public async Task WhyNot_ClosedDay_SaysTheClinicIsClosed()
    {
        (await WhyNot(At(ClosedWednesday, 10), At(ClosedWednesday, 11), Now))
            .Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
    }

    [Test]
    public async Task WhyNot_TimesWithoutAKind_AreTakenAsUtc()
    {
        var start = DateTime.SpecifyKind(At(NextMonday, 10), DateTimeKind.Unspecified);

        (await WhyNot(start, start.AddMinutes(30), Now)).Should().BeNull();
    }

    [Test]
    public async Task WhyNot_SummerWorkingDay_UsesUkTime()
    {
        // 07:00 UTC is 08:00 BST, the start of the working day; the clock is the day before.
        var start = new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc);

        (await WhyNot(start, start.AddHours(1), start.AddDays(-1))).Should().BeNull();
        (await WhyNot(start.AddHours(-1), start, start.AddDays(-1))).Should().Be(WorkingHours.OutsideWorkingDay);
    }

    [Test]
    public async Task WhyNotRecorded_EarlierToday_IsNull()
    {
        (await WhyNotRecorded(At(Today, 8), At(Today, 8, 30), Now)).Should().BeNull();
    }

    [Test]
    public async Task WhyNotRecorded_StartedButNotEnded_IsNull()
    {
        // Now is 09:00 UK time: one under way can be recorded, as its outcome could be once it started.
        (await WhyNotRecorded(At(Today, 8, 45), At(Today, 9, 15), Now)).Should().BeNull();
    }

    [Test]
    public async Task WhyNotRecorded_StartingNow_IsNull()
    {
        (await WhyNotRecorded(Now, Now.AddMinutes(30), Now)).Should().BeNull();
    }

    [Test]
    public async Task WhyNotRecorded_LaterToday_HasNotStarted()
    {
        (await WhyNotRecorded(At(Today, 10), At(Today, 10, 30), Now)).Should().Be(BookingTimes.NotStartedYet);
    }

    [Test]
    public async Task WhyNotRecorded_ThirtyDaysBack_IsTheLimit()
    {
        // From Wednesday 16 December 2026, 30 days back is Monday 16 November: that day can be recorded, the Friday
        // before can't.
        var now = At(new DateOnly(2026, 12, 16), 9);

        (await WhyNotRecorded(At(Today, 10), At(Today, 10, 30), now)).Should().BeNull();
        (await WhyNotRecorded(At(Today.AddDays(-3), 10), At(Today.AddDays(-3), 10, 30), now)).Should().Be(BookingTimes.TooLongAgo);
    }

    [Test]
    public async Task WhyNotRecorded_Weekend_SaysSo()
    {
        var saturday = Today.AddDays(-2);

        (await WhyNotRecorded(At(saturday, 10), At(saturday, 11), Now)).Should().Be(WorkingHours.Weekend);
    }

    [Test]
    public async Task WhyNotRecorded_ClosedDay_IsAllowed()
    {
        // What happened, happened: a visit on a day closed later, or part-way through, is recorded (decision 74).
        var now = At(ClosedWednesday.AddDays(2), 12);

        (await WhyNotRecorded(At(ClosedWednesday, 10), At(ClosedWednesday, 11), now)).Should().BeNull();
    }

    [Test]
    public async Task WhyNotRecorded_OutsideTheWorkingDay_SaysSo()
    {
        (await WhyNotRecorded(At(Today, 7), At(Today, 7, 30), Now)).Should().Be(WorkingHours.OutsideWorkingDay);
    }
}
