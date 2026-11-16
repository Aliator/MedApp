using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.AuthTestConstants;

namespace MedApp.UnitTests.Application.Auth.Session.Commands;

/// <summary>
/// The account changes that end a user's sessions: changing their own password (all but the session it came from), an
/// admin's reset (all), and signing out everywhere. A change of roles ends none: roles are read on every request. The
/// password change and the reset end them in the same transaction as the change.
/// </summary>
[TestFixture]
public sealed class EndingSessionsTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _users = null!;
    private Mock<IIdentityReadService> _read = null!;
    private Mock<ISessionService> _sessions = null!;
    private CancellationTokenSource _cts = null!;
    private PassThroughIdentityTransactions _transactions = null!;

    private string Target => Usernames[1];

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _users = new Mock<IIdentityUserService>();
        _read = new Mock<IIdentityReadService>();
        _sessions = new Mock<ISessionService>();
        _fixture.Inject(_users.Object);
        _fixture.Inject(_read.Object);
        _fixture.Inject(_sessions.Object);
        _cts = new CancellationTokenSource();
        _transactions = new PassThroughIdentityTransactions();
        _fixture.Inject<IIdentityTransactions>(_transactions);

        _read.Setup(r => r.GetUserAsync(Target, It.IsAny<CancellationToken>())).ReturnsAsync(SessionUser("User"));
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private void VerifyEnded(Guid? except) =>
        _sessions.Verify(s => s.RevokeUserSessionsAsync(SessionUserId, except, _cts.Token), Times.Once);

    private void VerifyNoneEnded() =>
        _sessions.Verify(
            s => s.RevokeUserSessionsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Test]
    public async Task UpdateUserPassword_Changed_EndsTheirOtherSessionsAndKeepsThisOne()
    {
        _users.Setup(u => u.UpdateUserPasswordAsync(Target, Passwords[0], Passwords[1], _cts.Token)).ReturnsAsync(IdentityResult.Success);

        var result = await _fixture.Create<UpdateUserPasswordHandler>()
            .Handle(new UpdateUserPasswordCommand(Target, Passwords[0], Passwords[1], SessionId), _cts.Token);

        result.Succeeded.Should().BeTrue();
        VerifyEnded(except: SessionId);
        _transactions.Runs.Should().Be(1);
        _transactions.LastToken.Should().Be(_cts.Token);
    }

    [Test]
    public async Task UpdateUserPassword_EndingSessionsFails_FailsTheWholeTransaction()
    {
        _users.Setup(u => u.UpdateUserPasswordAsync(Target, Passwords[0], Passwords[1], _cts.Token)).ReturnsAsync(IdentityResult.Success);
        _sessions.Setup(s => s.RevokeUserSessionsAsync(SessionUserId, SessionId, _cts.Token)).ThrowsAsync(new TimeoutException());

        var act = () => _fixture.Create<UpdateUserPasswordHandler>()
            .Handle(new UpdateUserPasswordCommand(Target, Passwords[0], Passwords[1], SessionId), _cts.Token);

        // Thrown inside the transaction's work, so IdentityTransactions rolls the password change back with it.
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Test]
    public async Task UpdateUserPassword_Refused_EndsNothing()
    {
        _users.Setup(u => u.UpdateUserPasswordAsync(Target, Passwords[0], Passwords[1], _cts.Token))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.OldPasswordIncorrect));

        var result = await _fixture.Create<UpdateUserPasswordHandler>()
            .Handle(new UpdateUserPasswordCommand(Target, Passwords[0], Passwords[1], SessionId), _cts.Token);

        result.Succeeded.Should().BeFalse();
        VerifyNoneEnded();
    }

    [Test]
    public async Task ResetUserPassword_Reset_EndsEverySessionTheyHave()
    {
        _users.Setup(u => u.ResetUserPasswordAsync(Target, _cts.Token)).ReturnsAsync(new ResetUserPasswordResponse(Passwords[1], []));

        var result = await _fixture.Create<ResetUserPasswordHandler>().Handle(new ResetUserPasswordCommand(Target), _cts.Token);

        result.GeneratedPassword.Should().Be(Passwords[1]);
        VerifyEnded(except: null);
        _transactions.Runs.Should().Be(1);
    }

    [Test]
    public async Task ResetUserPassword_Refused_EndsNothing()
    {
        _users.Setup(u => u.ResetUserPasswordAsync(Target, _cts.Token))
            .ReturnsAsync(new ResetUserPasswordResponse(null, [IdentityErrors.UserNotFound]));

        var result = await _fixture.Create<ResetUserPasswordHandler>().Handle(new ResetUserPasswordCommand(Target), _cts.Token);

        result.GeneratedPassword.Should().BeNull();
        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.UserNotFoundCode);
        VerifyNoneEnded();
    }

    [Test]
    public async Task SignOutEverywhere_UserFound_EndsEverySessionTheyHave()
    {
        var found = await _fixture.Create<SignOutEverywhereHandler>().Handle(new SignOutEverywhereCommand(Target), _cts.Token);

        found.Should().BeTrue();
        VerifyEnded(except: null);
    }

    [Test]
    public async Task SignOutEverywhere_NoSuchUser_ReturnsFalseAndEndsNothing()
    {
        _read.Setup(r => r.GetUserAsync(Target, It.IsAny<CancellationToken>())).ReturnsAsync((UserResponse?)null);

        var found = await _fixture.Create<SignOutEverywhereHandler>().Handle(new SignOutEverywhereCommand(Target), _cts.Token);

        found.Should().BeFalse();
        VerifyNoneEnded();
    }

    [Test]
    public void SignOutEverywhereValidator_Username_IsRequired()
    {
        var validator = new SignOutEverywhereValidator();

        validator.Validate(new SignOutEverywhereCommand(string.Empty)).IsValid.Should().BeFalse();
        validator.Validate(new SignOutEverywhereCommand(Target)).IsValid.Should().BeTrue();
    }
}
