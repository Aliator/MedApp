using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayLabels;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// The team walls on pathways, through the handlers with the real <see cref="TeamBoundary"/> (the unit half of the leak
/// tests, decision 205): another team's pathway is a 404 to read or change, recorded; one the viewer's team reads but
/// doesn't own (the lab's after hand-back, an admin's) is a 409 to change and read only in the response; lists pass
/// the viewer's scope to the repository.
/// </summary>
[TestFixture]
public sealed class PathwayTeamWallsTests : PathwayHandlerTestBase
{
    private static readonly Guid Dermatology = Guid.Parse("0000005a-0000-0000-0000-0000000000d1");
    private static readonly Guid Gynaecology = Guid.Parse("0000005a-0000-0000-0000-0000000000d2");
    private const string GynaecologyName = "Synthetic gynaecology";

    private readonly List<PathwayAccess> _grants = [];
    private RecordingActivityLog _activity = null!;

    private static readonly IReadOnlySet<Permission> Clinician = StandardRoles.Clinician.Permissions;

    private static PathwayActor StaffOf(Guid team, IReadOnlySet<Permission>? permissions = null) =>
        Staff with { Permissions = permissions ?? Clinician, Scope = new TeamScope(Staff.UserId, [team], seesAllTeams: false) };

    private static PathwayActor AnAdmin =>
        Admin with { Permissions = Permissions.Admin, Scope = TeamScope.For(Admin.UserId, [], Permissions.Admin) };

