using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class IdentityUserServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private IdentityUserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new IdentityUserService(_userManager.Object);
    }

    [Test]
    public async Task CreateUserAsync_CallsUserManagerWithCorrectUser()
    {
        _userManager
            .Setup(u => u.CreateAsync(
                It.IsAny<ApplicationUser>(),
                AuthTestConstants.Passwords[0]))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.CreateUserAsync(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        _userManager.Verify(
            u => u.CreateAsync(
                It.Is<ApplicationUser>(u =>
                    u.UserName == AuthTestConstants.Usernames[0] &&
                    u.CreatedAt <= DateTime.UtcNow),
                AuthTestConstants.Passwords[0]),
            Times.Once);
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
}