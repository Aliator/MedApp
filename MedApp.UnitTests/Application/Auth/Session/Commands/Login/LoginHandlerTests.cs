using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Session.Commands.Login;

[TestFixture]
public sealed class LoginHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IAuthenticationService> _authService = null!;
    private Mock<ISessionService> _sessionService = null!;
    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<ISignInChallengeRepository> _challenges = null!;
    private LoginHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _authService = new Mock<IAuthenticationService>();
        _sessionService = new Mock<ISessionService>();

        _twoFactor = new Mock<ITwoFactorService>();
        _challenges = new Mock<ISignInChallengeRepository>();

        _fixture.Inject(_authService.Object);
        _fixture.Inject(_sessionService.Object);
        _fixture.Inject(_twoFactor.Object);
        _fixture.Inject(_challenges.Object);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(EmailTestConstants.NowUtc));

        _handler = _fixture.Create<LoginHandler>();
    }

    [Test]
    public async Task Handle_ValidCredentials_ReturnsCreatedSession()
    {
        var userId = Guid.NewGuid();
        var expectedSession = new LoginResult(
            AuthTestConstants.SessionId,
            AuthTestConstants.SessionExpiresAtUtc);;

        _authService
            .Setup(s => s.ValidateCredentialsAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Passwords[0]))
            .ReturnsAsync((userId, AuthTestConstants.Usernames[0], AuthTestConstants.Roles));

        _sessionService
            .Setup(s => s.CreateSessionAsync(
                userId,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);

        var command = new LoginCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            null,
            null);

        var result = await _handler.Handle(
            command,
            CancellationToken.None);

        result.Session.Should().Be(expectedSession);
        result.NeedsTwoFactor.Should().BeFalse();
    }

    [Test]
    public async Task Handle_AccountLockedOut_ThrowsAndCreatesNoSession()
    {
        _authService
            .Setup(s => s.ValidateCredentialsAsync(AuthTestConstants.Usernames[0], AuthTestConstants.Passwords[0]))
            .ThrowsAsync(new AccountLockedOutException(5));

        var act = () => _handler.Handle(
            new LoginCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Passwords[0], null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedOutException>();
        _sessionService.Verify(
            s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_UsesValuesReturnedFromAuthenticationService()
    {
        var userId = Guid.NewGuid();

        _authService
            .Setup(s => s.ValidateCredentialsAsync(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((userId, AuthTestConstants.Usernames[0], AuthTestConstants.Roles));

        _sessionService
            .Setup(s => s.CreateSessionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(
                AuthTestConstants.SessionId,
                AuthTestConstants.SessionExpiresAtUtc));

        var command = new LoginCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            "127.0.0.1",
            "agent");

        await _handler.Handle(command, CancellationToken.None);

        _sessionService.Verify(
            s => s.CreateSessionAsync(
                userId,
            "127.0.0.1",
            "agent",
            It.IsAny<CancellationToken>()),
        Times.Once);
    }

    [Test]
    public async Task Handle_ValidatesCredentials_Once()
    {
        _authService
            .Setup(s => s.ValidateCredentialsAsync(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((Guid.NewGuid(), AuthTestConstants.Usernames[0], Array.Empty<string>()));
        
        _sessionService
            .Setup(s => s.CreateSessionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(
                AuthTestConstants.SessionId,
                AuthTestConstants.SessionExpiresAtUtc));

        var command = new LoginCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            null,
            null);

        await _handler.Handle(command, CancellationToken.None);

        _authService.Verify(
            s => s.ValidateCredentialsAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Passwords[0]),
            Times.Once);
    }

    [Test]
    public async Task Handle_TwoFactorAccount_ReturnsAChallengeAndNoSession()
    {
        var userId = Guid.NewGuid();
        SignInChallenge? saved = null;
        _authService
            .Setup(s => s.ValidateCredentialsAsync(AuthTestConstants.Usernames[0], AuthTestConstants.Passwords[0]))
            .ReturnsAsync((userId, AuthTestConstants.Usernames[0], AuthTestConstants.Roles));
        _twoFactor.Setup(t => t.IsEnabledAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _challenges
            .Setup(c => c.AddAsync(It.IsAny<SignInChallenge>(), EmailTestConstants.NowUtc, It.IsAny<CancellationToken>()))
            .Callback<SignInChallenge, DateTime, CancellationToken>((challenge, _, _) => saved = challenge)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new LoginCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Passwords[0], null, null),
            CancellationToken.None);

        result.NeedsTwoFactor.Should().BeTrue();
        result.Session.Should().BeNull();
        result.ChallengeExpiresAtUtc.Should().Be(EmailTestConstants.NowUtc.Add(SignInChallenge.Lifetime));
        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(userId);
        // Only the hash is stored, never the token the browser gets.
        saved.TokenHash.Should().Be(SignInChallengeTokens.Hash(result.Challenge!));
        saved.TokenHash.Should().NotBe(result.Challenge);
        _sessionService.Verify(
            s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_TwoFactorAccount_EachSignInGetsADifferentChallenge()
    {
        var userId = Guid.NewGuid();
        _authService
            .Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((userId, AuthTestConstants.Usernames[0], AuthTestConstants.Roles));
        _twoFactor.Setup(t => t.IsEnabledAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var command = new LoginCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Passwords[0], null, null);

        var first = await _handler.Handle(command, CancellationToken.None);
        var second = await _handler.Handle(command, CancellationToken.None);

        first.Challenge.Should().NotBe(second.Challenge);
    }
}
