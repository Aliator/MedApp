using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class IdentityUserServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private IdentityOptions _options = null!;
    private IdentityUserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _options = new IdentityOptions();
        _options.Password.RequiredLength = 8;
        _options.Password.RequireDigit = true;
        _options.Password.RequireLowercase = true;
        _options.Password.RequireUppercase = true;
        _options.Password.RequireNonAlphanumeric = false;

        // The password rules a PasswordValidator checks come from the manager's options.
        _userManager.Object.Options = _options;

        _service = new IdentityUserService(_userManager.Object, Options.Create(_options));
    }

    
    [Test]
    public async Task UpdateUserPasswordAsync_Succeeds_ReturnsSuccess()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0]))
            .ReturnsAsync(true);

        _userManager
            .Setup(u => u.ChangePasswordAsync(
                user,
                AuthTestConstants.Passwords[0],
                AuthTestConstants.Passwords[1]))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.UpdateUserPasswordAsync(
            user.UserName!,
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1],
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task UpdateUserPasswordAsync_Succeeds_ClearsMustChangePasswordInTheSameSave()
    {
        var user = AuthTestConstants.CreateValidUser();
        user.MustChangePassword = true;
        bool? flagWhenSaved = null;

        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0])).ReturnsAsync(true);
        _userManager
            .Setup(u => u.ChangePasswordAsync(user, AuthTestConstants.Passwords[0], AuthTestConstants.Passwords[1]))
            .Callback(() => flagWhenSaved = user.MustChangePassword)
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.UpdateUserPasswordAsync(
            user.UserName!, AuthTestConstants.Passwords[0], AuthTestConstants.Passwords[1], CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        // ChangePasswordAsync saves the user with its new hash, so the flag must already be cleared when it's called.
        flagWhenSaved.Should().BeFalse();
        user.MustChangePassword.Should().BeFalse();
    }

    [Test]
    public async Task UpdateUserPasswordAsync_ChangeFails_KeepsMustChangePassword()
    {
        var user = AuthTestConstants.CreateValidUser();
        user.MustChangePassword = true;

        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0])).ReturnsAsync(true);
        _userManager
            .Setup(u => u.ChangePasswordAsync(user, AuthTestConstants.Passwords[0], AuthTestConstants.Passwords[1]))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));

        var result = await _service.UpdateUserPasswordAsync(
            user.UserName!, AuthTestConstants.Passwords[0], AuthTestConstants.Passwords[1], CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        user.MustChangePassword.Should().BeTrue();
    }

    [Test]
    public async Task UpdateUserPasswordAsync_OldPasswordIncorrect_KeepsMustChangePassword()
    {
        var user = AuthTestConstants.CreateValidUser();
        user.MustChangePassword = true;

        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0])).ReturnsAsync(false);

        await _service.UpdateUserPasswordAsync(
            user.UserName!, AuthTestConstants.Passwords[0], AuthTestConstants.Passwords[1], CancellationToken.None);

        user.MustChangePassword.Should().BeTrue();
        _userManager.Verify(
            u => u.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task ResetUserPasswordAsync_Succeeds_SetsMustChangePasswordInTheSameSave()
    {
        var user = AuthTestConstants.CreateValidUser();
        bool? flagWhenSaved = null;

        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");
        _userManager
            .Setup(u => u.ResetPasswordAsync(user, "token", It.IsAny<string>()))
            .Callback(() => flagWhenSaved = user.MustChangePassword)
            .ReturnsAsync(IdentityResult.Success);

        await _service.ResetUserPasswordAsync(user.UserName!, CancellationToken.None);

        // ResetPasswordAsync saves the user with its new hash, so the flag must already be set when it's called.
        flagWhenSaved.Should().BeTrue();
        user.MustChangePassword.Should().BeTrue();
    }

    [Test]
    public async Task ResetUserPasswordAsync_ResetFails_LeavesMustChangePasswordAsItWas()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");
        _userManager
            .Setup(u => u.ResetPasswordAsync(user, "token", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        await _service.ResetUserPasswordAsync(user.UserName!, CancellationToken.None);

        user.MustChangePassword.Should().BeFalse();
    }

    [Test]
    public async Task ResetUserPasswordAsync_UserNotFound_ReturnsError()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.ResetUserPasswordAsync(
            AuthTestConstants.Usernames[0],
            CancellationToken.None);

        result.GeneratedPassword.Should().BeNull();
        result.Errors.Should().ContainSingle(e =>
            e.Code == IdentityErrors.UserNotFoundCode);
    }

    [Test]
    public async Task ResetUserPasswordAsync_ResetFails_ReturnsErrors()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("token");

        _userManager
            .Setup(u => u.ResetPasswordAsync(
                user,
                "token",
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(
                IdentityErrors.OldPasswordIncorrect));

        var result = await _service.ResetUserPasswordAsync(
            user.UserName!,
            CancellationToken.None);

        result.GeneratedPassword.Should().BeNull();
        result.Errors.Should().ContainSingle(e =>
            e.Code == IdentityErrors.OldPasswordIncorrectCode);
    }

    [Test]
    public async Task ResetUserPasswordAsync_Succeeds_ReturnsGeneratedPassword()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("token");

        _userManager
            .Setup(u => u.ResetPasswordAsync(
                user,
                "token",
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.ResetUserPasswordAsync(
            user.UserName!,
            CancellationToken.None);

        result.GeneratedPassword.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteUserAsync_UserNotFound_ReturnsFailedResult()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.DeleteUserAsync(
            AuthTestConstants.Usernames[0],
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Code == IdentityErrors.UserNotFoundCode);
    }

    [Test]
    public async Task DeleteUserAsync_UserExists_DeletesUser()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.DeleteUserAsync(
            user.UserName!,
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        _userManager.Verify(
            u => u.DeleteAsync(user),
            Times.Once);
    }

    [Test]
    public async Task CreateUserAsync_Succeeds_CreatesTheUserWithTheReturnedPassword()
    {
        _userManager
            .Setup(u => u.CreateAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.CreateUserAsync(
            AuthTestConstants.Usernames[0],
            CancellationToken.None);

        result.Result.Succeeded.Should().BeTrue();
        result.GeneratedPassword.Should().NotBeNullOrEmpty();

        _userManager.Verify(
            u => u.CreateAsync(
                It.Is<ApplicationUser>(u =>
                    u.UserName == AuthTestConstants.Usernames[0] &&
                    u.CreatedAt <= DateTime.UtcNow &&
                    // Saved with the account, so it must change its generated password at first sign-in.
                    u.MustChangePassword),
                result.GeneratedPassword!),
            Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CreateUserAsync_Succeeds_GeneratesAPasswordThatMeetsThePasswordRules(bool requireNonAlphanumeric)
    {
        _options.Password.RequiredLength = 12;
        _options.Password.RequiredUniqueChars = 6;
        _options.Password.RequireNonAlphanumeric = requireNonAlphanumeric;

        _userManager
            .Setup(u => u.CreateAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.CreateUserAsync(
            AuthTestConstants.Usernames[0],
            CancellationToken.None);

        var rules = await new PasswordValidator<ApplicationUser>().ValidateAsync(
            _userManager.Object,
            AuthTestConstants.CreateValidUser(),
            result.GeneratedPassword);

        rules.Succeeded.Should().BeTrue();
        // At least 16 characters, however short the rules allow.
        result.GeneratedPassword.Should().HaveLength(16);
    }

    [Test]
    public async Task CreateUserAsync_CalledTwice_GeneratesDifferentPasswords()
    {
        _userManager
            .Setup(u => u.CreateAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var first = await _service.CreateUserAsync(
            AuthTestConstants.Usernames[0],
            CancellationToken.None);
        var second = await _service.CreateUserAsync(
            AuthTestConstants.Usernames[1],
            CancellationToken.None);

        first.GeneratedPassword.Should().NotBe(second.GeneratedPassword);
    }

    [Test]
    public async Task CreateUserAsync_Fails_ReturnsTheErrorsWithoutAPassword()
    {
        var failed = IdentityResult.Failed(new IdentityError
        {
            Code = "DuplicateUserName",
            Description = "Synthetic problem."
        });

        _userManager
            .Setup(u => u.CreateAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(failed);

        var result = await _service.CreateUserAsync(
            AuthTestConstants.Usernames[0],
            CancellationToken.None);

        result.Result.Should().Be(failed);
        result.GeneratedPassword.Should().BeNull();
    }
    
    [Test]
    public async Task UpdateUserPasswordAsync_UserNotFound_ReturnsFailedResult()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.UpdateUserPasswordAsync(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1],
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Code == IdentityErrors.UserNotFoundCode);
    }

    [Test]
    public async Task UpdateUserPasswordAsync_OldPasswordIncorrect_ReturnsFailedResult()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.Passwords[0]))
            .ReturnsAsync(false);

        var result = await _service.UpdateUserPasswordAsync(
            user.UserName!,
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1],
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Code == IdentityErrors.OldPasswordIncorrectCode);
    }

    [Test]
    public async Task VerifyPasswordAsync_UserNotFound_FailsWithUserNotFound()
    {
        _userManager.Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0])).ReturnsAsync((ApplicationUser?)null);

        var result = await _service.VerifyPasswordAsync(AuthTestConstants.Usernames[0], AuthTestConstants.ValidPassword, CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.UserNotFoundCode);
    }

    [Test]
    public async Task VerifyPasswordAsync_LockedOut_FailsWithoutCheckingThePassword()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(true);

        var result = await _service.VerifyPasswordAsync(user.UserName!, AuthTestConstants.ValidPassword, CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.LockedOutCode);
        _userManager.Verify(u => u.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task VerifyPasswordAsync_RightPassword_SucceedsAndClearsTheFailedCount()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.CheckPasswordAsync(user, AuthTestConstants.ValidPassword)).ReturnsAsync(true);

        var result = await _service.VerifyPasswordAsync(user.UserName!, AuthTestConstants.ValidPassword, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _userManager.Verify(u => u.ResetAccessFailedCountAsync(user), Times.Once);
        _userManager.Verify(u => u.AccessFailedAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task VerifyPasswordAsync_WrongPassword_CountsTheFailureAndFailsWithPasswordIncorrect()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);

        var result = await _service.VerifyPasswordAsync(user.UserName!, AuthTestConstants.Passwords[0], CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.PasswordIncorrectCode);
        _userManager.Verify(u => u.AccessFailedAsync(user), Times.Once);
    }

    [Test]
    public async Task VerifyPasswordAsync_WrongPasswordThatLocksTheAccount_FailsWithLockedOut()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);
        _userManager.SetupSequence(u => u.IsLockedOutAsync(user)).ReturnsAsync(false).ReturnsAsync(true);

        var result = await _service.VerifyPasswordAsync(user.UserName!, AuthTestConstants.Passwords[0], CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.LockedOutCode);
    }
}
