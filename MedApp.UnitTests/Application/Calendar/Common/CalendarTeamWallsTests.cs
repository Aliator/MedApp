using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
using MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Calendar;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Application.Calendar.Meetings;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>
/// The team walls on calendars and meetings (MC5, MC11): another team's appointments and meetings that aren't the
/// viewer's are busy time only, naming nothing; opening another team's meeting is a 404, recorded; everyone invited to a
/// meeting shares a team with the organiser, except at an MDT (admins arrange any). The viewer is
/// <see cref="CalendarTestConstants.StaffUserId"/>, in Dermatology; <see cref="CalendarTestConstants.OtherStaffUserId"/>
/// is in Gynaecology only, and the admin in no team.
/// </summary>
[TestFixture]
public sealed class CalendarTeamWallsTests : MeetingHandlerTestBase
{
    private static readonly Guid Dermatology = PathwaysTestConstants.TeamId;
    private static readonly Guid Gynaecology = Guid.Parse("0000005a-0000-0000-0000-0000000000d2");

    private static readonly CalendarActor Viewer = StaffActor with
    {
        Permissions = StandardRoles.Clinician.Permissions,
        Scope = new TeamScope(StaffUserId, [Dermatology], seesAllTeams: false)
    };

    private static readonly CalendarActor AnAdmin = AdminActor with
    {
        Permissions = Permissions.Admin,
        Scope = TeamScope.For(AdminUserId, [], Permissions.Admin)
    };

    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUpWalls()
    {
        StaffTeams[OtherStaffUserId] = new HashSet<Guid> { Gynaecology };
        StaffTeams[AdminUserId] = new HashSet<Guid>();
        _activity = new RecordingActivityLog();
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var teams = new Mock<ITeamRepository>();
        Calendars
            .Setup(r => r.GetCalendarDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, DateOnly from, DateOnly to, CancellationToken token) =>
                Calendars.Object.GetCalendarsAsync([id], from, to, token).Result[0]);

