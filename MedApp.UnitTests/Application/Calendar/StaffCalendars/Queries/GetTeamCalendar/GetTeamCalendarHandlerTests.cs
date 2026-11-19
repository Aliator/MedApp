using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Application.Calendar.Meetings;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

/// <summary>
/// A team's calendar (MC11): any team, for anyone; the viewer's first team by default. In these tests the two members of
/// staff are in <see cref="Home"/> and the admin in none; <see cref="Other"/> has nobody unless a test says so.
/// </summary>
[TestFixture]
public sealed class GetTeamCalendarHandlerTests : MeetingHandlerTestBase
{
    private static readonly Team Home = TeamsTestConstants.CreateTeam(id: PathwaysTestConstants.TeamId);
    private static readonly Team Other = TeamsTestConstants.CreateTeam(
        TeamsTestConstants.OtherTeamName, id: Guid.Parse("0000005a-0000-0000-0000-0000000000bb"));

    /// <summary>A member of staff in <see cref="Home"/>, with their team walls.</summary>
    private static readonly CalendarActor Member = StaffActor with
    {
        Scope = TeamScope.For(StaffUserId, [Home.Id], TestPermissions.Staff)
    };

    private RecordingActivityLog _activity = null!;
    private InMemoryClosedDays _closedDays = null!;
    private GetTeamCalendarHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _activity = new RecordingActivityLog();
        _closedDays = new InMemoryClosedDays();
        var boundary = new Mock<ITeamBoundary>();
        boundary
            .Setup(b => b.VisiblePathwaysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        StaffTeams[AdminUserId] = new HashSet<Guid>();

        var teams = TeamsTestConstants.RepositoryWith(Home, Other);
        teams.Setup(t => t.GetAllAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync([Other, Home]);

        Fixture.Inject<IActivityLog>(_activity);
        Fixture.Inject<IClosedDayRepository>(_closedDays);
        Fixture.Inject(boundary.Object);
        Fixture.Inject(teams.Object);
        _handler = Fixture.Create<GetTeamCalendarHandler>();
    }

    [Test]
    public async Task Handle_NoTeamNamed_ShowsTheViewersFirstTeamsMembersByUsername()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], Member), Token);

        response.Date.Should().Be(NextMonday);
        response.TeamId.Should().Be(Home.Id);
        response.TeamName.Should().Be(Home.Name);
        response.Members.Select(m => m.StaffUsername).Should().Equal(OtherStaffUsername, StaffUsername);
        response.TeamMembers.Select(m => m.UserId).Should().Equal(OtherStaffUserId, StaffUserId);
        var read = CalendarReads.Should().ContainSingle().Subject;
        read.StaffUserIds.Should().Equal(OtherStaffUserId, StaffUserId);
        read.From.Should().Be(NextMonday);
        read.To.Should().Be(NextMonday);
        read.Token.Should().Be(Token);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoTeamNamedForSomeoneInNone_ShowsTheFirstTeamByName()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], AdminActor), Token);

        // "Synthetic dermatology" comes before "Synthetic urology", whatever order the repository gives them in.
        response.TeamId.Should().Be(Home.Id);
        response.Members.Select(m => m.StaffUserId).Should().Equal(OtherStaffUserId, StaffUserId);
    }

    [Test]
    public async Task Handle_AnotherTeam_IsShownToAnyone()
    {
        StaffTeams[OtherStaffUserId] = new HashSet<Guid> { Home.Id, Other.Id };

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], Member, Other.Id), Token);

        response.TeamId.Should().Be(Other.Id);
        response.TeamName.Should().Be(Other.Name);
        response.Members.Select(m => m.StaffUserId).Should().Equal(OtherStaffUserId);
        response.TeamMembers.Select(m => m.UserId).Should().Equal(OtherStaffUserId);
    }

    [Test]
    public async Task Handle_ATeamThatDoesNotExist_ThrowsNotFound()
    {
        var missing = Guid.Parse("0000005a-0000-0000-0000-0000000000ff");

        var act = () => _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], Member, missing), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Team '{missing}' was not found.");
        CalendarReads.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_SomeNamed_ShowsJustThoseInTheTeam()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [AdminUserId, StaffUserId], Member), Token);

        // The admin isn't in the team, so isn't shown; the team's members are still listed for choosing.
        response.Members.Select(m => m.StaffUserId).Should().Equal(StaffUserId);
        response.TeamMembers.Select(m => m.UserId).Should().Equal(OtherStaffUserId, StaffUserId);
        CalendarReads.Single().StaffUserIds.Should().Equal(StaffUserId);
    }

    [Test]
    public async Task Handle_ATeamWithNobody_ShowsNobodyAndNoFreeTime()
    {
        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], Member, Other.Id), Token);

        response.Members.Should().BeEmpty();
        response.TeamMembers.Should().BeEmpty();
        response.EveryoneFree.Should().BeEmpty();
        response.Day.Date.Should().Be(NextMonday);
    }

    [Test]
    public async Task Handle_Appointments_RecordTheirPatientsAsListed()
    {
        var first = CreateAppointment(startUtc: At(NextMonday, 10));
        var second = CreateAppointment(startUtc: At(NextMonday, 11));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, appointments: [first]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, appointments: [second]));

        await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], Member), Token);

        var (action, ids, pathwayId) = _activity.Views.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.PatientListed);
        ids.Should().Equal(second.PatientId, first.PatientId);
        pathwayId.Should().BeNull();
        _activity.Tokens.Should().Equal(Token);
    }

    [Test]
    public async Task Handle_AnotherTeamsAppointment_IsBusyTimeOnlyAndNotRecorded()
    {
        var ours = CreateAppointment(startUtc: At(NextMonday, 10));
        var theirs = CreateAppointment(startUtc: At(NextMonday, 11));
        theirs.TeamId = Other.Id;
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, appointments: [ours, theirs]));

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [StaffUserId], Member), Token);

        var calendar = response.Members.Single();
        calendar.Appointments.Should().ContainSingle().Which.Id.Should().Be(ours.Id);
        var busy = calendar.OtherTeamsBusy.Should().ContainSingle().Subject;
        busy.What.Should().Be(CalendarWalls.AppointmentBusy);
        busy.StaffUserId.Should().Be(StaffUserId);
        busy.Username.Should().Be(StaffUsername);
        (busy.StartUtc, busy.EndUtc).Should().Be((theirs.StartUtc!.Value, theirs.EndUtc!.Value));
        _activity.Views.Single().PatientIds.Should().Equal(ours.PatientId);
        // Free time still counts it.
        response.EveryoneFree.Should().NotContain(f => f.StartUtc < theirs.EndUtc && theirs.StartUtc < f.EndUtc);
    }

    [Test]
    public async Task Handle_AsAdmin_SeesEveryTeamsAppointmentsAndRecordsTheTeam()
    {
        var theirs = CreateAppointment(startUtc: At(NextMonday, 11));
        theirs.TeamId = Other.Id;
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, appointments: [theirs]));
        var admin = AdminActor with { Scope = TeamScope.For(AdminUserId, [], TestPermissions.Admin) };

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [StaffUserId], admin), Token);

        response.Members.Single().Appointments.Should().ContainSingle();
        response.Members.Single().OtherTeamsBusy.Should().BeEmpty();
        var details = JsonDocument.Parse(_activity.ViewDetails.Single()).RootElement;
        details.GetProperty("access").GetString().Should().Be("Admin");
        details.GetProperty("teamFilter").GetString().Should().Be(Home.Id.ToString());
    }

    [Test]
    public async Task Handle_Members_HaveTheirOwnCalendarsForTheDay()
    {
        var appointment = CreateAppointment(startUtc: At(NextMonday, 10));
        var meeting = NewMeeting(At(NextMonday, 15), At(NextMonday, 16));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)], appointments: [appointment], meetings: [meeting]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, meetings: [meeting]));

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [StaffUserId, OtherStaffUserId], AdminActor), Token);

        var staff = response.Members[1];
        staff.StaffUserId.Should().Be(StaffUserId);
        staff.From.Should().Be(NextMonday);
        staff.To.Should().Be(NextMonday);
        staff.Days.Should().ContainSingle().Which.Date.Should().Be(NextMonday);
        staff.Blocks.Should().ContainSingle().Which.ActivityName.Should().Be(Lunch.Name);
        staff.Appointments.Should().ContainSingle().Which.Id.Should().Be(appointment.Id);
        staff.Meetings.Should().ContainSingle().Which.CanEdit.Should().BeTrue();
        staff.DefaultWeekTemplateId.Should().Be(WeekTemplateId);
        response.Members[0].Blocks.Should().BeEmpty();
        response.Members[0].Meetings.Single().Attendees.Select(a => a.Username).Should().Equal(OtherStaffUsername, StaffUsername);
        Identity.Verify(i => i.GetUsernamesByIdAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Order().SequenceEqual(new[] { StaffUserId, OtherStaffUserId, AdminUserId }.Order())),
            Token), Times.Once);
    }

    [Test]
    public async Task Handle_AsStaff_OnlyTheirOwnCalendarCanBeChangedAndNoMeeting()
    {
        var meeting = NewMeeting(At(NextMonday, 15), At(NextMonday, 16));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, meetings: [meeting]));

        var response = await _handler.Handle(new GetTeamCalendarQuery(NextMonday, [], Member), Token);

        response.Members.Select(m => m.CanEdit).Should().Equal(false, true);
        response.Members[1].Meetings.Single().CanEdit.Should().BeFalse();
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
        response.Members.Single().OtherTeamsBusy.Should().BeEmpty();
        response.EveryoneFree.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal((At(NextMonday, 8), At(NextMonday, 18)));
    }

    [Test]
    public async Task Handle_AClosedDay_TheDayItselfSaysSo_ForTheEveryoneFreeColumn()
    {
        await _closedDays.AddAsync([CreateClosedDay()], CancellationToken.None);

        var response = await _handler.Handle(new GetTeamCalendarQuery(ClosedWednesday, [], AdminActor), Token);

        response.Day.IsWorkingDay.Should().BeFalse();
        response.Day.ClosedDayName.Should().Be(ClosedDayName);
    }
}
