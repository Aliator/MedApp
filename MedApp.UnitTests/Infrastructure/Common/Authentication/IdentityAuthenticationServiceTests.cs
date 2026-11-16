using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Infrastructure.Common.Authentication;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Authentication;

[TestFixture]
public sealed class IdentityAuthenticationServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private IdentityAuthenticationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new IdentityAuthenticationService(_userManager.Object, new FixedTimeProvider(Now));
    }

    /// <summary>A user the user manager finds, not locked out, whose password is (or isn't) the one given.</summary>
    private ApplicationUser FoundUser(bool isPasswordRight)
    {
        var user = AuthTestConstants.CreateValidUser();
        user.Id = Guid.NewGuid();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManager.Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0])).ReturnsAsync(isPasswordRight);
        _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(AuthTestConstants.Roles.ToList());
        return user;
    }

    [Test]
    public async Task ValidateCredentialsAsync_AccountLockedOut_ThrowsWithMinutesLeftEvenForTheRightPassword()
    {
        var user = FoundUser(isPasswordRight: true);
        _userManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(true);
        _userManager.Setup(u => u.GetLockoutEndDateAsync(user)).ReturnsAsync(new DateTimeOffset(Now.AddMinutes(4).AddSeconds(10)));

        var act = async () => await _service.ValidateCredentialsAsync(user.UserName!, AuthTestConstants.Passwords[0]);

        var thrownLocked = (await act.Should().ThrowAsync<AccountLockedOutException>()).Which;
        thrownLocked.MinutesLeft.Should().Be(5);
        thrownLocked.UserId.Should().Be(user.Id, "the activity log records which account was tried");
        _userManager.Verify(u => u.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        _userManager.Verify(u => u.AccessFailedAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task ValidateCredentialsAsync_LockedOutWithNoLockoutEnd_SaysTryAgainInOneMinute()
    {
        var user = FoundUser(isPasswordRight: true);
        _userManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(true);
        _userManager.Setup(u => u.GetLockoutEndDateAsync(user)).ReturnsAsync((DateTimeOffset?)null);

        var act = async () => await _service.ValidateCredentialsAsync(user.UserName!, AuthTestConstants.Passwords[0]);

        var thrown = (await act.Should().ThrowAsync<AccountLockedOutException>()).Which;
        thrown.MinutesLeft.Should().Be(1);
        thrown.Message.Should().Be("This account is locked after too many wrong passwords. Try again in 1 minute.");
    }

    [Test]
    public async Task ValidateCredentialsAsync_WrongPassword_CountsTheFailure()
    {
        var user = FoundUser(isPasswordRight: false);

        var act = async () => await _service.ValidateCredentialsAsync(user.UserName!, AuthTestConstants.Passwords[0]);

        (await act.Should().ThrowAsync<InvalidCredentialsException>()).Which.UserId.Should().Be(user.Id);
        _userManager.Verify(u => u.AccessFailedAsync(user), Times.Once);
        _userManager.Verify(u => u.ResetAccessFailedCountAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task ValidateCredentialsAsync_WrongPasswordReachesTheLimit_ThrowsAccountLockedOut()
    {
        var user = FoundUser(isPasswordRight: false);
        // Not locked when it starts; locked once this failure is counted.
        _userManager.SetupSequence(u => u.IsLockedOutAsync(user)).ReturnsAsync(false).ReturnsAsync(true);
        _userManager.Setup(u => u.GetLockoutEndDateAsync(user)).ReturnsAsync(new DateTimeOffset(Now.AddMinutes(5)));

        var act = async () => await _service.ValidateCredentialsAsync(user.UserName!, AuthTestConstants.Passwords[0]);

        var thrown = (await act.Should().ThrowAsync<AccountLockedOutException>()).Which;
        thrown.Message.Should().Be("This account is locked after too many wrong passwords. Try again in 5 minutes.");
        thrown.UserId.Should().Be(user.Id);
        _userManager.Verify(u => u.AccessFailedAsync(user), Times.Once);
    }

    [Test]
    public async Task ValidateCredentialsAsync_RightPassword_ClearsTheFailureCount()
    {
        var user = FoundUser(isPasswordRight: true);

        await _service.ValidateCredentialsAsync(user.UserName!, AuthTestConstants.Passwords[0]);

        _userManager.Verify(u => u.ResetAccessFailedCountAsync(user), Times.Once);
        _userManager.Verify(u => u.AccessFailedAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task ValidateCredentialsAsync_UserDoesNotExist_ThrowsInvalidCredentialsException()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var act = async () =>
            await _service.ValidateCredentialsAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Passwords[0]);

        (await act.Should().ThrowAsync<InvalidCredentialsException>()).Which.UserId.Should().BeNull();
    }

    [Test]
    public async Task ValidateCredentialsAsync_InvalidPassword_ThrowsInvalidCredentialsException()
    {
        var user = AuthTestConstants.CreateValidUser();
        user.Id = Guid.NewGuid();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0]))
            .ReturnsAsync(false);

        var act = async () =>
            await _service.ValidateCredentialsAsync(
                user.UserName!,
                AuthTestConstants.Passwords[0]);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Test]
    public async Task ValidateCredentialsAsync_ValidCredentials_ReturnsUserIdUsernameAndRoles()
    {
        var user = AuthTestConstants.CreateValidUser();
        user.Id = Guid.NewGuid();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0]))
            .ReturnsAsync(true);

        _userManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(AuthTestConstants.Roles.ToList());

        var result = await _service.ValidateCredentialsAsync(
            user.UserName!,
            AuthTestConstants.Passwords[0]);

        result.Item1.Should().Be(user.Id);
        result.Item2.Should().Be(user.UserName);
        result.Item3.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }
}
