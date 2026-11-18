using FluentAssertions;
using MedApp.Application.Teams.Commands.CreateTeam;
using MedApp.Application.Teams.Commands.UpdateTeam;

namespace MedApp.UnitTests.Application.Teams.Commands;

[TestFixture]
public sealed class TeamValidatorsTests
{
    private static readonly string LongestName = new('a', 100);

    [TestCase("Synthetic dermatology", true)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    public void Create_Name(string name, bool valid) =>
        new CreateTeamValidator().Validate(new CreateTeamCommand(name)).IsValid.Should().Be(valid);

    [Test]
    public void Create_NameLength_AtMostOneHundredOnceTrimmed()
    {
        var validator = new CreateTeamValidator();
        validator.Validate(new CreateTeamCommand($"  {LongestName}  ")).IsValid.Should().BeTrue();
        validator.Validate(new CreateTeamCommand(LongestName + "a")).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "Name must not exceed 100 characters.");
    }

    [TestCase(1, true)]
    [TestCase(365, true)]
    [TestCase(0, false)]
    [TestCase(366, false)]
    public void Targets_FromOneTo365Days(int days, bool valid)
    {
        new CreateTeamValidator().Validate(new CreateTeamCommand("Synthetic urology", days, days, days, days))
            .IsValid.Should().Be(valid);
        var update = new UpdateTeamValidator().Validate(
            new UpdateTeamCommand(Guid.NewGuid(), "Synthetic urology", 1, null, days, null, null));
        update.IsValid.Should().Be(valid);
        if (!valid)
            update.Errors.Single().ErrorMessage.Should().Be(MedApp.Application.Teams.Common.TeamValidation.TargetDaysMessage);
    }

    [Test]
    public void Update_Valid() =>
        new UpdateTeamValidator().Validate(new UpdateTeamCommand(Guid.NewGuid(), "Synthetic urology", 1))
            .IsValid.Should().BeTrue();

    [Test]
    public void Update_NoVersion_IsInvalid() =>
        new UpdateTeamValidator().Validate(new UpdateTeamCommand(Guid.NewGuid(), "Synthetic urology", 0))
            .Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateTeamCommand.ExpectedVersion));

    [Test]
    public void Update_NoIdOrBlankName_IsInvalid()
    {
        new UpdateTeamValidator().Validate(new UpdateTeamCommand(Guid.Empty, "Synthetic urology", 1)).IsValid.Should().BeFalse();
        new UpdateTeamValidator().Validate(new UpdateTeamCommand(Guid.NewGuid(), " ", 1)).IsValid.Should().BeFalse();
    }
}
