using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Users.Commands.SetUserTeams;
using MedApp.Application.Authentication.Users.Queries.GetMyTeams;
using MedApp.Application.Authentication.Users.Queries.GetUserTeams;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.TeamsTestConstants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.SetUserTeams;

[TestFixture]
public sealed class SetUserTeamsHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityReadService> _users = null!;
    private Mock<ITeamRepository> _repository = null!;
    private RecordingActivityLog _activityLog = null!;
    private Team _dermatology = null!;
    private Team _urology = null!;
    private Team _archived = null!;
    private Team _lab = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _dermatology = CreateTeam();
        _urology = CreateTeam(OtherTeamName);
        _archived = CreateTeam(ThirdTeamName, isArchived: true);
        _lab = CreateTeam("Synthetic lab", kind: TeamKind.Lab);
        _repository = RepositoryWith(_dermatology, _urology, _archived, _lab);
        _users = new Mock<IIdentityReadService>();
        _users.Setup(u => u.GetUserAsync(StaffUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(StaffUserId, StaffUsername, ["Clinician"]));
        _activityLog = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject(_users.Object);
        _fixture.Inject<IActivityLog>(_activityLog);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private void GivenTheyBelongTo(params Team[] teams) =>
        GivenMemberships(teams.Select(t => (t, TeamMemberRole.Member)).ToArray());

    private void GivenMemberships(params (Team Team, TeamMemberRole Role)[] memberships)
    {
        _repository.Setup(r => r.GetForUserAsync(StaffUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships.Select(m => m.Team).ToList());
        _repository.Setup(r => r.GetMembershipsAsync(StaffUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships
                .Select(m => new TeamMembership { UserId = StaffUserId, TeamId = m.Team.Id, Team = m.Team, Role = m.Role })
                .ToList());
    }

    private Task<IReadOnlyList<TeamMembershipResponse>> Set(params Guid[] ids) =>
        Set(ids.Select(id => new TeamChoice(id)).ToArray());

    private Task<IReadOnlyList<TeamMembershipResponse>> Set(params TeamChoice[] teams) =>
        _fixture.Create<SetUserTeamsHandler>().Handle(new SetUserTeamsCommand(StaffUsername, teams), _cts.Token);

    private void VerifyNotSaved() =>
        _repository.Verify(r => r.SetForUserAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<Guid, TeamMemberRole>>(), It.IsAny<ActivityEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);

    [Test]
    public async Task Handle_NewSet_SavesItWithTheActivityEntryAndReturnsItSorted()
    {
        GivenTheyBelongTo(_dermatology);
        ActivityEntry? saved = null;
        IReadOnlyDictionary<Guid, TeamMemberRole>? savedTeams = null;
        _repository
            .Setup(r => r.SetForUserAsync(StaffUserId, It.IsAny<IReadOnlyDictionary<Guid, TeamMemberRole>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, IReadOnlyDictionary<Guid, TeamMemberRole> teams, ActivityEntry entry, CancellationToken _) => (savedTeams, saved) = (teams, entry));

        var response = await Set(_urology.Id, _dermatology.Id);

        response.Select(s => s.Name).Should().Equal(TeamName, OtherTeamName);
        response.Select(s => (s.Kind, s.Role)).Should().AllBeEquivalentTo(("Clinic", "Member"));
        savedTeams.Should().BeEquivalentTo(new Dictionary<Guid, TeamMemberRole>
        {
            [_dermatology.Id] = TeamMemberRole.Member,
            [_urology.Id] = TeamMemberRole.Member,
        });
        saved!.Action.Should().Be(ActivityAction.TeamMembershipChanged);
        saved.TargetUsername.Should().Be(StaffUsername);
        var details = JsonDocument.Parse(saved.Details).RootElement;
        details.GetProperty("from").EnumerateArray().Select(e => e.GetString()).Should().Equal(TeamName);
        details.GetProperty("to").EnumerateArray().Select(e => e.GetString()).Should().Equal(TeamName, OtherTeamName);
        details.GetProperty("toIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(_dermatology.Id, _urology.Id);
        details.GetProperty("fromIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(_dermatology.Id);
        details.GetProperty("toRoles").EnumerateArray().Select(e => e.GetString()).Should().Equal("Member", "Member");
        _repository.Verify(r => r.SetForUserAsync(StaffUserId, It.IsAny<IReadOnlyDictionary<Guid, TeamMemberRole>>(), saved, _cts.Token));
    }

    [Test]
    public async Task Handle_ClinicAdmin_IsSavedAndNamedSoInTheLog()
    {
        GivenTheyBelongTo(_dermatology);
        ActivityEntry? saved = null;
        IReadOnlyDictionary<Guid, TeamMemberRole>? savedTeams = null;
        _repository
            .Setup(r => r.SetForUserAsync(StaffUserId, It.IsAny<IReadOnlyDictionary<Guid, TeamMemberRole>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, IReadOnlyDictionary<Guid, TeamMemberRole> teams, ActivityEntry entry, CancellationToken _) => (savedTeams, saved) = (teams, entry));

        var response = await Set(new TeamChoice(_dermatology.Id, "ClinicAdmin"));

        response.Should().ContainSingle().Which.Role.Should().Be("ClinicAdmin");
        savedTeams![_dermatology.Id].Should().Be(TeamMemberRole.ClinicAdmin);
        var details = JsonDocument.Parse(saved!.Details).RootElement;
        details.GetProperty("from").EnumerateArray().Select(e => e.GetString()).Should().Equal(TeamName);
        details.GetProperty("to").EnumerateArray().Select(e => e.GetString()).Should().Equal($"{TeamName} (clinic admin)");
    }

    [Test]
    public async Task Handle_Empty_RemovesThemAll()
    {
        GivenTheyBelongTo(_dermatology, _urology);

        var response = await Set(Array.Empty<Guid>());

        response.Should().BeEmpty();
        _repository.Verify(r => r.SetForUserAsync(
            StaffUserId, It.Is<IReadOnlyDictionary<Guid, TeamMemberRole>>(t => t.Count == 0), It.IsAny<ActivityEntry>(), _cts.Token));
    }

    [Test]
    public async Task Handle_SameSetAndRoles_SavesAndRecordsNothing()
    {
        GivenMemberships((_dermatology, TeamMemberRole.ClinicAdmin));

        var response = await Set(new TeamChoice(_dermatology.Id, "ClinicAdmin"), new TeamChoice(_dermatology.Id, "Member"));

        response.Should().ContainSingle(s => s.Id == _dermatology.Id && s.Role == "ClinicAdmin");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_SameTeamsNewRole_IsAChange()
    {
        GivenMemberships((_dermatology, TeamMemberRole.ClinicAdmin));

        await Set(_dermatology.Id);

        _repository.Verify(r => r.SetForUserAsync(
            StaffUserId,
            It.Is<IReadOnlyDictionary<Guid, TeamMemberRole>>(t => t[_dermatology.Id] == TeamMemberRole.Member),
            It.IsAny<ActivityEntry>(),
            _cts.Token));
    }

    [Test]
    public async Task Handle_AnAdmin_ThrowsConflictWithoutSaving()
    {
        _users.Setup(u => u.GetUserAsync(StaffUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(StaffUserId, StaffUsername, [AppRoles.Admin]));
        GivenTheyBelongTo();

        var act = () => Set(_dermatology.Id);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamRules.AdminHasNoTeam(StaffUsername));
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_AnAdminLosingEveryTeam_IsAllowed()
    {
        _users.Setup(u => u.GetUserAsync(StaffUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(StaffUserId, StaffUsername, [AppRoles.Admin]));
        GivenTheyBelongTo(_dermatology);

        var response = await Set(Array.Empty<Guid>());

        response.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ClinicAdminOfTheLab_ThrowsConflictWithoutSaving()
    {
        GivenTheyBelongTo();

        var act = () => Set(new TeamChoice(_lab.Id, "ClinicAdmin"));

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamRules.LabHasNoClinicAdmin);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_MemberOfTheLab_IsAllowed()
    {
        GivenTheyBelongTo();

        var response = await Set(_lab.Id);

        response.Should().ContainSingle().Which.Kind.Should().Be("Lab");
    }

    [Test]
    public async Task Handle_KeepingAnArchivedOne_IsAllowed()
    {
        GivenTheyBelongTo(_archived);

        var response = await Set(_archived.Id, _urology.Id);

        response.Select(s => (s.Name, s.IsArchived)).Should().Equal((ThirdTeamName, true), (OtherTeamName, false));
    }

    [Test]
    public async Task Handle_NewlyGivingAnArchivedOne_ThrowsConflictWithoutSaving()
    {
        GivenTheyBelongTo();

        var act = () => Set(_archived.Id);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{ThirdTeamName}*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_UnknownTeam_ThrowsNotFound()
    {
        GivenTheyBelongTo();

        var act = () => Set(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var act = () => _fixture.Create<SetUserTeamsHandler>()
            .Handle(new SetUserTeamsCommand("nobody", [new TeamChoice(_dermatology.Id)]), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetUserTeams_ReturnsTheirsSortedWithRoles_OrNotFound()
    {
        GivenMemberships((_urology, TeamMemberRole.Member), (_dermatology, TeamMemberRole.ClinicAdmin));
        var handler = _fixture.Create<GetUserTeamsHandler>();

        (await handler.Handle(new GetUserTeamsQuery(StaffUsername), _cts.Token))
            .Select(s => (s.Name, s.Role)).Should().Equal((TeamName, "ClinicAdmin"), (OtherTeamName, "Member"));
        var act = () => handler.Handle(new GetUserTeamsQuery("nobody"), _cts.Token);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetMyTeams_ReturnsTheSignedInUsers()
    {
        GivenTheyBelongTo(_urology);

        var response = await _fixture.Create<GetMyTeamsHandler>().Handle(new GetMyTeamsQuery(StaffUserId), _cts.Token);

        response.Should().ContainSingle(s => s.Id == _urology.Id);
        _repository.Verify(r => r.GetMembershipsAsync(StaffUserId, _cts.Token));
    }
}
