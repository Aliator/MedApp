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

/// <summary>
/// A refused sign-in is recorded against the account tried when it exists, so filtering the log by that user finds it,
/// and against no account when the username is unknown.
/// </summary>
[TestFixture]
public sealed class LoginHandlerRefusedAccountTests
{
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

    private static LoginCommand Command() => new("tried-name", "Synthetic-Password-1", ApiTestConstants.IpAddress, ApiTestConstants.UserAgent);

    private static IEnumerable<TestCaseData> Refusals()
    {
        var userId = Guid.Parse("5f1c2a8e-0000-4000-8000-000000000042");
        yield return new TestCaseData(new InvalidCredentialsException(userId), userId, "InvalidCredentials").SetName("WrongPassword_RecordsTheAccount");
        yield return new TestCaseData(new AccountLockedOutException(5, userId), userId, "LockedOut").SetName("LockedAccount_RecordsTheAccount");
        yield return new TestCaseData(new InvalidCredentialsException(), null, "InvalidCredentials").SetName("UnknownUsername_RecordsNoAccount");
    }

    [TestCaseSource(nameof(Refusals))]
    public async Task Handle_Refused(Exception refusal, Guid? userId, string reason)
    {
        _authService.Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(refusal);

        var act = () => _fixture.Create<LoginHandler>().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.SignInFailed);
        entry.UserId.Should().Be(userId);
        entry.Username.Should().Be("tried-name");
        entry.Details.Should().Contain(reason);
    }

    [Test]
    public void Exceptions_WithoutAnAccount_HaveNoUserId()
    {
        new InvalidCredentialsException().UserId.Should().BeNull();
        new AccountLockedOutException(1).UserId.Should().BeNull();
        new InvalidCredentialsException().Message.Should().Be("Invalid credentials.");
    }
}
