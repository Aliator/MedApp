using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Appointments;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

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
}
