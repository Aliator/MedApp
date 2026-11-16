using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.Session.Commands.LoginTwoFactor;

[TestFixture]
public sealed class LoginTwoFactorHandlerTests
{
    private Mock<ISignInChallengeRepository> _challenges = null!;
    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<ISessionService> _sessions = null!;
    private Mock<ISecurityNotices> _notices = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private FixedTimeProvider _clock = null!;
    private LoginTwoFactorHandler _handler = null!;
    private SignInChallenge _challenge = null!;
    private readonly LoginResult _session = new(SessionId, NowUtc.AddHours(8));

    [SetUp]
    public void SetUp()
    {
        _challenges = new Mock<ISignInChallengeRepository>();
        _twoFactor = new Mock<ITwoFactorService>();
        _sessions = new Mock<ISessionService>();
        _notices = new Mock<ISecurityNotices>();
        _events = new Mock<ISecurityEventLog>();
        _clock = new FixedTimeProvider(NowUtc);
        _handler = new LoginTwoFactorHandler(
            _challenges.Object, _twoFactor.Object, _sessions.Object, _notices.Object, _events.Object, _clock);

        _challenge = new SignInChallenge(UserId, SignInChallengeTokens.Hash(ChallengeToken), NowUtc.AddMinutes(-1));
        _challenges
            .Setup(c => c.FindByTokenHashAsync(SignInChallengeTokens.Hash(ChallengeToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_challenge);
        _challenges.Setup(c => c.ConsumeAsync(_challenge.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sessions
            .Setup(s => s.CreateSessionAsync(UserId, "192.0.2.1", "Browser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_session);
    }

    private static LoginTwoFactorCommand WithCode(string code = AuthenticatorCode) =>
        new(ChallengeToken, code, null, "192.0.2.1", "Browser");

    private static LoginTwoFactorCommand WithRecoveryCode(string code = RecoveryCode) =>
        new(ChallengeToken, null, code, "192.0.2.1", "Browser");

    [Test]
    public async Task Handle_RightCode_UsesUpTheChallengeAndStartsTheSession()
    {
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.Handle(WithCode(), CancellationToken.None);

        result.Should().Be(_session);
        _challenges.Verify(c => c.ConsumeAsync(_challenge.Id, It.IsAny<CancellationToken>()), Times.Once);
        _twoFactor.Verify(t => t.RecordSignInAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_UnknownChallenge_ThrowsSignInExpired()
    {
        _challenges.Setup(c => c.FindByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((SignInChallenge?)null);

        var act = () => _handler.Handle(WithCode(), CancellationToken.None);

        await act.Should().ThrowAsync<SignInExpiredException>();
        _sessions.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_ExpiredChallenge_ThrowsSignInExpiredWithoutCheckingTheCode()
    {
        var expired = new SignInChallenge(UserId, SignInChallengeTokens.Hash(ChallengeToken), NowUtc.Add(-SignInChallenge.Lifetime));
        _challenges.Setup(c => c.FindByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expired);

        var act = () => _handler.Handle(WithCode(), CancellationToken.None);

        await act.Should().ThrowAsync<SignInExpiredException>();
        _twoFactor.Verify(t => t.VerifyAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ChallengeUsedByAnotherRequestFirst_ThrowsSignInExpiredAndStartsNoSession()
    {
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _challenges.Setup(c => c.ConsumeAsync(_challenge.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => _handler.Handle(WithCode(), CancellationToken.None);

        await act.Should().ThrowAsync<SignInExpiredException>();
        _sessions.Verify(s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_LockedAccount_UsesUpTheChallengeAndThrowsLockedOut()
    {
        _twoFactor.Setup(t => t.LockoutMinutesLeftAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        var act = () => _handler.Handle(WithCode(), CancellationToken.None);

        (await act.Should().ThrowAsync<AccountLockedOutException>()).Which.MinutesLeft.Should().Be(4);
        _challenges.Verify(c => c.ConsumeAsync(_challenge.Id, It.IsAny<CancellationToken>()), Times.Once);
        _twoFactor.Verify(t => t.VerifyAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WrongCode_CountsAgainstTheChallengeAndTheLockout()
    {
        var act = () => _handler.Handle(WithCode(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTwoFactorCodeException>();
        _challenges.Verify(c => c.RecordFailureAsync(_challenge.Id, It.IsAny<CancellationToken>()), Times.Once);
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _challenges.Verify(c => c.ConsumeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.TwoFactorCodeRejected && ev.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
        _sessions.Verify(s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WrongCodeThatReachesTheLockout_UsesUpTheChallengeAndThrowsLockedOut()
    {
        _twoFactor.Setup(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = () => _handler.Handle(WithCode(), CancellationToken.None);

        (await act.Should().ThrowAsync<AccountLockedOutException>()).Which.MinutesLeft.Should().Be(5);
        _challenges.Verify(c => c.ConsumeAsync(_challenge.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_RecoveryCode_SignsInRecordsItAndTellsTheOwner()
    {
        _twoFactor
            .Setup(t => t.RedeemRecoveryCodeAsync(UserId, RecoveryCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodeRedemption(true, 9));

        var result = await _handler.Handle(WithRecoveryCode(), CancellationToken.None);

        result.Should().Be(_session);
        _twoFactor.Verify(t => t.VerifyAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.RecoveryCodeUsed && (int)ev.Details!["codesLeft"]! == 9),
            It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.RecoveryCodeUsed, It.IsAny<CancellationToken>(), null, 9), Times.Once);
    }

    [Test]
    public async Task Handle_WrongRecoveryCode_IsRejected()
    {
        _twoFactor
            .Setup(t => t.RedeemRecoveryCodeAsync(UserId, RecoveryCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodeRedemption(false, 10));

        var act = () => _handler.Handle(WithRecoveryCode(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTwoFactorCodeException>();
        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_PassesTheCancellationTokenOn()
    {
        using var cts = new CancellationTokenSource();
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, cts.Token)).ReturnsAsync(true);
        _sessions.Setup(s => s.CreateSessionAsync(UserId, "192.0.2.1", "Browser", cts.Token)).ReturnsAsync(_session);

        await _handler.Handle(WithCode(), cts.Token);

        _challenges.Verify(c => c.FindByTokenHashAsync(It.IsAny<string>(), cts.Token), Times.Once);
        _challenges.Verify(c => c.ConsumeAsync(_challenge.Id, cts.Token), Times.Once);
        _sessions.Verify(s => s.CreateSessionAsync(UserId, "192.0.2.1", "Browser", cts.Token), Times.Once);
    }
}
