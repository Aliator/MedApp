using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.SetUserSpecialties;
using MedApp.Application.Specialties.Common;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.SetUserSpecialties;

[TestFixture]
public sealed class SetUserSpecialtiesValidatorTests
{
    private readonly SetUserSpecialtiesValidator _validator = new();

    [Test]
    public void Validate_SomeOrNone_IsValid()
    {
        _validator.Validate(new SetUserSpecialtiesCommand(StaffUsername, [Guid.NewGuid()])).IsValid.Should().BeTrue();
        _validator.Validate(new SetUserSpecialtiesCommand(StaffUsername, [])).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoUsernameNullListOrEmptyId_IsInvalid()
    {
        _validator.Validate(new SetUserSpecialtiesCommand("", [])).IsValid.Should().BeFalse();
        _validator.Validate(new SetUserSpecialtiesCommand(StaffUsername, null!)).IsValid.Should().BeFalse();
        _validator.Validate(new SetUserSpecialtiesCommand(StaffUsername, [Guid.Empty])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooMany_IsInvalid() =>
        _validator.Validate(new SetUserSpecialtiesCommand(
                StaffUsername, Enumerable.Range(0, SpecialtyLimits.MaxPerUser + 1).Select(_ => Guid.NewGuid()).ToList()))
            .IsValid.Should().BeFalse();
}
