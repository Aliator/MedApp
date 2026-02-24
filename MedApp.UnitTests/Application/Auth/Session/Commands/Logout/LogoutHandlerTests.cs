using MedApp.Application.Authentication.Sessions.Commands.Logout;
using MedApp.Application.Common.Authentication;
using MedApp.UnitTests.Common.Constants;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Session.Commands.Logout;

[TestFixture]
public sealed class LogoutHandlerTests
{
    private Mock<ISessionService> _sessionService = null!;
    private LogoutHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _sessionService = new Mock<ISessionService>();
        _handler = new LogoutHandler(_sessionService.Object);
    }

    [Test]
    public async Task Handle_SessionIdIsNull_DoesNotRevokeSession()
    {
        var command = new LogoutCommand(null);

        await _handler.Handle(command, CancellationToken.None);

        _sessionService.Verify(
            s => s.RevokeSessionAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_SessionIdProvided_RevokesSession()
    {
        var command = new LogoutCommand(AuthTestConstants.SessionId);

        await _handler.Handle(command, CancellationToken.None);

        _sessionService.Verify(
            s => s.RevokeSessionAsync(
                AuthTestConstants.SessionId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        var command = new LogoutCommand(AuthTestConstants.SessionId);

        await _handler.Handle(command, cts.Token);

        _sessionService.Verify(
            s => s.RevokeSessionAsync(
                AuthTestConstants.SessionId,
                cts.Token),
            Times.Once);
    }
}