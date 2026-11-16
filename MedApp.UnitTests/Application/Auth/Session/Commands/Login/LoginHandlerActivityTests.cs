using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Session.Commands.Login;

/// <summary>What signing in records in the activity log, whether it succeeds or not.</summary>
[TestFixture]
public sealed class LoginHandlerActivityTests
{
    private const string TriedUsername = "tried-name";

    private IFixture _fixture = null!;
    private Mock<IAuthenticationService> _authService = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _authService = new Mock<IAuthenticationService>();
        _activity = new RecordingActivityLog();
        _fixture.Inject(_authService.Object);
        _fixture.Inject(new Mock<ISessionService>().Object);
        _fixture.Inject<IActivityLog>(_activity);
        // An account without two-factor authentication, which signs in with its password alone.
        _fixture.Inject(new Mock<MedApp.Application.Common.Identity.ITwoFactorService>().Object);
    }

    private static LoginCommand Command() => new(TriedUsername, "Synthetic-Password-1", ApiTestConstants.IpAddress, ApiTestConstants.UserAgent);

    [Test]
    public async Task Handle_SignedIn_RecordsTheUserAndTheirAddress()
    {
        var userId = Guid.NewGuid();
        _authService.Setup(s => s.ValidateCredentialsAsync(TriedUsername, It.IsAny<string>()))
            .ReturnsAsync((userId, "Canonical-Name", (IEnumerable<string>)new[] { "User" }));
        using var cts = new CancellationTokenSource();

        await _fixture.Create<LoginHandler>().Handle(Command(), cts.Token);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.SignedIn);
        entry.UserId.Should().Be(userId);
        entry.Username.Should().Be("Canonical-Name");
        entry.IpAddress.Should().Be(ApiTestConstants.IpAddress);
        _activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task Handle_WrongCredentials_RecordsTheFailure_AndRethrows()
    {
        _authService.Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidCredentialsException());

        var act = () => _fixture.Create<LoginHandler>().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.SignInFailed);
        entry.UserId.Should().BeNull();
        entry.Username.Should().Be(TriedUsername);
        entry.IpAddress.Should().Be(ApiTestConstants.IpAddress);
        entry.Details.Should().Be("""{"reason":"InvalidCredentials"}""");
    }

    [Test]
    public async Task Handle_LockedOut_RecordsTheFailure_AndRethrows()
    {
        _authService.Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new AccountLockedOutException(3));

        var act = () => _fixture.Create<LoginHandler>().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedOutException>();
        _activity.Recorded.Should().ContainSingle().Which.Details.Should().Be("""{"reason":"LockedOut"}""");
    }
}
