using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;
using MedApp.Application.Authentication.Sessions.Commands.ResendSignInCode;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.Session.Commands;

/// <summary>
/// Signing in with a code by email: the password step emails one for a longer-lived challenge, the second step checks it
/// against that challenge (counting a wrong one exactly as a wrong authenticator code), and a code can be sent again.
/// </summary>
[TestFixture]
public sealed class SignInWithEmailCodesTests
{
    private const string IpAddress = "192.0.2.1";
    private const string Browser = "Browser";

    private static readonly EmailCodeSent Sent = new(MaskedAddress, NowUtc.Add(EmailCode.Lifetime), NowUtc.AddMinutes(1), 3);

    private Mock<IAuthenticationService> _authentication = null!;
    private Mock<ISessionService> _sessions = null!;
    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<ISignInChallengeRepository> _challenges = null!;
    private Mock<IEmailCodeService> _emailCodes = null!;
    private Mock<ISecurityNotices> _notices = null!;
    private Mock<IAccountAttemptLimiter> _attempts = null!;
    private RecordingActivityLog _activity = null!;
    private FixedTimeProvider _clock = null!;
    private SignInChallenge? _saved;

    [SetUp]
    public void SetUp()
    {
        _authentication = new Mock<IAuthenticationService>();
        _authentication.Setup(a => a.ValidateCredentialsAsync(Username, CurrentPassword)).ReturnsAsync((UserId, Username, ["User"]));
        _sessions = new Mock<ISessionService>();
        _sessions.Setup(s => s.CreateSessionAsync(UserId, IpAddress, Browser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(SessionId, NowUtc.AddHours(8)));
        _twoFactor = new Mock<ITwoFactorService>();
        _twoFactor.Setup(t => t.GetMethodAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(TwoFactorMethod.Email);
        _challenges = new Mock<ISignInChallengeRepository>();
        _saved = null;
        _challenges.Setup(c => c.AddAsync(It.IsAny<SignInChallenge>(), NowUtc, It.IsAny<CancellationToken>()))
            .Callback<SignInChallenge, DateTime, CancellationToken>((challenge, _, _) => _saved = challenge)
            .Returns(Task.CompletedTask);
        _emailCodes = new Mock<IEmailCodeService>();
        _notices = new Mock<ISecurityNotices>();
        _attempts = new Mock<IAccountAttemptLimiter>();
        _activity = new RecordingActivityLog();
        _clock = new FixedTimeProvider(NowUtc);
    }

    private LoginHandler PasswordStep() =>
        new(_authentication.Object, _sessions.Object, _activity, _twoFactor.Object, _challenges.Object, _emailCodes.Object, _clock);

    private LoginTwoFactorHandler CodeStep()
    {
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [UserId] = Username });
        return new LoginTwoFactorHandler(_challenges.Object, _twoFactor.Object, _sessions.Object, read.Object, _activity,
            _notices.Object, Mock.Of<ISecurityEventLog>(), _attempts.Object, _emailCodes.Object, _clock);
    }

