using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Application.Calendar.Meetings;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

[TestFixture]
public sealed class GetTeamCalendarHandlerTests : MeetingHandlerTestBase
{
    private RecordingActivityLog _activity = null!;
    private GetTeamCalendarHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(_activity);
        _handler = Fixture.Create<GetTeamCalendarHandler>();
    }

    [Test]
    public async Task Handle_NobodyNamed_ShowsEveryMemberOfStaffInTheStaffOrder()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], AdminActor), Token);

        response.Date.Should().Be(NextMonday);
        response.Members.Select(m => m.StaffUsername).Should().Equal(StaffUsername, OtherStaffUsername, AdminUsername);
        response.Members.Select(m => m.StaffUserId).Should().Equal(StaffUserId, OtherStaffUserId, AdminUserId);
        var read = CalendarReads.Should().ContainSingle().Subject;
        read.StaffUserIds.Should().Equal(StaffUserId, OtherStaffUserId, AdminUserId);
        read.From.Should().Be(NextMonday);
        read.To.Should().Be(NextMonday);
        read.Token.Should().Be(Token);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_SomeNamed_ShowsJustThemInTheStaffOrder()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [AdminUserId, StaffUserId], AdminActor), Token);

        response.Members.Select(m => m.StaffUserId).Should().Equal(StaffUserId, AdminUserId);
        CalendarReads.Single().StaffUserIds.Should().Equal(StaffUserId, AdminUserId);
    }

    [Test]
    public async Task Handle_SomeoneWhoIsntStaff_ThrowsNotFoundWithoutReading()
    {
        var stranger = Guid.Parse("0000000d-0000-0000-0000-0000000000ff");

        var act = () => _handler.Handle(new GetTeamCalendarQuery(NextMonday, [StaffUserId, stranger], AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member(s) not found: {stranger}.");
        CalendarReads.Should().BeEmpty();
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Appointments_RecordTheirPatientsAsListed()
    {
        var first = CreateAppointment(startUtc: At(NextMonday, 10));
        var second = CreateAppointment(startUtc: At(NextMonday, 11));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, appointments: [first]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, appointments: [second]));

        await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], AdminActor), Token);

        var (action, ids, procedureId) = _activity.Views.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.PatientListed);
        ids.Should().Equal(first.PatientId, second.PatientId);
        procedureId.Should().BeNull();
        _activity.Tokens.Should().Equal(Token);
    }

    [Test]
    public async Task Handle_Members_HaveTheirOwnCalendarsForTheDay()
    {
        var appointment = CreateAppointment(startUtc: At(NextMonday, 10));
        var meeting = NewMeeting(At(NextMonday, 15), At(NextMonday, 16));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)], appointments: [appointment], meetings: [meeting]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, meetings: [meeting]));

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [StaffUserId, OtherStaffUserId], AdminActor), Token);

        var staff = response.Members[0];
        staff.From.Should().Be(NextMonday);
        staff.To.Should().Be(NextMonday);
        staff.Days.Should().ContainSingle().Which.Date.Should().Be(NextMonday);
        staff.Blocks.Should().ContainSingle().Which.ActivityName.Should().Be(Lunch.Name);
        staff.Appointments.Should().ContainSingle().Which.Id.Should().Be(appointment.Id);
        staff.Meetings.Should().ContainSingle().Which.CanEdit.Should().BeTrue();
        staff.DefaultWeekTemplateId.Should().Be(WeekTemplateId);
        response.Members[1].Blocks.Should().BeEmpty();
        response.Members[1].Meetings.Single().Attendees.Select(a => a.Username).Should().Equal(OtherStaffUsername, StaffUsername);
        Identity.Verify(i => i.GetUsernamesByIdAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Order().SequenceEqual(new[] { StaffUserId, OtherStaffUserId, AdminUserId }.Order())),
            Token), Times.Once);
    }

    [Test]
    public async Task Handle_AsStaff_OnlyTheirOwnCalendarCanBeChangedAndNoMeeting()
    {
        var meeting = NewMeeting(At(NextMonday, 15), At(NextMonday, 16));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, meetings: [meeting]));

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], StaffActor), Token);

        response.Members.Select(m => m.CanEdit).Should().Equal(true, false, false);
        response.Members[0].Meetings.Single().CanEdit.Should().BeFalse();
    }

    [Test]
    public async Task Handle_AsAdmin_EveryCalendarCanBeChanged()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], AdminActor), Token);

        response.Members.Should().OnlyContain(m => m.CanEdit);
    }

    [Test]
    public async Task Handle_Day_DescribesTheDate()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], AdminActor), Token);

        response.Day.Date.Should().Be(NextMonday);
        response.Day.IsWorkingDay.Should().BeTrue();
        response.Day.IsPast.Should().BeFalse();
        response.Day.IsToday.Should().BeFalse();
        response.Day.WorkStartUtc.Should().Be(At(NextMonday, 8));
        response.Day.WorkEndUtc.Should().Be(At(NextMonday, 18));
        response.Day.DayTemplateId.Should().BeNull();
    }

    [Test]
    public async Task Handle_Saturday_IsNotAWorkingDayAndNobodyIsFree()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextSaturday, [], AdminActor), Token);

        response.Day.IsWorkingDay.Should().BeFalse();
        response.EveryoneFree.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_EveryoneFree_IsWhenNobodyShownHasAnythingOn()
    {
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday,
            blocks: [CreateBlock(At(NextMonday, 15), At(NextMonday, 16))],
            meetings: [NewMeeting(At(NextMonday, 12, 30), At(NextMonday, 13, 30), [OtherStaffUserId])]));

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [StaffUserId, OtherStaffUserId], AdminActor), Token);

        response.EveryoneFree.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal(
            (At(NextMonday, 8), At(NextMonday, 12)),
            (At(NextMonday, 13, 30), At(NextMonday, 15)),
            (At(NextMonday, 16), At(NextMonday, 18)));
    }

    [Test]
    public async Task Handle_Today_EveryoneFreeStartsNow()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(Today, [], AdminActor), Token);

        response.Day.IsToday.Should().BeTrue();
        response.EveryoneFree.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal((Now, At(Today, 18)));
    }

    [Test]
    public async Task Handle_PastDay_NobodyIsFree()
    {
        var friday = Today.AddDays(-3);

        var response = await _handler.Handle(new GetTeamCalendarQuery(friday, [], AdminActor), Token);

        response.Day.IsPast.Should().BeTrue();
        response.EveryoneFree.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_CancelledAppointment_IsNotListed()
    {
        GivenCalendar(CalendarOf(StaffUserId, NextMonday,
            appointments: [CreateAppointment(AppointmentStatus.Cancelled, At(NextMonday, 10))]));

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [StaffUserId], AdminActor), Token);

        response.Members.Single().Appointments.Should().BeEmpty();
        response.EveryoneFree.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal((At(NextMonday, 8), At(NextMonday, 18)));
    }
}
