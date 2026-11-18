using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Appointments;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class AvailabilityTests
{
    /// <summary>Long before every test date, so "now" cuts nothing off.</summary>
    private static readonly DateTime LongBefore = At(LongAgo, 8);

    private static CalendarData Empty(Guid staffUserId, DateOnly from, DateOnly? to = null) =>
        CalendarData.Empty(staffUserId, from, to ?? from);

    // FreeTime

    [Test]
    public void FreeTime_NothingOn_IsTheWholeWorkingDay()
    {
        var days = Availability.FreeTime([Empty(StaffUserId, NextMonday)], NextMonday, NextMonday, LongBefore);

        var day = days.Should().ContainSingle().Subject;
        day.Date.Should().Be(NextMonday);
        day.Free.Should().Equal(Range(NextMonday, 8, 0, 18, 0));
    }

    [Test]
    public void FreeTime_EitherSideOfTheClockChange_IsTheUkWorkingDayInUtc()
    {
        var data = CalendarOf(StaffUserId, FridayBeforeClockChange, MondayAfterClockChange, templateBlocks: [CreateTemplateBlock(12, 13)]);

        var days = Availability.FreeTime([data], FridayBeforeClockChange, MondayAfterClockChange, LongBefore);

        days.Select(d => d.Date).Should().Equal(FridayBeforeClockChange, MondayAfterClockChange);
        // Friday is BST (UTC+1): 08:00 to 18:00 UK time is 07:00Z to 17:00Z, and lunch 11:00Z to 12:00Z.
        days[0].Free.Should().Equal(
            new TimeRange(Utc(2026, 10, 23, 7), Utc(2026, 10, 23, 11)),
            new TimeRange(Utc(2026, 10, 23, 12), Utc(2026, 10, 23, 17)));
        // Monday is GMT (UTC+0): the same UK clock times are the same in UTC.
        days[1].Free.Should().Equal(
            new TimeRange(Utc(2026, 10, 26, 8), Utc(2026, 10, 26, 12)),
            new TimeRange(Utc(2026, 10, 26, 13), Utc(2026, 10, 26, 18)));
    }

    [Test]
    public void FreeTime_Weekends_AreSkipped()
    {
        var days = Availability.FreeTime([Empty(StaffUserId, NextFriday, NextMonday.AddDays(7))], NextFriday, NextMonday.AddDays(7), LongBefore);

        days.Select(d => d.Date).Should().Equal(NextFriday, NextMonday.AddDays(7));
    }

    [Test]
    public void FreeTime_OnlyAWeekend_IsNoDays()
    {
        Availability.FreeTime([Empty(StaffUserId, NextSaturday, NextSunday)], NextSaturday, NextSunday, LongBefore)
            .Should().BeEmpty();
    }

    [Test]
    public void FreeTime_FullyBookedDay_IsListedWithNoFreeTime()
    {
        var data = CalendarOf(StaffUserId, NextMonday, NextMonday.AddDays(1),
            blocks: [CreateBlock(At(NextMonday, 8), At(NextMonday, 18))]);

        var days = Availability.FreeTime([data], NextMonday, NextMonday.AddDays(1), LongBefore);

        days.Should().HaveCount(2);
        days[0].Free.Should().BeEmpty();
        days[1].Free.Should().Equal(Range(NextMonday.AddDays(1), 8, 0, 18, 0));
        Availability.Suggestions(days.Take(1), 30).Should().BeEmpty();
    }

    [Test]
    public void FreeTime_SomeoneOnLeave_NobodyIsFreeThatDay()
    {
        var onLeave = CalendarOf(OtherStaffUserId, NextMonday, NextMonday.AddDays(1),
            blocks: [CreateBlock(At(NextMonday, 8), At(NextMonday, 18), Leave)]);

        var days = Availability.FreeTime([Empty(StaffUserId, NextMonday, NextMonday.AddDays(1)), onLeave], NextMonday, NextMonday.AddDays(1), LongBefore);

        days[0].Free.Should().BeEmpty();
        days[1].Free.Should().Equal(Range(NextMonday.AddDays(1), 8, 0, 18, 0));
    }

    [Test]
    public void FreeTime_OverlappingBusyTimesOfSeveralPeople_AreMerged()
    {
        var staff = CalendarOf(StaffUserId, NextMonday,
            blocks: [CreateBlock(At(NextMonday, 10), At(NextMonday, 11)), CreateBlock(At(NextMonday, 14), At(NextMonday, 16))]);
        var other = CalendarOf(OtherStaffUserId, NextMonday,
            templateBlocks: [CreateTemplateBlock(10, 12, startMinute: 30)],
            appointments: [CreateAppointment(startUtc: At(NextMonday, 14, 30))]);
        var admin = CalendarOf(AdminUserId, NextMonday,
            meetings: [NewMeeting(At(NextMonday, 11, 30), At(NextMonday, 11, 45), [AdminUserId])]);

        var free = Availability.FreeTime([staff, other, admin], NextMonday, NextMonday, LongBefore).Single().Free;

        // 10:00-11:00 and 10:30-12:00 (with 11:30-11:45 inside) make 10:00-12:00; 14:30-15:00 is inside 14:00-16:00.
        free.Should().Equal(
            Range(NextMonday, 8, 0, 10, 0),
            Range(NextMonday, 12, 0, 14, 0),
            Range(NextMonday, 16, 0, 18, 0));
    }

    [Test]
    public void FreeTime_BusyTimesTouching_LeaveNoGap()
    {
        var staff = CalendarOf(StaffUserId, NextMonday, blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 10))]);
        var other = CalendarOf(OtherStaffUserId, NextMonday, blocks: [CreateBlock(At(NextMonday, 10), At(NextMonday, 11))]);

        Availability.FreeTime([staff, other], NextMonday, NextMonday, LongBefore).Single().Free
            .Should().Equal(Range(NextMonday, 8, 0, 9, 0), Range(NextMonday, 11, 0, 18, 0));
    }

    [Test]
    public void FreeTime_CancelledAppointment_IsNotBusy()
    {
        var data = CalendarOf(StaffUserId, NextMonday,
            appointments: [CreateAppointment(AppointmentStatus.Cancelled, At(NextMonday, 10))]);

        Availability.FreeTime([data], NextMonday, NextMonday, LongBefore).Single().Free
            .Should().Equal(Range(NextMonday, 8, 0, 18, 0));
    }

    [Test]
    public void FreeTime_Today_StartsAfterNowRoundedUpToFiveMinutes()
    {
        var now = new DateTime(2026, 11, 16, 9, 2, 30, DateTimeKind.Utc);

        Availability.FreeTime([Empty(StaffUserId, Today)], Today, Today, now).Single().Free
            .Should().Equal(Range(Today, 9, 5, 18, 0));
    }

    [Test]
    public void FreeTime_NowOnAFiveMinuteBoundary_StartsThen()
    {
        Availability.FreeTime([Empty(StaffUserId, Today)], Today, Today, Now).Single().Free
            .Should().Equal(Range(Today, 9, 0, 18, 0));
    }

    [Test]
    public void FreeTime_BusyTimeAlreadyStarted_StartsWhenItEnds()
    {
        var data = CalendarOf(StaffUserId, Today, blocks: [CreateBlock(At(Today, 8), At(Today, 9, 30))]);

        Availability.FreeTime([data], Today, Today, Now).Single().Free.Should().Equal(Range(Today, 9, 30, 18, 0));
    }

    [Test]
    public void FreeTime_BusyTimeOffAFiveMinuteBoundary_IsRoundedInwards()
    {
        var data = CalendarOf(StaffUserId, NextMonday,
            appointments: [CreateAppointment(startUtc: At(NextMonday, 10, 2), minutes: 30)]);

        Availability.FreeTime([data], NextMonday, NextMonday, LongBefore).Single().Free
            .Should().Equal(Range(NextMonday, 8, 0, 10, 0), Range(NextMonday, 10, 35, 18, 0));
    }

    [Test]
    public void FreeTime_FreeTimeShorterThanFiveMinutesOnceRounded_IsLeftOut()
    {
        var data = CalendarOf(StaffUserId, NextMonday,
            appointments: [CreateAppointment(startUtc: At(NextMonday, 8, 3), minutes: 597)]);

        // Free only 08:00-08:03, which rounds to nothing.
        Availability.FreeTime([data], NextMonday, NextMonday, LongBefore).Single().Free.Should().BeEmpty();
    }

    [Test]
    public void FreeTime_AfterTheWorkingDay_HasNoFreeTimeLeftToday()
    {
        var evening = At(Today, 19);

        var days = Availability.FreeTime([Empty(StaffUserId, Today, Today.AddDays(1))], Today, Today.AddDays(1), evening);

        days[0].Free.Should().BeEmpty();
        days[1].Free.Should().Equal(Range(Today.AddDays(1), 8, 0, 18, 0));
    }

    [Test]
    public void FreeTime_PastDay_HasNoFreeTime()
    {
        var friday = Today.AddDays(-3);

        Availability.FreeTime([Empty(StaffUserId, friday)], friday, friday, Now).Single().Free.Should().BeEmpty();
    }

    [Test]
    public void FreeTime_BusyOnOtherDays_DoesNotAffectThisOne()
    {
        var tuesday = NextMonday.AddDays(1);
        var data = CalendarOf(StaffUserId, NextMonday, tuesday,
            blocks: [CreateBlock(At(NextMonday, 8), At(NextMonday, 18))]);

        Availability.FreeTime([data], tuesday, tuesday, LongBefore).Single().Free.Should().Equal(Range(tuesday, 8, 0, 18, 0));
    }

    [Test]
    public void FreeTime_TheMeetingBeingMoved_IsNotBusy()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));
        var other = NewMeeting(At(NextMonday, 14), At(NextMonday, 15), id: OtherMeetingId, title: OtherTitle);
        var data = CalendarOf(StaffUserId, NextMonday, meetings: [meeting, other]);

        Availability.FreeTime([data], NextMonday, NextMonday, LongBefore, MeetingId).Single().Free
            .Should().Equal(Range(NextMonday, 8, 0, 14, 0), Range(NextMonday, 15, 0, 18, 0));
        Availability.FreeTime([data], NextMonday, NextMonday, LongBefore).Single().Free
            .Should().Equal(Range(NextMonday, 8, 0, 10, 0), Range(NextMonday, 11, 0, 14, 0), Range(NextMonday, 15, 0, 18, 0));
    }

    [Test]
    public void FreeTime_NobodyAsked_IsEveryWorkingDay()
    {
        Availability.FreeTime([], NextMonday, NextMonday, LongBefore).Single().Free
            .Should().Equal(Range(NextMonday, 8, 0, 18, 0));
    }

    // Suggestions

    [Test]
    public void Suggestions_FreeDay_AtMostThreeBackToBackFromTheStart()
    {
        var days = new[] { Day(NextMonday, Range(NextMonday, 8, 0, 18, 0)) };

        Availability.Suggestions(days, 60).Should().Equal(
            Range(NextMonday, 8, 0, 9, 0),
            Range(NextMonday, 9, 0, 10, 0),
            Range(NextMonday, 10, 0, 11, 0));
    }

    [Test]
    public void Suggestions_StartsAndLengthsOffTheQuarterHour_StartOnFifteenMinuteSteps()
    {
        var days = new[] { Day(NextMonday, Range(NextMonday, 9, 5, 18, 0)) };

        // 09:05 rounds up to 09:15; each next start is rounded up from the end of the one before (09:50 -> 10:00).
        Availability.Suggestions(days, 35).Should().Equal(
            Range(NextMonday, 9, 15, 9, 50),
            Range(NextMonday, 10, 0, 10, 35),
            Range(NextMonday, 10, 45, 11, 20));
    }

    [Test]
    public void Suggestions_WithinADay_NeverOverlap()
    {
        var days = new[] { Day(NextMonday, Range(NextMonday, 8, 0, 18, 0)) };

        var suggestions = Availability.Suggestions(days, 50);

        suggestions.Zip(suggestions.Skip(1)).Should().OnlyContain(pair => pair.First.EndUtc <= pair.Second.StartUtc);
    }

    [Test]
    public void Suggestions_LengthThatFitsNowhere_IsNone()
    {
        var days = new[]
        {
            Day(NextMonday, Range(NextMonday, 8, 0, 9, 0), Range(NextMonday, 10, 0, 11, 0)),
            Day(NextMonday.AddDays(1), Range(NextMonday.AddDays(1), 13, 0, 14, 25))
        };

        Availability.Suggestions(days, 90).Should().BeEmpty();
    }

    [Test]
    public void Suggestions_FreeTimeTooShortOnceRoundedToTheQuarterHour_IsSkipped()
    {
        // 08:05-08:40 is 35 minutes, but a 30-minute meeting would have to start at 08:15 and end at 08:45.
        var days = new[] { Day(NextMonday, Range(NextMonday, 8, 5, 8, 40), Range(NextMonday, 9, 0, 9, 30)) };

        Availability.Suggestions(days, 30).Should().Equal(Range(NextMonday, 9, 0, 9, 30));
    }

    [Test]
    public void Suggestions_ExactFit_IsSuggested()
    {
        var days = new[] { Day(NextMonday, Range(NextMonday, 9, 0, 9, 30)) };

        Availability.Suggestions(days, 30).Should().Equal(Range(NextMonday, 9, 0, 9, 30));
    }

    [Test]
    public void Suggestions_SeveralFreeTimesInADay_StillAtMostThreeThatDay()
    {
        var tuesday = NextMonday.AddDays(1);
        var days = new[]
        {
            Day(NextMonday,
                Range(NextMonday, 8, 0, 8, 30),
                Range(NextMonday, 9, 0, 9, 30),
                Range(NextMonday, 10, 0, 10, 30),
                Range(NextMonday, 11, 0, 11, 30)),
            Day(tuesday, Range(tuesday, 8, 0, 8, 30))
        };

        Availability.Suggestions(days, 30).Should().Equal(
            Range(NextMonday, 8, 0, 8, 30),
            Range(NextMonday, 9, 0, 9, 30),
            Range(NextMonday, 10, 0, 10, 30),
            Range(tuesday, 8, 0, 8, 30));
    }

    [Test]
    public void Suggestions_ManyFreeDays_AtMostTenInAllSpreadAcrossTheEarliestDays()
    {
        var dates = Enumerable.Range(0, 5).Select(i => NextMonday.AddDays(i)).ToList();
        var days = dates.Select(d => Day(d, Range(d, 8, 0, 18, 0)));

        var suggestions = Availability.Suggestions(days, 30);

        suggestions.Should().HaveCount(Availability.MaxSuggestions);
        suggestions.GroupBy(s => UkTime.DateOf(s.StartUtc)).Select(g => (g.Key, g.Count()))
            .Should().Equal((dates[0], 3), (dates[1], 3), (dates[2], 3), (dates[3], 1));
    }

    [Test]
    public void Suggestions_DaysOutOfOrder_AreEarliestFirst()
    {
        var tuesday = NextMonday.AddDays(1);
        var days = new[] { Day(tuesday, Range(tuesday, 8, 0, 8, 30)), Day(NextMonday, Range(NextMonday, 17, 0, 17, 30)) };

        Availability.Suggestions(days, 30).Should().Equal(Range(NextMonday, 17, 0, 17, 30), Range(tuesday, 8, 0, 8, 30));
    }

    [Test]
    public void Suggestions_InSummer_AreOnTheUkClocksQuarterHours()
    {
        var days = new[] { Day(SummerMonday, Range(SummerMonday, 8, 10, 18, 0)) };

        // 08:10 BST is 07:10Z; the next quarter hour is 08:15 BST, 07:15Z.
        Availability.Suggestions(days, 15).First().Should().Be(new TimeRange(Utc(2026, 6, 15, 7, 15), Utc(2026, 6, 15, 7, 30)));
    }

    [Test]
    public void Suggestions_FromFreeTime_FitInsideIt()
    {
        var staff = CalendarOf(StaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(8, 12)]);
        var other = CalendarOf(OtherStaffUserId, NextMonday, blocks: [CreateBlock(At(NextMonday, 13), At(NextMonday, 18))]);

        var days = Availability.FreeTime([staff, other], NextMonday, NextMonday, LongBefore);

        Availability.Suggestions(days, 60).Should().Equal(Range(NextMonday, 12, 0, 13, 0));
    }

    private static FreeDay Day(DateOnly date, params TimeRange[] free) => new(date, free);

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    // Closed days

    [Test]
    public void FreeTime_ClosedDay_IsLeftOutLikeAWeekend()
    {
        var closed = new ClosedDaySet([CreateClosedDay()]);
        var data = Empty(StaffUserId, NextMonday, NextFriday) with { ClosedDays = closed };

        var days = Availability.FreeTime([data], NextMonday, NextFriday, LongBefore);

        days.Select(d => d.Date).Should().Equal(NextMonday, NextMonday.AddDays(1), NextMonday.AddDays(3), NextFriday);
        Availability.Suggestions(days, 60).Should().NotContain(s => UkTime.DateOf(s.StartUtc) == ClosedWednesday);
    }

    [Test]
    public void FreeTime_ClosedDayKnownToAnyOfThePeople_IsLeftOut()
    {
        var closed = new ClosedDaySet([CreateClosedDay()]);
        var people = new[]
        {
            Empty(StaffUserId, ClosedWednesday),
            Empty(OtherStaffUserId, ClosedWednesday) with { ClosedDays = closed }
        };

        Availability.FreeTime(people, ClosedWednesday, ClosedWednesday, LongBefore).Should().BeEmpty();
    }
}
