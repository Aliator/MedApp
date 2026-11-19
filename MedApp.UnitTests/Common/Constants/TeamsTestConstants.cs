using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Teams;
using Moq;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic teams for the tests of teams, template teams and staff memberships.</summary>
public static class TeamsTestConstants
{
    public const string TeamName = "Synthetic dermatology";
    public const string OtherTeamName = "Synthetic urology";
    public const string ThirdTeamName = "Synthetic biomechanics";
    public const string StaffUsername = "s.synthetic";

    public static readonly Guid StaffUserId = Guid.Parse("0000005a-0000-0000-0000-000000000001");
    public static readonly DateTime CreatedAtUtc = new(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc);
    public static readonly DateTime NowUtc = new(2026, 11, 18, 10, 15, 0, DateTimeKind.Utc);

    public static Team CreateTeam(
        string name = TeamName, bool isArchived = false, int version = 3, Guid? id = null, TeamKind kind = TeamKind.Clinic) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        IsArchived = isArchived,
        Kind = kind,
        Version = version,
        CreatedAt = CreatedAtUtc,
        LastUpdated = CreatedAtUtc
    };

    /// <summary>A repository mock whose <c>GetByIdsAsync</c> and <c>GetAsync</c> find these teams.</summary>
    public static Mock<ITeamRepository> RepositoryWith(params Team[] teams)
    {
        var repository = new Mock<ITeamRepository>();
        repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<Team>)teams.Where(s => ids.Contains(s.Id)).ToList());
        foreach (var team in teams)
            repository.Setup(r => r.GetAsync(team.Id, It.IsAny<CancellationToken>())).ReturnsAsync(team);
        repository
            .Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamUsage>());
        return repository;
    }
}
