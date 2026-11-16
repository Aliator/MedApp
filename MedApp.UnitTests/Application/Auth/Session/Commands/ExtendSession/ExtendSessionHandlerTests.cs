using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Commands.ExtendSession;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Session.Commands.ExtendSession;

[TestFixture]
public sealed class ExtendSessionHandlerTests
{
    private Mock<ISessionService> _sessionService = null!;
    private ExtendSessionHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _sessionService = new Mock<ISessionService>();
        _handler = new ExtendSessionHandler(_sessionService.Object, new FixedTimeProvider(AuthTestConstants.SessionNowUtc));
    }

    [Test]
    public async Task Handle_ActiveSession_ExtendsItFromNowAndReturnsTheNewExpiry()
    {
        var session = AuthTestConstants.CreateSession();
        using var cts = new CancellationTokenSource();
        _sessionService
            .Setup(s => s.ExtendSessionAsync(AuthTestConstants.SessionId, AuthTestConstants.SessionNowUtc, cts.Token))
            .ReturnsAsync(() =>
            {
                session.Extend(AuthTestConstants.SessionNowUtc);
                return session;
            });

        var result = await _handler.Handle(new ExtendSessionCommand(AuthTestConstants.SessionId), cts.Token);

        result.Should().Be(new SessionResponse(AuthTestConstants.SessionNowUtc.AddHours(8), 8 * 60 * 60, true));
    }

    [Test]
    public async Task Handle_SessionEnded_ThrowsNotFound()
    {
        _sessionService
            .Setup(s => s.ExtendSessionAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        var act = () => _handler.Handle(new ExtendSessionCommand(AuthTestConstants.SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
