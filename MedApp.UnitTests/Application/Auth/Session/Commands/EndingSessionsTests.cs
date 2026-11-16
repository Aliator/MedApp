using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
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
/// The account changes that end a user's sessions: changing their own password (all but the session it came from),
/// an admin's reset (all), a change of roles (all but the one making it, if it's theirs), and signing out everywhere.
/// </summary>
[TestFixture]
public sealed class EndingSessionsTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _users = null!;
    private Mock<IIdentityRoleService> _roles = null!;
    private Mock<IIdentityReadService> _read = null!;
    private Mock<ISessionService> _sessions = null!;
    private CancellationTokenSource _cts = null!;

    private string Target => Usernames[1];

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _users = new Mock<IIdentityUserService>();
        _roles = new Mock<IIdentityRoleService>();
        _read = new Mock<IIdentityReadService>();
        _sessions = new Mock<ISessionService>();
        _fixture.Inject(_users.Object);
        _fixture.Inject(_roles.Object);
        _fixture.Inject(_read.Object);
        _fixture.Inject(_sessions.Object);
        _cts = new CancellationTokenSource();

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
    }

    [Test]
    public async Task ResetUserPassword_Refused_EndsNothing()
    {
        _users.Setup(u => u.ResetUserPasswordAsync(Target, _cts.Token))
            .ReturnsAsync(new ResetUserPasswordResponse(null, [IdentityErrors.UserNotFound]));

        await _fixture.Create<ResetUserPasswordHandler>().Handle(new ResetUserPasswordCommand(Target), _cts.Token);

        VerifyNoneEnded();
    }

    [Test]
    public async Task SetUserRoles_RolesChanged_EndsEverySessionTheyHave()
    {
        _roles.Setup(r => r.SetRolesAsync(Target, It.IsAny<IReadOnlyCollection<string>>(), _cts.Token)).ReturnsAsync(IdentityResult.Success);

        var result = await _fixture.Create<SetUserRolesHandler>()
            .Handle(new SetUserRolesCommand(Target, ["Admin", "User"], Usernames[0]), _cts.Token);

        result.Succeeded.Should().BeTrue();
        VerifyEnded(except: null);
    }

    [Test]
    public async Task SetUserRoles_TheRolesTheyHad_EndsNothing()
    {
        _roles.Setup(r => r.SetRolesAsync(Target, It.IsAny<IReadOnlyCollection<string>>(), _cts.Token)).ReturnsAsync(IdentityResult.Success);

        // The same role, in another case: Identity matches role names ignoring case.
        await _fixture.Create<SetUserRolesHandler>().Handle(new SetUserRolesCommand(Target, ["user"], Usernames[0]), _cts.Token);

        VerifyNoneEnded();
    }

    [Test]
    public async Task SetUserRoles_Refused_EndsNothing()
    {
        _roles.Setup(r => r.SetRolesAsync(Target, It.IsAny<IReadOnlyCollection<string>>(), _cts.Token))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.RoleNotFound));

        await _fixture.Create<SetUserRolesHandler>().Handle(new SetUserRolesCommand(Target, ["Auditor"], Usernames[0]), _cts.Token);

        VerifyNoneEnded();
    }

    [Test]
    public async Task SetUserRoles_NoSuchUser_EndsNothing()
    {
        _read.Setup(r => r.GetUserAsync(Target, It.IsAny<CancellationToken>())).ReturnsAsync((UserResponse?)null);
        _roles.Setup(r => r.SetRolesAsync(Target, It.IsAny<IReadOnlyCollection<string>>(), _cts.Token))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.UserNotFound));

        await _fixture.Create<SetUserRolesHandler>().Handle(new SetUserRolesCommand(Target, ["Admin"], Usernames[0]), _cts.Token);

        VerifyNoneEnded();
    }

    [Test]
    public async Task AssignRole_Assigned_EndsTheirSessionsButTheOneMakingTheChange()
    {
        _roles.Setup(r => r.AssignRoleAsync(Target, "Admin", _cts.Token)).ReturnsAsync(new IdentityRole<Guid>("Admin"));

        var role = await _fixture.Create<AssignRoleHandler>().Handle(new AssignRoleCommand(Target, "Admin", SessionId), _cts.Token);

        role.Should().NotBeNull();
        VerifyEnded(except: SessionId);
    }

    [Test]
    public async Task AssignRole_NothingChanged_EndsNothing()
    {
        await _fixture.Create<AssignRoleHandler>().Handle(new AssignRoleCommand(Target, "Admin", SessionId), _cts.Token);

        VerifyNoneEnded();
    }

    [Test]
    public async Task RevokeRole_Revoked_EndsTheirSessionsButTheOneMakingTheChange()
    {
        _roles.Setup(r => r.RevokeRoleAsync(Target, "Admin", _cts.Token)).ReturnsAsync(new IdentityRole<Guid>("Admin"));

        var role = await _fixture.Create<RevokeRoleHandler>().Handle(new RevokeRoleCommand(Target, "Admin", SessionId), _cts.Token);

        role.Should().NotBeNull();
        VerifyEnded(except: SessionId);
    }

    [Test]
    public async Task RevokeRole_NothingChanged_EndsNothing()
    {
        await _fixture.Create<RevokeRoleHandler>().Handle(new RevokeRoleCommand(Target, "Admin", SessionId), _cts.Token);

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
