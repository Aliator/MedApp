using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.SetUserTeams;
using MedApp.Application.Teams.Common;
using static MedApp.UnitTests.Common.Constants.TeamsTestConstants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.SetUserTeams;

[TestFixture]
public sealed class SetUserTeamsValidatorTests
{
    private readonly SetUserTeamsValidator _validator = new();

    [Test]
    public void Validate_SomeOrNone_IsValid()
    {
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, [new TeamChoice(Guid.NewGuid())])).IsValid.Should().BeTrue();
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, [])).IsValid.Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("Member")]
    [TestCase("ClinicAdmin")]
    public void Validate_EachRole_IsValid(string? role) =>
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, [new TeamChoice(Guid.NewGuid(), role)])).IsValid.Should().BeTrue();

    [TestCase("Admin")]
    [TestCase("clinicadmin")]
    [TestCase("1")]
    public void Validate_AnyOtherRole_IsInvalidWithTheWords(string role)
    {
        var result = _validator.Validate(new SetUserTeamsCommand(StaffUsername, [new TeamChoice(Guid.NewGuid(), role)]));

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(TeamRules.ChooseRoleMessage);
    }

    [Test]
    public void Validate_NoUsernameNullListOrEmptyId_IsInvalid()
    {
        _validator.Validate(new SetUserTeamsCommand("", [])).IsValid.Should().BeFalse();
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, null!)).IsValid.Should().BeFalse();
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, [new TeamChoice(Guid.Empty)])).Errors
            .Should().ContainSingle().Which.ErrorMessage.Should().Be(TeamRules.ChooseTeamMessage);
    }

    [Test]
    public void Validate_TooMany_IsInvalid() =>
        _validator.Validate(new SetUserTeamsCommand(
                StaffUsername, Enumerable.Range(0, TeamLimits.MaxPerUser + 1).Select(_ => new TeamChoice(Guid.NewGuid())).ToList()))
            .IsValid.Should().BeFalse();
}