        Fixture.Inject<IActivityLog>(_activity);
        Fixture.Inject(teams.Object);
        Fixture.Inject<ITeamBoundary>(new TeamBoundary(access.Object, teams.Object, _activity));
    }

    /// <summary>A meeting the admin arranged for these people, and nobody else.</summary>
    private static Meeting MeetingFor(Guid id, int hour, params Guid[] attendees) =>
        NewMeeting(At(NextMonday, hour), At(NextMonday, hour + 1), attendees, id, OtherTitle);

    private static readonly Guid TheirMeetingId = Guid.Parse("0000000d-0000-0000-0000-0000000000a1");
    private static readonly Guid OurMdtId = Guid.Parse("0000000d-0000-0000-0000-0000000000a2");

    [Test]
    public async Task Calendar_AnotherTeamsAppointmentAndMeeting_AreBusyTimeOnly()
    {
        var theirs = CreateAppointment(startUtc: At(NextMonday, 10));
        theirs.TeamId = Gynaecology;
        var theirMeeting = MeetingFor(TheirMeetingId, 12, OtherStaffUserId);
        var ourMdt = MeetingFor(OurMdtId, 14, OtherStaffUserId);
        ourMdt.IsMdt = true;
        ourMdt.TeamId = Dermatology;
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, appointments: [theirs], meetings: [theirMeeting, ourMdt]));

        var response = await Fixture.Create<GetCalendarHandler>().Handle(
            new GetCalendarQuery(OtherStaffUserId, NextMonday, NextMonday, Viewer), Token);

        response.Appointments.Should().BeEmpty();
        response.Meetings.Should().ContainSingle().Which.Id.Should().Be(OurMdtId, "it's the viewer's team's MDT");
        response.OtherTeamsBusy.Select(b => (b.What, b.StartUtc, b.StaffUserId, b.Username)).Should().Equal(
            (CalendarWalls.AppointmentBusy, At(NextMonday, 10), OtherStaffUserId, OtherStaffUsername),
            (CalendarWalls.MeetingBusy, At(NextMonday, 12), OtherStaffUserId, OtherStaffUsername));
        _activity.Views.Single().PatientIds.Should().BeEmpty("the viewer saw no patient");
    }

    [Test]
    public async Task Calendar_AMeetingWithSomeoneInTheViewersTeam_IsShown()
    {
        var meeting = MeetingFor(TheirMeetingId, 12, OtherStaffUserId, StaffUserId);
        meeting.Attendees.Single(a => a.StaffUserId == StaffUserId).Reply = MeetingReply.Declined;
        StaffTeams[StaffUserId] = new HashSet<Guid> { Dermatology };
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, meetings: [meeting]));

        var response = await Fixture.Create<GetCalendarHandler>().Handle(
            new GetCalendarQuery(OtherStaffUserId, NextMonday, NextMonday, Viewer), Token);

        response.Meetings.Should().ContainSingle().Which.Title.Should().Be(OtherTitle);
        response.OtherTeamsBusy.Should().BeEmpty();
    }

    [Test]
    public async Task Calendar_AHiddenMeetingTheyDeclined_IsntBusyTime()
    {
        var meeting = MeetingFor(TheirMeetingId, 12, OtherStaffUserId);
        meeting.Attendees.Single().Reply = MeetingReply.Declined;
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, meetings: [meeting]));

        var response = await Fixture.Create<GetCalendarHandler>().Handle(
            new GetCalendarQuery(OtherStaffUserId, NextMonday, NextMonday, Viewer), Token);

        response.Meetings.Should().BeEmpty();
        response.OtherTeamsBusy.Should().BeEmpty();
    }

    [Test]
    public async Task Calendar_AsAdmin_ShowsEverything()
    {
        var theirs = CreateAppointment(startUtc: At(NextMonday, 10));
        theirs.TeamId = Gynaecology;
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, appointments: [theirs], meetings: [MeetingFor(TheirMeetingId, 12, OtherStaffUserId)]));

        var response = await Fixture.Create<GetCalendarHandler>().Handle(
            new GetCalendarQuery(OtherStaffUserId, NextMonday, NextMonday, AnAdmin), Token);

        response.Appointments.Should().ContainSingle();
        response.Appointments.Single().CanCancel.Should().BeFalse("admins read every team's, and change none");
        response.Meetings.Should().ContainSingle();
        response.OtherTeamsBusy.Should().BeEmpty();
    }

    [Test]
    public async Task GetMeeting_AnotherTeams_IsA404_AndRecorded()
    {
        var meeting = MeetingFor(TheirMeetingId, 12, OtherStaffUserId);
        Meetings.Setup(r => r.GetAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(meeting);

        var act = () => Fixture.Create<GetMeetingByIdHandler>().Handle(new GetMeetingByIdQuery(meeting.Id, Viewer), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(MeetingWalls.NotFoundMessage(meeting.Id));
        var (action, details) = _activity.Once.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.TeamBoundaryRefused);
        JsonDocument.Parse(details).RootElement.GetProperty("targetKind").GetString().Should().Be(MeetingWalls.Target);
    }

    [Test]
    public async Task GetMeeting_WithSomeoneInTheirTeam_IsShown()
    {
        StaffTeams[OtherStaffUserId] = new HashSet<Guid> { Gynaecology, Dermatology };
        var meeting = MeetingFor(TheirMeetingId, 12, OtherStaffUserId);
        Meetings.Setup(r => r.GetAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(meeting);

        var response = await Fixture.Create<GetMeetingByIdHandler>().Handle(new GetMeetingByIdQuery(meeting.Id, Viewer), Token);

        response.Id.Should().Be(meeting.Id);
        _activity.Once.Should().BeEmpty();
    }

    [Test]
    public async Task CreateMeeting_WithSomeoneOutsideTheOrganisersTeam_IsA400NamingThem()
    {
        var act = () => Fixture.Create<CreateMeetingHandler>().Handle(
            new CreateMeetingCommand(Title, null, At(NextMonday, 12), At(NextMonday, 13), [OtherStaffUserId], Viewer), Token);

        var refused = await act.Should().ThrowAsync<ValidationException>();
        var failure = refused.Which.Errors.Should().ContainSingle().Subject;
        failure.PropertyName.Should().Be(nameof(IMeetingCommand.AttendeeUserIds));
        failure.ErrorMessage.Should().Be(
            $"{OtherStaffUsername} isn’t in your team. Meetings are with your own team, except an MDT.");
        Added.Should().BeEmpty();
        VerifyNotLocked();
    }

    [Test]
    public async Task CreateMeeting_NamesEveryoneOutside()
    {
        StaffTeams[StaffUserId] = new HashSet<Guid> { Gynaecology };
        var organiser = AdminActor with { Scope = new TeamScope(AdminUserId, [Dermatology], seesAllTeams: false) };

        var act = () => Fixture.Create<CreateMeetingHandler>().Handle(
            new CreateMeetingCommand(Title, null, At(NextMonday, 12), At(NextMonday, 13), [StaffUserId, OtherStaffUserId], organiser), Token);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Be(
            MeetingWalls.NotInYourTeam([StaffUsername, OtherStaffUsername]));
        MeetingWalls.NotInYourTeam([StaffUsername, OtherStaffUsername]).Should().Contain("aren’t in your team");
    }

    [Test]
    public async Task CreateMeeting_WithTheirOwnTeam_IsArranged()
    {
        StaffTeams[OtherStaffUserId] = new HashSet<Guid> { Gynaecology, Dermatology };

        await Fixture.Create<CreateMeetingHandler>().Handle(
            new CreateMeetingCommand(Title, null, At(NextMonday, 12), At(NextMonday, 13), [OtherStaffUserId, StaffUserId], Viewer), Token);

        Added.Should().ContainSingle();
    }

    [Test]
    public async Task CreateMeeting_AnMdt_MayHaveAnyone()
    {
        var teams = TeamsTestConstants.RepositoryWith(TeamsTestConstants.CreateTeam(id: Dermatology));
        Fixture.Inject(teams.Object);

        await Fixture.Create<CreateMeetingHandler>().Handle(
            new CreateMeetingCommand(Title, null, At(NextMonday, 12), At(NextMonday, 13), [OtherStaffUserId], Viewer,
                IsMdt: true, TeamId: Dermatology), Token);

        Added.Should().ContainSingle().Which.IsMdt.Should().BeTrue();
    }

    [Test]
    public async Task CreateMeeting_ByAnAdmin_MayHaveAnyone()
    {
        await Fixture.Create<CreateMeetingHandler>().Handle(
            new CreateMeetingCommand(Title, null, At(NextMonday, 12), At(NextMonday, 13), [OtherStaffUserId, StaffUserId], AnAdmin), Token);

        Added.Should().ContainSingle();
    }

    [Test]
    public async Task Availability_AMeetingTheViewerCantSee_IsBusyWithoutItsTitle()
    {
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, meetings: [MeetingFor(TheirMeetingId, 12, OtherStaffUserId)]));

        var response = await Fixture.Create<GetAvailabilityHandler>().Handle(
            new GetAvailabilityQuery([OtherStaffUserId], NextMonday, NextMonday, 30, null, Viewer.Scope), Token);

        response.Busy.Should().ContainSingle().Which.What.Should().Be(CalendarWalls.HiddenMeeting);
        response.Days.Single().Free.Should().NotContain(f => f.StartUtc < At(NextMonday, 13) && At(NextMonday, 12) < f.EndUtc);
    }

    [Test]
    public async Task Availability_ForAnAdmin_NamesTheMeeting()
    {
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, meetings: [MeetingFor(TheirMeetingId, 12, OtherStaffUserId)]));

        var response = await Fixture.Create<GetAvailabilityHandler>().Handle(
            new GetAvailabilityQuery([OtherStaffUserId], NextMonday, NextMonday, 30, null, AnAdmin.Scope), Token);

        response.Busy.Should().ContainSingle().Which.What.Should().Be($"the meeting '{OtherTitle}'");
    }
}
