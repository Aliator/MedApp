using FluentAssertions;
using MedApp.Application.Authentication.Passwords.Commands.ForgotPassword;
using MedApp.Application.Common.Identity;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.PasswordReset.Commands.ForgotPassword;

[TestFixture]
public sealed class ForgotPasswordValidatorTests
{
    private readonly ForgotPasswordValidator _validator = new();

    [TestCase(Username)]
    [TestCase(Address)]
    public void Validate_UsernameOrAddress_Passes(string value) =>
        _validator.Validate(new ForgotPasswordCommand(value)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_Empty_Fails() =>
        _validator.Validate(new ForgotPasswordCommand("")).IsValid.Should().BeFalse();

    [Test]
    public void Validate_LongerThanAnyName_Fails() =>
        _validator.Validate(new ForgotPasswordCommand(new string('a', UsernameRules.MaxLength + 1))).IsValid.Should().BeFalse();
}
