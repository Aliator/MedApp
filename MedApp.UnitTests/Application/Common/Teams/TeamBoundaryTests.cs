using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Common.Teams;

/// <summary>
/// The team walls at a single record (<see cref="TeamBoundary"/>): across a wall is a 404 with a missing record's words,
/// recorded as <c>TeamBoundaryRefused</c> with only IDs; seen but another team's is a 409 naming whose it is.
/// </summary>
[TestFixture]
public sealed class TeamBoundaryTests
{
    private static readonly Guid Dermatology = Guid.Parse("0000005a-0000-0000-0000-0000000000d1");
    private static readonly Guid Gynaecology = Guid.Parse("0000005a-0000-0000-0000-0000000000d2");
    private static readonly DateTime Now = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);
    private const string Missing = "Pathway 'x' was not found.";

    private readonly List<PathwayAccess> _grants = [];
    private RecordingActivityLog _activity = null!;
    private TeamBoundary _boundary = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _grants.Clear();
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken _) =>
                (IReadOnlyList<PathwayAccess>)_grants.Where(g => ids.Contains(g.PathwayId)).Where(TeamAccess.LiveGrantFor(scope).Compile()).ToList());
        var teams = new Mock<ITeamRepository>();
        teams.Setup(t => t.GetAsync(Gynaecology, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = Gynaecology, Name = "Synthetic gynaecology" });
        _activity = new RecordingActivityLog();
        _boundary = new TeamBoundary(access.Object, teams.Object, _activity);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private PatientPathway Given(Guid teamId)
    {
        var pathway = new PatientPathway { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), TeamId = teamId };
        _grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, teamId, Now));
        return pathway;
    }

    private static TeamScope StaffOf(Guid team) => new(Guid.NewGuid(), [team], seesAllTeams: false);

    [Test]
    public async Task EnsurePathwayVisible_AnotherTeams_IsA404WithAMissingOnesWords_AndIsRecordedWithIdsOnly()
    {
        var theirs = Given(Gynaecology);

        var act = () => _boundary.EnsurePathwayVisibleAsync(theirs, StaffOf(Dermatology), Missing, _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(Missing);
        var (action, details) = _activity.Once.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.TeamBoundaryRefused);
        var json = JsonDocument.Parse(details).RootElement;
        json.GetProperty("targetKind").GetString().Should().Be(TeamWalls.PathwayTarget);
        json.GetProperty("targetId").GetGuid().Should().Be(theirs.Id);
        json.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["targetKind", "targetId"]);
        _activity.Tokens.Should().Contain(_cts.Token);
    }

    [Test]
    public async Task EnsurePathwayVisible_OwnTeams_SaysWhy_AndRecordsNothing()
    {
        var mine = Given(Dermatology);

        (await _boundary.EnsurePathwayVisibleAsync(mine, StaffOf(Dermatology), Missing, _cts.Token)).Should().Be(TeamAccessKind.Owner);
        _activity.Once.Should().BeEmpty();
    }

    [Test]
    public async Task EnsurePathwayVisible_AnAdmin_ReadsAnyTeams()
    {
        var theirs = Given(Gynaecology);
        var admin = new TeamScope(Guid.NewGuid(), [], seesAllTeams: true);

        (await _boundary.EnsurePathwayVisibleAsync(theirs, admin, Missing, _cts.Token)).Should().Be(TeamAccessKind.Admin);
    }

    [Test]
    public async Task EnsureCanChangePathway_SeenButAnotherTeams_IsA409NamingWhose()
    {
        var theirs = Given(Gynaecology);
        _grants.Add(PathwayAccess.StageOwner(theirs.Id, theirs.PatientId, Team.LabId, Now));

        var act = () => _boundary.EnsureCanChangePathwayAsync(theirs, StaffOf(Team.LabId), Missing, _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams("Synthetic gynaecology"));
        _activity.Once.Should().BeEmpty("the lab may read it, so nothing was refused across a wall");
    }

    [Test]
    public async Task EnsureCanChangePathway_AnAdmin_IsA409()
    {
        var theirs = Given(Gynaecology);

        var act = () => _boundary.EnsureCanChangePathwayAsync(theirs, new TeamScope(Guid.NewGuid(), [], true), Missing, _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task EnsureCanChangePathway_BehindTheWall_IsA404()
    {
        var theirs = Given(Gynaecology);

        var act = () => _boundary.EnsureCanChangePathwayAsync(theirs, StaffOf(Dermatology), Missing, _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        _activity.Once.Should().ContainSingle();
    }

    [Test]
    public async Task EnsureCanChangePathway_OwnTeams_Passes()
    {
        var mine = Given(Dermatology);

        await _boundary.EnsureCanChangePathwayAsync(mine, StaffOf(Dermatology), Missing, _cts.Token);
    }

    [Test]
    public async Task VisiblePathways_OnlyThoseWithTheViewersGrants()
    {
        var mine = Given(Dermatology);
        var theirs = Given(Gynaecology);

        (await _boundary.VisiblePathwaysAsync([mine.Id, theirs.Id], StaffOf(Dermatology), _cts.Token)).Should().BeEquivalentTo([mine.Id]);
        (await _boundary.VisiblePathwaysAsync([mine.Id, theirs.Id], TeamScope.System, _cts.Token)).Should().BeEquivalentTo([mine.Id, theirs.Id]);
    }

    [Test]
    public async Task EnsureTeamVisible_AndCanChangeTeam_FollowTheRecordsOwnTeam()
    {
        var record = Guid.NewGuid();

        await _boundary.EnsureTeamVisibleAsync("Referral", record, Dermatology, StaffOf(Dermatology), "gone", _cts.Token);
        var hidden = () => _boundary.EnsureTeamVisibleAsync("Referral", record, Gynaecology, StaffOf(Dermatology), "gone", _cts.Token);
        await hidden.Should().ThrowAsync<NotFoundException>().WithMessage("gone");

        var change = () => _boundary.EnsureCanChangeTeamAsync(Gynaecology, new TeamScope(Guid.NewGuid(), [], true), "This referral", _cts.Token);
        await change.Should().ThrowAsync<ConflictException>()
            .WithMessage(TeamWalls.RecordNotYours("This referral", "Synthetic gynaecology"));
    }

    [Test]
    public async Task EnsureRecordVisible_OwnTeam_PathwaySeen_Admin_OrA404Recorded()
    {
        var record = Guid.NewGuid();
        var lab = Guid.NewGuid();
        var pathway = Given(Gynaecology);
        _grants.Add(new PathwayAccess
        {
            Id = Guid.NewGuid(), PathwayId = pathway.Id, PatientId = pathway.PatientId, TeamId = lab,
            Reason = PathwayAccessReason.StageOwner, StartedAtUtc = Now
        });

        (await _boundary.EnsureRecordVisibleAsync("Letter", record, Dermatology, pathway.Id, StaffOf(Dermatology), "gone", _cts.Token))
            .Should().Be(TeamAccessKind.Owner);
        (await _boundary.EnsureRecordVisibleAsync("Letter", record, Gynaecology, pathway.Id, StaffOf(lab), "gone", _cts.Token))
            .Should().Be(TeamAccessKind.StageOwner);
        (await _boundary.EnsureRecordVisibleAsync("Letter", record, Gynaecology, null, new TeamScope(Guid.NewGuid(), [], true), "gone", _cts.Token))
            .Should().Be(TeamAccessKind.Admin);

        var hidden = () => _boundary.EnsureRecordVisibleAsync("Letter", record, Gynaecology, pathway.Id, StaffOf(Dermatology), "gone", _cts.Token);
        await hidden.Should().ThrowAsync<NotFoundException>().WithMessage("gone");
        _activity.Once.Should().ContainSingle(e => e.Action == ActivityAction.TeamBoundaryRefused);
    }
}