    private SignInChallenge GivenChallenge(TimeSpan? lifetime = null)
    {
        var challenge = new SignInChallenge(UserId, SignInChallengeTokens.Hash(ChallengeToken), NowUtc.AddMinutes(-1), lifetime ?? SignInChallenge.EmailLifetime);
        _challenges.Setup(c => c.FindByTokenHashAsync(SignInChallengeTokens.Hash(ChallengeToken), It.IsAny<CancellationToken>())).ReturnsAsync(challenge);
        _challenges.Setup(c => c.TryReserveAttemptAsync(challenge.Id, NowUtc, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _challenges.Setup(c => c.ConsumeAsync(challenge.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return challenge;
    }

    // The password step

    [Test]
    public async Task Password_EmailCodesAccount_EmailsACodeForATenMinuteChallenge()
    {
        _emailCodes.Setup(c => c.SendAsync(UserId, EmailCodePurpose.SignIn, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Sent);

        var outcome = await PasswordStep().Handle(new LoginCommand(Username, CurrentPassword, IpAddress, Browser), CancellationToken.None);

        outcome.NeedsTwoFactor.Should().BeTrue();
        outcome.Method.Should().Be(TwoFactorMethod.Email);
        outcome.ChallengeExpiresAtUtc.Should().Be(NowUtc.Add(SignInChallenge.EmailLifetime));
        outcome.EmailCode.Should().Be(new EmailCodeSentResponse(MaskedAddress, Sent.ExpiresAtUtc, 60, 3));
        _emailCodes.Verify(c => c.SendAsync(UserId, EmailCodePurpose.SignIn, _saved!.Id, It.IsAny<CancellationToken>()), Times.Once);
        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.TwoFactorChallenged);
        entry.Details.Should().Be("{\"method\":\"email\"}");
        _sessions.Verify(s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Password_NoAddressToSendTo_StillChallengesForARecoveryCode()
    {
        var outcome = await PasswordStep().Handle(new LoginCommand(Username, CurrentPassword, IpAddress, Browser), CancellationToken.None);

        outcome.Method.Should().Be(TwoFactorMethod.Email);
        outcome.EmailCode.Should().BeNull();
    }

    [Test]
    public async Task Password_AuthenticatorAccount_SendsNoEmail()
    {
        _twoFactor.Setup(t => t.GetMethodAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(TwoFactorMethod.Authenticator);

        var outcome = await PasswordStep().Handle(new LoginCommand(Username, CurrentPassword, IpAddress, Browser), CancellationToken.None);

        outcome.Method.Should().Be(TwoFactorMethod.Authenticator);
        outcome.ChallengeExpiresAtUtc.Should().Be(NowUtc.Add(SignInChallenge.Lifetime));
        outcome.EmailCode.Should().BeNull();
        _emailCodes.VerifyNoOtherCalls();
        _activity.Recorded.Single().Details.Should().Be("{}");
    }

    [Test]
    public async Task Password_TooManyCodesEmailed_IsRefused()
    {
        _emailCodes.Setup(c => c.SendAsync(UserId, EmailCodePurpose.SignIn, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TooManyAttemptsException("Too many codes emailed to this account.", 300));

        var act = () => PasswordStep().Handle(new LoginCommand(Username, CurrentPassword, IpAddress, Browser), CancellationToken.None);

        await act.Should().ThrowAsync<TooManyAttemptsException>();
        _sessions.VerifyNoOtherCalls();
    }

    // The code step

    [Test]
    public async Task Code_RightEmailedCode_SignsInWithTheEmailStepRecorded()
    {
        var challenge = GivenChallenge();
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.SignIn, challenge.Id, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var session = await CodeStep().Handle(new LoginTwoFactorCommand(ChallengeToken, EmailedCode, null, IpAddress, Browser), CancellationToken.None);

        session.SessionId.Should().Be(SessionId);
        _twoFactor.Verify(t => t.VerifyAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _twoFactor.Verify(t => t.RecordSignInAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _attempts.Verify(a => a.Attempt(AccountAttempt.TwoFactorCode, UserId), Times.Once);
        _challenges.Verify(c => c.TryReserveAttemptAsync(challenge.Id, NowUtc, It.IsAny<CancellationToken>()), Times.Once);
        var signedIn = _activity.Recorded.Should().ContainSingle().Subject;
        signedIn.Action.Should().Be(ActivityAction.SignedIn);
        signedIn.Details.Should().Be("{\"secondStep\":\"email\"}");
    }

    [Test]
    public async Task Code_WrongEmailedCode_CountsLikeAWrongAuthenticatorCode()
    {
        GivenChallenge();

        var act = () => CodeStep().Handle(new LoginTwoFactorCommand(ChallengeToken, EmailedCode, null, IpAddress, Browser), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTwoFactorCodeException>();
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _activity.Recorded.Should().ContainSingle(e => e.Action == ActivityAction.SignInFailed && e.Details.Contains("\"InvalidCode\""));
        _activity.Recorded.Should().OnlyContain(e => !(e.Details ?? string.Empty).Contains(EmailedCode));
        _sessions.Verify(s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Code_WrongEmailedCodeThatLocks_TellsTheHolder()
    {
        GivenChallenge();
        _twoFactor.Setup(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = () => CodeStep().Handle(new LoginTwoFactorCommand(ChallengeToken, EmailedCode, null, IpAddress, Browser), CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedOutException>();
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorCodesLockedOut, It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    [Test]
    public async Task Code_NoAttemptsLeft_ChecksNoEmailedCode()
    {
        var challenge = GivenChallenge();
        _challenges.Setup(c => c.TryReserveAttemptAsync(challenge.Id, NowUtc, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => CodeStep().Handle(new LoginTwoFactorCommand(ChallengeToken, EmailedCode, null, IpAddress, Browser), CancellationToken.None);

        await act.Should().ThrowAsync<SignInExpiredException>();
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Code_RecoveryCode_StillWorksForEmailCodes()
    {
        GivenChallenge();
        _twoFactor.Setup(t => t.RedeemRecoveryCodeAsync(UserId, RecoveryCode, It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryCodeRedemption(true, 9));

        await CodeStep().Handle(new LoginTwoFactorCommand(ChallengeToken, null, RecoveryCode, IpAddress, Browser), CancellationToken.None);

        _emailCodes.VerifyNoOtherCalls();
        _activity.Recorded.Should().Contain(e => e.Action == ActivityAction.SignedIn && e.Details.Contains("recoveryCode"));
    }

    // Sending the code again

    private ResendSignInCodeHandler Resend() => new(_challenges.Object, _twoFactor.Object, _emailCodes.Object, _clock);

    [Test]
    public async Task Resend_UsableChallenge_SendsANewCode()
    {
        var challenge = GivenChallenge();
        _emailCodes.Setup(c => c.SendAsync(UserId, EmailCodePurpose.SignIn, challenge.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sent with { ResendAvailableAtUtc = NowUtc.AddMinutes(1), ResendsLeft = 2 });

        var result = await Resend().Handle(new ResendSignInCodeCommand(ChallengeToken), CancellationToken.None);

        result.Should().Be(new EmailCodeSentResponse(MaskedAddress, Sent.ExpiresAtUtc, 60, 2));
    }

    [Test]
    public async Task Resend_UnknownOrExpiredChallenge_IsSignInExpired()
    {
        var act = () => Resend().Handle(new ResendSignInCodeCommand(ChallengeToken), CancellationToken.None);
        await act.Should().ThrowAsync<SignInExpiredException>();

        var expired = new SignInChallenge(UserId, SignInChallengeTokens.Hash(ChallengeToken), NowUtc.Add(-SignInChallenge.EmailLifetime), SignInChallenge.EmailLifetime);
        _challenges.Setup(c => c.FindByTokenHashAsync(SignInChallengeTokens.Hash(ChallengeToken), It.IsAny<CancellationToken>())).ReturnsAsync(expired);
        await act.Should().ThrowAsync<SignInExpiredException>();
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Resend_AuthenticatorAccount_IsAConflict()
    {
        GivenChallenge(SignInChallenge.Lifetime);
        _twoFactor.Setup(t => t.GetMethodAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(TwoFactorMethod.Authenticator);

        var act = () => Resend().Handle(new ResendSignInCodeCommand(ChallengeToken), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Resend_NoAddress_IsAConflict()
    {
        GivenChallenge();

        var act = () => Resend().Handle(new ResendSignInCodeCommand(ChallengeToken), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("recovery code");
    }

    [Test]
    public void ResendValidator_NeedsAChallenge()
    {
        new ResendSignInCodeValidator().Validate(new ResendSignInCodeCommand(ChallengeToken)).IsValid.Should().BeTrue();
        new ResendSignInCodeValidator().Validate(new ResendSignInCodeCommand("")).IsValid.Should().BeFalse();
        new ResendSignInCodeValidator().Validate(new ResendSignInCodeCommand(new string('a', 100))).IsValid.Should().BeFalse();
        new ResendSignInCodeCommand(ChallengeToken).ToString().Should().NotContain(ChallengeToken);
    }

    [Test]
    public void Outcome_NeverPrintsTheChallenge() =>
        LoginOutcome.TwoFactorRequired(ChallengeToken, NowUtc, TwoFactorMethod.Email, null).ToString()
            .Should().NotContain(ChallengeToken).And.Contain("Email");
}
