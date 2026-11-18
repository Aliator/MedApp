using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Users.Commands.SetUserTeams;
using MedApp.Application.Authentication.Users.Queries.GetMyTeams;
using MedApp.Application.Authentication.Users.Queries.GetUserTeams;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
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
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _dermatology = CreateTeam();
        _urology = CreateTeam(OtherTeamName);
        _archived = CreateTeam(ThirdTeamName, isArchived: true);
        _repository = RepositoryWith(_dermatology, _urology, _archived);
        _users = new Mock<IIdentityReadService>();
        _users.Setup(u => u.GetUserAsync(StaffUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(StaffUserId, StaffUsername, []));
        _activityLog = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject(_users.Object);
        _fixture.Inject<IActivityLog>(_activityLog);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private void GivenTheyBelongTo(params Team[] teams) =>
        _repository.Setup(r => r.GetForUserAsync(StaffUserId, It.IsAny<CancellationToken>())).ReturnsAsync(teams);

    private Task<IReadOnlyList<TeamMembershipResponse>> Set(params Guid[] ids) =>
        _fixture.Create<SetUserTeamsHandler>().Handle(new SetUserTeamsCommand(StaffUsername, ids), _cts.Token);

    [Test]
    public async Task Handle_NewSet_SavesItWithTheActivityEntryAndReturnsItSorted()
    {
        GivenTheyBelongTo(_dermatology);
        ActivityEntry? saved = null;
        IReadOnlyCollection<Guid>? savedIds = null;
        _repository
            .Setup(r => r.SetForUserAsync(StaffUserId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, IReadOnlyCollection<Guid> ids, ActivityEntry entry, CancellationToken _) => (savedIds, saved) = (ids, entry));

        var response = await Set(_urology.Id, _dermatology.Id);

        response.Select(s => s.Name).Should().Equal(TeamName, OtherTeamName);
        savedIds.Should().BeEquivalentTo([_dermatology.Id, _urology.Id]);
        saved!.Action.Should().Be(ActivityAction.TeamMembershipChanged);
        saved.TargetUsername.Should().Be(StaffUsername);
        var details = JsonDocument.Parse(saved.Details).RootElement;
        details.GetProperty("from").EnumerateArray().Select(e => e.GetString()).Should().Equal(TeamName);
        details.GetProperty("to").EnumerateArray().Select(e => e.GetString()).Should().Equal(TeamName, OtherTeamName);
        details.GetProperty("toIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(_dermatology.Id, _urology.Id);
        details.GetProperty("fromIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(_dermatology.Id);
        _repository.Verify(r => r.SetForUserAsync(StaffUserId, It.IsAny<IReadOnlyCollection<Guid>>(), saved, _cts.Token));
    }

    [Test]
    public async Task Handle_Empty_RemovesThemAll()
    {
        GivenTheyBelongTo(_dermatology, _urology);

        var response = await Set();

        response.Should().BeEmpty();
        _repository.Verify(r => r.SetForUserAsync(
            StaffUserId, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), It.IsAny<ActivityEntry>(), _cts.Token));
    }

    [Test]
    public async Task Handle_SameSet_SavesAndRecordsNothing()
    {
        GivenTheyBelongTo(_dermatology);

        var response = await Set(_dermatology.Id, _dermatology.Id);

        response.Should().ContainSingle(s => s.Id == _dermatology.Id);
        _repository.Verify(r => r.SetForUserAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _repository.Verify(r => r.SetForUserAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
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
            .Handle(new SetUserTeamsCommand("nobody", [_dermatology.Id]), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetUserTeams_ReturnsTheirsSorted_OrNotFound()
    {
        GivenTheyBelongTo(_urology, _dermatology);
        var handler = _fixture.Create<GetUserTeamsHandler>();

        (await handler.Handle(new GetUserTeamsQuery(StaffUsername), _cts.Token))
            .Select(s => s.Name).Should().Equal(TeamName, OtherTeamName);
        var act = () => handler.Handle(new GetUserTeamsQuery("nobody"), _cts.Token);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetMyTeams_ReturnsTheSignedInUsers()
    {
        GivenTheyBelongTo(_urology);

        var response = await _fixture.Create<GetMyTeamsHandler>().Handle(new GetMyTeamsQuery(StaffUserId), _cts.Token);

        response.Should().ContainSingle(s => s.Id == _urology.Id);
        _repository.Verify(r => r.GetForUserAsync(StaffUserId, _cts.Token));
    }
}
