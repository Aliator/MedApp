using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarMapperTests
{
    private static readonly IReadOnlyDictionary<Guid, string> Usernames = new Dictionary<Guid, string>
    {
        [StaffUserId] = StaffUsername
    };

    private static StaffWeekTemplate DefaultWeek()
    {
        var week = CreateStaffWeek();
        week.WeekTemplate = CreateWeekTemplate();
        return week;
    }

    [Test]
    public void ToResponse_Calendar_GivesTheWorkingDayAndWhoseItIs()
    {
        var data = CreateData(NextMonday, NextSunday);

        var response = CalendarMapper.ToResponse(data, StaffUsername, DefaultWeek(), canEdit: true, Today, Usernames, Now);

        response.StaffUserId.Should().Be(StaffUserId);
        response.StaffUsername.Should().Be(StaffUsername);
        response.From.Should().Be(NextMonday);
        response.To.Should().Be(NextSunday);
        response.WorkingDayStart.Should().Be("08:00");
        response.WorkingDayEnd.Should().Be("18:00");
        response.SlotMinutes.Should().Be(15);
        response.CanEdit.Should().BeTrue();
        response.DefaultWeekTemplateId.Should().Be(WeekTemplateId);
        response.DefaultWeekTemplateName.Should().Be(WeekTemplateName);
        response.Days.Select(d => d.Date).Should().Equal(Enumerable.Range(0, 7).Select(i => NextMonday.AddDays(i)));
        response.Days.Select(d => d.IsWorkingDay).Should().Equal(true, true, true, true, true, false, false);
    }

    [Test]
    public void ToResponse_NoDefaultWeek_LeavesItEmpty()
    {
        var response = CalendarMapper.ToResponse(CreateData(NextMonday), StaffUsername, null, canEdit: false, Today, Usernames, Now);

        response.DefaultWeekTemplateId.Should().BeNull();
        response.DefaultWeekTemplateName.Should().BeNull();
        response.CanEdit.Should().BeFalse();
    }

    [Test]
    public void ToResponse_DefaultWeekWithoutItsTemplateLoaded_HasNoName()
    {
        var response = CalendarMapper.ToResponse(CreateData(NextMonday), StaffUsername, CreateStaffWeek(), canEdit: true, Today, Usernames, Now);

        response.DefaultWeekTemplateId.Should().Be(WeekTemplateId);
        response.DefaultWeekTemplateName.Should().BeNull();
    }

    [Test]
    public void ToResponse_Blocks_StoredAndFromTemplatesInTimeOrder()
    {
        var training = CreateBlock(At(NextMonday, 9), At(NextMonday, 10));
        var lunch = CreateTemplateBlock(12, 13);
        var data = CreateData(NextMonday, templateBlocks: [lunch], blocks: [training]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Blocks.Should().HaveCount(2);
        var stored = response.Blocks[0];
        stored.BlockId.Should().Be(training.Id);
        stored.TemplateBlockId.Should().BeNull();
        stored.IsFromTemplate.Should().BeFalse();
        stored.ActivityName.Should().Be(Training.Name);

        var projected = response.Blocks[1];
        projected.BlockId.Should().BeNull();
        projected.TemplateBlockId.Should().Be(lunch.Id);
        projected.IsFromTemplate.Should().BeTrue();
        projected.StaffUserId.Should().Be(StaffUserId);
        projected.Date.Should().Be(NextMonday);
        projected.StartUtc.Should().Be(At(NextMonday, 12));
        projected.EndUtc.Should().Be(At(NextMonday, 13));
        projected.ActivityTypeId.Should().Be(Lunch.Id);
        projected.ActivityName.Should().Be(Lunch.Name);
        projected.Colour.Should().Be(Lunch.Colour);
        projected.CanEdit.Should().BeTrue();
        projected.OverlapsAppointment.Should().BeFalse();
    }

    [Test]
    public void ToResponse_BlocksStartingTogether_ShorterFirst()
    {
        var longer = CreateBlock(At(NextMonday, 9), At(NextMonday, 11));
        var shorter = CreateBlock(At(NextMonday, 9), At(NextMonday, 10));
        var data = CreateData(NextMonday, staffWeeks: [], blocks: [longer, shorter]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Blocks.Select(b => b.BlockId).Should().Equal(shorter.Id, longer.Id);
    }

    [Test]
    public void ToResponse_BlocksOnPastDays_CantBeEditedEvenByTheOwner()
    {
        var friday = Today.AddDays(-3);
        var data = CreateData(friday, Today,
            templateBlocks: [CreateTemplateBlock(12, 13)],
            blocks: [CreateBlock(At(friday, 9), At(friday, 10)), CreateBlock(At(Today, 9), At(Today, 10))]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Blocks.Where(b => b.Date == friday).Should().HaveCount(2).And.OnlyContain(b => !b.CanEdit);
        response.Blocks.Where(b => b.Date == Today).Should().HaveCount(2).And.OnlyContain(b => b.CanEdit);
        response.Days.Single(d => d.Date == friday).IsPast.Should().BeTrue();
        response.Days.Single(d => d.Date == Today).IsToday.Should().BeTrue();
    }

    [Test]
    public void ToResponse_SomeoneElsesCalendar_NoBlockCanBeEdited()
    {
        var data = CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)], blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 10))]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: false, Today, Usernames, Now);

        response.Blocks.Should().HaveCount(2).And.OnlyContain(b => !b.CanEdit);
    }

    [Test]
    public void ToResponse_BlocksUnderAnAppointment_AreFlaggedAsOverlapping()
    {
        var appointment = CreateAppointment(startUtc: At(NextMonday, 12, 30));
        var storedOver = CreateBlock(At(NextMonday, 12, 45), At(NextMonday, 13, 30));
        var storedTouching = CreateBlock(At(NextMonday, 13, 0), At(NextMonday, 14));
        var data = CreateData(NextMonday,
            templateBlocks: [CreateTemplateBlock(12, 13), CreateTemplateBlock(15, 16, Training)],
            blocks: [storedOver, storedTouching],
            appointments: [appointment]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Blocks.Single(b => b.ActivityName == Lunch.Name).OverlapsAppointment.Should().BeTrue();
        response.Blocks.Single(b => b.BlockId == storedOver.Id).OverlapsAppointment.Should().BeTrue();
        response.Blocks.Single(b => b.BlockId == storedTouching.Id).OverlapsAppointment.Should().BeFalse();
        response.Blocks.Single(b => b.IsFromTemplate && b.ActivityName == Training.Name).OverlapsAppointment.Should().BeFalse();
    }

    [Test]
    public void ToResponse_CancelledAppointment_DoesntFlagABlockOrShow()
    {
        var cancelled = CreateAppointment(AppointmentStatus.Cancelled, At(NextMonday, 12, 30));
        var data = CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)], appointments: [cancelled]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Appointments.Should().BeEmpty();
        response.Blocks.Single().OverlapsAppointment.Should().BeFalse();
    }

    [Test]
    public void ToResponse_Appointments_OnlyThoseOnTheCalendarInTimeOrder()
    {
        var later = CreateAppointment(startUtc: At(NextMonday, 15));
        var attended = CreateAppointment(AppointmentStatus.Attended, At(NextMonday, 9));
        var didNotAttend = CreateAppointment(AppointmentStatus.DidNotAttend, At(NextMonday, 11));
        var requested = CreateAppointment(AppointmentStatus.Requested);
        var cancelled = CreateAppointment(AppointmentStatus.Cancelled, At(NextMonday, 10));
        var data = CreateData(NextMonday, staffWeeks: [], appointments: [later, attended, didNotAttend, requested, cancelled]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Appointments.Select(a => a.Id).Should().Equal(attended.Id, didNotAttend.Id, later.Id);
    }

    [Test]
    public void ToResponse_AppointmentsAtTheSameTime_InIdOrder()
    {
        var first = CreateAppointment(startUtc: At(NextMonday, 10));
        var second = CreateAppointment(startUtc: At(NextMonday, 10));
        var ordered = new[] { first, second }.OrderBy(a => a.Id).ToList();
        var data = CreateData(NextMonday, staffWeeks: [], appointments: [ordered[1], ordered[0]]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Appointments.Select(a => a.Id).Should().Equal(ordered[0].Id, ordered[1].Id);
    }

    [Test]
    public void ToResponse_AppointmentWithoutATime_IsLeftOut()
    {
        var broken = CreateAppointment(startUtc: At(NextMonday, 10));
        broken.EndUtc = null;
        var data = CreateData(NextMonday, staffWeeks: [], appointments: [broken]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now);

        response.Appointments.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_Appointment_HasItsUsernamesDurationAndWhatCanBeDone()
    {
        var appointment = CreateAppointment(startUtc: At(NextMonday, 10), minutes: 45);
        appointment.BookedByUserId = OtherStaffUserId;
        var data = CreateData(NextMonday, staffWeeks: [], appointments: [appointment]);

        var response = CalendarMapper.ToResponse(data, StaffUsername, null, canEdit: true, Today, Usernames, Now).Appointments.Single();

        response.StaffUsername.Should().Be(StaffUsername);
        response.BookedByUsername.Should().Be(ProcedureMapper.DeletedUser);
        response.DurationMinutes.Should().Be(45);
        response.CanReschedule.Should().BeTrue();
        response.CanRecordOutcome.Should().BeFalse("it hasn't started yet");
        response.CanCancel.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Day_CopiesEveryField()
    {
        var day = new CalendarDay(NextMonday, true, false, false, At(NextMonday, 8), At(NextMonday, 18), DayTemplateId, DayTemplateName, true);

        var response = CalendarMapper.ToResponse(day);

        response.Date.Should().Be(NextMonday);
        response.IsWorkingDay.Should().BeTrue();
        response.IsPast.Should().BeFalse();
        response.IsToday.Should().BeFalse();
        response.WorkStartUtc.Should().Be(At(NextMonday, 8));
        response.WorkEndUtc.Should().Be(At(NextMonday, 18));
        response.DayTemplateId.Should().Be(DayTemplateId);
        response.DayTemplateName.Should().Be(DayTemplateName);
        response.IsDayTemplateOverridden.Should().BeTrue();
    }

    [Test]
    public void ToResponse_StoredBlock_CopiesItsFields()
    {
        var block = CreateBlock(At(NextMonday, 9), At(NextMonday, 10));
        block.Note = BlockNote;

        var response = CalendarMapper.ToResponse(block, canEditCalendar: true, Today, overlapsAppointment: true);

        response.BlockId.Should().Be(block.Id);
        response.StaffUserId.Should().Be(StaffUserId);
        response.Date.Should().Be(NextMonday);
        response.StartUtc.Should().Be(At(NextMonday, 9));
        response.EndUtc.Should().Be(At(NextMonday, 10));
        response.ActivityTypeId.Should().Be(Training.Id);
        response.ActivityName.Should().Be(Training.Name);
        response.Colour.Should().Be(Training.Colour);
        response.Note.Should().Be(BlockNote);
        response.CanEdit.Should().BeTrue();
        response.OverlapsAppointment.Should().BeTrue();
    }

    [Test]
    public void ToResponse_StoredBlockWithAnActivityTypeGiven_UsesThatOne()
    {
        var block = CreateBlock(At(NextMonday, 9), At(NextMonday, 10));
        block.ActivityTypeId = Lunch.Id;

        var response = CalendarMapper.ToResponse(block, canEditCalendar: true, Today, overlapsAppointment: false, Lunch);

        response.ActivityName.Should().Be(Lunch.Name);
        response.Colour.Should().Be(Lunch.Colour);
    }

    [Test]
    public void ToResponse_StoredBlockWithoutItsActivityType_HasEmptyNameAndColour()
    {
        var block = CreateBlock(At(NextMonday, 9), At(NextMonday, 10));
        block.ActivityType = null!;

        var response = CalendarMapper.ToResponse(block, canEditCalendar: true, Today, overlapsAppointment: false);

        response.ActivityName.Should().BeEmpty();
        response.Colour.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_StoredBlockStartingLateInTheUkEvening_IsOnItsUkDate()
    {
        // 23:30Z on a summer Sunday is 00:30 on Monday UK time.
        var block = CreateBlock(new DateTime(2026, 6, 14, 23, 30, 0, DateTimeKind.Utc), new DateTime(2026, 6, 15, 0, 30, 0, DateTimeKind.Utc));

        CalendarMapper.ToResponse(block, canEditCalendar: true, Today, overlapsAppointment: false).Date.Should().Be(SummerMonday);
    }

    [Test]
    public void ToResponse_ProjectedBlockWithoutItsActivityType_HasEmptyNameAndColour()
    {
        var source = CreateTemplateBlock(12, 13, note: BlockNote);
        source.ActivityType = null!;
        var projected = new ProjectedBlock(source, NextMonday, At(NextMonday, 12), At(NextMonday, 13));

        var response = CalendarMapper.ToResponse(projected, StaffUserId, canEditCalendar: true, Today, overlapsAppointment: true);

        response.ActivityName.Should().BeEmpty();
        response.Colour.Should().BeEmpty();
        response.Note.Should().Be(BlockNote);
        response.OverlapsAppointment.Should().BeTrue();
    }

    [Test]
    public void ToResponse_ProjectedBlockOnAPastDay_CantBeEdited()
    {
        var projected = new ProjectedBlock(CreateTemplateBlock(12, 13), Today.AddDays(-1), At(Today.AddDays(-1), 12), At(Today.AddDays(-1), 13));

        CalendarMapper.ToResponse(projected, StaffUserId, canEditCalendar: true, Today, overlapsAppointment: false)
            .CanEdit.Should().BeFalse();
    }

    [Test]
    public void ToResponse_StaffMemberWithADefaultWeek_NamesIt()
    {
        var response = CalendarMapper.ToResponse((StaffUserId, StaffUsername), DefaultWeek());

        response.UserId.Should().Be(StaffUserId);
        response.Username.Should().Be(StaffUsername);
        response.WeekTemplateId.Should().Be(WeekTemplateId);
        response.WeekTemplateName.Should().Be(WeekTemplateName);
    }

    [Test]
    public void ToResponse_StaffMemberWithoutADefaultWeek_HasNone()
    {
        var response = CalendarMapper.ToResponse((StaffUserId, StaffUsername), null);

        response.WeekTemplateId.Should().BeNull();
        response.WeekTemplateName.Should().BeNull();
    }

    [Test]
    public void ToResponse_StaffWeekTemplate_GivesItAndSince()
    {
        var response = CalendarMapper.ToResponse(StaffUserId, DefaultWeek());

        response.StaffUserId.Should().Be(StaffUserId);
        response.WeekTemplateId.Should().Be(WeekTemplateId);
        response.WeekTemplateName.Should().Be(WeekTemplateName);
        response.Since.Should().Be(LongAgo);
    }

    [Test]
    public void ToResponse_NoStaffWeekTemplate_GivesNone()
    {
        var response = CalendarMapper.ToResponse(StaffUserId, (StaffWeekTemplate?)null);

        response.StaffUserId.Should().Be(StaffUserId);
        response.WeekTemplateId.Should().BeNull();
        response.WeekTemplateName.Should().BeNull();
        response.Since.Should().BeNull();
    }
}
