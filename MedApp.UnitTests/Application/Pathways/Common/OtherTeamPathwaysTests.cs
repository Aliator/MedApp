using FluentAssertions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// Other teams' pathways as summary lines (decision 183): the team, in progress or completed, since when, and nothing
/// else; cancelled and archived ones left out; none for an admin.
/// </summary>
[TestFixture]
public sealed class OtherTeamPathwaysTests
{
    private static readonly Guid Dermatology = Guid.NewGuid();
    private static readonly Guid Gynaecology = Guid.NewGuid();
    private static readonly Guid Diabetes = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> Names = new()
    {
        [Dermatology] = "Synthetic dermatology",
        [Gynaecology] = "Synthetic gynaecology",
        [Diabetes] = "Synthetic diabetes",
    };

    private static PatientPathway Pathway(Guid team, PatientPathwayStatus status, DateTime created, DateTime? updated = null) => new()
    {
        Id = Guid.NewGuid(),
        TeamId = team,
        Status = status,
        Title = "Synthetic title that must never show",
        CreatedAt = created,
        LastUpdated = updated ?? created,
    };

    [Test]
    public void Summarise_OthersOnly_InProgressSinceStartedAndCompletedSinceFinished_NewestFirst()
    {
        var mine = Pathway(Dermatology, PatientPathwayStatus.InProgress, new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc));
        var gynaecology = Pathway(Gynaecology, PatientPathwayStatus.InProgress, new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));
        // 23:30 UTC in summer is the next day in the UK.
        var diabetes = Pathway(Diabetes, PatientPathwayStatus.Completed, new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 14, 23, 30, 0, DateTimeKind.Utc));
        var cancelled = Pathway(Gynaecology, PatientPathwayStatus.Cancelled, new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));
        var archived = Pathway(Gynaecology, PatientPathwayStatus.Archived, new(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc));
        var scope = new TeamScope(Guid.NewGuid(), [Dermatology], seesAllTeams: false);

        var lines = OtherTeamPathways.Summarise([mine, gynaecology, diabetes, cancelled, archived], new HashSet<Guid> { mine.Id }, Names, scope);

        lines.Select(l => (l.TeamName, l.State, l.Since)).Should().Equal(
            ("Synthetic diabetes", OtherTeamPathways.Completed, new DateOnly(2026, 9, 15)),
            ("Synthetic gynaecology", OtherTeamPathways.InProgress, new DateOnly(2026, 9, 1)));
    }

    [Test]
    public void Summarise_ForAnAdmin_IsEmpty()
    {
        var pathway = Pathway(Gynaecology, PatientPathwayStatus.InProgress, new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));

        OtherTeamPathways.Summarise([pathway], new HashSet<Guid>(), Names, new TeamScope(Guid.NewGuid(), [], seesAllTeams: true))
            .Should().BeEmpty();
    }
}
