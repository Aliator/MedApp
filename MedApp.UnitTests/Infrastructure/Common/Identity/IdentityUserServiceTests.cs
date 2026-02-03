using FluentAssertions;
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
                AuthTestConstants.Password))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.CreateUserAsync(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Password,
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        _userManager.Verify(
            u => u.CreateAsync(
                It.Is<ApplicationUser>(u =>
                    u.UserName == AuthTestConstants.Usernames[0] &&
                    u.CreatedAt <= DateTime.UtcNow),
                AuthTestConstants.Password),
            Times.Once);
    }
}