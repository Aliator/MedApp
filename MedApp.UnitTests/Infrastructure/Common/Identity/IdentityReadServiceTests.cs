using FluentAssertions;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class IdentityReadServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<RoleManager<IdentityRole<Guid>>> _roleManager = null!;
    private IdentityReadService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        _roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStore.Object, null!, null!, null!, null!);

        _service = new IdentityReadService(_userManager.Object, _roleManager.Object);
    }

    [Test]
    public async Task GetUsernamesAsync_ReturnsAllUsernames()
    {
        var users = AuthTestConstants.Usernames
            .Select(u => new ApplicationUser { UserName = u })
            .AsQueryable();

        _userManager.SetupGet(u => u.Users).Returns(users);

        var result = await _service.GetUsernamesAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Usernames);
    }

    [Test]
    public async Task GetRoleNamesAsync_ReturnsAllRoleNames()
    {
        var roles = AuthTestConstants.Roles
            .Select(r => new IdentityRole<Guid>(r))
            .AsQueryable();

        _roleManager.SetupGet(r => r.Roles).Returns(roles);

        var result = await _service.GetRoleNamesAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }

    [Test]
    public async Task GetRolesForUserAsync_UserDoesNotExist_ReturnsEmpty()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.GetRolesForUserAsync(AuthTestConstants.Usernames[0], CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetRolesForUserAsync_UserExists_ReturnsRoles()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(AuthTestConstants.Roles.ToList());

        var result = await _service.GetRolesForUserAsync(user.UserName!, CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }
}
