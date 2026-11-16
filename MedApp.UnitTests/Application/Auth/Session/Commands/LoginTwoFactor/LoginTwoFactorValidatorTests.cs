using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.Session.Commands.LoginTwoFactor;

[TestFixture]
public sealed class LoginTwoFactorValidatorTests
{
    private readonly LoginTwoFactorValidator _validator = new();

    private static LoginTwoFactorCommand Command(string challenge = ChallengeToken, string? code = AuthenticatorCode, string? recovery = null) =>
        new(challenge, code, recovery, null, null);

    [Test]
    public void Validate_ChallengeAndCode_Passes() =>
        _validator.Validate(Command()).IsValid.Should().BeTrue();

    [Test]
    public void Validate_ChallengeAndRecoveryCode_Passes() =>
        _validator.Validate(Command(code: null, recovery: RecoveryCode)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoChallenge_Fails() =>
        _validator.Validate(Command(challenge: "")).Errors.Should().Contain(e => e.PropertyName == nameof(LoginTwoFactorCommand.Challenge));

    [Test]
    public void Validate_ChallengeTooLong_Fails() =>
        _validator.Validate(Command(challenge: new string('a', LoginTwoFactorValidator.MaxLength + 1)))
            .Errors.Should().Contain(e => e.PropertyName == nameof(LoginTwoFactorCommand.Challenge));

    [Test]
    public void Validate_NeitherCode_Fails() =>
        _validator.Validate(Command(code: null)).Errors.Should().Contain(e => e.PropertyName == nameof(LoginTwoFactorCommand.Code));

    [Test]
    public void Validate_BothCodes_Fails() =>
        _validator.Validate(Command(recovery: RecoveryCode)).Errors.Should().Contain(e => e.PropertyName == nameof(LoginTwoFactorCommand.RecoveryCode));

    [Test]
    public void Validate_CodesTooLong_Fail()
    {
        var tooLong = new string('1', LoginTwoFactorValidator.MaxLength + 1);

        _validator.Validate(Command(code: tooLong)).Errors.Should().Contain(e => e.PropertyName == nameof(LoginTwoFactorCommand.Code));
        _validator.Validate(Command(code: null, recovery: tooLong)).Errors.Should().Contain(e => e.PropertyName == nameof(LoginTwoFactorCommand.RecoveryCode));
    }

    [Test]
    public void ToString_NeverShowsTheChallengeOrCodes() =>
        Command(recovery: RecoveryCode).ToString().Should().NotContain(ChallengeToken).And.NotContain(AuthenticatorCode).And.NotContain(RecoveryCode);
}
