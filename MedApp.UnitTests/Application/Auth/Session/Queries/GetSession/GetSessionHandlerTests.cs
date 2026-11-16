using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Queries.GetSession;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Session.Queries.GetSession;

[TestFixture]
public sealed class GetSessionHandlerTests
{
    private Mock<ISessionService> _sessionService = null!;
    private GetSessionHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _sessionService = new Mock<ISessionService>();
        _handler = new GetSessionHandler(_sessionService.Object, new FixedTimeProvider(AuthTestConstants.SessionNowUtc));
    }

    [Test]
    public async Task Handle_ActiveSession_ReturnsItsExpiry()
    {
        var session = AuthTestConstants.CreateSession();
        using var cts = new CancellationTokenSource();
        _sessionService
            .Setup(s => s.GetActiveSessionAsync(AuthTestConstants.SessionId, AuthTestConstants.SessionNowUtc, cts.Token))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new GetSessionQuery(AuthTestConstants.SessionId), cts.Token);

        result.Should().Be(new SessionResponse(session.ExpiresAtUtc, 300, true));
    }

    [Test]
    public async Task Handle_SessionEnded_ThrowsNotFound()
    {
        _sessionService
            .Setup(s => s.GetActiveSessionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        var act = () => _handler.Handle(new GetSessionQuery(AuthTestConstants.SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_NeverExtendsTheSession()
    {
        _sessionService
            .Setup(s => s.GetActiveSessionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthTestConstants.CreateSession());

        await _handler.Handle(new GetSessionQuery(AuthTestConstants.SessionId), CancellationToken.None);

        _sessionService.Verify(
            s => s.ExtendSessionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
