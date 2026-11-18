using FluentAssertions;
using MedApp.Application.Teams.Common;
using static MedApp.UnitTests.Common.Constants.TeamsTestConstants;

namespace MedApp.UnitTests.Application.Teams.Common;

[TestFixture]
public sealed class TeamMapperTests
{
    [Test]
    public void ToResponse_CopiesItAndWhatHasIt()
    {
        var team = CreateTeam(isArchived: true, version: 7);

        var response = TeamMapper.ToResponse(team, new TeamUsage(2, 9, 4));

        response.Should().BeEquivalentTo(new
        {
            team.Id,
            Name = TeamName,
            IsArchived = true,
            Version = 7,
            TemplateCount = 2,
            PathwayCount = 9,
            MemberCount = 4,
            CanBeDeleted = false,
            CreatedAt = CreatedAtUtc,
            LastUpdated = CreatedAtUtc
        });
    }

    [Test]
    public void ToResponse_OnlyALetterTemplateHasIt_CountsIt_AndCantBeDeleted()
    {
        var usage = new TeamUsage(0, 0, 0, LetterTemplateCount: 2);

        var response = TeamMapper.ToResponse(CreateTeam(), usage);

        response.LetterTemplateCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
        TeamRules.InUseMessage(CreateTeam(), usage).Should().Contain("2 letter templates");
    }

    [Test]
    public void ToResponse_NothingHasIt_CanBeDeleted() =>
        TeamMapper.ToResponse(CreateTeam(), TeamUsage.None).CanBeDeleted.Should().BeTrue();

    [Test]
    public void ToStaffResponses_SortsByNameIgnoringCase()
    {
        var urology = CreateTeam(OtherTeamName, isArchived: true);
        var biomechanics = CreateTeam("synthetic Biomechanics");
        var dermatology = CreateTeam();

        var responses = TeamMapper.ToStaffResponses([urology, biomechanics, dermatology]);

        responses.Select(r => (r.Id, r.Name, r.IsArchived)).Should().Equal(
            (biomechanics.Id, "synthetic Biomechanics", false),
            (dermatology.Id, TeamName, false),
            (urology.Id, OtherTeamName, true));
    }
}
