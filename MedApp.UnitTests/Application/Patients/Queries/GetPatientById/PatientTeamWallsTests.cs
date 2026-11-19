using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Teams;
using MedApp.Application.Patients.Queries.GetPatientById;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.GetPatientById;

/// <summary>
/// Patients are centre-wide, their pathways aren't (decision 183): the record and the timeline's overview list only the
/// pathways the viewer sees in full, and every other team's (not cancelled or archived) as a summary line, with no title
/// or ID.
/// </summary>
[TestFixture]
public sealed class PatientTeamWallsTests
{
    private static readonly Guid Dermatology = Guid.Parse("0000005a-0000-0000-0000-0000000000d1");
    private static readonly Guid Gynaecology = Guid.Parse("0000005a-0000-0000-0000-0000000000d2");
    private static readonly DateTime Now = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TeamScope DermatologyStaff = new(Guid.NewGuid(), [Dermatology], seesAllTeams: false);

    private IFixture _fixture = null!;
    private Patient _patient = null!;
    private PatientPathway _mine = null!;
    private PatientPathway _theirs = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _patient = PatientsTestConstants.CreateValidPatient();
        _mine = Pathway(Dermatology, "Synthetic own title");
        _theirs = Pathway(Gynaecology, "Synthetic hidden title");
        List<PathwayAccess> grants =
        [
            PathwayAccess.Owner(_mine.Id, _patient.Id, Dermatology, Now),
            PathwayAccess.Owner(_theirs.Id, _patient.Id, Gynaecology, Now),
        ];

        var patients = new Mock<IPatientRepository>();
        patients.Setup(p => p.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        var pathways = new Mock<IPatientPathwayRepository>();
        pathways.Setup(p => p.GetByPatientAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync([_mine, _theirs]);
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken _) =>
                (IReadOnlyList<PathwayAccess>)grants.Where(g => ids.Contains(g.PathwayId)).Where(TeamAccess.LiveGrantFor(scope).Compile()).ToList());
        var teams = TeamsTestConstants.RepositoryWith(
            new Team { Id = Dermatology, Name = "Synthetic dermatology" },
            new Team { Id = Gynaecology, Name = "Synthetic gynaecology" });
        var referrals = new Mock<IReferralRepository>();
        referrals.Setup(r => r.GetAwaitingTriageForPatientAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _fixture.Inject(patients.Object);
        _fixture.Inject(pathways.Object);
        _fixture.Inject(teams.Object);
        _fixture.Inject(referrals.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
        _fixture.Inject<ITeamBoundary>(new TeamBoundary(access.Object, teams.Object, new RecordingActivityLog()));
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    private PatientPathway Pathway(Guid team, string title) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = _patient.Id,
        TeamId = team,
        Title = title,
        Status = PatientPathwayStatus.InProgress,
        DueDateUtc = Now.AddDays(10),
        CreatedAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
        LastUpdated = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
    };

    [Test]
    public async Task Patient_ToStaff_SummarisesOnlyAnotherTeamsPathways()
    {
        var response = await _fixture.Create<GetPatientByIdHandler>()
            .Handle(new GetPatientByIdQuery(_patient.Id, DermatologyStaff), CancellationToken.None);

        var line = response!.OtherTeamPathways.Should().ContainSingle().Subject;
        line.TeamName.Should().Be("Synthetic gynaecology");
        line.State.Should().Be(OtherTeamPathways.InProgress);
        line.Since.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Test]
    public async Task Patient_ToAnAdmin_HasNoSummaries()
    {
        var response = await _fixture.Create<GetPatientByIdHandler>()
            .Handle(new GetPatientByIdQuery(_patient.Id, new TeamScope(Guid.NewGuid(), [], true)), CancellationToken.None);

        response!.OtherTeamPathways.Should().BeEmpty();
    }

    [Test]
    public async Task Overview_ToStaff_ListsOnlyTheirTeamsPathways_AndSummarisesTheRest()
    {
        var response = await _fixture.Create<GetPatientTimelineOverviewHandler>()
            .Handle(new GetPatientTimelineOverviewQuery(_patient.Id, false, DermatologyStaff), CancellationToken.None);

        response.Pathways.Select(p => p.Id).Should().Equal(_mine.Id);
        response.Upcoming.Should().ContainSingle();
        response.OpenCount.Should().Be(1);
        response.OtherTeamPathways.Should().ContainSingle().Which.TeamName.Should().Be("Synthetic gynaecology");
    }

    [Test]
    public async Task Overview_ToAnAdmin_ListsEveryTeamsPathways()
    {
        var response = await _fixture.Create<GetPatientTimelineOverviewHandler>()
            .Handle(new GetPatientTimelineOverviewQuery(_patient.Id, false, new TeamScope(Guid.NewGuid(), [], true)), CancellationToken.None);

        response.Pathways.Should().HaveCount(2);
        response.OtherTeamPathways.Should().BeEmpty();
    }
}