    [SetUp]
    public void SetUpWalls()
    {
        _grants.Clear();
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken _) =>
                (IReadOnlyList<PathwayAccess>)_grants.Where(g => ids.Contains(g.PathwayId)).Where(TeamAccess.LiveGrantFor(scope).Compile()).ToList());
        var teams = new Mock<ITeamRepository>();
        teams.Setup(t => t.GetAsync(Gynaecology, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = Gynaecology, Name = GynaecologyName });
        _activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(_activity);
        Fixture.Inject(teams.Object);
        Fixture.Inject<ITeamBoundary>(new TeamBoundary(access.Object, teams.Object, _activity));
    }

    /// <summary>A Gynaecology pathway, its team's grant written as it was made.</summary>
    private PatientPathway GivenGynaecologyPathway(Status status = Status.InProgress, int finishedStages = 1)
    {
        var pathway = CreatePathway(status, finishedStages);
        pathway.TeamId = Gynaecology;
        _grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, Gynaecology, Now));
        return GivenPathway(pathway);
    }

    private void PassedToTheLab(PatientPathway pathway) =>
        _grants.Add(PathwayAccess.StageOwner(pathway.Id, pathway.PatientId, Team.LabId, Now));

    private void ExpectRefusalRecordedFor(PatientPathway pathway)
    {
        var (action, details) = _activity.Once.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.TeamBoundaryRefused);
        JsonDocument.Parse(details).RootElement.GetProperty("targetId").GetGuid().Should().Be(pathway.Id);
    }

    [Test]
    public async Task Get_AnotherTeamsPathway_IsA404AsIfItWerentThere_AndIsRecorded()
    {
        var pathway = GivenGynaecologyPathway();

        var act = () => Fixture.Create<GetPatientPathwayByIdHandler>().Handle(
            new GetPatientPathwayByIdQuery(pathway.Id, Staff.UserId, Clinician, StaffOf(Dermatology).Scope), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(PathwayRepositoryExtensions.NotFoundMessage(pathway.Id));
        ExpectRefusalRecordedFor(pathway);
        _activity.Views.Should().BeEmpty("nothing was shown");
    }

    [Test]
    public async Task Get_OwnTeamsPathway_IsShownAndItsViewSaysWhy()
    {
        var pathway = GivenGynaecologyPathway();

        var response = await Fixture.Create<GetPatientPathwayByIdHandler>().Handle(
            new GetPatientPathwayByIdQuery(pathway.Id, Staff.UserId, Clinician, StaffOf(Gynaecology).Scope), CancellationToken.None);

        response.ReadOnlyReason.Should().BeNull();
        response.CanWorkOnStages.Should().BeTrue();
        _activity.Views.Should().ContainSingle().Which.Action.Should().Be(ActivityAction.PathwayViewed);
        JsonDocument.Parse(_activity.ViewDetails.Single()).RootElement.GetProperty("access").GetString().Should().Be("Owner");
    }

    [Test]
    public async Task Get_TheLabAfterItWasPassedThePathway_ReadsItOnly()
    {
        var pathway = GivenGynaecologyPathway();
        PassedToTheLab(pathway);

        var response = await Fixture.Create<GetPatientPathwayByIdHandler>().Handle(
            new GetPatientPathwayByIdQuery(pathway.Id, Staff.UserId, StandardRoles.Lab.Permissions, StaffOf(Team.LabId).Scope),
            CancellationToken.None);

        response.ReadOnlyReason.Should().Be(TeamWalls.NotYourTeams(GynaecologyName));
        response.Should().BeEquivalentTo(new
        {
            CanWorkOnStages = false, CanEditDetails = false, CanAddNotes = false, CanChangeAssignments = false, CanBeDeleted = false,
        });
        response.AllowedStatusChanges.Should().BeEmpty();
        response.Stages.Should().AllSatisfy(s => (s.CanEdit || s.CanSkip || s.CanUnlock || s.CanReinstate || s.CanRecordSpecimen).Should().BeFalse());
        JsonDocument.Parse(_activity.ViewDetails.Single()).RootElement.GetProperty("access").GetString().Should().Be("StageOwner");
    }

    [Test]
    public async Task Get_AnAdmin_ReadsAnyTeamsPathway_WithNothingToDo()
    {
        var pathway = GivenGynaecologyPathway();

        var response = await Fixture.Create<GetPatientPathwayByIdHandler>().Handle(
            new GetPatientPathwayByIdQuery(pathway.Id, Admin.UserId, Permissions.Admin, AnAdmin.Scope), CancellationToken.None);

        response.ReadOnlyReason.Should().BeNull();
        response.CanWorkOnStages.Should().BeFalse();
        response.CanChangeAssignments.Should().BeFalse();
        JsonDocument.Parse(_activity.ViewDetails.Single()).RootElement.GetProperty("access").GetString().Should().Be("Admin");
    }

    [Test]
    public async Task Change_AnotherTeamsPathway_IsA404_AndNothingIsSaved()
    {
        var pathway = GivenGynaecologyPathway();

        var addNote = () => Fixture.Create<AddPathwayNoteHandler>().Handle(
            new AddPathwayNoteCommand(pathway.Id, "Synthetic note.", StaffOf(Dermatology)), CancellationToken.None);
        var start = () => Fixture.Create<StartPathwayStageHandler>().Handle(
            new StartPathwayStageCommand(pathway.Id, pathway.Stages[1].Id, StaffOf(Dermatology)), CancellationToken.None);

        await addNote.Should().ThrowAsync<NotFoundException>();
        await start.Should().ThrowAsync<NotFoundException>();
        VerifyNotSaved();
        _activity.Once.Should().HaveCount(2);
    }

    [Test]
    public async Task Change_ThePathwayTheLabWasPassed_IsA409NamingWhoseItIs()
    {
        var pathway = GivenGynaecologyPathway();
        PassedToTheLab(pathway);

        var act = () => Fixture.Create<AddPathwayNoteHandler>().Handle(
            new AddPathwayNoteCommand(pathway.Id, "Synthetic note.", StaffOf(Team.LabId, StandardRoles.Lab.Permissions)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams(GynaecologyName));
        VerifyNotSaved();
        _activity.Once.Should().BeEmpty();
    }

    [Test]
    public async Task Pause_AnotherTeamsPathway_IsA404Too()
    {
        var pathway = GivenGynaecologyPathway();

        var act = () => Fixture.Create<ChangePatientPathwayStatusHandler>().Handle(
            new ChangePatientPathwayStatusCommand(pathway.Id, nameof(Status.Blocked), "Synthetic reason.", StaffOf(Dermatology)),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Change_OwnTeamsPathway_GoesAhead()
    {
        var pathway = GivenGynaecologyPathway();

        await Fixture.Create<AddPathwayNoteHandler>().Handle(
            new AddPathwayNoteCommand(pathway.Id, "Synthetic note.", StaffOf(Gynaecology)), CancellationToken.None);

        VerifySavedOnce();
    }

    [Test]
    public async Task AskingForANoteToBeDeleted_OnAnotherTeamsPathway_IsA404()
    {
        var pathway = GivenGynaecologyPathway();

        var act = () => Fixture.Create<RequestNoteDeletionHandler>().Handle(
            new RequestNoteDeletionCommand(pathway.Id, Guid.NewGuid(), "Duplicate", null, StaffOf(Dermatology)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task DeletingANote_AnAdmin_ReachesAnyTeamsPathway_AndOtherStaffDont()
    {
        var pathway = GivenGynaecologyPathway();

        // Past the wall, the admin is told the note isn't there (it isn't): not the pathway's 404.
        var admin = () => Fixture.Create<DeletePathwayNoteHandler>().Handle(
            new DeletePathwayNoteCommand(pathway.Id, Guid.NewGuid(), "Other", "Synthetic detail.", 1, AnAdmin), CancellationToken.None);
        var staff = () => Fixture.Create<DeletePathwayNoteHandler>().Handle(
            new DeletePathwayNoteCommand(pathway.Id, Guid.NewGuid(), "Other", "Synthetic detail.", 1, StaffOf(Dermatology)), CancellationToken.None);

        (await admin.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().NotBe(PathwayRepositoryExtensions.NotFoundMessage(pathway.Id));
        (await staff.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be(PathwayRepositoryExtensions.NotFoundMessage(pathway.Id));
    }

    [Test]
    public async Task Search_PassesTheViewersScopeToTheRepository()
    {
        var scope = StaffOf(Dermatology).Scope!;
        var query = CreateSearchQuery() with { Scope = scope };
        Repository.Setup(r => r.SearchAsync(query, scope, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedApp.Application.Common.Paging.PagedResult<PatientPathway>([], 0));

        await Fixture.Create<SearchPatientPathwaysHandler>().Handle(query, CancellationToken.None);

        Repository.Verify(r => r.SearchAsync(query, scope, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Labels_PassTheViewersScopeToTheRepository()
    {
        var scope = StaffOf(Dermatology).Scope!;
        Repository.Setup(r => r.GetLabelsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), scope, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Fixture.Create<GetPatientPathwayLabelsHandler>().Handle(new GetPatientPathwayLabelsQuery([Guid.NewGuid()], scope), CancellationToken.None);

        Repository.Verify(r => r.GetLabelsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), scope, It.IsAny<CancellationToken>()), Times.Once);
    }
}
