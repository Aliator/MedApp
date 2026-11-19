using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Commands.BookAppointment;
using MedApp.Application.Appointments.Commands.CancelAppointment;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Queries.GetAppointmentById;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.PatientMessages.Queries.GetAppointmentMessages;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

/// <summary>
/// The team walls on appointments (MC5), through the handlers with the real <see cref="TeamBoundary"/>: another team's
/// appointment is a 404 to read or change, recorded; one the viewer sees but doesn't own (an admin's read, a pathway
/// shared with their team) is a 409 to change and read only; the clinician must be in the appointment's team (a 400);
/// a new appointment takes its pathway's team, else the booker's, else the clinician's; lists leave out what the viewer
/// can't see, and the clinic picker refuses another team (a 400). The test appointments are Dermatology's
/// (<see cref="PathwaysTestConstants.TeamId"/>), as is everyone on staff unless a test says otherwise.
/// </summary>
[TestFixture]
public sealed class AppointmentTeamWallsTests : AppointmentHandlerTestBase
{
    private static readonly Guid Dermatology = PathwaysTestConstants.TeamId;
    private static readonly Guid Gynaecology = Guid.Parse("0000005a-0000-0000-0000-0000000000d2");
    private const string DermatologyName = "Synthetic dermatology";
    private const string GynaecologyName = "Synthetic gynaecology";
    private static readonly DateOnly LastThursday = new(2026, 11, 12);

    private readonly List<PathwayAccess> _grants = [];
    private RecordingActivityLog _activity = null!;
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Patient _patient = null!;

    private static PathwayActor StaffOf(params Guid[] teams) =>
        Colleague with { Permissions = StandardRoles.Clinician.Permissions, Scope = new TeamScope(Colleague.UserId, teams, seesAllTeams: false) };

    private static PathwayActor AnAdmin =>
        Colleague with { Permissions = Permissions.Admin, Scope = TeamScope.For(Colleague.UserId, [], Permissions.Admin) };

