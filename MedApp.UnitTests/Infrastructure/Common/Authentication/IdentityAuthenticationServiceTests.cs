using FluentAssertions;
using MedApp.Infrastructure.Common.Authentication;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Authentication;

[TestFixture]
public sealed class IdentityAuthenticationServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private IdentityAuthenticationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new IdentityAuthenticationService(_userManager.Object);
    }

    [Test]
    public async Task ValidateCredentialsAsync_UserDoesNotExist_ThrowsUnauthorizedAccessException()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var act = async () =>
            await _service.ValidateCredentialsAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Passwords[0]);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task ValidateCredentialsAsync_InvalidPassword_ThrowsUnauthorizedAccessException()
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

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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
