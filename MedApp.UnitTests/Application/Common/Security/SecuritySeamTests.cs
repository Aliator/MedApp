using FluentAssertions;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Common.Security;

/// <summary>The security event log seam, sign-in challenges and their tokens, and the small records they use.</summary>
[TestFixture]
public sealed class SecuritySeamTests
{
    [Test]
    public async Task LoggingSecurityEventLog_WritesTheEventAtInformation()
    {
        var logger = new Mock<ILogger<LoggingSecurityEventLog>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        await new LoggingSecurityEventLog(logger.Object).RecordAsync(
            new SecurityEvent(SecurityEventKind.TwoFactorReset, UserId, Username, ActorId, "an.admin"),
            CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("TwoFactorReset") && state.ToString()!.Contains("an.admin")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Test]
    public void Describe_ListsTheDetailsOrNone()
    {
        LoggingSecurityEventLog.Describe(null).Should().Be("none");
        LoggingSecurityEventLog.Describe(new Dictionary<string, object?>()).Should().Be("none");
        LoggingSecurityEventLog.Describe(new Dictionary<string, object?> { ["a"] = 1, ["b"] = true }).Should().Be("a=1, b=True");
    }

    [Test]
    public void ChallengeTokens_AreRandomUrlSafeAndHashedStably()
    {
        var first = SignInChallengeTokens.Generate();
        var second = SignInChallengeTokens.Generate();

        first.Should().NotBe(second);
        first.Should().HaveLength(43).And.MatchRegex("^[A-Za-z0-9_-]+$");
        SignInChallengeTokens.Hash(first).Should().Be(SignInChallengeTokens.Hash(first)).And.HaveLength(64).And.NotBe(first);
        SignInChallengeTokens.Hash("abc").Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Test]
    public void Challenge_IsUsableUntilItExpiresOrRunsOutOfAttempts()
    {
        var challenge = new SignInChallenge(UserId, "hash", NowUtc);

        challenge.ExpiresAtUtc.Should().Be(NowUtc.Add(SignInChallenge.Lifetime));
        challenge.IsUsable(NowUtc.AddMinutes(4)).Should().BeTrue();
        challenge.IsUsable(NowUtc.Add(SignInChallenge.Lifetime)).Should().BeFalse();

        typeof(SignInChallenge).GetProperty(nameof(SignInChallenge.FailedAttempts))!.SetValue(challenge, SignInChallenge.MaxFailedAttempts);
        challenge.IsUsable(NowUtc).Should().BeFalse();
    }

    [Test]
    public void LoginOutcome_IsASessionOrAChallenge()
    {
        var session = new LoginResult(SessionId, NowUtc);

        var signedIn = LoginOutcome.SignedIn(session);
        signedIn.NeedsTwoFactor.Should().BeFalse();
        signedIn.Session.Should().Be(session);

        var challenge = LoginOutcome.TwoFactorRequired(ChallengeToken, NowUtc);
        challenge.NeedsTwoFactor.Should().BeTrue();
        challenge.Session.Should().BeNull();
        challenge.ToString().Should().NotContain(ChallengeToken);
    }

    [Test]
    public void AuthenticatorUri_IsTheKeyUriFormat()
    {
        AuthenticatorUri.Build("a user", AuthenticatorKeyBase32).Should()
            .Be($"otpauth://totp/MedApp%3Aa%20user?secret={AuthenticatorKeyBase32}&issuer=MedApp&digits=6&period=30");
        AuthenticatorUri.FormatKey("ABCDEFGHIJ").Should().Be("abcd efgh ij");
    }

    [Test]
    public void SecretRecords_NeverPrintTheirSecrets()
    {
        new AuthenticatorKey(Username, AuthenticatorKeyBase32).ToString().Should().NotContain(AuthenticatorKeyBase32);
        new RecoveryCodesResult(IdentityResult.Success, [RecoveryCode]).ToString().Should().NotContain(RecoveryCode);
        new PasswordResetTarget(UserId, Username, Address, Token).ToString().Should().NotContain(Token).And.NotContain(Address);
        new EmailChangeRequest(Username, Address, Token).ToString().Should().NotContain(Token);
        new EmailChangeRequest(Username, Address, null).ToString().Should().Contain("null");
    }
}
