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
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, [Guid.NewGuid()])).IsValid.Should().BeTrue();
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, [])).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoUsernameNullListOrEmptyId_IsInvalid()
    {
        _validator.Validate(new SetUserTeamsCommand("", [])).IsValid.Should().BeFalse();
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, null!)).IsValid.Should().BeFalse();
        _validator.Validate(new SetUserTeamsCommand(StaffUsername, [Guid.Empty])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooMany_IsInvalid() =>
        _validator.Validate(new SetUserTeamsCommand(
                StaffUsername, Enumerable.Range(0, TeamLimits.MaxPerUser + 1).Select(_ => Guid.NewGuid()).ToList()))
            .IsValid.Should().BeFalse();
}
