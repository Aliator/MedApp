using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Teams;

namespace MedApp.UnitTests.Application.Common.Teams;

/// <summary>The clinic picker (MC6): admins pick any team, anyone else one of theirs (a 400 otherwise).</summary>
[TestFixture]
public sealed class TeamPickerTests
{
    private static readonly Guid Mine = Guid.Parse("0000005a-0000-0000-0000-0000000000e1");
    private static readonly Guid Theirs = Guid.Parse("0000005a-0000-0000-0000-0000000000e2");
    private static readonly TeamScope Staff = new(Guid.NewGuid(), [Mine], seesAllTeams: false);
    private static readonly TeamScope Admin = new(Guid.NewGuid(), [], seesAllTeams: true);

    [Test]
    public void EnsureCanPick_NoneOrOwnTeam_ForStaff_AnyForAdmins()
    {
        TeamPicker.EnsureCanPick(null, Staff);
        TeamPicker.EnsureCanPick(Mine, Staff);
        TeamPicker.EnsureCanPick(Theirs, Admin);
    }

    [Test]
    public void EnsureCanPick_AnotherTeam_IsA400OnTeamId()
    {
        var pick = () => TeamPicker.EnsureCanPick(Theirs, Staff);

        pick.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "TeamId" && e.ErrorMessage == TeamPicker.NotYourTeam);
    }

    [Test]
    public void ListDetails_AdminsRecordTheirFilter_OthersNothing()
    {
        JsonSerializer.Serialize(TeamPicker.ListDetails(null, Admin)).Should().Be("""{"access":"Admin","teamFilter":"all"}""");
        JsonSerializer.Serialize(TeamPicker.ListDetails(Theirs, Admin)).Should().Contain(Theirs.ToString());
        TeamPicker.ListDetails(Mine, Staff).Should().BeNull();
        TeamPicker.ListDetails(null, TeamScope.System).Should().BeNull();
    }
}
