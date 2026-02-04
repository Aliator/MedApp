using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Commands.Login;
using MedApp.Application.Common.Authentication;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Commands.Login;

[TestFixture]
public sealed class LoginHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IAuthenticationService> _authService = null!;
    private Mock<ISessionService> _sessionService = null!;
    private LoginHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _authService = new Mock<IAuthenticationService>();
        _sessionService = new Mock<ISessionService>();

        _fixture.Inject(_authService.Object);
        _fixture.Inject(_sessionService.Object);

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

        result.Should().Be(expectedSession);
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
}
