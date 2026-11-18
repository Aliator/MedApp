using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Appointments;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarClashesTests
{
    [Test]
    public void Find_EmptyCalendar_IsNull()
    {
        CalendarClashes.Find(CreateData(NextMonday), At(NextMonday, 10), At(NextMonday, 11)).Should().BeNull();
    }

    [Test]
    public void Find_OverlapsATemplateBlock_NamesItsActivityAndTime()
    {
        var data = CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)]);

        CalendarClashes.Find(data, At(NextMonday, 12, 30), At(NextMonday, 13, 30))
            .Should().Be("That time overlaps Lunch, 12:00 to 13:00.");
    }

    [Test]
    public void Find_TouchingABlock_IsNull()
    {
        var data = CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)]);

        CalendarClashes.Find(data, At(NextMonday, 11), At(NextMonday, 12)).Should().BeNull();
        CalendarClashes.Find(data, At(NextMonday, 13), At(NextMonday, 14)).Should().BeNull();
    }

    [Test]
    public void Find_OverlapsAStoredBlock_NamesItsActivity()
    {
        var data = CreateData(NextMonday, blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 10, 30))]);

        CalendarClashes.Find(data, At(NextMonday, 10), At(NextMonday, 11))
            .Should().Be("That time overlaps Training, 09:00 to 10:30.");
    }

    [Test]
    public void Find_OverlapsAnAppointment_SaysSoWithoutThePatient()
    {
        var data = CreateData(NextMonday, appointments: [CreateAppointment(startUtc: At(NextMonday, 10))]);

        var clash = CalendarClashes.Find(data, At(NextMonday, 10, 15), At(NextMonday, 10, 45));

        clash.Should().Be("That time overlaps another appointment, 10:00 to 10:30.");
        clash.Should().NotContain(PatientFirstName).And.NotContain(PatientLastName);
    }

    [TestCase(AppointmentStatus.Booked, true)]
    [TestCase(AppointmentStatus.Attended, true)]
    [TestCase(AppointmentStatus.DidNotAttend, true)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void Find_AppointmentStatus_OnlyThoseOnTheCalendarClash(AppointmentStatus status, bool clashes)
    {
        var data = CreateData(NextMonday, appointments: [CreateAppointment(status, At(NextMonday, 10))]);

        var clash = CalendarClashes.Find(data, At(NextMonday, 10), At(NextMonday, 10, 30));

        (clash is not null).Should().Be(clashes);
    }

    [Test]
    public void Find_SeveralOverlaps_NamesTheEarliest()
    {
        var data = CreateData(
            NextMonday,
            templateBlocks: [CreateTemplateBlock(12, 13)],
            blocks: [CreateBlock(At(NextMonday, 11), At(NextMonday, 11, 30))],
            appointments: [CreateAppointment(startUtc: At(NextMonday, 11, 30))]);

        CalendarClashes.Find(data, At(NextMonday, 10), At(NextMonday, 14))
            .Should().Be("That time overlaps Training, 11:00 to 11:30.");
    }

    [Test]
    public void Find_TheBlockBeingMoved_IsIgnored()
    {
        var block = CreateBlock(At(NextMonday, 9), At(NextMonday, 10));
        var data = CreateData(NextMonday, blocks: [block]);

        CalendarClashes.Find(data, At(NextMonday, 9, 30), At(NextMonday, 10, 30), ignoreBlockId: block.Id).Should().BeNull();
    }

    [Test]
    public void Find_TheTemplateBlockBeingChangedForTheDay_IsIgnored()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var data = CreateData(NextMonday, templateBlocks: [lunch]);

        CalendarClashes.Find(data, At(NextMonday, 12, 30), At(NextMonday, 13, 30), ignoreTemplateBlockId: lunch.Id).Should().BeNull();
    }

    [Test]
    public void Find_IgnoringATemplateBlock_StillSeesTheOtherTemplateBlocks()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var training = CreateTemplateBlock(13, 14, Training);
        var data = CreateData(NextMonday, templateBlocks: [lunch, training]);

        CalendarClashes.Find(data, At(NextMonday, 12, 30), At(NextMonday, 13, 30), ignoreTemplateBlockId: lunch.Id)
            .Should().Be("That time overlaps Training, 13:00 to 14:00.");
    }

    [Test]
    public void Find_TheAppointmentBeingMoved_IsIgnored()
    {
        var appointment = CreateAppointment(startUtc: At(NextMonday, 10));
        var data = CreateData(NextMonday, appointments: [appointment]);

        CalendarClashes.Find(data, At(NextMonday, 10, 15), At(NextMonday, 10, 45), ignoreAppointmentId: appointment.Id)
            .Should().BeNull();
    }

    [Test]
    public void Find_SummerTimes_AreDescribedInUkTime()
    {
        var data = CreateData(SummerMonday, templateBlocks: [CreateTemplateBlock(12, 13)]);

        // 11:30Z is 12:30 BST.
        CalendarClashes.Find(
                data,
                new DateTime(2026, 6, 15, 11, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc))
            .Should().Be("That time overlaps Lunch, 12:00 to 13:00.");
    }

    [Test]
    public void Find_OverlapsAMeeting_NamesItAndItsTime()
    {
        var data = CreateData(NextMonday) with { Meetings = [NewMeeting(At(NextMonday, 9), At(NextMonday, 10))] };

        CalendarClashes.Find(data, At(NextMonday, 9, 30), At(NextMonday, 10, 30))
            .Should().Be("That time overlaps the meeting 'MDT', 09:00 to 10:00.");
    }

    [Test]
    public void Find_TheMeetingBeingMoved_IsIgnoredButOthersAreNot()
    {
        var mdt = NewMeeting(At(NextMonday, 9), At(NextMonday, 10));
        var huddle = NewMeeting(At(NextMonday, 10), At(NextMonday, 10, 30), id: OtherMeetingId, title: OtherTitle);
        var data = CreateData(NextMonday) with { Meetings = [mdt, huddle] };

        CalendarClashes.Find(data, At(NextMonday, 9), At(NextMonday, 10), ignoreMeetingId: MeetingId).Should().BeNull();
        CalendarClashes.Find(data, At(NextMonday, 9, 30), At(NextMonday, 10, 30), ignoreMeetingId: MeetingId)
            .Should().Be($"That time overlaps the meeting '{OtherTitle}', 10:00 to 10:30.");
    }

    [Test]
    public void FindFirst_Overlap_ReturnsWhatAndWhen()
    {
        var data = CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)]);

        CalendarClashes.FindFirst(data, At(NextMonday, 12, 30), At(NextMonday, 14))
            .Should().Be(new CalendarClash(At(NextMonday, 12), At(NextMonday, 13), "Lunch"));
    }

    [Test]
    public void FindFirst_NothingOverlaps_IsNull()
    {
        CalendarClashes.FindFirst(CreateData(NextMonday), At(NextMonday, 12), At(NextMonday, 13)).Should().BeNull();
    }

    [Test]
    public void FindFirst_IgnoringATemplateBlockOnOneDate_StillSeesItOnTheOthers()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var tuesday = NextMonday.AddDays(1);
        var data = CreateData(NextMonday, tuesday, templateBlocks: [lunch]);

        CalendarClashes.FindFirst(data, At(tuesday, 12), At(tuesday, 13), ignoreTemplateBlockId: lunch.Id).Should().BeNull();
        CalendarClashes.FindFirst(data, At(NextMonday, 12), At(NextMonday, 13), ignoreTemplateBlockId: lunch.Id).Should().BeNull();
        // Ignored only on the date the time starts: a time from Monday to Tuesday still sees Tuesday's.
        CalendarClashes.FindFirst(data, At(NextMonday, 12), At(tuesday, 13), ignoreTemplateBlockId: lunch.Id)
            .Should().Be(new CalendarClash(At(tuesday, 12), At(tuesday, 13), "Lunch"));
    }

    [Test]
    public void BusyTimes_EverythingOnTheCalendar_ByStartThenEnd()
    {
        var data = CreateData(
            NextMonday,
            templateBlocks: [CreateTemplateBlock(12, 13)],
            blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 11))],
            appointments: [CreateAppointment(startUtc: At(NextMonday, 9)), CreateAppointment(AppointmentStatus.Cancelled, At(NextMonday, 15))]) with
        {
            Meetings = [NewMeeting(At(NextMonday, 8), At(NextMonday, 8, 30))]
        };

        CalendarClashes.BusyTimes(data).Should().Equal(
            new CalendarClash(At(NextMonday, 8), At(NextMonday, 8, 30), "the meeting 'MDT'"),
            new CalendarClash(At(NextMonday, 9), At(NextMonday, 9, 30), "another appointment"),
            new CalendarClash(At(NextMonday, 9), At(NextMonday, 11), "Training"),
            new CalendarClash(At(NextMonday, 12), At(NextMonday, 13), "Lunch"));
    }

    [Test]
    public void BusyTimes_OverSeveralDays_ListsEachWorkingDaysTemplateBlocks()
    {
        var data = CreateData(NextMonday, NextSunday, templateBlocks: [CreateTemplateBlock(12, 13)]);

        CalendarClashes.BusyTimes(data).Select(b => b.StartUtc)
            .Should().Equal(Enumerable.Range(0, 5).Select(i => At(NextMonday.AddDays(i), 12)));
    }

    [Test]
    public void BusyTimes_IgnoringAMeeting_LeavesOnlyThatOneOut()
    {
        var data = CreateData(NextMonday) with
        {
            Meetings =
            [
                NewMeeting(At(NextMonday, 9), At(NextMonday, 10)),
                NewMeeting(At(NextMonday, 14), At(NextMonday, 15), id: OtherMeetingId, title: OtherTitle)
            ]
        };

        CalendarClashes.BusyTimes(data, MeetingId).Should().Equal(
            new CalendarClash(At(NextMonday, 14), At(NextMonday, 15), $"the meeting '{OtherTitle}'"));
    }

    [Test]
    public void BusyTimes_BlocksWithoutTheirActivityType_AreCalledABlock()
    {
        var stored = CreateBlock(At(NextMonday, 9), At(NextMonday, 10));
        stored.ActivityType = null!;
        var template = CreateTemplateBlock(12, 13);
        template.ActivityType = null!;
        var data = CreateData(NextMonday, templateBlocks: [template], blocks: [stored]);

        CalendarClashes.BusyTimes(data).Select(b => b.What).Should().Equal("a block", "a block");
    }

    [Test]
    public void BusyTimes_BookedAppointmentMissingATime_IsLeftOut()
    {
        var noStart = CreateAppointment(startUtc: At(NextMonday, 9));
        noStart.StartUtc = null;
        var noEnd = CreateAppointment(startUtc: At(NextMonday, 10));
        noEnd.EndUtc = null;
        var data = CreateData(NextMonday, appointments: [noStart, noEnd, CreateAppointment(AppointmentStatus.Requested)]);

        CalendarClashes.BusyTimes(data).Should().BeEmpty();
    }

    [Test]
    public void BusyTimes_EmptyCalendar_IsNothing()
    {
        CalendarClashes.BusyTimes(CalendarData.Empty(StaffUserId, NextMonday, NextSunday)).Should().BeEmpty();
    }

    [Test]
    public void Describe_Meeting_NamesItByTitle()
    {
        CalendarClashes.Describe(NewMeeting(At(NextMonday, 9), At(NextMonday, 10), title: "Ward round"))
            .Should().Be("the meeting 'Ward round'");
    }

    [TestCase(10, 0, 11, 0, true)]
    [TestCase(9, 0, 10, 30, true)]
    [TestCase(10, 15, 10, 45, true)]
    [TestCase(9, 0, 10, 0, false)]
    [TestCase(11, 0, 12, 0, false)]
    public void Overlaps_Times_OnlyWhenTheyShareTime(int startHour, int startMinute, int endHour, int endMinute, bool expected)
    {
        CalendarClashes.Overlaps(
                At(NextMonday, 10), At(NextMonday, 11),
                At(NextMonday, startHour, startMinute), At(NextMonday, endHour, endMinute))
            .Should().Be(expected);
    }

    // FindAppointment: a time recorded after the event

    [Test]
    public void FindAppointment_OverlapsAnAppointment_NamesItWithoutThePatient()
    {
        var data = CreateData(NextMonday, appointments: [CreateAppointment(AppointmentStatus.DidNotAttend, At(NextMonday, 10, 15))]);

        CalendarClashes.FindAppointment(data, At(NextMonday, 10), At(NextMonday, 10, 30))
            .Should().Be("That time overlaps another appointment, 10:15 to 10:45.");
    }

    [Test]
    public void FindAppointment_BlocksAndMeetings_DontCount()
    {
        var data = CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(10, 11)], blocks: [CreateBlock(At(NextMonday, 10), At(NextMonday, 11))])
            with { Meetings = [NewMeeting(At(NextMonday, 10), At(NextMonday, 11))] };

        CalendarClashes.FindAppointment(data, At(NextMonday, 10), At(NextMonday, 10, 30)).Should().BeNull();
        CalendarClashes.Find(data, At(NextMonday, 10), At(NextMonday, 10, 30)).Should().NotBeNull("a booking would clash");
    }

    [TestCase(AppointmentStatus.Requested)]
    [TestCase(AppointmentStatus.Cancelled)]
    public void FindAppointment_AppointmentsOffTheCalendar_DontCount(AppointmentStatus status)
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        appointment.Status = status;

        CalendarClashes.FindAppointment(CreateData(NextMonday, appointments: [appointment]), At(NextMonday, 10), At(NextMonday, 10, 30))
            .Should().BeNull();
    }

    [Test]
    public void FindAppointment_TouchingOrIgnored_IsNull()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        var data = CreateData(NextMonday, appointments: [appointment]);

        CalendarClashes.FindAppointment(data, At(NextMonday, 10, 30), At(NextMonday, 11)).Should().BeNull();
        CalendarClashes.FindAppointment(data, At(NextMonday, 10), At(NextMonday, 10, 30), ignoreAppointmentId: appointment.Id).Should().BeNull();
    }

    [Test]
    public void FindAppointment_SeveralOverlap_NamesTheEarliest()
    {
        var data = CreateData(NextMonday, appointments:
        [
            CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10, 30)),
            CreateAppointment(AppointmentStatus.Attended, At(NextMonday, 9, 45))
        ]);

        CalendarClashes.FindAppointment(data, At(NextMonday, 10), At(NextMonday, 11))
            .Should().Be("That time overlaps another appointment, 09:45 to 10:15.");
    }

    // A day closed since the validator ran

    [Test]
    public void Find_OnAClosedDay_SaysTheClinicIsClosedBeforeAnyClash()
    {
        var data = CreateData(ClosedWednesday, blocks: [CreateBlock(At(ClosedWednesday, 10), At(ClosedWednesday, 11))])
            with { ClosedDays = new ClosedDaySet([CreateClosedDay()]) };

        CalendarClashes.Find(data, At(ClosedWednesday, 10), At(ClosedWednesday, 11))
            .Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
        CalendarClashes.WhyClosed(data, At(ClosedWednesday, 10)).Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
        CalendarClashes.WhyClosed(data with { ClosedDays = ClosedDaySet.None }, At(ClosedWednesday, 10)).Should().BeNull();
    }
}
