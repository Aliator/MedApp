using FluentAssertions;
using MedApp.Application.Authentication.Passwords.Commands.ResetPassword;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.PasswordReset.Commands.ResetPassword;

[TestFixture]
public sealed class ResetPasswordValidatorTests
{
    private readonly ResetPasswordValidator _validator = new();

    [Test]
    public void Validate_CompleteLinkAndPassword_Passes() =>
        _validator.Validate(new ResetPasswordCommand(UserId, Token, NewPassword)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoUser_Fails() =>
        _validator.Validate(new ResetPasswordCommand(Guid.Empty, Token, NewPassword))
            .Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordCommand.UserId));

    [Test]
    public void Validate_NoToken_Fails() =>
        _validator.Validate(new ResetPasswordCommand(UserId, "", NewPassword))
            .Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordCommand.Token));

    [Test]
    public void Validate_AbsurdToken_Fails() =>
        _validator.Validate(new ResetPasswordCommand(UserId, new string('a', ResetPasswordValidator.MaxTokenLength + 1), NewPassword))
            .Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordCommand.Token));

    [Test]
    public void Validate_NoPassword_Fails() =>
        _validator.Validate(new ResetPasswordCommand(UserId, Token, ""))
            .Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordCommand.NewPassword));
}