    [SetUp]
    public void SetUpWalls()
    {
        _grants.Clear();
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken _) =>
                (IReadOnlyList<PathwayAccess>)_grants.Where(g => ids.Contains(g.PathwayId)).Where(TeamAccess.LiveGrantFor(scope).Compile()).ToList());
        var teams = new Mock<ITeamRepository>();
        teams.Setup(t => t.GetAsync(Dermatology, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = Dermatology, Name = DermatologyName });
        teams.Setup(t => t.GetAsync(Gynaecology, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = Gynaecology, Name = GynaecologyName });
        _activity = new RecordingActivityLog();

        _patient = PatientsTestConstants.CreateValidPatient();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(p => p.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        _pathways = new Mock<IPatientPathwayRepository>();

        Fixture.Inject<IActivityLog>(_activity);
        Fixture.Inject(teams.Object);
        Fixture.Inject(patients.Object);
        Fixture.Inject(_pathways.Object);
        Fixture.Inject<ITeamBoundary>(new TeamBoundary(access.Object, teams.Object, _activity));
    }

    /// <summary>A Gynaecology appointment, optionally on a Gynaecology pathway (its owner's grant written).</summary>
    private Appointment GivenGynaecologyAppointment(bool onAPathway = false)
    {
        var appointment = CreateAppointment();
        appointment.TeamId = Gynaecology;
        if (onAPathway)
        {
            appointment.PathwayId = Guid.NewGuid();
            _grants.Add(PathwayAccess.Owner(appointment.PathwayId.Value, appointment.PatientId, Gynaecology, Now));
        }

        return GivenAppointment(appointment);
    }

    private void ExpectRefusalRecordedFor(Guid targetId)
    {
        var (action, details) = _activity.Once.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.TeamBoundaryRefused);
        var root = JsonDocument.Parse(details).RootElement;
        root.GetProperty("targetKind").GetString().Should().Be(AppointmentWalls.Target);
        root.GetProperty("targetId").GetGuid().Should().Be(targetId);
    }

    private string ViewAccess() => JsonDocument.Parse(_activity.ViewDetails.Single()).RootElement.GetProperty("access").GetString()!;

    [Test]
    public async Task Get_AnotherTeamsAppointment_IsA404AsIfItWerentThere_AndIsRecorded()
    {
        var appointment = GivenGynaecologyAppointment();

        var act = () => Fixture.Create<GetAppointmentByIdHandler>().Handle(
            new GetAppointmentByIdQuery(appointment.Id, StaffOf(Dermatology).Scope), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(AppointmentWalls.NotFoundMessage(appointment.Id));
        ExpectRefusalRecordedFor(appointment.Id);
        _activity.Views.Should().BeEmpty("nothing was shown");
    }

    [Test]
    public async Task Get_OwnTeams_IsShownWithWhatTheyCanDo_AndRecordsOwnerAccess()
    {
        var appointment = GivenAppointment(CreateAppointment());

        var response = await Fixture.Create<GetAppointmentByIdHandler>().Handle(
            new GetAppointmentByIdQuery(appointment.Id, StaffOf(Dermatology).Scope), CancellationToken.None);

        response.CanCancel.Should().BeTrue();
        response.CanEditNotes.Should().BeTrue();
        ViewAccess().Should().Be(nameof(TeamAccessKind.Owner));
    }

    [Test]
    public async Task Get_ThroughAPathwaySharedWithTheirTeam_IsShownReadOnly()
    {
        var appointment = GivenGynaecologyAppointment(onAPathway: true);
        _grants.Add(PathwayAccess.StageOwner(appointment.PathwayId!.Value, appointment.PatientId, Dermatology, Now));

        var response = await Fixture.Create<GetAppointmentByIdHandler>().Handle(
            new GetAppointmentByIdQuery(appointment.Id, StaffOf(Dermatology).Scope), CancellationToken.None);

        response.Id.Should().Be(appointment.Id);
        response.CanCancel.Should().BeFalse();
        response.CanReschedule.Should().BeFalse();
        response.CanEditNotes.Should().BeFalse();
        ViewAccess().Should().Be(nameof(TeamAccessKind.StageOwner));
    }

    [Test]
    public async Task Get_AsAdmin_EveryTeamsIsShownReadOnly()
    {
        var appointment = GivenGynaecologyAppointment();

        var response = await Fixture.Create<GetAppointmentByIdHandler>().Handle(
            new GetAppointmentByIdQuery(appointment.Id, AnAdmin.Scope), CancellationToken.None);

        response.CanCancel.Should().BeFalse();
        response.CanRecordOutcome.Should().BeFalse();
        ViewAccess().Should().Be(nameof(TeamAccessKind.Admin));
    }

    [Test]
    public async Task Messages_OfAnotherTeamsAppointment_AreA404_AndRecorded()
    {
        var appointment = GivenGynaecologyAppointment();
        var messages = new Mock<IPatientMessageRepository>();
        Fixture.Inject(messages.Object);

        var act = () => Fixture.Create<GetAppointmentMessagesHandler>().Handle(
            new GetAppointmentMessagesQuery(appointment.Id, StaffOf(Dermatology).Scope), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(AppointmentWalls.NotFoundMessage(appointment.Id));
        ExpectRefusalRecordedFor(appointment.Id);
        messages.Verify(m => m.GetForAppointmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Cancel_AnotherTeamsAppointment_IsA404_AndRecorded()
    {
        var appointment = GivenGynaecologyAppointment();

        var act = () => Fixture.Create<CancelAppointmentHandler>().Handle(
            new CancelAppointmentCommand(appointment.Id, CancelReason, StaffOf(Dermatology)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        ExpectRefusalRecordedFor(appointment.Id);
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Cancel_ByAnAdmin_IsA409NamingItsTeam()
    {
        var appointment = GivenGynaecologyAppointment();

        var act = () => Fixture.Create<CancelAppointmentHandler>().Handle(
            new CancelAppointmentCommand(appointment.Id, CancelReason, AnAdmin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(TeamWalls.RecordNotYours(AppointmentWalls.What, GynaecologyName));
        appointment.Status.Should().Be(AppointmentStatus.Booked);
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Notes_OnAPathwaySharedWithTheirTeam_IsA409()
    {
        var appointment = GivenGynaecologyAppointment(onAPathway: true);
        _grants.Add(PathwayAccess.StageOwner(appointment.PathwayId!.Value, appointment.PatientId, Dermatology, Now));

        var act = () => Fixture.Create<UpdateAppointmentNotesHandler>().Handle(
            new UpdateAppointmentNotesCommand(appointment.Id, OtherNotes, StaffOf(Dermatology)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(TeamWalls.RecordNotYours(AppointmentWalls.What, GynaecologyName));
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Cancel_OwnTeams_IsCancelled()
    {
        var appointment = GivenAppointment(CreateAppointment());

        await Fixture.Create<CancelAppointmentHandler>().Handle(
            new CancelAppointmentCommand(appointment.Id, CancelReason, StaffOf(Dermatology)), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Test]
    public async Task Book_WithAClinicianOutsideTheAppointmentsTeam_IsA400OnStaffUserId()
    {
        StaffTeams[StaffUserId] = new HashSet<Guid> { Gynaecology };
        var appointment = GivenAppointment(CreateAppointment(AppointmentStatus.Requested));

        var act = () => Fixture.Create<BookAppointmentHandler>().Handle(
            new BookAppointmentCommand(appointment.Id, StaffUserId, At(NextMonday, 14), At(NextMonday, 14, 30),
                nameof(AppointmentStatus.Requested), StaffOf(Dermatology)),
            CancellationToken.None);

        var refused = await act.Should().ThrowAsync<ValidationException>();
        var failure = refused.Which.Errors.Should().ContainSingle().Subject;
        failure.PropertyName.Should().Be("StaffUserId");
        failure.ErrorMessage.Should().Be(AppointmentWalls.StaffNotInTeam(StaffUsername, DermatologyName));
        failure.ErrorMessage.Should().Be(
            $"{StaffUsername} isn’t in {DermatologyName}, so can’t see its patients. Choose someone in the team.");
        appointment.Status.Should().Be(AppointmentStatus.Requested);
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Create_WithoutAPathway_TakesTheBookersTeam()
    {
        StaffTeams[StaffUserId] = new HashSet<Guid> { Dermatology, Gynaecology };

        await Fixture.Create<CreateAppointmentHandler>().Handle(Booking(StaffOf(Gynaecology)), CancellationToken.None);

        Added.Single().TeamId.Should().Be(Gynaecology);
    }

    [Test]
    public async Task Create_WithoutAPathwayByTheSystem_TakesTheCliniciansTeam()
    {
        StaffTeams[StaffUserId] = new HashSet<Guid> { Gynaecology };

        await Fixture.Create<CreateAppointmentHandler>().Handle(Booking(Colleague), CancellationToken.None);

        Added.Single().TeamId.Should().Be(Gynaecology);
    }

    [Test]
    public async Task Create_WithAClinicianOutsideTheBookersTeam_IsA400OnStaffUserId()
    {
        StaffTeams[StaffUserId] = new HashSet<Guid> { Gynaecology };

        var act = () => Fixture.Create<CreateAppointmentHandler>().Handle(Booking(StaffOf(Dermatology)), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("StaffUserId");
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Create_ForAnotherTeamsPathway_IsA404_AndRecorded()
    {
        var pathway = GivenGynaecologyPathway();

        var act = () => Fixture.Create<CreateAppointmentHandler>().Handle(
            Booking(StaffOf(Dermatology)) with { PathwayId = pathway.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(PathwayRepositoryExtensions.NotFoundMessage(pathway.Id));
        _activity.Once.Should().ContainSingle().Which.Action.Should().Be(ActivityAction.TeamBoundaryRefused);
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Create_ForAPathwaySharedWithTheirTeam_IsA409()
    {
        var pathway = GivenGynaecologyPathway();
        _grants.Add(PathwayAccess.StageOwner(pathway.Id, pathway.PatientId, Dermatology, Now));

        var act = () => Fixture.Create<CreateAppointmentHandler>().Handle(
            Booking(StaffOf(Dermatology)) with { PathwayId = pathway.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams(GynaecologyName));
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task RecordPast_WithoutAPathway_TakesTheRecordersTeam()
    {
        StaffTeams[StaffUserId] = new HashSet<Guid> { Gynaecology };
        var start = At(LastThursday, 10);

        await Fixture.Create<RecordPastAppointmentHandler>().Handle(
            new RecordPastAppointmentCommand(_patient.Id, null, null, StaffUserId, start, start.AddMinutes(30),
                nameof(AppointmentStatus.Attended), null, StaffOf(Gynaecology)),
            CancellationToken.None);

        Added.Single().TeamId.Should().Be(Gynaecology);
    }

    [Test]
    public async Task Search_PickingAnotherTeam_IsA400OnTeamId()
    {
        var act = () => Fixture.Create<SearchAppointmentsHandler>().Handle(
            CreateSearchQuery() with { TeamId = Gynaecology, Scope = StaffOf(Dermatology).Scope }, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Be(TeamPicker.NotYourTeam);
        Repository.Verify(r => r.SearchAsync(It.IsAny<SearchAppointmentsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Search_AsAdmin_RecordsTheTeamPicked()
    {
        Repository.Setup(r => r.SearchAsync(It.IsAny<SearchAppointmentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Appointment>([CreateAppointment()], 1));

        await Fixture.Create<SearchAppointmentsHandler>().Handle(
            CreateSearchQuery() with { TeamId = Gynaecology, Scope = AnAdmin.Scope }, CancellationToken.None);

        var details = JsonDocument.Parse(_activity.ViewDetails.Single()).RootElement;
        details.GetProperty("access").GetString().Should().Be("Admin");
        details.GetProperty("teamFilter").GetString().Should().Be(Gynaecology.ToString());
    }

    [Test]
    public void Filter_LeavesOutWhatTheViewerCantSee_AndThePickerNarrowsToOneTeam()
    {
        var ours = CreateAppointment();
        var theirs = CreateAppointment();
        theirs.TeamId = Gynaecology;
        var shared = CreateAppointment();
        shared.TeamId = Gynaecology;
        shared.PathwayId = Guid.NewGuid();
        var grants = new[] { PathwayAccess.StageOwner(shared.PathwayId.Value, shared.PatientId, Dermatology, Now) }.AsQueryable();
        var all = new[] { ours, theirs, shared }.AsQueryable();

        all.Filter(CreateSearchQuery() with { Scope = StaffOf(Dermatology).Scope }, all, grants)
            .Should().BeEquivalentTo([ours, shared]);
        all.Filter(CreateSearchQuery() with { Scope = StaffOf(Dermatology).Scope, TeamId = Gynaecology }, all, grants)
            .Should().BeEquivalentTo([shared]);
        all.Filter(CreateSearchQuery() with { Scope = AnAdmin.Scope }, all, grants)
            .Should().HaveCount(3);
        all.Filter(CreateSearchQuery() with { Scope = AnAdmin.Scope, TeamId = Gynaecology }, all, grants)
            .Should().BeEquivalentTo([theirs, shared]);
    }

    private CreateAppointmentCommand Booking(PathwayActor by) =>
        new(_patient.Id, null, null, StaffUserId, At(NextMonday, 10), At(NextMonday, 10, 30), Notes, by);

    private PatientPathway GivenGynaecologyPathway()
    {
        var pathway = PathwaysTestConstants.CreatePathway(PatientPathwayStatus.Assigned, patient: _patient, title: PathwayTitle);
        pathway.TeamId = Gynaecology;
        _grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, Gynaecology, Now));
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
    }
}
